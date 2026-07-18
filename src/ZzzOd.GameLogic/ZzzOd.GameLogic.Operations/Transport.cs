using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 从任意常见状态传送到指定区域和传送点。
/// </summary>
public sealed class Transport : ZOperation
{
	/// <summary>
	/// 不在地图页面的状态文本。
	/// </summary>
	public const string StatusNotInMap = "未在地图页面";

	private readonly string _areaName;

	private readonly string _tpName;

	private readonly bool _waitAtLast;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private readonly TimeSpan _mapOpenDelay;

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	/// <summary>
	/// 初始化传送操作。
	/// </summary>
	public Transport(ZContext context, string areaName, string tpName, bool waitAtLast = true, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null, TimeSpan? timeout = null, TimeSpan? mapOpenDelay = null, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null)
		: base(context, "传送 " + areaName + " " + tpName, 3, timeout?.TotalSeconds ?? (-1.0))
	{
		_areaName = areaName;
		_tpName = tpName;
		_waitAtLast = waitAtLast;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
		_mapOpenDelay = mapOpenDelay ?? TimeSpan.FromSeconds(2L);
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
	}

	[OperationNode("画面识别", IsStartNode = true)]
	private OperationRoundResult CheckScreen()
	{
		return IsMapScreen(base.LastScreenshot) ? RoundSuccess() : RoundSuccess("未在地图页面");
	}

	[NodeFrom("画面识别", Status = "未在地图页面")]
	[OperationNode("返回大世界")]
	private async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("返回大世界")]
	[OperationNode("打开地图")]
	private OperationRoundResult OpenMap()
	{
		if (IsMapScreen(base.LastScreenshot))
		{
			return RoundSuccess();
		}
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string applicationId = ActionLevelDebugEvidenceWriter.GetApplicationId();
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(applicationId + "-transport-open-map");
		MapScreenRecognitionSummary beforeSummary = (isEnabled ? GetMapScreenRecognitionSummary(base.LastScreenshot) : null);
		AreaClickTargetSummary actionTarget = (isEnabled ? GetAreaClickTargetSummary(base.LastScreenshot, "大世界", "地图") : null);
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", base.LastScreenshot) : null);
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "大世界", "地图", preDelay, null, retryDelay);
		MapScreenRecognitionSummary mapScreenRecognitionSummary = null;
		string text = null;
		if (isEnabled)
		{
			if (operationRoundResult.IsSuccess && _mapOpenDelay > TimeSpan.Zero)
			{
				Thread.Sleep(_mapOpenDelay);
			}
			Mat mat = Screenshot();
			mapScreenRecognitionSummary = GetMapScreenRecognitionSummary(mat);
			text = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat);
			WriteOpenMapEvidence(fileStem, beforeScreenshotPath, beforeSummary, actionTarget, text, mapScreenRecognitionSummary, operationRoundResult);
		}
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry(operationRoundResult.Status, null, _retryDelay);
		}
		return RoundWait(operationRoundResult.Status, null, isEnabled ? TimeSpan.Zero : _mapOpenDelay);
	}

	[NodeFrom("打开地图")]
	[NodeFrom("画面识别")]
	[OperationNode("执行传送")]
	private async Task<OperationRoundResult> DoTransport()
	{
		MapTransport operation = new MapTransport(base.ZContext, _areaName, _tpName, _retryDelay, _preClickDelay);
		return RoundByOperationResult(await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("执行传送")]
	[OperationNode("等待大世界加载")]
	private async Task<OperationRoundResult> WaitInWorld()
	{
		if (!_waitAtLast)
		{
			return RoundSuccess("不等待大世界加载");
		}
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		BackToNormalWorld backToNormalWorld = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, _retryDelay, _preClickDelay);
		return backToNormalWorld.ExecuteAsync();
	}

	/// <summary>
	/// 判断当前截图是否为地图选择画面。
	/// </summary>
	public bool IsMapScreen(Mat? screen)
	{
		return GetMapScreenRecognitionSummary(screen).IsMapScreen;
	}

	/// <summary>
	/// 获取地图选择画面判断明细。
	/// </summary>
	public MapScreenRecognitionSummary GetMapScreenRecognitionSummary(Mat? screen)
	{
		if (screen == null)
		{
			return new MapScreenRecognitionSummary
			{
				BackButtonResult = "未获取截图",
				FailureReason = "未获取截图"
			};
		}
		List<string> targetWords = base.ZContext.MapService.AreaList.Select((MapArea area) => area.AreaName).ToList();
		List<string> targetWords2 = base.ZContext.MapService.AreaList.SelectMany((MapArea area) => area.TpList).ToList();
		int num = 0;
		int num2 = 0;
		IReadOnlyList<OcrMatchResult> source = (from result in base.ZContext.OcrService.GetOcrResultList(screen)
			orderby result.Y, result.X
			select result).ToArray();
		string[] array = source.Select((OcrMatchResult result) => result.Text).Distinct<string>(StringComparer.Ordinal).ToArray();
		string[] array2 = array;
		foreach (string word in array2)
		{
			if (StringUtils.FindBestMatchByDifflib(word, targetWords).HasValue)
			{
				num++;
			}
			if (StringUtils.FindBestMatchByDifflib(word, targetWords2).HasValue)
			{
				num2++;
			}
		}
		OperationRoundResult operationRoundResult = RoundByFindArea(screen, "地图", "左上角返回");
		string matchScreenName = ScreenUtils.GetMatchScreenName(base.ZContext, screen);
		string failureReason = null;
		if (num < 3)
		{
			failureReason = $"未在地图页面: 区域名数量不足 {num}/3";
		}
		else if (num2 < 1)
		{
			failureReason = $"未在地图页面: 传送点数量不足 {num2}/1";
		}
		else if (!operationRoundResult.IsSuccess)
		{
			failureReason = "未在地图页面: " + (operationRoundResult.Status ?? "左上角返回未命中");
		}
		return new MapScreenRecognitionSummary
		{
			ActiveScreenName = matchScreenName,
			AreaNameMatchCount = num,
			TransportPointNameMatchCount = num2,
			BackButtonResult = (operationRoundResult.Status ?? string.Empty),
			IsMapScreen = (num >= 3 && num2 >= 1 && operationRoundResult.IsSuccess),
			OcrTexts = array,
			FailureReason = failureReason
		};
	}

	private void WriteOpenMapEvidence(string fileStem, string? beforeScreenshotPath, MapScreenRecognitionSummary? beforeSummary, AreaClickTargetSummary? actionTarget, string? afterScreenshotPath, MapScreenRecognitionSummary? afterSummary, OperationRoundResult actionResult)
	{
		bool flag = afterSummary?.IsMapScreen ?? false;
		string failureReason = ((!actionResult.IsSuccess) ? actionResult.Status : (flag ? null : "after action did not satisfy IsMapScreen"));
		ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
		{
			FileStem = fileStem,
			AppId = ActionLevelDebugEvidenceWriter.GetApplicationId(),
			OperationName = base.OperationName,
			NodeName = "打开地图",
			DotNetMethod = "ZzzOd.GameLogic.Operations.Transport.OpenMap()",
			BaselineParityRequirement = "Transport.open_map opens the map by clicking area 大世界/地图; key j is not part of this business path.",
			BeforeScreenshotPath = beforeScreenshotPath,
			BeforeRecognitionSummary = beforeSummary,
			ActionKind = "click_area",
			ActionTarget = "大世界/地图",
			ActionTargetDetails = actionTarget,
			ExpectedNextState = "地图 page, IsMapScreen true",
			AfterScreenshotPath = afterScreenshotPath,
			AfterRecognitionSummary = afterSummary,
			TransitionResult = (flag ? "entered_map" : (actionResult.IsSuccess ? "not_entered_map_yet" : "action_failed")),
			FailureReason = failureReason,
			RetryStoppedBecauseOfSuspectedLoop = false
		});
	}

	private AreaClickTargetSummary GetAreaClickTargetSummary(Mat? screen, string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return new AreaClickTargetSummary
			{
				ScreenName = screenName,
				AreaName = areaName,
				FailureReason = "area not configured"
			};
		}
		AreaClickTargetSummary areaClickTargetSummary = new AreaClickTargetSummary
		{
			ScreenName = screenName,
			AreaName = areaName,
			AreaKind = (area.IsTextArea ? "text" : (area.IsTemplateArea ? "template" : "static")),
			PcAlt = area.PcAlt,
			GamepadAction = area.GamepadKey
		};
		if (screen == null)
		{
			areaClickTargetSummary.FailureReason = "screenshot missing";
			return areaClickTargetSummary;
		}
		if (area.IsTextArea)
		{
			OcrMatchResult ocrMatchResult = base.ZContext.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect).FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(area.Text, item.Text, area.LcsPercent));
			if (ocrMatchResult == null)
			{
				areaClickTargetSummary.FailureReason = "text target not found";
				return areaClickTargetSummary;
			}
			areaClickTargetSummary.ClickX = ocrMatchResult.Center.X;
			areaClickTargetSummary.ClickY = ocrMatchResult.Center.Y;
			areaClickTargetSummary.MatchConfidence = ocrMatchResult.Confidence;
			return areaClickTargetSummary;
		}
		if (area.IsTemplateArea)
		{
			MatchResult matchResult = ScreenUtils.FindTemplateCoordInArea(base.ZContext, screen, screenName, areaName);
			if (matchResult == null)
			{
				areaClickTargetSummary.FailureReason = "template target not found";
				return areaClickTargetSummary;
			}
			areaClickTargetSummary.ClickX = matchResult.Center.X;
			areaClickTargetSummary.ClickY = matchResult.Center.Y;
			areaClickTargetSummary.MatchConfidence = matchResult.Confidence;
			return areaClickTargetSummary;
		}
		OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
		areaClickTargetSummary.ClickX = center.X;
		areaClickTargetSummary.ClickY = center.Y;
		return areaClickTargetSummary;
	}
}
