using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.ChallengeMission;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class ScreenLostVoidRunLevelRuntime : ILostVoidRunLevelRuntime
{
	private readonly TimeSpan? _battleMenuRetryDelay;

	private readonly TimeSpan? _battleMenuPreClickDelay;

	private readonly Func<Mat, double?, YoloDetectFrameResult>? _inBattleDetectorOverride;

	private readonly LostVoidInBattleProbe _inBattleProbe;

	public static ScreenLostVoidRunLevelRuntime Instance { get; } = new ScreenLostVoidRunLevelRuntime();

	public ScreenLostVoidRunLevelRuntime(TimeSpan? battleMenuRetryDelay = null, TimeSpan? battleMenuPreClickDelay = null)
	{
		_battleMenuRetryDelay = battleMenuRetryDelay;
		_battleMenuPreClickDelay = battleMenuPreClickDelay;
		_inBattleProbe = new LostVoidInBattleProbe();
	}

	internal ScreenLostVoidRunLevelRuntime(Func<Mat, double?, YoloDetectFrameResult> inBattleDetectorOverride, TimeSpan? probeMinInterval = null, Action<Action>? probeDispatchOverride = null)
	{
		_inBattleDetectorOverride = inBattleDetectorOverride;
		_inBattleProbe = new LostVoidInBattleProbe(probeMinInterval, probeDispatchOverride);
	}

	public Task<LostVoidRunLevelLoadingState> GetLoadingStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
	{
		if (IsInNormalWorld(operation.GameContext, screen))
		{
			return Task.FromResult(new LostVoidRunLevelLoadingState(InNormalWorld: true));
		}
		if (MatchScreen(operation.GameContext, screen, new string[2] { "迷失之地-武备选择", "迷失之地-通用选择" }) != null)
		{
			return Task.FromResult(new LostVoidRunLevelLoadingState(InNormalWorld: false, IsChoosingReward: true));
		}
		if (TryFindAndClickArea(operation.GameContext, screen, "迷失之地-大世界", "按钮-挑战-确认"))
		{
			return Task.FromResult(new LostVoidRunLevelLoadingState(InNormalWorld: false, IsChoosingReward: false, ChallengeConfirmAvailable: true));
		}
		LostVoidInteractResult? talkResult = TryTalk(operation, screen);
		return Task.FromResult(new LostVoidRunLevelLoadingState(InNormalWorld: false, IsChoosingReward: false, ChallengeConfirmAvailable: false, talkResult?.Status, talkResult?.Delay));
	}

	public Task<LostVoidRunLevelWorldState> GetNonBattleWorldStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
	{
		if (IsInNormalWorld(operation.GameContext, screen))
		{
			return Task.FromResult(new LostVoidRunLevelWorldState(InNormalWorld: true));
		}
		bool challengeConfirmAvailable = TryFindAndClickArea(operation.GameContext, screen, "迷失之地-大世界", "按钮-挑战-确认");
		return Task.FromResult(new LostVoidRunLevelWorldState(InNormalWorld: false, challengeConfirmAvailable));
	}

	public Task<LostVoidRunLevelFrame> GetNonBattleFrameAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, IReadOnlyList<string> ignoreList, CancellationToken cancellationToken)
	{
		bool flag = false;
		bool flag2 = false;
		if (operation.BossPreBattle)
		{
			flag2 = FindArea(operation.GameContext, screen, "迷失之地-大世界", "标识-BOSS血条") || CheckBattleEncounter(operation.GameContext, screen, screenshotTimeUtc);
			if (flag2)
			{
				return Task.FromResult(new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, BossBattleStarted: true));
			}
			flag = FindArea(operation.GameContext, screen, "战斗画面", "按键-交互");
			if (flag)
			{
				return Task.FromResult(new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, BossBattleStarted: false, BossInteractAvailable: true));
			}
		}
		YoloDetectFrameResult detectResult = null;
		if (screen != null && operation.GameContext.LostVoid.Detector != null)
		{
			IReadOnlyList<string> labelList = BuildLabelList(operation.GameContext.LostVoid.Detector, ignoreList);
				detectResult = operation.GameContext.LostVoid.Detector.Run(
					screen,
				0.6f,
				0.5f,
				(double?)screenshotTimeUtc?.ToUnixTimeMilliseconds() / 1000.0,
				labelList,
				null,
					null,
					LostVoidDetector.OverlaySourceNavigation);
			operation.GameContext.Logger.Information(
				"迷失之地非战斗检测: FrameTimeUtc={FrameTimeUtc}, FrameId={FrameId}, OverlaySource={OverlaySource}, Detect={Detect}",
				screenshotTimeUtc ?? DateTimeOffset.UtcNow,
				detectResult.FrameId,
				detectResult.OverlaySource,
				LostVoidDetectorResultHelper.DescribeDetectedClasses(detectResult));
		}
		return Task.FromResult(new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, flag2, flag, detectResult));
	}

	public bool CheckBattleEncounterInPeriod(LostVoidRunLevel operation, float totalCheckSeconds)
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		while (DateTimeOffset.UtcNow - utcNow < TimeSpan.FromSeconds(totalCheckSeconds))
		{
			(DateTimeOffset, Mat)? tuple = operation.GameContext.Controller?.Screenshot();
			if (tuple.HasValue)
			{
				(DateTimeOffset, Mat) valueOrDefault = tuple.GetValueOrDefault();
				if (true)
				{
					using Mat screen = valueOrDefault.Item2;
					if (CheckBattleEncounter(operation.GameContext, screen, valueOrDefault.Item1))
					{
						return true;
					}
				}
			}
			Thread.Sleep(TimeSpan.FromSeconds(operation.GameContext.BattleAssistantConfig.ScreenshotInterval));
		}
		return false;
	}

	public bool CheckBattleEncounterInCurrentFrame(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc)
	{
		return CheckBattleEncounter(operation.GameContext, screen, screenshotTimeUtc);
	}

	public (OperationRoundResult? Result, bool Advance) HandleFriendlyTalkInit(LostVoidRunLevel operation, int roomInitedTimes)
	{
		switch (roomInitedTimes)
		{
		case 0:
			// 进入大世界一秒内可能触发战略奖励鸣徽的画面，先等待再继续识别。
			return (new OperationRoundResult(OperationRoundResultKind.Wait, null, null, TimeSpan.FromSeconds(2L)), true);
		case 1:
			// 挚交会谈开局先向右移动一段距离避开桌子，执行后不提前返回，但计数需要推进到 2，
			// 否则下一轮会把移动动作重复执行。
			operation.GameContext.AutoBattleContext.MoveW(press: true, TimeSpan.FromMilliseconds(700L), release: true);
			operation.GameContext.AutoBattleContext.MoveD(press: true, TimeSpan.FromMilliseconds(1400L), release: true);
			return (null, true);
		default:
			return (null, false);
		}
	}

	public void TurnToFindTarget(LostVoidRunLevel operation)
	{
		operation.GameContext.AutoBattleContext.TurnByDistance(-200f);
	}

	public Task<OperationResult> MoveByDetectionAsync(LostVoidRunLevel operation, string regionType, string targetType, bool stopWhenInteract, bool stopWhenDisappear, bool allowArrivalByInteractButton, IReadOnlyList<string> ignoreEntries, CancellationToken cancellationToken)
	{
		ZContext gameContext = operation.GameContext;
		bool allowArrivalByInteractButton2 = allowArrivalByInteractButton;
		LostVoidMoveByDetectionOperation lostVoidMoveByDetectionOperation = new LostVoidMoveByDetectionOperation(gameContext, regionType, targetType, stopWhenInteract, stopWhenDisappear, ignoreEntries, allowArrivalByInteractButton2);
		return lostVoidMoveByDetectionOperation.ExecuteAsync(cancellationToken);
	}

	public Task<OperationResult> UpdatePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
	{
		LostVoidUpdatePriorityOperation lostVoidUpdatePriorityOperation = new LostVoidUpdatePriorityOperation(operation.GameContext);
		return lostVoidUpdatePriorityOperation.ExecuteAsync(cancellationToken);
	}

	public Task<OperationResult> AppendAgentTypePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
	{
		operation.GameContext.LostVoid.AppendAgentTypePriorityFromCurrentTeam();
		return Task.FromResult(new OperationResult(IsSuccess: true, "非战斗区域"));
	}

	public Task<LostVoidTryInteractResult> TryInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, IReadOnlyList<string> interactedTargetKeys, bool interactAttempted, Mat? screen, CancellationToken cancellationToken)
	{
		if (screen == null)
		{
			return Task.FromResult(LostVoidTryInteractResult.Retry("未获取截图"));
		}
		if (FindArea(operation.GameContext, screen, "战斗画面", "按键-交互"))
		{
			Thread.Sleep(TimeSpan.FromMilliseconds(500L));
			using Mat mat = Screenshot(operation.GameContext);
			LostVoidInteractTarget lostVoidInteractTarget = ((mat == null) ? null : MatchCurrentInteractTarget(operation.GameContext, mat));
			if (lostVoidInteractTarget != null)
			{
				string interactTargetKey = operation.GetInteractTargetKey(lostVoidInteractTarget);
				if (interactedTargetKeys.Contains<string>(interactTargetKey, StringComparer.Ordinal))
				{
					return Task.FromResult(LostVoidTryInteractResult.Fail("重复交互对象"));
				}
			}
			if (!(operation.GameContext.Controller is IZzzControllerActions zzzControllerActions))
			{
				return Task.FromResult(LostVoidTryInteractResult.Retry("未接入ZZZ控制器"));
			}
			zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
			return Task.FromResult(LostVoidTryInteractResult.Wait("交互", lostVoidInteractTarget ?? currentTarget));
		}
		if (!IsInNormalWorld(operation.GameContext, screen) && interactAttempted)
		{
			return Task.FromResult(LostVoidTryInteractResult.Success("交互成功"));
		}
		if (operation.GameContext.Controller is IZzzControllerActions zzzControllerActions2)
		{
			for (int i = 0; i < 3; i++)
			{
				zzzControllerActions2.MoveS(press: true, TimeSpan.FromMilliseconds(200L), release: true);
				Thread.Sleep(TimeSpan.FromMilliseconds(200L));
			}
			zzzControllerActions2.MoveW(press: true, TimeSpan.FromMilliseconds(200L), release: true);
			Thread.Sleep(TimeSpan.FromSeconds(1L));
		}
		return Task.FromResult(LostVoidTryInteractResult.Retry("未发现交互按键"));
	}

	public async Task<LostVoidInteractResult> HandleInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken)
	{
		string screenName = MatchScreen(operation.GameContext, screen, new string[7] { "迷失之地-武备选择", "迷失之地-通用选择", "迷失之地-邦布商店", "迷失之地-路径迭换", "迷失之地-抽奖机", "迷失之地-挑战结果", "迷失之地-大世界" });
		if (1 == 0)
		{
		}
		ZOperation zOperation = screenName switch
		{
			"迷失之地-武备选择" => new LostVoidChooseGearOperation(operation.GameContext), 
			"迷失之地-通用选择" => new LostVoidChooseCommonOperation(operation.GameContext), 
			"迷失之地-邦布商店" => new LostVoidBangbooStoreOperation(operation.GameContext), 
			"迷失之地-路径迭换" => new LostVoidRouteChangeOperation(operation.GameContext), 
			"迷失之地-抽奖机" => new LostVoidLotteryOperation(operation.GameContext), 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		ZOperation interactOperation = zOperation;
		if (1 == 0)
		{
		}
		string text = screenName switch
		{
			"迷失之地-邦布商店" => "邦布商店", 
			"迷失之地-路径迭换" => "路径迭换", 
			"迷失之地-抽奖机" => "邦布商店", 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		string hadBeenType = text;
		if (interactOperation != null)
		{
			LostVoidInteractTarget target = ((currentTarget?.IsEntry ?? false) ? new LostVoidInteractTarget("未知", "感叹号", isAgent: false, isNpc: false, isEntry: false, isExclamation: true) : null);
			OperationResult result = await interactOperation.ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return result.IsSuccess ? LostVoidInteractResult.Wait(result.Status ?? screenName, hadBeenType, target) : LostVoidInteractResult.Fail(result.Status ?? "未知画面");
		}
		if (string.Equals(screenName, "迷失之地-挑战结果", StringComparison.Ordinal))
		{
			return LostVoidInteractResult.Success("迷失之地-挑战结果");
		}
		if (string.Equals(screenName, "迷失之地-大世界", StringComparison.Ordinal))
		{
			return LostVoidInteractResult.Success("迷失之地-大世界");
		}
		LostVoidInteractResult talkResult = TryTalk(operation, screen);
		if ((object)talkResult != null)
		{
			LostVoidInteractTarget target2 = ((currentTarget?.IsEntry ?? false) ? new LostVoidInteractTarget("未知", "感叹号", isAgent: false, isNpc: false, isEntry: false, isExclamation: true) : null);
			return talkResult with
			{
				Target = (target2 ?? talkResult.Target)
			};
		}
		OneDragon.Core.Screen.ScreenArea blackArea = operation.GameContext.ScreenContext.GetArea("迷失之地-通用选择", "中间区域-识别黑屏");
		if (screen != null && blackArea != null)
		{
			using Mat blackPart = CvImageUtils.Crop(screen, blackArea.Rect);
			if (!IsColorful(blackPart, 1.0, 0.01))
			{
				operation.GameContext.Controller?.Click();
				return LostVoidInteractResult.Wait("黑屏点击")with
				{
					Delay = TimeSpan.FromMilliseconds(500L)
				};
			}
		}
		if (IsInNormalWorld(operation.GameContext, screen))
		{
			return LostVoidInteractResult.Success("迷失之地-大世界");
		}
		if (FindArea(operation.GameContext, screen, "迷失之地-挑战结果", "标题-挑战结果"))
		{
			return LostVoidInteractResult.Success("迷失之地-挑战结果");
		}
		if (TryFindAndClickArea(operation.GameContext, screen, "迷失之地-大世界", "按钮-挑战-确认"))
		{
			return LostVoidInteractResult.Wait("按钮-挑战-确认")with
			{
				Delay = TimeSpan.FromSeconds(1L)
			};
		}
		if ((currentTarget?.IsEntry ?? false) && screen != null)
		{
			return LostVoidInteractResult.Success("进入下层");
		}
		if (TryFindAndClickArea(operation.GameContext, screen, "迷失之地-大世界", "按钮-黑屏文本-确认"))
		{
			return LostVoidInteractResult.Wait("按钮-黑屏文本-确认")with
			{
				Delay = TimeSpan.FromSeconds(1L)
			};
		}
		return LostVoidInteractResult.Retry("未知画面");
	}

	public Task<LostVoidAfterInteractState> GetAfterInteractStateAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken)
	{
		bool inNormalWorld = IsInNormalWorld(operation.GameContext, screen);
		// 挑战结果标题出现后按钮才可能出现较晚，因此只有先命中标题才检测两个按钮，否则一律按未出现处理。
		bool challengeResultTitleFound = FindArea(operation.GameContext, screen, "迷失之地-挑战结果", "标题-挑战结果");
		bool challengeResultConfirmAvailable = challengeResultTitleFound && FindArea(operation.GameContext, screen, "迷失之地-挑战结果", "按钮-确定");
		bool challengeResultFinishAvailable = challengeResultTitleFound && FindArea(operation.GameContext, screen, "迷失之地-挑战结果", "按钮-完成");
		return Task.FromResult(new LostVoidAfterInteractState(inNormalWorld, challengeResultConfirmAvailable, challengeResultFinishAvailable));
	}

	public void MoveAfterInteract(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget)
	{
		if (currentTarget == null || currentTarget.AfterBattle)
		{
			return;
		}
		if (string.Equals(operation.RegionType, "入口", StringComparison.Ordinal))
		{
			operation.GameContext.AutoBattleContext.MoveS(press: true, TimeSpan.FromSeconds(2L), release: true);
			if (currentTarget.IsNpc && string.Equals(currentTarget.Name, "神出鬼没的研究员", StringComparison.Ordinal))
			{
				operation.GameContext.AutoBattleContext.MoveD(press: true, TimeSpan.FromMilliseconds(500L), release: true);
			}
		}
		else if (string.Equals(operation.RegionType, "挚交会谈", StringComparison.Ordinal))
		{
			operation.GameContext.AutoBattleContext.MoveS(press: true, TimeSpan.FromSeconds(1L), release: true);
			if (currentTarget.IsAgent || (currentTarget.IsNpc && (string.Equals(currentTarget.Name, "阿援", StringComparison.Ordinal) || string.Equals(currentTarget.Name, "玛琳", StringComparison.Ordinal))))
			{
				operation.GameContext.AutoBattleContext.MoveD(press: true, TimeSpan.FromMilliseconds(1500L), release: true);
			}
			else if (currentTarget.IsNpc && string.Equals(currentTarget.Name, "奥菲莉亚", StringComparison.Ordinal))
			{
				operation.GameContext.AutoBattleContext.MoveA(press: true, TimeSpan.FromSeconds(1L), release: true);
			}
		}
		else
		{
			operation.GameContext.AutoBattleContext.MoveS(press: true, TimeSpan.FromSeconds(1L), release: true);
		}
	}

	public void StartAutoBattle(LostVoidRunLevel operation)
	{
		operation.GameContext.Logger.Information("迷失之地运行时动作: Action=StartAutoBattle, Region={Region}", operation.RegionType);
		_inBattleProbe.Reset();
		operation.GameContext.AutoBattleContext.StartAutoBattle();
	}

	public void StopAutoBattle(LostVoidRunLevel operation)
	{
		operation.GameContext.Logger.Information("迷失之地运行时动作: Action=StopAutoBattle, Region={Region}", operation.RegionType);
		_inBattleProbe.Reset();
		operation.GameContext.AutoBattleContext.StopAutoBattle();
	}

	/// <summary>
	/// 按节流间隔调度一次战斗中识别探测（YOLO 交互/距离/入口 + 前往下一个区域 OCR）。
	/// 探测在专用线程上使用帧租借执行，不占用战斗轮时间。
	/// </summary>
	private void ScheduleInBattleProbe(LostVoidRunLevel operation, Mat screen, DateTimeOffset frameTimeUtc)
	{
		// 对齐参考实现：道中危机与终结之役不识别下层入口，因此也不受 0.8 秒检测节流限制，
		// 只按单飞节奏持续做 前往下一个区域 OCR。
		bool skipDetector = string.Equals(operation.RegionType, "战斗-道中危机", StringComparison.Ordinal) || string.Equals(operation.RegionType, "战斗-终结之役", StringComparison.Ordinal);
		TimeSpan? minIntervalOverride = (skipDetector ? new TimeSpan?(TimeSpan.Zero) : null);
		Mat? leasedScreen = null;
		try
		{
			leasedScreen = AutoBattleContext.CreateFrameLease(screen);
			Mat probeScreen = leasedScreen;
			if (!_inBattleProbe.TrySchedule(frameTimeUtc, () => RunInBattleProbe(operation, probeScreen, frameTimeUtc, skipDetector), minIntervalOverride))
			{
				return;
			}
			leasedScreen = null;
		}
		finally
		{
			leasedScreen?.Dispose();
		}
	}

	private LostVoidInBattleProbeResult? RunInBattleProbe(LostVoidRunLevel operation, Mat screen, DateTimeOffset frameTimeUtc, bool skipDetector)
	{
		try
		{
			bool detectorRan = false;
			bool noLongerInBattleByDetection = false;
			string detect = "未检测";
			double elapsedMilliseconds = 0.0;
			if (!skipDetector && (_inBattleDetectorOverride != null || operation.GameContext.LostVoid.Detector != null))
			{
				detectorRan = true;
				long timestamp = Stopwatch.GetTimestamp();
				YoloDetectFrameResult frameResult = _inBattleDetectorOverride?.Invoke(screen, (double)frameTimeUtc.ToUnixTimeMilliseconds() / 1000.0) ?? operation.GameContext.LostVoid.Detector.Run(
					screen,
					0.9f,
					0.5f,
					(double)frameTimeUtc.ToUnixTimeMilliseconds() / 1000.0,
					null,
					null,
					null,
					LostVoidDetector.OverlaySourceBattle);
				elapsedMilliseconds = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
				(bool WithInteract, bool WithDistance, bool WithEntry) tuple = LostVoidDetectorResultHelper.IsFrameWithAll(frameResult);
				bool item = tuple.WithInteract;
				bool item2 = tuple.WithDistance;
				bool item3 = tuple.WithEntry;
				noLongerInBattleByDetection = item || item2 || item3;
				detect = LostVoidDetectorResultHelper.DescribeDetectedClasses(frameResult);
				operation.GameContext.Logger.Information("迷失之地战斗检测: Region={Region}, FrameTimeUtc={FrameTimeUtc}, FrameId={FrameId}, OverlaySource={OverlaySource}, Detect={Detect}, Interact={Interact}, Distance={Distance}, Entry={Entry}, ElapsedMilliseconds={ElapsedMilliseconds:F2}", operation.RegionType, frameTimeUtc, frameResult.FrameId, frameResult.OverlaySource, detect, item, item2, item3, elapsedMilliseconds);
			}
			bool nextRegionHint = false;
			if (!noLongerInBattleByDetection)
			{
				OneDragon.Core.Screen.ScreenArea area = operation.GameContext.ScreenContext.GetArea("迷失之地-大世界", "区域-文本提示");
				IReadOnlyList<OcrMatchResult> ocrResults = operation.GameContext.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
				string targetText = operation.GameContext.ResolveGameText("前往下一个区域");
				OcrMatchResult? match = ocrResults.FirstOrDefault(result => StringUtils.FindByLcs(targetText, result.Text, 0.5));
				nextRegionHint = match != null;
				operation.GameContext.Logger.Information("迷失之地下层入口 OCR: Region={Region}, FrameTimeUtc={FrameTimeUtc}, Target={Target}, Texts={Texts}, Match={Match}, MatchConfidence={MatchConfidence}", operation.RegionType, frameTimeUtc, targetText, string.Join("|", ocrResults.Select(result => result.Text)), match?.Text ?? "无", match?.Confidence ?? 0d);
			}
			return new LostVoidInBattleProbeResult(frameTimeUtc, noLongerInBattleByDetection, nextRegionHint, detect, elapsedMilliseconds, detectorRan);
		}
		catch (Exception exception)
		{
			operation.GameContext.Logger.Error(exception, "迷失之地战斗中识别交互出现异常");
			return null;
		}
		finally
		{
			screen.Dispose();
		}
	}

	public Task<LostVoidBattleState> GetBattleStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
	{
		if (screen == null || screen.Empty())
		{
			return Task.FromResult(new LostVoidBattleState(operation.LastFrameInBattle, NextRegionHint: false, NoLongerInBattleByDetection: false, InInteractScreen: false, BattleFailed: false, TransitionCheckPerformed: false, DetectorChecked: false, FinishScreenChecked: false, FrameValid: false));
		}
		DateTimeOffset dateTimeOffset = screenshotTimeUtc ?? DateTimeOffset.UtcNow;
		bool flag = operation.GameContext.AutoBattleContext.CheckBattleState(screen, dateTimeOffset, checkBattleEndNormalResult: false, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: false, checkDistance: false, sync: false, "lost_void");
		// 战斗中的 YOLO 与 OCR 走异步探测：本轮只消费已完成的结果、并按节流调度下一次，
		// 不阻塞战斗轮，从而保证同一轮提交的角色状态识别维持高频供帧。
		bool flag2 = false;
		bool flag3 = false;
		bool probeConsumed = false;
		bool nextRegionHint = false;
		string text = "未检测";
		double num2 = 0.0;
		if (flag)
		{
			if (_inBattleProbe.TryConsume(dateTimeOffset, out LostVoidInBattleProbeResult probeResult))
			{
				probeConsumed = true;
				flag3 = probeResult.DetectorRan;
				flag2 = probeResult.NoLongerInBattleByDetection;
				nextRegionHint = probeResult.NextRegionHint;
				text = probeResult.Detect;
				num2 = probeResult.DetectorElapsedMilliseconds;
			}
			ScheduleInBattleProbe(operation, screen, dateTimeOffset);
		}
		bool num;
		if (!flag)
		{
			if (!HasElapsed(dateTimeOffset, operation.LastCheckFinishTimeUtc, TimeSpan.FromSeconds(1L)))
			{
				if (operation.NoInBattleTimes > 0)
				{
					num = HasElapsed(dateTimeOffset, operation.LastCheckFinishTimeUtc, TimeSpan.FromMilliseconds(100L));
					goto IL_012d;
				}
				goto IL_013b;
			}
		}
		else if (!probeConsumed && operation.LastFrameInBattle)
		{
			// 战斗中且本轮没有新的识别证据：保持轮次轻量，不做状态转移判定
			if (operation.NoInBattleTimes > 0)
			{
				num = HasElapsed(dateTimeOffset, operation.LastCheckFinishTimeUtc, TimeSpan.FromMilliseconds(100L));
				goto IL_012d;
			}
			goto IL_013b;
		}
		goto IL_01ca;
		IL_01ca:
		bool flag4 = !flag;
		long startingTimestamp = (flag4 ? Stopwatch.GetTimestamp() : 0);
		if (flag4)
		{
			operation.GameContext.Logger.Information("[.NET诊断] 迷失之地战斗结束识别: Stage=Interact.Begin");
		}
		bool flag5 = flag4 && FindArea(operation.GameContext, screen, "战斗画面", "按键-交互");
		double num3 = (flag4 ? Stopwatch.GetElapsedTime(startingTimestamp).TotalMilliseconds : 0.0);
		long startingTimestamp2 = (flag4 ? Stopwatch.GetTimestamp() : 0);
		if (flag4)
		{
			operation.GameContext.Logger.Information("[.NET诊断] 迷失之地战斗结束识别: Stage=ScreenMatch.Begin");
		}
		string text2 = (flag4 ? MatchScreen(operation.GameContext, screen, new string[4] { "迷失之地-武备选择", "迷失之地-通用选择", "迷失之地-挑战结果", "迷失之地-战斗失败" }) : null);
		double num4 = (flag4 ? Stopwatch.GetElapsedTime(startingTimestamp2).TotalMilliseconds : 0.0);
		bool battleFailed = string.Equals(text2, "迷失之地-战斗失败", StringComparison.Ordinal);
		long startingTimestamp3 = (flag4 ? Stopwatch.GetTimestamp() : 0);
		if (flag4)
		{
			operation.GameContext.Logger.Information("[.NET诊断] 迷失之地战斗结束识别: Stage=ChallengeConfirm.Begin");
		}
		bool flag6 = flag4 && TryFindAndClickArea(operation.GameContext, screen, "迷失之地-大世界", "按钮-挑战-确认");
		double num5 = (flag4 ? Stopwatch.GetElapsedTime(startingTimestamp3).TotalMilliseconds : 0.0);
		bool flag7 = nextRegionHint;
		bool inInteractScreen = flag5 || text2 != null || flag6;
		operation.GameContext.Logger.Information("[.NET诊断] 迷失之地战斗状态: Region={Region}, FrameTimeUtc={FrameTimeUtc}, InBattle={InBattle}, DetectorChecked={DetectorChecked}, Detect={Detect}, NoLongerInBattleByDetection={NoLongerInBattleByDetection}, Screen={Screen}, Interact={Interact}, ConfirmClicked={ConfirmClicked}, NextRegion={NextRegion}, InteractElapsedMilliseconds={InteractElapsedMilliseconds:F2}, ScreenMatchElapsedMilliseconds={ScreenMatchElapsedMilliseconds:F2}, ChallengeConfirmElapsedMilliseconds={ChallengeConfirmElapsedMilliseconds:F2}, DetectorElapsedMilliseconds={DetectorElapsedMilliseconds:F2}", operation.RegionType, dateTimeOffset, flag, flag3, text, flag2, text2 ?? "无", flag5, flag6, flag7, num3, num4, num5, num2);
		return Task.FromResult(new LostVoidBattleState(flag, flag7, flag2, inInteractScreen, battleFailed, TransitionCheckPerformed: true, flag3, flag4));
		IL_012d:
		if (!num)
		{
			goto IL_013b;
		}
		goto IL_01ca;
		IL_013b:
		if (operation.ShouldLogTransitionDiagnostic())
		{
			operation.GameContext.Logger.Information("[.NET诊断] 迷失之地战斗轮次: Phase=TransitionSkipped, InBattle={InBattle}, FrameTimeUtc={FrameTimeUtc}, LastFrameInBattle={LastFrameInBattle}, NoInBattleTimes={NoInBattleTimes}, LastDetectTimeUtc={LastDetectTimeUtc}, LastCheckFinishTimeUtc={LastCheckFinishTimeUtc}", flag, dateTimeOffset, operation.LastFrameInBattle, operation.NoInBattleTimes, operation.LastDetectTimeUtc, operation.LastCheckFinishTimeUtc);
		}
		return Task.FromResult(new LostVoidBattleState(flag, NextRegionHint: false, NoLongerInBattleByDetection: false, InInteractScreen: false, BattleFailed: false, TransitionCheckPerformed: false));
	}

	private static bool HasElapsed(DateTimeOffset currentTime, DateTimeOffset? previousTime, TimeSpan threshold)
	{
		return !previousTime.HasValue || currentTime - previousTime.Value >= threshold;
	}

	public Task<OperationResult> RestartForRetryAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
	{
		RestartInBattle restartInBattle = new RestartInBattle(operation.GameContext, _battleMenuRetryDelay, _battleMenuPreClickDelay);
		return restartInBattle.ExecuteAsync(cancellationToken);
	}

	public async Task<OperationResult> PushErrorAsync(LostVoidRunLevel operation, Mat? screen, string? previousNodeName, string? previousStatus, CancellationToken cancellationToken)
	{
		string status = (string.IsNullOrWhiteSpace(previousNodeName) ? (previousStatus ?? "运行失败") : (previousNodeName + ": " + previousStatus));
		if (screen != null)
		{
			TimeSpan preClickDelay = _battleMenuPreClickDelay ?? TimeSpan.FromMilliseconds(300L);
			if (preClickDelay > TimeSpan.Zero)
			{
				await Task.Delay(preClickDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			OperationResult clickResult = ConvertClickResult(ScreenUtils.FindAndClickArea(operation.GameContext, screen, "迷失之地-大世界", "迷失之地-TAB"), "迷失之地-TAB");
			TimeSpan postClickDelay = _battleMenuRetryDelay ?? TimeSpan.FromSeconds(1L);
			if (postClickDelay > TimeSpan.Zero)
			{
				await Task.Delay(postClickDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (!clickResult.IsSuccess)
			{
				return new OperationResult(IsSuccess: false, status + "（打开tab页面失败）");
			}
			using (Screenshot(operation.GameContext))
			{
				return new OperationResult(IsSuccess: true, status);
			}
		}
		return new OperationResult(IsSuccess: false, status + "（打开tab页面失败）");
	}

	public Task<OperationResult> FailExitAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
	{
		ExitInBattle exitInBattle = new ExitInBattle(operation.GameContext, "迷失之地-挑战结果", "按钮-完成", _battleMenuRetryDelay, _battleMenuPreClickDelay);
		return exitInBattle.ExecuteAsync(cancellationToken);
	}

	private static bool IsInNormalWorld(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return false;
		}
		return FindArea(context, screen, "战斗画面", "按键-普通攻击") || FindArea(context, screen, "战斗画面", "按键-交互") || FindArea(context, screen, "迷失之地-大世界", "按键-交互-不可用");
	}

	private static LostVoidInteractResult? TryTalk(LostVoidRunLevel operation, Mat? screen)
	{
		if (screen == null || operation.GameContext.Controller == null)
		{
			return null;
		}
		OneDragon.Core.Screen.ScreenArea area = operation.GameContext.ScreenContext.GetArea("迷失之地-大世界", "区域-对话角色名称");
		IReadOnlyList<OcrMatchResult> whiteOcrResults = GetWhiteOcrResults(operation.GameContext, screen, area);
		if (whiteOcrResults.Count > 0)
		{
			LostVoidInteractResult lostVoidInteractResult = TryTalkOptions(operation, screen);
			if ((object)lostVoidInteractResult != null)
			{
				return lostVoidInteractResult;
			}
			operation.GameContext.Controller.Click(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, 50));
			string text = string.Join(", ", whiteOcrResults.Select((OcrMatchResult result) => result.Text));
			operation.GameContext.Logger.Information("迷失之地推进对话: {Text}", text);
			return LostVoidInteractResult.Wait("尝试交互 [" + text + "]")with
			{
				Delay = TimeSpan.FromMilliseconds(500L)
			};
		}
		OneDragon.Core.Screen.ScreenArea area2 = operation.GameContext.ScreenContext.GetArea("迷失之地-大世界", "区域-对话内容");
		IReadOnlyList<OcrMatchResult> whiteOcrResults2 = GetWhiteOcrResults(operation.GameContext, screen, area2);
		string[] specialTalks = new string[4] { "似乎购买了充值卡就会得到齿轮硬币奖励，但是在离开之后身上的齿轮硬币都", "（声音消失了，伸手从裂隙那头好像摸到了什么）", "这位似曾相识的研究员为我们准备了一些「礼物」。", "但当正要选择的时候，她却拦住了我们。" };
		if (whiteOcrResults2.Any((OcrMatchResult result) => IsSpecialTalkText(result.Text, specialTalks)))
		{
			LostVoidInteractResult lostVoidInteractResult2 = TryTalkOptions(operation, screen);
			if ((object)lostVoidInteractResult2 != null)
			{
				return lostVoidInteractResult2;
			}
			operation.GameContext.Controller.Click(area2.Center);
			string text2 = string.Join(", ", whiteOcrResults2.Select((OcrMatchResult result) => result.Text));
			operation.GameContext.Logger.Information("迷失之地推进特殊对话: {Text}", text2);
			return LostVoidInteractResult.Wait("尝试交互 [" + text2 + "]")with
			{
				Delay = TimeSpan.FromMilliseconds(500L)
			};
		}
		return null;
	}

	internal static bool IsSpecialTalkText(string text, IEnumerable<string> specialTalks)
	{
		return text.Length > 10 || specialTalks.Any((string target) => StringUtils.FindByLcs(target, text, 0.3));
	}

	private static LostVoidInteractResult? TryTalkOptions(LostVoidRunLevel operation, Mat screen)
	{
		OneDragon.Core.Screen.ScreenArea area = operation.GameContext.ScreenContext.GetArea("迷失之地-大世界", "区域-右侧对话选项");
		IReadOnlyList<OcrMatchResult> whiteOcrResults = GetWhiteOcrResults(operation.GameContext, screen, area);
		if (whiteOcrResults.Count > 0)
		{
			int num = ((operation.TalkOptionIndex < whiteOcrResults.Count) ? operation.TalkOptionIndex : 0);
			OcrMatchResult ocrMatchResult = whiteOcrResults[num];
			OneDragon.Core.Abstractions.Geometry.Point value = new OneDragon.Core.Abstractions.Geometry.Point(ocrMatchResult.Center.X + area.Rect.X1, ocrMatchResult.Center.Y + area.Rect.Y1);
			operation.GameContext.Controller?.Click(value);
			operation.TalkOptionIndex++;
			operation.GameContext.Logger.Information("迷失之地选择对话选项: {Option}, Index={Index}", ocrMatchResult.Text, num);
			return LostVoidInteractResult.Wait("尝试交互选项 " + ocrMatchResult.Text)with
			{
				Delay = TimeSpan.FromMilliseconds(500L)
			};
		}
		operation.TalkOptionIndex = 0;
		if (TryFindAndClickArea(operation.GameContext, screen, "迷失之地-大世界", "区域-右侧对话图标"))
		{
			return LostVoidInteractResult.Wait("尝试交互选项图标")with
			{
				Delay = TimeSpan.FromMilliseconds(500L)
			};
		}
		return null;
	}

	private static IReadOnlyList<OcrMatchResult> GetWhiteOcrResults(ZContext context, Mat screen, OneDragon.Core.Screen.ScreenArea? area)
	{
		if (area == null)
		{
			return Array.Empty<OcrMatchResult>();
		}
		using Mat mat = CvImageUtils.Crop(screen, area.Rect);
		using Mat mat2 = new Mat();
		Cv2.InRange(mat, new Scalar(200.0, 200.0, 200.0), new Scalar(255.0, 255.0, 255.0), mat2);
		using Mat mat3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
		using Mat mat4 = new Mat();
		Cv2.Dilate(mat2, mat4, mat3);
		using Mat mat5 = new Mat();
		Cv2.BitwiseAnd(mat, mat, mat5, mat4);
		return context.OcrService.GetOcrResultListForCrop(
			mat5,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1);
	}

	private static bool TryFindAndClickArea(ZContext context, Mat? screen, string screenName, string areaName)
	{
		return screen != null && ScreenUtils.FindAndClickArea(context, screen, screenName, areaName) == OcrClickResultEnum.OcrClickSuccess;
	}

	private static bool CheckBattleEncounter(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
	{
		return AutoBattleUtils.CheckBattleEncounter(context, screen, screenshotTimeUtc);
	}

	private static bool IsColorful(Mat image, double saturationThreshold, double colorRatioThreshold)
	{
		if (image.Empty() || image.Channels() != 3)
		{
			return false;
		}
		using Mat mat = new Mat();
		Cv2.CvtColor(image, mat, ColorConversionCodes.RGB2HSV);
		Mat[] array = Cv2.Split(mat);
		try
		{
			using Mat mat2 = new Mat();
			Cv2.Threshold(array[1], mat2, saturationThreshold, 255.0, ThresholdTypes.Binary);
			double val = Cv2.Mean(array[1]).Val0;
			double num = ((mat2.Total() == 0L) ? 0.0 : ((double)Cv2.CountNonZero(mat2) / (double)mat2.Total()));
			return val > saturationThreshold && num > colorRatioThreshold;
		}
		finally
		{
			Mat[] array2 = array;
			foreach (Mat mat3 in array2)
			{
				mat3.Dispose();
			}
		}
	}

	private static bool FindArea(ZContext context, Mat? screen, string screenName, string areaName)
	{
		if (screen == null)
		{
			return false;
		}
		return ScreenUtils.FindArea(context, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	private static Mat? Screenshot(ZContext context)
	{
		return context.Controller?.Screenshot().Screen;
	}

	private static OperationResult ConvertClickResult(OcrClickResultEnum result, string targetName)
	{
		if (1 == 0)
		{
		}
		OperationResult result2 = result switch
		{
			OcrClickResultEnum.OcrClickSuccess => new OperationResult(IsSuccess: true, targetName), 
			OcrClickResultEnum.AreaNoConfig => new OperationResult(IsSuccess: false, "区域未配置 " + targetName), 
			OcrClickResultEnum.OcrClickFail => new OperationResult(IsSuccess: false, "点击失败 " + targetName), 
			_ => new OperationResult(IsSuccess: false, "未找到 " + targetName), 
		};
		if (1 == 0)
		{
		}
		return result2;
	}

	private static string? MatchScreen(ZContext context, Mat? screen, IReadOnlyList<string> screenNames)
	{
		if (screen == null)
		{
			context.ScreenContext.UpdateCurrentScreenName(null);
			return null;
		}
		string? matchScreenName = ScreenUtils.GetMatchScreenName(context, screen, screenNames);
		context.ScreenContext.UpdateCurrentScreenName(matchScreenName);
		return matchScreenName;
	}

	private static LostVoidInteractTarget? MatchCurrentInteractTarget(ZContext context, Mat screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("迷失之地-大世界", "区域-交互文本");
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect);
		foreach (OcrMatchResult item in ocrResultList)
		{
			LostVoidInteractTarget lostVoidInteractTarget = LostVoidInteractService.Instance.MatchInteractTarget(item.Text, context.GameTextResolver);
			if (lostVoidInteractTarget != null)
			{
				return lostVoidInteractTarget;
			}
		}
		return null;
	}

	private static IReadOnlyList<string>? BuildLabelList(LostVoidDetector detector, IReadOnlyList<string> ignoreList)
	{
		if (ignoreList.Count == 0 || detector.CoreDetector.Classes.Count == 0)
		{
			return null;
		}
		return (from item in detector.CoreDetector.Classes.Values
			select item.ClassName into label
			where label.Length <= 5 || !ignoreList.Contains<string>(label.Substring(5), StringComparer.Ordinal)
			select label).ToArray();
	}
}
