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
/// Chooses a tab in the opened compendium.
/// </summary>
public sealed class CompendiumChooseTab : ZOperation
{
	private readonly string _tabName;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _successDelay;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public CompendiumChooseTab(ZContext context, string tabName, TimeSpan? retryDelay = null, TimeSpan? successDelay = null)
		: base(context, "快捷手册 选择Tab " + tabName)
	{
		_tabName = tabName;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_successDelay = successDelay ?? TimeSpan.FromSeconds(1L);
	}

	[OperationNode("选择TAB", IsStartNode = true)]
	private OperationRoundResult ChooseTab()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("快捷手册", "TAB列表");
		if (area == null || base.LastScreenshot == null)
		{
			return RoundRetry("找不到 " + _tabName, null, _retryDelay);
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect);
		string resolvedTabName = base.ZContext.GameTextResolver(_tabName);
		OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult result) => StringUtils.FindByLcs(resolvedTabName, result.Text, 0.5));
		if (ocrMatchResult == null || base.ZContext.Controller == null)
		{
			return RoundRetry("找不到 " + _tabName, null, _retryDelay);
		}
		base.ZContext.Controller.Click(ocrMatchResult.Center);
		return RoundSuccess(null, null, _successDelay);
	}
}
