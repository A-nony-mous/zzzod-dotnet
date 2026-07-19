using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地层间移动。
/// </summary>
public sealed class LostVoidRunLevel : ZOperation
{
	private const int NoInBattleThreshold = 3;

	public const string StatusNextLevel = "进入下层";

	public const string StatusComplete = "通关";

	public const string StatusInBattle = "进入战斗";

	public const string StatusNeedDetect = "需要重新识别";

	public const string StatusInteract = "交互";

	public const string StatusNeedUpdatePriority = "需要更新优先级";

	public const string StatusAppendAgentTypePriority = "需要追加代理人类型优先级";

	public const string StatusNeedMoveInteract = "识别需移动交互";

	public const string StatusBattleFail = "迷失之地-战斗失败";

	public const string StatusUnknownScreen = "未知画面";

	public const string StatusPrepareRetry = "准备重试";

	public const string StatusPrepareFinalExit = "准备最终退出";

	public const string StatusTimeout = "执行超时";

	public const string ItBattle = "xxxx-战斗";

	private readonly LostVoidRunRecord _runRecord;

	private readonly ILostVoidRunLevelRuntime _runtime;

	private int _nothingTimes;

	private int _findTargetFailCount;

	private int _roomInitedTimes;

	private CancellationToken _cancellationToken;

	private int _noInBattleTimes;

	private bool _interactAttempted;

	private bool _lastFrameInBattle = true;

	private bool _currentFrameInBattle = true;

	private bool _rewardEvalFound;

	private bool _rewardDnFound;

	private bool _clickChallengeConfirm;

	private bool _bossPreBattle;

	private int _talkOptionIndex;

	private DateTimeOffset? _lastDetectTimeUtc;

	private DateTimeOffset? _lastCheckFinishTimeUtc;

	private long _lastBattleRoundDiagnosticAtMilliseconds;

	private long _lastTransitionDiagnosticAtMilliseconds;

	private readonly List<string> _hadBeenList = new List<string>();

	private readonly List<string> _interactedTargetKeyList = new List<string>();

	public string RegionType { get; private set; }

	public ZContext GameContext => base.ZContext;

	public LostVoidInteractTarget? InteractTarget { get; private set; }

	public bool BossPreBattle => _bossPreBattle;

	internal bool LastFrameInBattle => _lastFrameInBattle;

	internal int NoInBattleTimes => _noInBattleTimes;

	internal DateTimeOffset? LastDetectTimeUtc => _lastDetectTimeUtc;

	internal DateTimeOffset? LastCheckFinishTimeUtc => _lastCheckFinishTimeUtc;

	internal int TalkOptionIndex
	{
		get
		{
			return _talkOptionIndex;
		}
		set
		{
			_talkOptionIndex = value;
		}
	}

	public IReadOnlyList<string> HadBeenList => _hadBeenList;

	public IReadOnlyList<string> InteractedTargetKeyList => _interactedTargetKeyList;

	public LostVoidRunLevel(ZContext context, LostVoidRunRecord runRecord, string regionType, ILostVoidRunLevelRuntime? runtime = null)
		: base(context, "迷失之地-层间移动")
	{
		_runRecord = runRecord;
		_runtime = runtime ?? ScreenLostVoidRunLevelRuntime.Instance;
		RegionType = LostVoidRegionType.FromValue(regionType);
		_bossPreBattle = string.Equals(RegionType, "战斗-终结之役", StringComparison.Ordinal);
	}

	internal bool ShouldLogTransitionDiagnostic()
	{
		return ShouldLogDiagnostic(ref _lastTransitionDiagnosticAtMilliseconds);
	}

	public string InitForRegionTypeStatus()
	{
		string regionType = RegionType;
		if (1 == 0)
		{
		}
		string result;
		switch (regionType)
		{
		case "战斗-道中危机":
			result = "战斗区域";
			break;
		case "战斗-终结之役":
			if (_bossPreBattle)
			{
				goto default;
			}
			result = "战斗区域";
			break;
		case "挑战-限时":
			if (!_clickChallengeConfirm)
			{
				goto default;
			}
			result = ResetChallengeConfirmAndReturnBattle();
			break;
		default:
			result = "非战斗区域";
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	public OperationRoundResult EnterBattle(DateTimeOffset? screenshotTimeUtc = null, bool endBossPreBattle = false)
	{
		if (endBossPreBattle)
		{
			_bossPreBattle = false;
		}
		_nothingTimes = 0;
		DateTimeOffset value = screenshotTimeUtc ?? base.LastScreenshotTimeUtc ?? DateTimeOffset.UtcNow;
		_lastDetectTimeUtc = value;
		_lastCheckFinishTimeUtc = value;
		return RoundSuccess("进入战斗");
	}

	public string GetInteractTargetKey(LostVoidInteractTarget target)
	{
		return target.Icon + ":" + target.Name;
	}

	public void RecordAfterInteract(bool inNormalWorld)
	{
		if (InteractTarget != null)
		{
			string interactTargetKey = GetInteractTargetKey(InteractTarget);
			if (!_interactedTargetKeyList.Contains<string>(interactTargetKey, StringComparer.Ordinal))
			{
				_interactedTargetKeyList.Add(interactTargetKey);
			}
		}
		if (inNormalWorld && InteractTarget != null && string.Equals(InteractTarget.Name, "奥菲莉亚", StringComparison.Ordinal))
		{
			base.ZContext.LostVoid.HadInteractedOpheliaOnCurrentLevel = true;
		}
	}

	public void ApplyChallengeResultFinish(bool rewardEvalFound, bool rewardDnFound)
	{
		AccumulateChallengeResultRewards(rewardEvalFound, rewardDnFound);
		ApplyAccumulatedChallengeResultRewards();
	}

	internal void AccumulateChallengeResultRewards(bool rewardEvalFound, bool rewardDnFound)
	{
		_rewardEvalFound |= rewardEvalFound;
		_rewardDnFound |= rewardDnFound;
	}

	private void ApplyAccumulatedChallengeResultRewards()
	{
		if (_rewardEvalFound)
		{
			_runRecord.EvalPointComplete = false;
			_runRecord.PeriodRewardComplete = false;
		}
		else
		{
			_runRecord.EvalPointComplete = true;
			_runRecord.PeriodRewardComplete = !_rewardDnFound;
		}
	}

	/// <summary>
	/// 暂停时停止自动战斗并释放前进键。
	/// </summary>
	public void HandlePause()
	{
		_runtime.StopAutoBattle(this);
		base.ZContext.AutoBattleContext.MoveW(press: false, null, release: true);
	}

	/// <summary>
	/// 仅在自动战斗节点恢复自动战斗。
	/// </summary>
	public void HandleResume()
	{
		if (string.Equals(base.CurrentNode.Name, "战斗中", StringComparison.Ordinal))
		{
			base.ZContext.AutoBattleContext.ResumeAutoBattle();
		}
	}

	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_cancellationToken = cancellationToken;
		return Task.CompletedTask;
	}

	[NodeFrom("非战斗画面识别", Status = "未在大世界")]
	[NodeFrom("非战斗画面识别", Status = "按钮-挑战-确认")]
	[NodeFrom("处理寻路失败", Status = "准备重试")]
	[OperationNode("等待加载", IsStartNode = true, NodeMaxRetryTimes = 60)]
	private async Task<OperationRoundResult> WaitLoadingAsync()
	{
		LostVoidRunLevelLoadingState state = await _runtime.GetLoadingStateAsync(this, base.LastScreenshot, base.LastScreenshotTimeUtc, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (state.InNormalWorld)
		{
			_roomInitedTimes = 0;
			return RoundSuccess("大世界");
		}
		if (state.IsChoosingReward)
		{
			InteractTarget = new LostVoidInteractTarget("未知", "感叹号", isAgent: false, isNpc: false, isEntry: false, isExclamation: true);
			return RoundSuccess("识别正在交互");
		}
		if (state.ChallengeConfirmAvailable)
		{
			RegionType = "挑战-限时";
			_clickChallengeConfirm = true;
			return RoundWait("按钮-挑战-确认");
		}
		if (!string.IsNullOrWhiteSpace(state.TalkStatus))
		{
			return RoundWait(state.TalkStatus);
		}
		return RoundRetry("未找到攻击交互按键", null, TimeSpan.FromSeconds(1L));
	}

	[NodeFrom("等待加载")]
	[OperationNode("区域类型初始化")]
	private OperationRoundResult InitForRegionType()
	{
		return RoundSuccess(InitForRegionTypeStatus());
	}

	[NodeFrom("区域类型初始化", Status = "非战斗区域")]
	[NodeFrom("非战斗画面识别", Status = "0001-距离")]
	[NodeFrom("非战斗画面识别", Status = "需要重新识别")]
	[NodeFrom("交互后处理", Status = "大世界")]
	[NodeFrom("战斗中", Status = "识别需移动交互")]
	[NodeFrom("尝试交互", Success = false)]
	[NodeFrom("更新优先级")]
	[NodeFrom("追加代理人类型优先级", Status = "非战斗区域")]
	[OperationNode("非战斗画面识别", TimeoutSeconds = 180.0)]
	private async Task<OperationRoundResult> NonBattleCheckAsync()
	{
		LostVoidRunLevelWorldState worldState = await _runtime.GetNonBattleWorldStateAsync(this, base.LastScreenshot, base.LastScreenshotTimeUtc, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!worldState.InNormalWorld)
		{
			if (worldState.ChallengeConfirmAvailable)
			{
				RegionType = "挑战-限时";
				_clickChallengeConfirm = true;
				return RoundSuccess("按钮-挑战-确认");
			}
			_nothingTimes++;
			return (_nothingTimes >= 10) ? RoundSuccess("未在大世界") : RoundWait("未在大世界", null, TimeSpan.FromSeconds(1L));
		}
		if (base.OperationUsageTime >= TimeSpan.FromSeconds(600L))
		{
			return RoundFail("执行超时");
		}
		LostVoidRunLevelFrame frame = await _runtime.GetNonBattleFrameAsync(this, base.LastScreenshot, base.LastScreenshotTimeUtc, _hadBeenList, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (_bossPreBattle)
		{
			if (frame.BossBattleStarted)
			{
				return EnterBattle(base.LastScreenshotTimeUtc, endBossPreBattle: true);
			}
			if (frame.BossInteractAvailable)
			{
				_nothingTimes = 0;
				InteractTarget = new LostVoidInteractTarget("未知", "感叹号", isAgent: false, isNpc: false, isEntry: false, isExclamation: true);
				return RoundSuccess("0000-感叹号", null, TimeSpan.FromMilliseconds(500L));
			}
		}
		if (string.Equals(RegionType, "挚交会谈", StringComparison.Ordinal))
		{
			OperationRoundResult friendlyTalkInit = _runtime.HandleFriendlyTalkInit(this, _roomInitedTimes);
			if (friendlyTalkInit != null)
			{
				_roomInitedTimes++;
				return friendlyTalkInit;
			}
		}
		var (withInteract, withDistance, withEntry) = LostVoidDetectorResultHelper.IsFrameWithAll(frame.DetectResult);
		if (withInteract)
		{
			return await MoveByDetectionAsync("0000-感叹号", stopWhenInteract: true, stopWhenDisappear: false, _bossPreBattle).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (withDistance && !_bossPreBattle)
		{
			return await MoveByDetectionAsync("0001-距离", stopWhenInteract: false).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (!_bossPreBattle && !base.ZContext.LostVoid.PriorityUpdated)
		{
			return RoundSuccess("需要更新优先级");
		}
		if (withEntry && !_bossPreBattle)
		{
			return await MoveByDetectionAsync("xxxx-入口", stopWhenInteract: true, stopWhenDisappear: false, allowArrivalByInteractButton: false, _hadBeenList).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (_runtime.CheckBattleEncounterInCurrentFrame(this, base.LastScreenshot, base.LastScreenshotTimeUtc))
		{
			return EnterBattle(base.LastScreenshotTimeUtc, _bossPreBattle);
		}
		_runtime.TurnToFindTarget(this);
		_nothingTimes++;
		if (_nothingTimes >= 50)
		{
			_nothingTimes = 0;
			return RoundSuccess("处理寻路失败");
		}
		return _runtime.CheckBattleEncounterInPeriod(this, 0.5f) ? EnterBattle(DateTimeOffset.UtcNow, _bossPreBattle) : RoundWait("转动识别目标", null, TimeSpan.FromMilliseconds(500L));
	}

	[NodeFrom("非战斗画面识别", Status = "需要更新优先级")]
	[OperationNode("更新优先级")]
	private async Task<OperationRoundResult> UpdatePriorityAsync()
	{
		OperationResult result = await _runtime.UpdatePriorityAsync(this, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess)
		{
			base.ZContext.LostVoid.PriorityUpdated = true;
			return RoundSuccess("需要追加代理人类型优先级");
		}
		return RoundFail(result.Status);
	}

	[NodeFrom("更新优先级", Status = "需要追加代理人类型优先级")]
	[OperationNode("追加代理人类型优先级")]
	private async Task<OperationRoundResult> AppendAgentTypePriorityAsync()
	{
		OperationResult result = await _runtime.AppendAgentTypePriorityAsync(this, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess("非战斗区域") : RoundFail(result.Status);
	}

	[NodeFrom("非战斗画面识别", Status = "xxxx-入口")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("下层入口处理")]
	private OperationRoundResult OnEntry()
	{
		return RoundSuccess();
	}

	[NodeFrom("非战斗画面识别", Status = "0000-感叹号")]
	[NodeFrom("下层入口处理")]
	[OperationNode("尝试交互")]
	private async Task<OperationRoundResult> TryInteractAsync()
	{
		LostVoidTryInteractResult result = await _runtime.TryInteractAsync(this, InteractTarget, _interactedTargetKeyList, _interactAttempted, base.LastScreenshot, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		InteractTarget = result.Target ?? InteractTarget;
		_interactAttempted = result.InteractAttempted;
		LostVoidTryInteractKind kind = result.Kind;
		if (1 == 0)
		{
		}
		OperationRoundResult result2 = kind switch
		{
			LostVoidTryInteractKind.Wait => RoundWait(result.Status, null, result.Delay), 
			LostVoidTryInteractKind.Success => RoundSuccess(result.Status), 
			LostVoidTryInteractKind.Fail => RoundFail(result.Status), 
			_ => RoundRetry(result.Status, null, result.Delay), 
		};
		if (1 == 0)
		{
		}
		return result2;
	}

	[NodeFrom("等待加载", Status = "识别正在交互")]
	[NodeFrom("尝试交互", Status = "交互成功")]
	[NodeFrom("战斗中", Status = "识别正在交互")]
	[OperationNode("交互处理")]
	private async Task<OperationRoundResult> HandleInteractAsync()
	{
		LostVoidInteractResult result = await _runtime.HandleInteractAsync(this, InteractTarget, base.LastScreenshot, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (result.Target != null)
		{
			InteractTarget = result.Target;
		}
		if (!string.IsNullOrWhiteSpace(result.HadBeenType) && !_hadBeenList.Contains<string>(result.HadBeenType, StringComparer.Ordinal))
		{
			_hadBeenList.Add(result.HadBeenType);
		}
		LostVoidInteractResultKind kind = result.Kind;
		if (1 == 0)
		{
		}
		OperationRoundResult result2 = kind switch
		{
			LostVoidInteractResultKind.Wait => RoundWait(result.Status, null, result.Delay), 
			LostVoidInteractResultKind.Success => RoundSuccess(result.Status, result.Data, result.Delay), 
			LostVoidInteractResultKind.Fail => RoundFail(result.Status, result.Data, result.Delay), 
			_ => RoundRetry(result.Status ?? "未知画面", null, result.Delay), 
		};
		if (1 == 0)
		{
		}
		return result2;
	}

	[NodeFrom("交互处理", Status = "迷失之地-大世界")]
	[NodeFrom("交互处理", Status = "迷失之地-挑战结果")]
	[NodeFrom("交互处理", Status = "进入下层")]
	[NodeFrom("交互处理", Success = false, Status = "未知画面")]
	[OperationNode("交互后处理", NodeMaxRetryTimes = 10)]
	private async Task<OperationRoundResult> AfterInteractAsync()
	{
		LostVoidAfterInteractState state = await _runtime.GetAfterInteractStateAsync(this, InteractTarget, base.LastScreenshot, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		RecordAfterInteract(state.InNormalWorld);
		if (state.InNormalWorld)
		{
			if (!_bossPreBattle || InteractTarget == null || InteractTarget.AfterBattle)
			{
				_runtime.MoveAfterInteract(this, InteractTarget);
			}
			return RoundSuccess("大世界", null, TimeSpan.FromSeconds(1L));
		}
		if (state.ChallengeResultConfirmAvailable)
		{
			return RoundSuccess("挑战结果-确定", null, TimeSpan.FromSeconds(2L));
		}
		if (state.ChallengeResultFinishAvailable)
		{
			return RoundSuccess("挑战结果-完成", null, TimeSpan.FromSeconds(2L));
		}
		LostVoidInteractTarget interactTarget = InteractTarget;
		return (interactTarget != null && interactTarget.IsEntry) ? RoundSuccess("进入下层", InteractTarget.Icon) : RoundRetry("等待画面返回", null, TimeSpan.FromSeconds(1L));
	}

	[NodeFrom("非战斗画面识别", Status = "进入战斗")]
	[NodeFrom("非战斗画面识别", Status = "遭遇战斗")]
	[NodeFrom("区域类型初始化", Status = "战斗区域")]
	[OperationNode("准备自动战斗")]
	private OperationRoundResult InitAutoOp()
	{
		_runtime.StartAutoBattle(this);
		return RoundSuccess();
	}

	[NodeFrom("准备自动战斗")]
	[OperationNode("战斗中", Mute = true, TimeoutSeconds = 600.0)]
	private async Task<OperationRoundResult> InBattleAsync()
	{
		_lastFrameInBattle = _currentFrameInBattle;
		long battleStateStartedAt = Stopwatch.GetTimestamp();
		bool logRoundDiagnostic = ShouldLogDiagnostic(ref _lastBattleRoundDiagnosticAtMilliseconds);
		if (logRoundDiagnostic)
		{
			base.ZContext.Logger.Information("[.NET诊断] 迷失之地战斗轮次: Phase=GetBattleState.Begin, ScreenshotTimeUtc={ScreenshotTimeUtc}, ScreenNull={ScreenNull}, LastFrameInBattle={LastFrameInBattle}, NoInBattleTimes={NoInBattleTimes}, LastDetectTimeUtc={LastDetectTimeUtc}, LastCheckFinishTimeUtc={LastCheckFinishTimeUtc}", base.LastScreenshotTimeUtc, base.LastScreenshot == null, _lastFrameInBattle, _noInBattleTimes, _lastDetectTimeUtc, _lastCheckFinishTimeUtc);
		}
		LostVoidBattleState state = await _runtime.GetBattleStateAsync(this, base.LastScreenshot, base.LastScreenshotTimeUtc, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!state.FrameValid)
		{
			return RoundRetry("未获取截图");
		}
		if (logRoundDiagnostic)
		{
			base.ZContext.Logger.Information("[.NET诊断] 迷失之地战斗轮次: Phase=GetBattleState.End, ElapsedMilliseconds={ElapsedMilliseconds:F2}, CurrentFrameInBattle={CurrentFrameInBattle}, TransitionCheckPerformed={TransitionCheckPerformed}, DetectorChecked={DetectorChecked}, NoLongerInBattleByDetection={NoLongerInBattleByDetection}, FinishScreenChecked={FinishScreenChecked}, NoInBattleTimes={NoInBattleTimes}, NoInBattleThreshold={NoInBattleThreshold}, LastDetectTimeUtc={LastDetectTimeUtc}, LastCheckFinishTimeUtc={LastCheckFinishTimeUtc}", Stopwatch.GetElapsedTime(battleStateStartedAt).TotalMilliseconds, state.CurrentFrameInBattle, state.TransitionCheckPerformed, state.DetectorChecked, state.NoLongerInBattleByDetection, state.FinishScreenChecked, _noInBattleTimes, NoInBattleThreshold, _lastDetectTimeUtc, _lastCheckFinishTimeUtc);
		}
		_currentFrameInBattle = state.CurrentFrameInBattle;
		DateTimeOffset frameTime = base.LastScreenshotTimeUtc ?? DateTimeOffset.UtcNow;
		if (state.DetectorChecked)
		{
			_lastDetectTimeUtc = frameTime;
		}
		if (state.FinishScreenChecked)
		{
			_lastCheckFinishTimeUtc = frameTime;
		}
		if (!state.TransitionCheckPerformed)
		{
			return RoundWaitForScreenshotRound(TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
		}
		if (_currentFrameInBattle)
		{
			if (state.NextRegionHint)
			{
				_runtime.StopAutoBattle(this);
				_noInBattleTimes = 0;
				return RoundSuccess("识别需移动交互");
			}
			_noInBattleTimes = (state.NoLongerInBattleByDetection ? (_noInBattleTimes + 1) : 0);
			if (_noInBattleTimes >= NoInBattleThreshold)
			{
				_runtime.StopAutoBattle(this);
				_noInBattleTimes = 0;
				return RoundSuccess("识别需移动交互");
			}
		}
		else if (state.InInteractScreen)
		{
			_noInBattleTimes++;
			if (_noInBattleTimes >= NoInBattleThreshold)
			{
				_runtime.StopAutoBattle(this);
				_noInBattleTimes = 0;
				if (state.BattleFailed)
				{
					return RoundSuccess("迷失之地-战斗失败");
				}
				InteractTarget = new LostVoidInteractTarget("战斗后", "战斗后", isAgent: false, isNpc: false, isEntry: false, isExclamation: false, isDistance: false, afterBattle: true);
				return RoundSuccess("识别正在交互");
			}
		}
		else
		{
			_noInBattleTimes = 0;
		}
		return RoundWaitForScreenshotRound(TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
	}

	private static bool ShouldLogDiagnostic(ref long lastDiagnosticAtMilliseconds)
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long num2 = Interlocked.Read(in lastDiagnosticAtMilliseconds);
		return num - num2 >= 1000 && Interlocked.CompareExchange(ref lastDiagnosticAtMilliseconds, num, num2) == num2;
	}

	[NodeFrom("交互后处理", Status = "挑战结果-确定")]
	[OperationNode("挑战结果处理确定")]
	private Task<OperationRoundResult> HandleChallengeResultConfirmAsync()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-挑战结果", "按钮-确定") };
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "迷失之地-挑战结果", "按钮-确定", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
		return Task.FromResult(operationRoundResult.IsSuccess ? RoundSuccess("进入下层", "挚交会谈", operationRoundResult.Delay) : operationRoundResult);
	}

	[NodeFrom("交互后处理", Status = "挑战结果-完成")]
	[OperationNode("挑战结果处理完成")]
	private Task<OperationRoundResult> HandleChallengeResultFinishAsync()
	{
		if (RoundByFindArea(base.LastScreenshot, "迷失之地-挑战结果", "奖励-零号业绩").IsSuccess)
		{
			_rewardEvalFound = true;
		}
		if (RoundByFindArea(base.LastScreenshot, "迷失之地-挑战结果", "奖励-丁尼").IsSuccess)
		{
			_rewardDnFound = true;
		}
		Mat? lastScreenshot = base.LastScreenshot;
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-挑战结果", "按钮-完成") };
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "迷失之地-挑战结果", "按钮-完成", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
		if (!operationRoundResult.IsSuccess)
		{
			return Task.FromResult(operationRoundResult);
		}
		ApplyAccumulatedChallengeResultRewards();
		return Task.FromResult(RoundSuccess("通关", "挚交会谈", operationRoundResult.Delay));
	}

	[NodeFrom("非战斗画面识别", Success = false, Status = "处理寻路失败")]
	[OperationNode("处理寻路失败")]
	private async Task<OperationRoundResult> HandleFindTargetFailAsync()
	{
		if (_findTargetFailCount < 3)
		{
			_findTargetFailCount++;
			OperationResult retry = await _runtime.RestartForRetryAsync(this, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return retry.IsSuccess ? RoundSuccess("准备重试") : RoundFail(retry.Status);
		}
		return RoundSuccess("准备最终退出");
	}

	[NodeFrom("非战斗画面识别", Success = false, Status = "执行超时")]
	[NodeFrom("非战斗画面识别", Success = false, Status = "节点超时")]
	[NodeFrom("战斗中", Success = false, Status = "执行超时")]
	[NodeFrom("战斗中", Success = false, Status = "节点超时")]
	[NodeFrom("处理寻路失败", Status = "准备最终退出")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("保存错误信息")]
	private async Task<OperationRoundResult> PushErrorAsync()
	{
		OperationResult result = await _runtime.PushErrorAsync(this, base.LastScreenshot, base.PreviousNode.Name, base.PreviousNode.Status, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundFail(result.Status) : RoundRetry(result.Status);
	}

	[NodeFrom("保存错误信息", Success = false)]
	[OperationNode("失败退出空洞")]
	private async Task<OperationRoundResult> FailExitLostVoidAsync()
	{
		_runtime.StopAutoBattle(this);
		return RoundByOperationResult(await _runtime.FailExitAsync(this, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("战斗中", Status = "迷失之地-战斗失败")]
	[OperationNodeNotify(OperationNodeNotifyTiming.PreviousDone, Detail = true)]
	[OperationNode("处理战斗失败")]
	private OperationRoundResult HandleBattleFail()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-战斗失败", "按钮-撤退") };
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "迷失之地-战斗失败", "按钮-撤退", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
	}

	[NodeFrom("失败退出空洞")]
	[NodeFrom("处理战斗失败")]
	[OperationNode("点击失败退出完成")]
	private OperationRoundResult HandleFailExitAsync()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-挑战结果", "按钮-完成") };
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "迷失之地-挑战结果", "按钮-完成", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
		return operationRoundResult.IsSuccess ? RoundSuccess("通关", "入口", operationRoundResult.Delay) : operationRoundResult;
	}

	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		if (base.CurrentNode.Name == "战斗中")
		{
			_runtime.StopAutoBattle(this);
		}
		return Task.CompletedTask;
	}

	private async Task<OperationRoundResult> MoveByDetectionAsync(string targetType, bool stopWhenInteract = true, bool stopWhenDisappear = true, bool allowArrivalByInteractButton = false, IReadOnlyList<string>? ignoreEntries = null)
	{
		_nothingTimes = 0;
		OperationResult moveResult = await _runtime.MoveByDetectionAsync(this, RegionType, targetType, stopWhenInteract, stopWhenDisappear, allowArrivalByInteractButton, ignoreEntries ?? Array.Empty<string>(), _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!moveResult.IsSuccess)
		{
			return (string.Equals(moveResult.Status, "执行超时", StringComparison.Ordinal) || string.Equals(moveResult.Status, "节点超时", StringComparison.Ordinal)) ? RoundFail("执行超时") : RoundRetry(moveResult.Status ?? "移动失败");
		}
		if (string.Equals(moveResult.Status, "遭遇战斗", StringComparison.Ordinal))
		{
			if (_bossPreBattle)
			{
				if ((await _runtime.GetNonBattleFrameAsync(this, base.LastScreenshot, base.LastScreenshotTimeUtc, _hadBeenList, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).BossBattleStarted)
				{
					return EnterBattle(base.LastScreenshotTimeUtc, endBossPreBattle: true);
				}
				return RoundWait("等待BOSS进入战斗", null, TimeSpan.FromMilliseconds(200L));
			}
			InteractTarget = new LostVoidInteractTarget("战斗后", "战斗后", isAgent: false, isNpc: false, isEntry: false, isExclamation: false, isDistance: false, afterBattle: true);
			return RoundSuccess("遭遇战斗");
		}
		if (string.Equals(moveResult.Status, "交互", StringComparison.Ordinal))
		{
			InteractTarget = new LostVoidInteractTarget("未知", "感叹号", isAgent: false, isNpc: false, isEntry: false, isExclamation: true);
			return RoundSuccess("未在大世界");
		}
		if (string.Equals(moveResult.Status, "需要重新识别", StringComparison.Ordinal))
		{
			return RoundSuccess("需要重新识别");
		}
		if (string.Equals(targetType, "0000-感叹号", StringComparison.Ordinal))
		{
			InteractTarget = new LostVoidInteractTarget("感叹号", "感叹号", isAgent: false, isNpc: false, isEntry: false, isExclamation: true);
			return RoundSuccess("0000-感叹号", null, TimeSpan.FromSeconds(1L));
		}
		if (string.Equals(targetType, "0001-距离", StringComparison.Ordinal))
		{
			InteractTarget = new LostVoidInteractTarget("0001-距离", "0001-距离", isAgent: false, isNpc: false, isEntry: false, isExclamation: false, isDistance: true);
			return RoundSuccess("0001-距离");
		}
		string interactType = (moveResult.Data as string) ?? "入口";
		InteractTarget = new LostVoidInteractTarget(interactType, interactType, isAgent: false, isNpc: false, isEntry: true);
		return RoundSuccess("xxxx-入口", null, TimeSpan.FromSeconds(1L));
	}

	private string ResetChallengeConfirmAndReturnBattle()
	{
		_clickChallengeConfirm = false;
		return "战斗区域";
	}
}
