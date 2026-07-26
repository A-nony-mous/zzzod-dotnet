using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Windows.Controller;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Opens the game, waits for the window, restores HDR and enters the game.
/// </summary>
public sealed class OpenAndEnterGame : ZOperation
{
	private readonly Func<Task<OperationResult>> _disableAutoHdrAsync;

	private readonly Func<Task<OperationResult>> _openGameAsync;

	private readonly Func<bool> _refreshWindowAndCheckReady;

	private readonly Action _activateWindow;

	private readonly Func<Task<OperationResult>> _enableAutoHdrAsync;

	private readonly Func<Task<OperationResult>> _enterGameAsync;

	private readonly TimeSpan _retryDelay;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public OpenAndEnterGame(ZContext context, Func<Task<OperationResult>>? disableAutoHdrAsync = null, Func<Task<OperationResult>>? openGameAsync = null, Func<bool>? refreshWindowAndCheckReady = null, Action? activateWindow = null, Func<Task<OperationResult>>? enableAutoHdrAsync = null, Func<Task<OperationResult>>? enterGameAsync = null, TimeSpan? retryDelay = null)
		: base(context, "打开并登录游戏")
	{
		_disableAutoHdrAsync = disableAutoHdrAsync ?? ((Func<Task<OperationResult>>)(() => new DisableAutoHdr(context).ExecuteAsync()));
		_openGameAsync = openGameAsync ?? ((Func<Task<OperationResult>>)(() => new OpenGame(context).ExecuteAsync()));
		_refreshWindowAndCheckReady = refreshWindowAndCheckReady ?? new Func<bool>(DefaultRefreshWindowAndCheckReady);
		_activateWindow = activateWindow ?? new Action(DefaultActivateWindow);
		_enableAutoHdrAsync = enableAutoHdrAsync ?? ((Func<Task<OperationResult>>)(() => new EnableAutoHdr(context).ExecuteAsync()));
		_enterGameAsync = enterGameAsync ?? ((Func<Task<OperationResult>>)(() => new EnterGame(context).ExecuteAsync()));
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
	}

	[OperationNode("打开游戏", IsStartNode = true, ScreenshotBeforeRound = false)]
	private async Task<OperationRoundResult> OpenGameAsync()
	{
		// 禁用自动 HDR 属尽力而为：注册表读写失败不阻断游戏启动
		OperationResult hdrResult = await _disableAutoHdrAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (!hdrResult.IsSuccess)
		{
			base.ZContext.Logger.Warning("禁用自动 HDR 失败，继续启动游戏：{Status}", hdrResult.Status);
		}
		return RoundByOperationResult(await _openGameAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("打开游戏")]
	[OperationNode("等待游戏打开", NodeMaxRetryTimes = 60, ScreenshotBeforeRound = false)]
	private async Task<OperationRoundResult> WaitGameAsync()
	{
		if (!_refreshWindowAndCheckReady())
		{
			return RoundRetry("等待游戏窗口", null, _retryDelay);
		}
		_activateWindow();
		// 恢复自动 HDR 同样尽力而为，失败不影响后续登录
		OperationResult hdrResult = await _enableAutoHdrAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (!hdrResult.IsSuccess)
		{
			base.ZContext.Logger.Warning("恢复自动 HDR 失败，继续进入游戏：{Status}", hdrResult.Status);
		}
		return RoundSuccess();
	}

	[NodeFrom("等待游戏打开")]
	[OperationNode("进入游戏")]
	private async Task<OperationRoundResult> EnterGameAsync()
	{
		return RoundByOperationResult(await _enterGameAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	private bool DefaultRefreshWindowAndCheckReady()
	{
		try
		{
			ControllerBase controller = base.ZContext.Controller;
			return controller != null && controller.InitBeforeContextRun() && controller.IsGameWindowReady;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void DefaultActivateWindow()
	{
		if (base.ZContext.Controller is WindowsGameController windowsGameController)
		{
			windowsGameController.ActivateWindow();
		}
	}
}
