using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.HollowZero;

/// <summary>
/// 在零号空洞事件画面通过菜单离开空洞。
/// </summary>
public sealed class HollowExitByMenu : ZOperation
{
	private readonly TimeSpan _retryDelay;

	/// <summary>
	/// 初始化离开空洞操作。
	/// </summary>
	public HollowExitByMenu(ZContext context, TimeSpan? retryDelay = null)
		: base(context, "离开空洞")
	{
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
	}

	[OperationNode("点击菜单", IsStartNode = true)]
	private OperationRoundResult ClickMenu()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "零号空洞-事件", "放弃");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess();
		}
		OperationRoundResult operationRoundResult2 = RoundByClickArea("零号空洞-事件", "菜单");
		return operationRoundResult2.IsSuccess ? RoundWait(null, null, _retryDelay) : RoundRetry(operationRoundResult2.Status, null, _retryDelay);
	}

	[NodeFrom("点击菜单")]
	[OperationNode("点击离开")]
	private OperationRoundResult ClickLeave()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindAndClickArea(lastScreenshot, "零号空洞-事件", "放弃", null, successDelay, retryDelay);
	}

	[NodeFrom("点击离开")]
	[OperationNode("确认离开")]
	private OperationRoundResult ConfirmLeave()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindAndClickArea(lastScreenshot, "零号空洞-事件", "放弃-确认", null, successDelay, retryDelay);
	}

	[NodeFrom("确认离开")]
	[OperationNode("点击完成", NodeMaxRetryTimes = 20)]
	private OperationRoundResult ClickFinish()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "零号空洞-事件", "通关-完成");
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(null, null, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "零号空洞-入口", "街区");
		return operationRoundResult2.IsSuccess ? RoundSuccess() : RoundRetry(null, null, _retryDelay);
	}
}
