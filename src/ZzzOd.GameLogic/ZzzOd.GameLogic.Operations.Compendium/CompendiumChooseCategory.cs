using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// Chooses a category in the opened compendium tab.
/// </summary>
public sealed class CompendiumChooseCategory : ZOperation
{
	private readonly string _categoryName;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _successDelay;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public CompendiumChooseCategory(ZContext context, string categoryName, TimeSpan? retryDelay = null, TimeSpan? successDelay = null)
		: base(context, "快捷手册 选择分类 " + categoryName)
	{
		_categoryName = categoryName;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_successDelay = successDelay ?? TimeSpan.FromSeconds(1L);
	}

	[OperationNode("选择分类", IsStartNode = true)]
	private OperationRoundResult ChooseCategory()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("快捷手册", "分类列表");
		if (area == null || base.LastScreenshot == null)
		{
			return RoundRetry("找不到 " + _categoryName, null, _retryDelay);
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect, cropFirst: true, 0.0, 40.0);
		string resolvedCategoryName = base.ZContext.GameTextResolver(_categoryName);
		OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult result) => StringUtils.FindByLcs(resolvedCategoryName, result.Text, 0.5));
		if (ocrMatchResult == null || base.ZContext.Controller == null)
		{
			return RoundRetry("找不到 " + _categoryName, null, _retryDelay);
		}
		base.ZContext.Controller.Click(ocrMatchResult.Center);
		return RoundSuccess(null, null, _successDelay);
	}
}
