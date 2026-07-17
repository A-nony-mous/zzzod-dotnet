using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// Opens the in-game compendium from the menu.
/// </summary>
public sealed class OpenCompendium : ZOperation
{
	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _successDelay;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public OpenCompendium(ZContext context, TimeSpan? retryDelay = null, TimeSpan? successDelay = null)
		: base(context, "打开快捷手册")
	{
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_successDelay = successDelay ?? TimeSpan.FromSeconds(1L);
	}

	[OperationNode("打开菜单", IsStartNode = true)]
	private OperationRoundResult OpenMenu()
	{
		TimeSpan? successDelay = _successDelay;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByGotoScreen(null, "菜单", null, successDelay, retryDelay);
	}

	[NodeFrom("打开菜单")]
	[OperationNode("点击更多")]
	private OperationRoundResult ClickMore()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("菜单", "底部列表");
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = _successDelay;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByOcrAndClick(lastScreenshot, "快捷手册", area, 0.6, null, successDelay, retryDelay);
	}
}
