using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public abstract class SuibianTempleSubOperation : ZOperation
{
	protected static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(300L);

	protected static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1L);

	protected SuibianTempleConfig Config { get; }

	protected SuibianTempleSubOperation(ZContext context, SuibianTempleConfig config, string operationName)
		: base(context, operationName)
	{
		Config = config;
	}

	protected OperationRoundResult EnsureScreen(string screenName)
	{
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { screenName });
		return (text == null) ? RoundRetry("未识别当前画面", null, OneSecond) : RoundSuccess(text);
	}

	protected OperationRoundResult GoToScreenByText(string screenName, params string[] texts)
	{
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { screenName });
		if (text != null)
		{
			return RoundSuccess(text);
		}
		OperationRoundResult operationRoundResult = ClickText(texts);
		return operationRoundResult.IsSuccess ? RoundWait(operationRoundResult.Status, null, OneSecond) : RoundRetry("未识别当前画面", null, OneSecond);
	}

	protected OperationRoundResult ClickText(params string[] texts)
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = ShortDelay;
		return RoundByOcrAndClickByPriority(lastScreenshot, texts, null, 0.5, null, successDelay, retryDelay);
	}

	protected OperationRoundResult ClickTextByPriority(IReadOnlyList<string> texts, OneDragon.Core.Screen.ScreenArea? area = null, OneDragon.Core.Abstractions.Geometry.Point? offset = null, IReadOnlyList<string>? ignoreTexts = null, TimeSpan? successDelay = null, TimeSpan? retryDelay = null)
	{
		return RoundByOcrAndClickByPriority(base.LastScreenshot, texts, area, 0.5, offset, successDelay ?? OneSecond, retryDelay ?? ShortDelay, null, cropFirst: true, ignoreTexts);
	}

	protected IReadOnlyList<OcrMatchResult> GetAreaOcrResults(string screenName, string areaName, IReadOnlyList<IReadOnlyList<int>>? colorRange = null)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea(screenName, areaName);
		IReadOnlyList<OcrMatchResult> result;
		if (base.LastScreenshot != null)
		{
			result = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, colorRange ?? area?.ColorRange, area?.Rect);
		}
		else
		{
			IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
			result = readOnlyList;
		}
		return result;
	}

	protected static bool MatchesAnyTarget(string text, IReadOnlyList<string> targets, double lcsPercent = 0.5)
	{
		return ZOperation.MatchOcrResultsByTargetPriority(new OcrMatchResult[] { new OcrMatchResult(1.0, 0, 0, 1, 1, text) }, targets, lcsPercent).Count > 0;
	}

	protected static int? ExtractPositiveDigits(string text)
	{
		string s = new string(text.Where(char.IsDigit).ToArray());
		int result;
		return (int.TryParse(s, out result) && result > 0) ? new int?(result) : ((int?)null);
	}

	protected OperationRoundResult ClickArea(string screenName, string areaName)
	{
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = ShortDelay;
		return RoundByClickArea(screenName, areaName, clickLeftTop: false, null, successDelay, retryDelay);
	}

	protected OperationRoundResult FindAndClickArea(string screenName, string areaName)
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = ShortDelay;
		return RoundByFindAndClickArea(lastScreenshot, screenName, areaName, null, successDelay, retryDelay);
	}

	protected OperationRoundResult BackToEntry()
	{
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "随便观-入口" });
		if (text != null)
		{
			return RoundSuccess(text);
		}
		OperationRoundResult operationRoundResult = FindAndClickArea("菜单", "返回");
		return operationRoundResult.IsSuccess ? RoundWait(operationRoundResult.Status, null, OneSecond) : RoundRetry(operationRoundResult.Status, null, OneSecond);
	}

	protected OperationRoundResult RunChild(ZOperation operation)
	{
		OperationResult result = operation.ExecuteAsync().GetAwaiter().GetResult();
		return RoundByOperationResult(result);
	}

	protected static string GetOptionLabel(IReadOnlyList<ConfigItem> options, string value)
	{
		return options.FirstOrDefault((ConfigItem item) => object.Equals(item.Value, value))?.Label ?? value;
	}
}
