using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.ScratchCard;

/// <summary>
/// 报刊亭刮刮卡流程。
/// </summary>
public sealed class ScratchCardOperation : ZOperation
{
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan InteractDelay = TimeSpan.FromSeconds(3L);

	private readonly Func<ZContext, Task<OperationResult>> _transportAsync;

	private readonly Func<ZContext, Task<OperationResult>> _waitNormalWorldAsync;

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	private readonly Func<ZContext, OperationResult> _moveAndInteract;

	private readonly Func<ZContext, OperationResult> _scratchCard;

	/// <summary>
	/// 初始化刮刮卡流程。
	/// </summary>
	public ScratchCardOperation(ZContext context, Func<ZContext, Task<OperationResult>>? transportAsync = null, Func<ZContext, Task<OperationResult>>? waitNormalWorldAsync = null, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null, Func<ZContext, OperationResult>? moveAndInteract = null, Func<ZContext, OperationResult>? scratchCard = null)
		: base(context, "刮刮卡")
	{
		_transportAsync = transportAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultTransportAsync);
		_waitNormalWorldAsync = waitNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultWaitNormalWorldAsync);
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
		_moveAndInteract = moveAndInteract ?? new Func<ZContext, OperationResult>(DefaultMoveAndInteract);
		_scratchCard = scratchCard ?? new Func<ZContext, OperationResult>(DefaultScratchCard);
	}

	/// <summary>
	/// 传送到六分街报刊亭。
	/// </summary>
	[OperationNode("传送", IsStartNode = true)]
	public async Task<OperationRoundResult> Transport()
	{
		return RoundByOperationResult(await _transportAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 等待报刊亭交互入口或大世界加载完成。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("等待加载", NodeMaxRetryTimes = 60)]
	public async Task<OperationRoundResult> WaitWorld()
	{
		OperationRoundResult scratchCard = RoundByFindArea(base.LastScreenshot, "报刊亭", "刮刮卡");
		if (scratchCard.IsSuccess)
		{
			return RoundSuccess(scratchCard.Status);
		}
		OperationResult result = await _waitNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess)
		{
			return RoundSuccess(result.Status);
		}
		return RoundRetry(result.Status, null, RetryDelay);
	}

	/// <summary>
	/// 传送后向前移动并交互。
	/// </summary>
	[NodeFrom("等待加载")]
	[OperationNode("移动交互")]
	public OperationRoundResult MoveAndInteract()
	{
		OperationResult operationResult = _moveAndInteract(base.ZContext);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status, null, InteractDelay) : RoundFail(operationResult.Status);
	}

	/// <summary>
	/// 点击刮刮卡入口或处理报刊亭对话。
	/// </summary>
	[NodeFrom("等待加载", Status = "刮刮卡")]
	[NodeFrom("移动交互")]
	[OperationNode("点击刮刮卡", NodeMaxRetryTimes = 20)]
	public OperationRoundResult ClickScratchCard()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "报刊亭", "每日可刮取一次");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status, null, RetryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "报刊亭", "按钮-同类型确认");
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status, null, RetryDelay);
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = RetryDelay;
		TimeSpan? retryDelay = RetryDelay;
		OperationRoundResult operationRoundResult3 = RoundByFindAndClickArea(lastScreenshot, "报刊亭", "刮刮卡", null, successDelay, retryDelay);
		if (operationRoundResult3.IsSuccess)
		{
			return RoundWait(operationRoundResult3.Status, null, RetryDelay);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("报刊亭", "对话选项");
		Mat? lastScreenshot2 = base.LastScreenshot;
		retryDelay = RetryDelay;
		successDelay = RetryDelay;
		OperationRoundResult operationRoundResult4 = RoundByOcrAndClick(lastScreenshot2, "叫醒他", area, 0.6, null, retryDelay, successDelay);
		if (operationRoundResult4.IsSuccess)
		{
			return RoundWait(operationRoundResult4.Status, null, RetryDelay);
		}
		Mat? lastScreenshot3 = base.LastScreenshot;
		successDelay = RetryDelay;
		retryDelay = RetryDelay;
		OperationRoundResult operationRoundResult5 = RoundByOcrAndClick(lastScreenshot3, "叫醒嗷呜", area, 0.6, null, successDelay, retryDelay);
		if (operationRoundResult5.IsSuccess)
		{
			return RoundWait(operationRoundResult5.Status, null, RetryDelay);
		}
		Mat? lastScreenshot4 = base.LastScreenshot;
		retryDelay = RetryDelay;
		successDelay = RetryDelay;
		OperationRoundResult operationRoundResult6 = RoundByOcrAndClick(lastScreenshot4, "只是来看刮刮卡和报纸", area, 0.6, null, retryDelay, successDelay);
		if (operationRoundResult6.IsSuccess)
		{
			return RoundWait(operationRoundResult6.Status, null, RetryDelay);
		}
		Mat? lastScreenshot5 = base.LastScreenshot;
		successDelay = RetryDelay;
		retryDelay = RetryDelay;
		OperationRoundResult operationRoundResult7 = RoundByFindAndClickArea(lastScreenshot5, "报刊亭", "嗷呜被你叫醒了", null, successDelay, retryDelay);
		if (operationRoundResult7.IsSuccess)
		{
			return RoundWait(operationRoundResult7.Status, null, RetryDelay);
		}
		retryDelay = RetryDelay;
		OperationRoundResult operationRoundResult8 = RoundByClickArea("报刊亭", "嗷呜标题", clickLeftTop: false, null, null, retryDelay);
		return RoundRetry(operationRoundResult8.Status, null, RetryDelay);
	}

	/// <summary>
	/// 执行刮卡拖拽。
	/// </summary>
	[NodeFrom("点击刮刮卡")]
	[OperationNode("刮刮")]
	public OperationRoundResult Scratch()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "报刊亭", "每日可刮取一次");
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry(operationRoundResult.Status, null, RetryDelay);
		}
		OperationResult operationResult = _scratchCard(base.ZContext);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, RetryDelay);
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("点击刮刮卡", Status = "按钮-同类型确认")]
	[NodeFrom("刮刮")]
	[OperationNodeNotify(OperationNodeNotifyTiming.PreviousDone)]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static Task<OperationResult> DefaultTransportAsync(ZContext context)
	{
		return new Transport(context, "六分街", "报刊亭", waitAtLast: false).ExecuteAsync();
	}

	private static Task<OperationResult> DefaultWaitNormalWorldAsync(ZContext context)
	{
		return new WaitNormalWorld(context, checkOnce: true).ExecuteAsync();
	}

	private static Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	private static OperationResult DefaultMoveAndInteract(ZContext context)
	{
		if (!(context.Controller is IZzzControllerActions zzzControllerActions))
		{
			return new OperationResult(IsSuccess: false, "控制器不支持前台键鼠移动交互");
		}
		zzzControllerActions.MoveW(press: true, TimeSpan.FromSeconds(1L), release: true);
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		return new OperationResult(IsSuccess: true);
	}

	private static OperationResult DefaultScratchCard(ZContext context)
	{
		if (context.Controller == null)
		{
			return new OperationResult(IsSuccess: false, "未获取控制器");
		}
		for (int i = 1; i <= 3; i++)
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("报刊亭", $"刮层-{i}");
			if (area == null)
			{
				return new OperationResult(IsSuccess: false, $"区域未配置 刮层-{i}");
			}
			context.Controller.DragTo(area.RightBottom, area.LeftTop, TimeSpan.FromSeconds(1.5));
			Thread.Sleep(TimeSpan.FromSeconds(1L));
		}
		return new OperationResult(IsSuccess: true);
	}
}
