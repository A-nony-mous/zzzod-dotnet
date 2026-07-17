using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operations;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// ZZZ 业务 Operation 基类。
/// </summary>
public abstract class ZOperation : Operation
{
	private static readonly TimeSpan DefaultFindAndClickPreDelay = TimeSpan.FromMilliseconds(300L);

	private string? _lastRoundNodeName;

	/// <summary>
	/// ZZZ 类型化上下文。
	/// </summary>
	protected ZContext ZContext { get; }

	/// <summary>
	/// 上一次节点轮次截图。
	/// </summary>
	protected Mat? LastScreenshot { get; private set; }

	/// <summary>
	/// 上一次节点轮次截图时间。
	/// </summary>
	protected DateTimeOffset? LastScreenshotTimeUtc { get; private set; }

	/// <summary>
	/// 当前节点是否已经点击过。
	/// </summary>
	protected bool NodeClicked { get; private set; }

	/// <summary>
	/// 初始化 ZZZ Operation。
	/// </summary>
	protected ZOperation(ZContext context, string operationName = "", int nodeMaxRetryTimes = 3, double timeoutSeconds = -1.0)
		: base(context, operationName, nodeMaxRetryTimes, timeoutSeconds)
	{
		ZContext = context;
	}

	/// <inheritdoc />
	protected override Task BeforeNodeRoundAsync(string nodeName, CancellationToken cancellationToken)
	{
		if (!string.Equals(_lastRoundNodeName, nodeName, StringComparison.Ordinal))
		{
			NodeClicked = false;
			_lastRoundNodeName = nodeName;
		}
		Screenshot();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override Task OnNodeRoundCompletedAsync(string currentNodeName, MethodInfo currentNodeMethod, OperationRoundResult roundResult, string? nextNodeName, MethodInfo? nextNodeMethod, CancellationToken cancellationToken)
	{
		ZContext.OperationNotificationService.OnNodeCompleted(base.OperationName, LastScreenshot, currentNodeName, currentNodeMethod, roundResult, nextNodeName, nextNodeMethod);
		return Task.CompletedTask;
	}

	/// <summary>
	/// 截图并更新最后截图缓存。
	/// </summary>
	protected Mat? Screenshot(bool independent = false)
	{
		if (ZContext.Controller == null)
		{
			LastScreenshot = null;
			LastScreenshotTimeUtc = null;
			return null;
		}
		var (value, mat) = ZContext.Controller.Screenshot(independent);
		if (mat == null)
		{
			LastScreenshot = null;
			LastScreenshotTimeUtc = null;
			return null;
		}
		if (LastScreenshot != mat)
		{
			LastScreenshot?.Dispose();
		}
		LastScreenshot = mat;
		LastScreenshotTimeUtc = value;
		return LastScreenshot;
	}

	/// <summary>
	/// 按 BaselineParity wait_round_time 语义补足从本轮截图开始计算的最小轮次时间。
	/// </summary>
	protected OperationRoundResult RoundWaitForScreenshotRound(TimeSpan minimumRoundTime, string? status = null, object? data = null)
	{
		return RoundWait(status, data, GetRemainingScreenshotRoundDelay(minimumRoundTime));
	}

	/// <summary>
	/// 按本轮截图时间计算距 BaselineParity wait_round_time 还需等待的时长。
	/// </summary>
	protected TimeSpan GetRemainingScreenshotRoundDelay(TimeSpan minimumRoundTime)
	{
		TimeSpan timeSpan = (LastScreenshotTimeUtc.HasValue ? (DateTimeOffset.UtcNow - LastScreenshotTimeUtc.Value) : TimeSpan.Zero);
		TimeSpan timeSpan2 = minimumRoundTime - timeSpan;
		return (timeSpan2 > TimeSpan.Zero) ? timeSpan2 : TimeSpan.Zero;
	}

	/// <summary>
	/// 将子 Operation 最终结果转换成本轮结果。
	/// </summary>
	protected OperationRoundResult RoundByOperationResult(OperationResult operationResult, string? status = null, bool retryOnFail = false, TimeSpan? delay = null)
	{
		ArgumentNullException.ThrowIfNull(operationResult, "operationResult");
		string status2 = status ?? operationResult.Status;
		if (operationResult.IsSuccess)
		{
			return RoundSuccess(status2, operationResult.Data, delay);
		}
		return retryOnFail ? RoundRetry(status2, operationResult.Data, delay) : RoundFail(status2, operationResult.Data, delay);
	}

	/// <summary>
	/// 查找画面区域并转换为轮次结果。
	/// </summary>
	protected OperationRoundResult RoundByFindArea(Mat? screen, string screenName, string areaName, TimeSpan? successDelay = null, TimeSpan? retryDelay = null, bool cropFirst = true)
	{
		if (screen == null)
		{
			return RoundRetry("未获取截图", null, retryDelay);
		}
		if (string.IsNullOrWhiteSpace(screenName) || string.IsNullOrWhiteSpace(areaName))
		{
			return RoundFail("未指定画面区域");
		}
		FindAreaResultEnum findAreaResultEnum = ScreenUtils.FindArea(ZContext, screen, screenName, areaName, cropFirst);
		if (1 == 0)
		{
		}
		OperationRoundResult result = findAreaResultEnum switch
		{
			FindAreaResultEnum.AreaNoConfig => RoundFail("区域未配置 " + areaName), 
			FindAreaResultEnum.True => RoundSuccess(areaName, null, successDelay), 
			_ => RoundRetry("未找到 " + areaName, null, retryDelay), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	/// <summary>
	/// 使用二值化图像查找画面区域并转换为轮次结果。
	/// </summary>
	protected OperationRoundResult RoundByFindAreaBinary(Mat? screen, string screenName, string areaName, double binaryThreshold = 127.0, TimeSpan? successDelay = null, TimeSpan? retryDelay = null, bool cropFirst = true)
	{
		if (screen == null)
		{
			return RoundRetry("未获取截图", null, retryDelay);
		}
		if (string.IsNullOrWhiteSpace(screenName) || string.IsNullOrWhiteSpace(areaName))
		{
			return RoundFail("未指定画面区域");
		}
		FindAreaResultEnum findAreaResultEnum = ScreenUtils.FindAreaBinary(ZContext, screen, screenName, areaName, binaryThreshold, cropFirst);
		if (1 == 0)
		{
		}
		OperationRoundResult result = findAreaResultEnum switch
		{
			FindAreaResultEnum.AreaNoConfig => RoundFail("区域未配置 " + areaName), 
			FindAreaResultEnum.True => RoundSuccess(areaName, null, successDelay), 
			_ => RoundRetry("未找到 " + areaName, null, retryDelay), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	protected OperationRoundResult RoundByFindAndClickArea(Mat? screen = null, string? screenName = null, string? areaName = null, TimeSpan? preDelay = null, TimeSpan? successDelay = null, TimeSpan? retryDelay = null, bool cropFirst = true, bool centerX = false, IReadOnlyList<(string ScreenName, string AreaName)>? untilFindAll = null, IReadOnlyList<(string ScreenName, string AreaName)>? untilNotFindAll = null)
	{
		if (screen == null)
		{
			screen = LastScreenshot;
		}
		if (screen == null)
		{
			return RoundRetry("未获取截图", null, retryDelay);
		}
		if (string.IsNullOrWhiteSpace(screenName) || string.IsNullOrWhiteSpace(areaName))
		{
			return RoundFail("未指定画面区域");
		}
		if (NodeClicked && AreAllAreasFound(screen, untilFindAll, cropFirst))
		{
			return RoundSuccess(areaName, null, successDelay);
		}
		if (NodeClicked && AreAllAreasNotFound(screen, untilNotFindAll, cropFirst))
		{
			return RoundSuccess(areaName, null, successDelay);
		}
		SleepIfNeeded(preDelay ?? DefaultFindAndClickPreDelay);
		OcrClickResultEnum ocrClickResultEnum = ScreenUtils.FindAndClickArea(ZContext, screen, screenName, areaName, cropFirst, centerX);
		if (1 == 0)
		{
		}
		OperationRoundResult result = ocrClickResultEnum switch
		{
			OcrClickResultEnum.OcrClickSuccess => OnAreaClicked(screenName, areaName, successDelay, untilFindAll != null || untilNotFindAll != null), 
			OcrClickResultEnum.OcrClickNotFound => RoundRetry("未找到 " + areaName, null, retryDelay), 
			OcrClickResultEnum.OcrClickFail => RoundRetry("点击失败 " + areaName, null, retryDelay), 
			OcrClickResultEnum.AreaNoConfig => RoundFail("区域未配置 " + areaName), 
			_ => RoundRetry("未知状态", null, retryDelay), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	/// <summary>
	/// 点击配置中的画面区域。
	/// </summary>
	protected OperationRoundResult RoundByClickArea(string screenName, string areaName, bool clickLeftTop = false, TimeSpan? preDelay = null, TimeSpan? successDelay = null, TimeSpan? retryDelay = null)
	{
		if (string.IsNullOrWhiteSpace(screenName) || string.IsNullOrWhiteSpace(areaName))
		{
			return RoundFail("未指定画面区域");
		}
		OneDragon.Core.Screen.ScreenArea area = ZContext.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return RoundFail("区域未配置 " + areaName);
		}
		if (ZContext.Controller == null)
		{
			return RoundRetry("点击失败 " + areaName, null, retryDelay);
		}
		SleepIfNeeded(preDelay);
		ControllerBase? controller = ZContext.Controller;
		OneDragon.Core.Abstractions.Geometry.Point? position = (clickLeftTop ? area.LeftTop : area.Center);
		bool pcAlt = area.PcAlt;
		string gamepadKey = area.GamepadKey;
		return controller.Click(position, null, pcAlt, gamepadKey) ? OnAreaClicked(screenName, areaName, successDelay) : RoundRetry("点击失败 " + areaName, null, retryDelay);
	}

	/// <summary>
	/// OCR 查找目标文本。
	/// </summary>
	protected OperationRoundResult RoundByOcr(Mat? screen, string targetText, OneDragon.Core.Screen.ScreenArea? area = null, double lcsPercent = 0.5, TimeSpan? successDelay = null, TimeSpan? retryDelay = null, IReadOnlyList<IReadOnlyList<int>>? colorRange = null, bool cropFirst = true)
	{
		if (screen == null)
		{
			return RoundRetry("未获取截图", null, retryDelay);
		}
		if (string.IsNullOrWhiteSpace(targetText))
		{
			return RoundFail("未指定 OCR 文本");
		}
		string targetText2 = ZContext.GameTextResolver(targetText);
		return ScreenUtils.FindByOcr(ZContext, screen, targetText2, area, lcsPercent, colorRange, cropFirst) ? RoundSuccess(targetText, null, successDelay) : RoundRetry("找不到 " + targetText, null, retryDelay);
	}

	/// <summary>
	/// OCR 查找文本并点击。
	/// </summary>
	protected OperationRoundResult RoundByOcrAndClick(Mat? screen, string targetText, OneDragon.Core.Screen.ScreenArea? area = null, double lcsPercent = 0.6, OneDragon.Core.Abstractions.Geometry.Point? offset = null, TimeSpan? successDelay = null, TimeSpan? retryDelay = null, IReadOnlyList<IReadOnlyList<int>>? colorRange = null, bool cropFirst = true)
	{
		return RoundByOcrAndClickByPriority(screen, new string[] { targetText }, area, lcsPercent, offset, successDelay, retryDelay, colorRange, cropFirst);
	}

	/// <summary>
	/// 按优先级 OCR 查找文本并点击。
	/// </summary>
	protected OperationRoundResult RoundByOcrAndClickByPriority(Mat? screen, IReadOnlyList<string> targetTextList, OneDragon.Core.Screen.ScreenArea? area = null, double lcsPercent = 0.5, OneDragon.Core.Abstractions.Geometry.Point? offset = null, TimeSpan? successDelay = null, TimeSpan? retryDelay = null, IReadOnlyList<IReadOnlyList<int>>? colorRange = null, bool cropFirst = true, IReadOnlyList<string>? ignoreTextList = null)
	{
		if (screen == null)
		{
			return RoundRetry("未获取截图", null, retryDelay);
		}
		if (targetTextList.Count == 0)
		{
			return RoundRetry("未指定 OCR 文本", null, retryDelay);
		}
		if (ZContext.Controller == null)
		{
			return RoundRetry("点击失败", null, retryDelay);
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = ZContext.OcrService.GetOcrResultList(screen, colorRange ?? area?.ColorRange, area?.Rect, cropFirst);
		string[] array = targetTextList.Where((string text) => !string.IsNullOrWhiteSpace(text)).Distinct<string>(StringComparer.Ordinal).ToArray();
		Dictionary<string, string> dictionary = array.ToDictionary<string, string, string>((string target) => target, (string target) => ZContext.GameTextResolver(target), StringComparer.Ordinal);
		IReadOnlyDictionary<string, OcrMatchResult> readOnlyDictionary = MatchOcrResultsByTargetPriority(ocrResultList, dictionary.Values.Distinct<string>(StringComparer.Ordinal).ToArray(), lcsPercent);
		Dictionary<string, OcrMatchResult> dictionary2 = new Dictionary<string, OcrMatchResult>(StringComparer.Ordinal);
		string[] array2 = array;
		foreach (string key in array2)
		{
			if (readOnlyDictionary.TryGetValue(dictionary[key], out var value))
			{
				dictionary2[key] = value;
			}
		}
		foreach (string item in targetTextList.Where((string text) => !string.IsNullOrWhiteSpace(text)))
		{
			if ((ignoreTextList != null && ignoreTextList.Contains<string>(item, StringComparer.Ordinal)) || !dictionary2.TryGetValue(item, out var value2))
			{
				continue;
			}
			OneDragon.Core.Abstractions.Geometry.Point value3 = ((!offset.HasValue) ? value2.Center : (value2.Center + offset.Value));
			SleepIfNeeded(DefaultFindAndClickPreDelay);
			ControllerBase? controller = ZContext.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = value3;
			bool pcAlt = area?.PcAlt ?? false;
			string gamepadAction = area?.GamepadKey;
			return controller.Click(position, null, pcAlt, gamepadAction) ? RoundSuccess(item, null, successDelay) : RoundRetry("点击失败 " + item, null, retryDelay);
		}
		return RoundRetry("找不到 " + string.Join("/", targetTextList), null, retryDelay);
	}

	/// <summary>
	/// 将 OCR 结果归入最相近的目标文本。
	/// </summary>
	protected static IReadOnlyDictionary<string, OcrMatchResult> MatchOcrResultsByTargetPriority(IReadOnlyList<OcrMatchResult> results, IReadOnlyList<string> targetTextList, double threshold)
	{
		string[] array = targetTextList.Where((string text) => !string.IsNullOrWhiteSpace(text)).Distinct<string>(StringComparer.Ordinal).ToArray();
		Dictionary<string, OcrMatchResult> dictionary = new Dictionary<string, OcrMatchResult>(StringComparer.Ordinal);
		if (array.Length == 0)
		{
			return dictionary;
		}
		foreach (OcrMatchResult result in results)
		{
			int? num = StringUtils.FindBestMatchByDifflib(result.Text, array, threshold);
			if (num.HasValue)
			{
				string key = array[num.Value];
				if (!dictionary.TryGetValue(key, out var value) || result.Confidence > value.Confidence)
				{
					dictionary[key] = result;
				}
			}
		}
		return dictionary;
	}

	internal static bool ShouldIgnoreOcrClickText(string? ocrText, string? ignoreText)
	{
		if (string.IsNullOrWhiteSpace(ocrText) || string.IsNullOrWhiteSpace(ignoreText))
		{
			return false;
		}
		string text = ocrText.Trim();
		string text2 = ignoreText.Trim();
		if (string.Equals(text, text2, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		int num = Math.Abs(text.Length - text2.Length);
		int num2 = Math.Max(1, text2.Length / 3);
		return num <= num2 && StringUtils.FindByLcs(text2, text, 0.85);
	}

	/// <summary>
	/// 通过 ScreenContext 路由前往目标画面。
	/// </summary>
	protected OperationRoundResult RoundByGotoScreen(Mat? screen = null, string? screenName = null, TimeSpan? preDelay = null, TimeSpan? successDelay = null, TimeSpan? retryDelay = null, bool cropFirst = true)
	{
		if (screen == null)
		{
			screen = LastScreenshot;
		}
		if (screen == null)
		{
			return RoundRetry("未获取截图", null, retryDelay);
		}
		if (string.IsNullOrWhiteSpace(screenName))
		{
			return RoundFail("未指定目标画面");
		}
		string matchScreenName = ScreenUtils.GetMatchScreenName(ZContext, screen, null, cropFirst);
		if (matchScreenName != null)
		{
			ZContext.ScreenContext.UpdateCurrentScreenName(matchScreenName);
		}
		if (matchScreenName == null)
		{
			return RoundRetry("未能识别当前画面", null, retryDelay);
		}
		if (string.Equals(matchScreenName, screenName, StringComparison.Ordinal))
		{
			return RoundSuccess(matchScreenName, null, successDelay);
		}
		ScreenRoute screenRoute = ZContext.ScreenContext.GetScreenRoute(matchScreenName, screenName);
		if (screenRoute == null || !screenRoute.CanGo)
		{
			return RoundFail("无法从 " + matchScreenName + " 前往 " + screenName);
		}
		ScreenRouteNode screenRouteNode = screenRoute.NodeList[0];
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(screen, matchScreenName, screenRouteNode.FromArea, preDelay);
		if (operationRoundResult.IsSuccess)
		{
			ZContext.ScreenContext.UpdateCurrentScreenName(screenRouteNode.ToScreen);
			return RoundWait(operationRoundResult.Status, null, retryDelay);
		}
		return RoundRetry(operationRoundResult.Status, null, retryDelay);
	}

	/// <summary>
	/// 识别当前画面并同步到 ScreenContext。
	/// </summary>
	protected string? CheckAndUpdateCurrentScreen(Mat? screen = null, IReadOnlyList<string>? screenNameList = null, bool cropFirst = true)
	{
		if (screen == null)
		{
			screen = LastScreenshot;
		}
		if (screen == null)
		{
			return null;
		}
		string matchScreenName = ScreenUtils.GetMatchScreenName(ZContext, screen, screenNameList, cropFirst);
		if (matchScreenName != null)
		{
			ZContext.ScreenContext.UpdateCurrentScreenName(matchScreenName);
		}
		return matchScreenName;
	}

	/// <summary>
	/// 点击区域后更新点击状态和画面状态。
	/// </summary>
	protected OperationRoundResult OnAreaClicked(string screenName, string areaName, TimeSpan? delay = null, bool waitForConfirmation = false)
	{
		NodeClicked = true;
		UpdateScreenAfterOperation(screenName, areaName);
		return waitForConfirmation ? RoundWait(areaName, null, delay) : RoundSuccess(areaName, null, delay);
	}

	/// <summary>
	/// 按区域跳转配置更新当前画面。
	/// </summary>
	protected void UpdateScreenAfterOperation(string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = ZContext.ScreenContext.GetArea(screenName, areaName);
		if (area != null && area.GotoList.Count > 0)
		{
			ZContext.ScreenContext.UpdateCurrentScreenName(area.GotoList[0]);
		}
	}

	private static void SleepIfNeeded(TimeSpan? delay)
	{
		if (delay.HasValue)
		{
			TimeSpan valueOrDefault = delay.GetValueOrDefault();
			if (valueOrDefault > TimeSpan.Zero)
			{
				Thread.Sleep(valueOrDefault);
			}
		}
	}

	private bool AreAllAreasFound(Mat screen, IReadOnlyList<(string ScreenName, string AreaName)>? areas, bool cropFirst)
	{
		return areas?.All<(string, string)>(((string ScreenName, string AreaName) area) => ScreenUtils.FindArea(ZContext, screen, area.ScreenName, area.AreaName, cropFirst) == FindAreaResultEnum.True) ?? false;
	}

	private bool AreAllAreasNotFound(Mat screen, IReadOnlyList<(string ScreenName, string AreaName)>? areas, bool cropFirst)
	{
		return areas?.All<(string, string)>(((string ScreenName, string AreaName) area) => ScreenUtils.FindArea(ZContext, screen, area.ScreenName, area.AreaName, cropFirst) != FindAreaResultEnum.True) ?? false;
	}
}
