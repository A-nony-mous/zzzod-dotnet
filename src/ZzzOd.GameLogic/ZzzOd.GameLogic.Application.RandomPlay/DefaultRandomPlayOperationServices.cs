using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Turning;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 默认录像店营业流程服务。
/// </summary>
public sealed class DefaultRandomPlayOperationServices : IRandomPlayOperationServices
{
	private static readonly IReadOnlyList<IReadOnlyList<int>> AgentNameColorRange = new IReadOnlyList<int>[2]
	{
		new int[3] { 230, 230, 230 },
		new int[3] { 255, 255, 255 }
	};

	private static readonly TimeSpan FindAndOcrClickPreDelay = TimeSpan.FromMilliseconds(300L);

	private readonly Action<TimeSpan> _sleep;

	private AngleTurnCompensator? _turnCompensator;

	private int _moveAndInteractEvidenceIndex;

	/// <summary>
	/// 初始化录像店营业操作服务。
	/// </summary>
	/// <param name="sleep">用于点击前等待的可替换时钟。</param>
	public DefaultRandomPlayOperationServices(Action<TimeSpan>? sleep = null)
	{
		_sleep = sleep ?? new Action<TimeSpan>(Thread.Sleep);
	}

	/// <inheritdoc />
	public Task<OperationResult> TransportAsync(ZContext context, RandomPlayTransportPoint point)
	{
		return new Transport(context, point.AreaName, point.TransportPointName).ExecuteAsync();
	}

	/// <inheritdoc />
	public void ClearPendingTurnSample()
	{
		_turnCompensator?.ClearPendingSample();
	}

	/// <inheritdoc />
	public Task<OperationRoundResult> MoveAndInteractAsync(ZContext context, RandomPlayConfig config, Mat? screen)
	{
		if (!(context.Controller is IZzzControllerActions zzzControllerActions))
		{
			return Task.FromResult(new OperationRoundResult(OperationRoundResultKind.Retry, "控制器不可用", null, TimeSpan.FromMilliseconds(500L)));
		}
		string text = null;
		string beforeScreenshotPath = null;
		RandomPlayMoveAndInteractRecognitionSummary beforeSummary = null;
		if (ActionLevelDebugEvidenceWriter.IsEnabled)
		{
			text = CreateMoveAndInteractEvidenceFileStem("random_play-move-and-interact");
			beforeScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(text, "before", screen);
			beforeSummary = GetMoveAndInteractRecognitionSummary(context, screen);
		}
		// 录像店-柜台 / 布亚斯特城区-录像店营业点：转向正东后前移再交互；澄辉坪-录像店营业点：传送落点已正对入口，直接交互
		bool isEastTurn = string.Equals(config.TransportPoint, RandomPlayTransportPoint.VideoStoreCounter.Value, StringComparison.Ordinal)
			|| string.Equals(config.TransportPoint, RandomPlayTransportPoint.BuyasteBusinessPoint.Value, StringComparison.Ordinal);
		if (isEastTurn)
		{
			if (!(context.Controller is ZPcController controller))
			{
				return Task.FromResult(new OperationRoundResult(OperationRoundResultKind.Retry, "转向控制器不可用", null, TimeSpan.FromMilliseconds(500L)));
			}
			if (_turnCompensator == null)
			{
				_turnCompensator = new AngleTurnCompensator(controller);
			}
			MiniMapAngleResult miniMapAngle = GetMiniMapAngle(context, screen);
			OperationRoundResult operationRoundResult = TurnToAngle(context, screen, miniMapAngle, _turnCompensator, 0.0, 2.0, "转向正东");
			if (!operationRoundResult.IsSuccess)
			{
				return Task.FromResult(operationRoundResult);
			}
			zzzControllerActions.MoveW(press: true, TimeSpan.FromSeconds(1L), release: true);
		}
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		if (text != null)
		{
			Thread.Sleep(TimeSpan.FromSeconds(2L));
			Mat mat = context.Controller?.Screenshot().Screen;
			RandomPlayMoveAndInteractRecognitionSummary moveAndInteractRecognitionSummary = GetMoveAndInteractRecognitionSummary(context, mat);
			string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(text, "after", mat);
			WriteMoveAndInteractEvidence(text, beforeScreenshotPath, beforeSummary, afterScreenshotPath, moveAndInteractRecognitionSummary, config);
		}
		return Task.FromResult(new OperationRoundResult(OperationRoundResultKind.Success, "移动交互"));
	}

	private OperationRoundResult TurnToAngle(ZContext context, Mat? screen, MiniMapAngleResult miniMap, AngleTurnCompensator compensator, double targetAngle, double angleThreshold, string turnStatus)
	{
		if (!miniMap.PlayMaskFound)
		{
			OperationRoundResult operationRoundResult = new OperationRoundResult(OperationRoundResultKind.Retry, "未识别到小地图", null, TimeSpan.FromSeconds(1L));
			WriteTurnEvidence(context, screen, screen, miniMap, null, null, null, compensator.Scale, compensator.Scale, targetAngle, angleThreshold, operationRoundResult.Status, "mini_map_missing");
			return operationRoundResult;
		}
		if (!miniMap.ViewAngle.HasValue)
		{
			OperationRoundResult operationRoundResult2 = new OperationRoundResult(OperationRoundResultKind.Retry, "识别朝向失败", null, TimeSpan.FromSeconds(1L));
			WriteTurnEvidence(context, screen, screen, miniMap, null, null, null, compensator.Scale, compensator.Scale, targetAngle, angleThreshold, operationRoundResult2.Status, "angle_missing");
			return operationRoundResult2;
		}
		double num = CalUtils.AngleDelta(miniMap.ViewAngle.Value, targetAngle);
		if (Math.Abs(num) <= angleThreshold)
		{
			return new OperationRoundResult(OperationRoundResultKind.Success);
		}
		double scale = compensator.Scale;
		double value = compensator.TurnFromAngle(miniMap.ViewAngle.Value, num);
		double scale2 = compensator.Scale;
		Mat mat = null;
		MiniMapAngleResult afterMiniMap = null;
		if (ActionLevelDebugEvidenceWriter.IsEnabled)
		{
			Thread.Sleep(TimeSpan.FromMilliseconds(250L));
			mat = context.Controller?.Screenshot().Screen;
			afterMiniMap = GetMiniMapAngle(context, mat);
		}
		WriteTurnEvidence(context, screen, mat, miniMap, afterMiniMap, num, value, scale, scale2, targetAngle, angleThreshold, turnStatus, "turn_issued");
		return new OperationRoundResult(OperationRoundResultKind.Retry, turnStatus, null, TimeSpan.FromMilliseconds(500L));
	}

	private static MiniMapAngleResult GetMiniMapAngle(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new MiniMapAngleResult(PlayMaskFound: false, null);
		}
		WorldPatrolMiniMapSnapshot worldPatrolMiniMapSnapshot = context.WorldPatrolService.CutMiniMap(context, screen);
		return new MiniMapAngleResult(worldPatrolMiniMapSnapshot.PlayMaskFound, worldPatrolMiniMapSnapshot.ViewAngle);
	}

	private string CreateMoveAndInteractEvidenceFileStem(string suffix)
	{
		int value = Interlocked.Increment(ref _moveAndInteractEvidenceIndex);
		return ActionLevelDebugEvidenceWriter.CreateFileStem($"{suffix}-{value:00}");
	}

	private RandomPlayMoveAndInteractRecognitionSummary GetMoveAndInteractRecognitionSummary(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new RandomPlayMoveAndInteractRecognitionSummary
			{
				ActiveScreenName = null,
				MiniMapAngle = new MiniMapAngleResult(PlayMaskFound: false, null),
				BusinessStatusResult = "未获取截图",
				FailureReason = "未获取截图"
			};
		}
		OperationResult operationResult = FindArea(context, screen, "影像店营业", "经营状况");
		OperationResult operationResult2 = FindArea(context, screen, "影像店营业", "昨日账本");
		OperationResult operationResult3 = FindArea(context, screen, "影像店营业", "右侧选项区域");
		return new RandomPlayMoveAndInteractRecognitionSummary
		{
			ActiveScreenName = ScreenUtils.GetMatchScreenName(context, screen),
			MiniMapAngle = GetMiniMapAngle(context, screen),
			BusinessStatusResult = (operationResult.Status ?? string.Empty),
			BusinessStatusVisible = operationResult.IsSuccess,
			YesterdayLedgerResult = (operationResult2.Status ?? string.Empty),
			YesterdayLedgerVisible = operationResult2.IsSuccess,
			RightOptionsResult = (operationResult3.Status ?? string.Empty),
			RightOptionsVisible = operationResult3.IsSuccess,
			OcrTexts = (from result in context.OcrService.GetOcrResultList(screen)
				orderby result.Y, result.X
				select result.Text).ToArray(),
			FailureReason = ((operationResult.IsSuccess || operationResult2.IsSuccess || operationResult3.IsSuccess) ? null : operationResult.Status)
		};
	}

	private static OperationResult FindArea(ZContext context, Mat screen, string screenName, string areaName)
	{
		FindAreaResultEnum findAreaResultEnum = ScreenUtils.FindArea(context, screen, screenName, areaName);
		return (findAreaResultEnum == FindAreaResultEnum.True) ? new OperationResult(IsSuccess: true, areaName) : new OperationResult(IsSuccess: false, "未找到 " + areaName);
	}

	private void WriteTurnEvidence(ZContext context, Mat? beforeScreenshot, Mat? afterScreenshot, MiniMapAngleResult beforeMiniMap, MiniMapAngleResult? afterMiniMap, double? angleDiff, double? effectiveAngleDiff, double scaleBefore, double scaleAfter, double targetAngle, double angleThreshold, string? failureReason, string transitionResult)
	{
		if (ActionLevelDebugEvidenceWriter.IsEnabled)
		{
			string fileStem = CreateMoveAndInteractEvidenceFileStem("random_play-move-and-interact-turn");
			string text = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", beforeScreenshot);
			string text2 = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", afterScreenshot);
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("random_play"),
				OperationName = "录像店营业",
				NodeName = "移动交互",
				DotNetMethod = "ZzzOd.GameLogic.Application.RandomPlay.DefaultRandomPlayOperationServices.MoveAndInteractAsync()",
				BaselineParityRequirement = "RandomPlay move_and_interact for 录像店-柜台 uses turn_to_angle target_angle=0, then moves forward for 1 second, waits 1 second, and presses interact for 0.2 seconds.",
				BeforeScreenshotPath = text,
				BeforeRecognitionSummary = GetMoveAndInteractRecognitionSummary(context, beforeScreenshot),
				ActionKind = "turn_to_angle",
				ActionTarget = "target_angle=0",
				ActionTargetDetails = new RandomPlayTurnActionTargetSummary
				{
					TargetAngle = targetAngle,
					AngleThreshold = angleThreshold,
					BeforeMiniMap = beforeMiniMap,
					AfterMiniMap = afterMiniMap,
					AngleDiff = angleDiff,
					EffectiveAngleDiff = effectiveAngleDiff,
					ScaleBefore = scaleBefore,
					ScaleAfter = scaleAfter,
					TurnDx = context.GameConfig.TurnDx
				},
				ExpectedNextState = "mini map angle recognized and facing east",
				AfterScreenshotPath = (text2 ?? text),
				AfterRecognitionSummary = GetMoveAndInteractRecognitionSummary(context, afterScreenshot ?? beforeScreenshot),
				TransitionResult = transitionResult,
				FailureReason = failureReason,
				RetryStoppedBecauseOfSuspectedLoop = false
			});
		}
	}

	private static void WriteMoveAndInteractEvidence(string fileStem, string? beforeScreenshotPath, RandomPlayMoveAndInteractRecognitionSummary? beforeSummary, string? afterScreenshotPath, RandomPlayMoveAndInteractRecognitionSummary afterSummary, RandomPlayConfig config)
	{
		bool flag = afterSummary.BusinessStatusVisible || afterSummary.YesterdayLedgerVisible || afterSummary.RightOptionsVisible;
		ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
		{
			FileStem = fileStem,
			AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("random_play"),
			OperationName = "录像店营业",
			NodeName = "移动交互",
			DotNetMethod = "ZzzOd.GameLogic.Application.RandomPlay.DefaultRandomPlayOperationServices.MoveAndInteractAsync()",
			BaselineParityRequirement = "RandomPlay move_and_interact for 录像店-柜台 uses turn_to_angle target_angle=0, then moves forward for 1 second, waits 1 second, and presses interact for 0.2 seconds.",
			BeforeScreenshotPath = beforeScreenshotPath,
			BeforeRecognitionSummary = beforeSummary,
			ActionKind = (string.Equals(config.TransportPoint, RandomPlayTransportPoint.VideoStoreCounter.Value, StringComparison.Ordinal) ? "turn_move_key_press" : "key_press"),
			ActionTarget = (string.Equals(config.TransportPoint, RandomPlayTransportPoint.VideoStoreCounter.Value, StringComparison.Ordinal) ? "target_angle=0; key=w; key=f" : "key=f"),
			ExpectedNextState = "影像店营业 page, 经营状况 or 昨日账本 visible",
			AfterScreenshotPath = afterScreenshotPath,
			AfterRecognitionSummary = afterSummary,
			TransitionResult = (flag ? "entered_random_play_business_page" : "business_page_not_visible"),
			FailureReason = (flag ? null : afterSummary.FailureReason),
			RetryStoppedBecauseOfSuspectedLoop = false
		});
	}

	/// <inheritdoc />
	public bool IsAreaVisible(ZContext context, Mat? screen, string screenName, string areaName)
	{
		return screen != null && ScreenUtils.FindArea(context, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	/// <inheritdoc />
	public OperationResult FindAndClickArea(ZContext context, Mat? screen, string screenName, string areaName)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		_sleep(FindAndOcrClickPreDelay);
		return ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, screenName, areaName), areaName);
	}

	/// <inheritdoc />
	public OperationResult ClickArea(ZContext context, string screenName, string areaName, TimeSpan? preDelay = null)
	{
		if (preDelay.HasValue)
		{
			TimeSpan valueOrDefault = preDelay.GetValueOrDefault();
			if (true)
			{
				Thread.Sleep(valueOrDefault);
			}
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 " + areaName);
		}
		ControllerBase? controller = context.Controller;
		OperationResult result;
		if (controller != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			if (controller.Click(position, null, pcAlt, gamepadKey))
			{
				result = new OperationResult(IsSuccess: true, areaName);
				goto IL_00ad;
			}
		}
		result = new OperationResult(IsSuccess: false, "点击失败 " + areaName);
		goto IL_00ad;
		IL_00ad:
		return result;
	}

	/// <inheritdoc />
	public OperationResult ClickText(ZContext context, Mat? screen, string targetText, string screenName, string areaName)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect);
		OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(targetText, item.Text, area?.LcsPercent ?? 0.5));
		if (ocrMatchResult == null)
		{
			return new OperationResult(IsSuccess: false, "找不到 " + targetText);
		}
		_sleep(FindAndOcrClickPreDelay);
		ControllerBase? controller = context.Controller;
		OperationResult result;
		if (controller != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point? position = ocrMatchResult.Center;
			bool pcAlt = area?.PcAlt ?? false;
			string gamepadAction = area?.GamepadKey;
			if (controller.Click(position, null, pcAlt, gamepadAction))
			{
				result = new OperationResult(IsSuccess: true, targetText);
				goto IL_015d;
			}
		}
		result = new OperationResult(IsSuccess: false, "点击失败 " + targetText);
		goto IL_015d;
		IL_015d:
		return result;
	}

	/// <inheritdoc />
	public bool TrySelectAgent(ZContext context, Mat? screen, string agentName)
	{
		if (screen == null)
		{
			return false;
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("影像店营业", "宣传员列表");
		if (area == null)
		{
			return false;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, AgentNameColorRange, area.Rect);
		OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(agentName, item.Text, area.LcsPercent));
		if (ocrMatchResult != null)
		{
			_sleep(FindAndOcrClickPreDelay);
			ControllerBase? controller = context.Controller;
			int result;
			if (controller == null)
			{
				result = 0;
			}
			else
			{
				OneDragon.Core.Abstractions.Geometry.Point? position = ocrMatchResult.Center;
				bool pcAlt = area.PcAlt;
				string gamepadKey = area.GamepadKey;
				result = (controller.Click(position, null, pcAlt, gamepadKey) ? 1 : 0);
			}
			return (byte)result != 0;
		}
		MatchResult posByAvatar = GetPosByAvatar(context, screen, agentName, area);
		return posByAvatar != null && (context.Controller?.Click(posByAvatar.Center) ?? false);
	}

	/// <inheritdoc />
	public void ScrollPromoterList(ZContext context)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("影像店营业", "宣传员列表");
		if (area != null)
		{
			ScreenUtils.ScrollArea(context, area);
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<string> ReadVideoThemes(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		string[] array = new string[3] { "录像带主题-1", "录像带主题-2", "录像带主题-3" };
		foreach (string areaName in array)
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("影像店营业", areaName);
			if (area == null)
			{
				continue;
			}
			using Mat image = CvImageUtils.Crop(screen, area.Rect);
			string ocrText = context.OcrService.RunOcrSingleLineForCrop(
				image,
				screen.Width,
				screen.Height,
				area.X1,
				area.Y1);
			string text = FindThemeByGameText(context, ocrText);
			if (text != null)
			{
				list.Add(text);
			}
		}
		return list;
	}

	/// <inheritdoc />
	public OperationResult ClickTheme(ZContext context, Mat? screen, string theme)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("影像店营业", "主题筛选");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 主题筛选");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		foreach (OcrMatchResult item in ocrResultList)
		{
			string a = FindThemeByGameText(context, item.Text);
			if (!string.Equals(a, theme, StringComparison.Ordinal))
			{
				continue;
			}
			ControllerBase? controller = context.Controller;
			OperationResult result;
			if (controller != null)
			{
				OneDragon.Core.Abstractions.Geometry.Point? position = item.Center;
				bool pcAlt = area.PcAlt;
				string gamepadKey = area.GamepadKey;
				if (controller.Click(position, null, pcAlt, gamepadKey))
				{
					result = new OperationResult(IsSuccess: true, theme);
					goto IL_011a;
				}
			}
			result = new OperationResult(IsSuccess: false, "点击失败 " + theme);
			goto IL_011a;
			IL_011a:
			return result;
		}
		return new OperationResult(IsSuccess: false, "未找到" + theme);
	}

	/// <inheritdoc />
	public void ScrollThemeList(ZContext context)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("影像店营业", "主题筛选");
		if (area != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
			OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(0, -100);
			context.Controller?.DragTo(end, center);
		}
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	private static MatchResult? GetPosByAvatar(ZContext context, Mat screen, string targetAgentName, OneDragon.Core.Screen.ScreenArea area)
	{
		Agent agent = AgentEnum.Values.Select((AgentEnum item) => item.Value).FirstOrDefault((Agent item) => string.Equals(item.AgentName, targetAgentName, StringComparison.Ordinal));
		if (agent == null)
		{
			return null;
		}
		using Mat source = CvImageUtils.Crop(screen, area.Rect);
		using (IEnumerator<string> enumerator = agent.TemplateIdList.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				string current = enumerator.Current;
				MatchResult matchResult = context.TemplateMatcher.MatchOneByFeature(
					source,
					"predefined_team",
					"avatar_" + current,
					visionContext: TemplateMatchVisionContext.ForCrop(screen.Width, screen.Height, area.X1, area.Y1));
				if (matchResult == null)
				{
					return null;
				}
				matchResult.AddOffset(area.LeftTop);
				return matchResult;
			}
		}
		return null;
	}

	private static string? FindThemeByGameText(ZContext context, string? ocrText)
	{
		string[] array = RandomPlayVideoThemes.All.Select(context.GameTextResolver).ToArray();
		string text = RandomPlayOperation.FindBestTheme(ocrText, array);
		int num = ((text == null) ? (-1) : Array.IndexOf(array, text));
		return (num >= 0) ? RandomPlayVideoThemes.All[num] : null;
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
}
