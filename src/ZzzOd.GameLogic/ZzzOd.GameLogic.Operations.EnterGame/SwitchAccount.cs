using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Logs out from the in-game menu and enters the game with switch-account login.
/// </summary>
public sealed class SwitchAccount : ZOperation
{
	private readonly Func<ZContext, bool, Task<OperationResult>> _enterGameAsync;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _successDelay;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public SwitchAccount(ZContext context, Func<ZContext, bool, Task<OperationResult>>? enterGameAsync = null, TimeSpan? retryDelay = null, TimeSpan? successDelay = null)
		: base(context, "切换账号")
	{
		_enterGameAsync = enterGameAsync ?? ((Func<ZContext, bool, Task<OperationResult>>)((ZContext ctx, bool switchAccount) => new EnterGame(ctx, switchAccount).ExecuteAsync()));
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
		return RoundByOcrAndClick(lastScreenshot, "更多", area, 0.6, null, successDelay, retryDelay);
	}

	[NodeFrom("点击更多")]
	[OperationNode("更多选择登出")]
	private OperationRoundResult MoreClickLogout()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("菜单", "更多功能");
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = _successDelay;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByOcrAndClick(lastScreenshot, "登出", area, 0.6, null, successDelay, retryDelay);
	}

	[NodeFrom("更多选择登出")]
	[OperationNode("更多登出确认")]
	private OperationRoundResult MoreLogoutConfirm()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(10L);
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindAndClickArea(lastScreenshot, "菜单", "更多登出确认", null, successDelay, retryDelay);
	}

	[NodeFrom("更多登出确认")]
	[OperationNode("等待切换账号可按", NodeMaxRetryTimes = 20)]
	private OperationRoundResult WaitSwitchCanClick()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByFindArea(lastScreenshot, "打开游戏", "点击进入游戏", null, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return operationRoundResult;
		}
		Mat? lastScreenshot2 = base.LastScreenshot;
		retryDelay = _retryDelay;
		return RoundByFindArea(lastScreenshot2, "打开游戏", "B服新-登录记录", null, retryDelay);
	}

	[NodeFrom("等待切换账号可按")]
	[OperationNode("进入游戏")]
	private async Task<OperationRoundResult> EnterGameAsync()
	{
		return RoundByOperationResult(await _enterGameAsync(base.ZContext, arg2: true).ConfigureAwait(continueOnCapturedContext: false));
	}
}
