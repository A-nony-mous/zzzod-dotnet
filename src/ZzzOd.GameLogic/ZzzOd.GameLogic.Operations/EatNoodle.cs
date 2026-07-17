using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 前往拉面店并点一碗拉面。
/// </summary>
public sealed class EatNoodle : ZOperation
{
	private readonly string _noodleName;

	private readonly Func<ZContext, Task<OperationResult>> _transportAsync;

	private readonly Func<ZContext, Task<OperationResult>> _waitNormalWorldAsync;

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	private readonly Action<ZContext> _moveAndInteract;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化吃拉面操作。
	/// </summary>
	public EatNoodle(ZContext context, string noodleName, Func<ZContext, Task<OperationResult>>? transportAsync = null, Func<ZContext, Task<OperationResult>>? waitNormalWorldAsync = null, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null, Action<ZContext>? moveAndInteract = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "吃拉面 " + noodleName)
	{
		_noodleName = noodleName;
		_transportAsync = transportAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultTransportAsync);
		_waitNormalWorldAsync = waitNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultWaitNormalWorldAsync);
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
		_moveAndInteract = moveAndInteract ?? new Action<ZContext>(DefaultMoveAndInteract);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[OperationNode("传送", IsStartNode = true)]
	private async Task<OperationRoundResult> DoTransport()
	{
		return RoundByOperationResult(await _transportAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("传送")]
	[OperationNode("等待大世界加载")]
	private async Task<OperationRoundResult> WaitWorld()
	{
		return RoundByOperationResult(await _waitNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("等待大世界加载")]
	[OperationNode("移动交互")]
	private OperationRoundResult MoveAndInteract()
	{
		_moveAndInteract(base.ZContext);
		return RoundSuccess();
	}

	[NodeFrom("移动交互")]
	[OperationNode("等待拉面店加载", NodeMaxRetryTimes = 10)]
	private OperationRoundResult WaitNoodleShop()
	{
		return RoundByFindArea(base.LastScreenshot, "菜单", "返回", _retryDelay, _retryDelay);
	}

	[NodeFrom("等待拉面店加载")]
	[OperationNode("选择拉面")]
	private OperationRoundResult ChooseNoodle()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("拉面店", "拉面列表");
		Mat? lastScreenshot = base.LastScreenshot;
		string noodleName = _noodleName;
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByOcrAndClick(lastScreenshot, noodleName, area, 0.6, null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status, null, _retryDelay);
		}
		if (area == null || base.ZContext.Controller == null)
		{
			return RoundRetry(operationRoundResult.Status, null, _retryDelay);
		}
		OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
		OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(-100, 0);
		base.ZContext.Controller.DragTo(end, center);
		return RoundRetry(operationRoundResult.Status, null, TimeSpan.FromMilliseconds(500L));
	}

	[NodeFrom("选择拉面")]
	[OperationNode("点单")]
	private OperationRoundResult ClickOrder()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "拉面店", "点单", _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("点单")]
	[OperationNode("点单后确认")]
	private OperationRoundResult ConfirmAfterOrder()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "拉面店", "点单确认", _preClickDelay, TimeSpan.FromMilliseconds(500L), _retryDelay);
	}

	[NodeFrom("点单后确认")]
	[OperationNode("点单后跳过")]
	private OperationRoundResult SkipAfterOrder()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "拉面店", "效果确认", _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status, null, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "咖啡店", "点单后跳过");
		if (!operationRoundResult2.IsSuccess)
		{
			return RoundRetry(operationRoundResult2.Status, null, _retryDelay);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("咖啡店", "点单后跳过");
		if (area != null && base.ZContext.Controller != null)
		{
			base.ZContext.Controller.DragTo(area.Center, area.LeftTop, TimeSpan.FromMilliseconds(200L));
			base.ZContext.Controller.Click();
		}
		return RoundSuccess(operationRoundResult2.Status, null, _retryDelay);
	}

	[NodeFrom("点单后跳过")]
	[OperationNode("效果确认")]
	private OperationRoundResult EffectAfterOrder()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "拉面店", "效果确认", _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("效果确认")]
	[OperationNode("返回大世界")]
	private async Task<OperationRoundResult> BackToNormalWorld()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static async Task<OperationResult> DefaultTransportAsync(ZContext context)
	{
		Transport operation = new Transport(context, "六分街", "拉面店");
		return await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<OperationResult> DefaultWaitNormalWorldAsync(ZContext context)
	{
		WaitNormalWorld operation = new WaitNormalWorld(context);
		return await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		BackToNormalWorld operation = new BackToNormalWorld(context);
		return await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	private static void DefaultMoveAndInteract(ZContext context)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.MoveW(press: true, TimeSpan.FromSeconds(1L), release: true);
			zPcController.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		}
	}
}
