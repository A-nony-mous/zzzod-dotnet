using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 从任意可处理界面前往菜单。
/// </summary>
public sealed class GotoMenu : ZOperation
{
	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化前往菜单操作。
	/// </summary>
	public GotoMenu(ZContext context, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "前往菜单")
	{
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[OperationNode("画面识别", IsStartNode = true, NodeMaxRetryTimes = 60)]
	private async Task<OperationRoundResult> CheckScreenAndRun()
	{
		OperationRoundResult gotoMenu = RoundByGotoScreen(base.LastScreenshot, "菜单", _preClickDelay, null, TimeSpan.Zero);
		if (gotoMenu.IsSuccess)
		{
			return RoundSuccess(gotoMenu.Status);
		}
		if (!gotoMenu.IsFail && base.ZContext.ScreenContext.CurrentScreenName != null)
		{
			return RoundWait(gotoMenu.Status, null, _retryDelay);
		}
		OperationResult backResult = await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		return backResult.IsSuccess ? RoundRetry(backResult.Status, null, _retryDelay) : RoundFail(backResult.Status);
	}

	private static async Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		BackToNormalWorld operation = new BackToNormalWorld(context);
		return await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
	}
}
