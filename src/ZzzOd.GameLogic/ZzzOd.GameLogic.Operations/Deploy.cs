using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 在出战页面点击出战，并处理常见确认弹窗。
/// </summary>
public sealed class Deploy : ZOperation
{
	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化出战操作。
	/// </summary>
	public Deploy(ZContext context, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "出战")
	{
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[OperationNode("出战", IsStartNode = true)]
	private OperationRoundResult ClickDeploy()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "通用-出战", "按钮-出战", _preClickDelay, _retryDelay, _retryDelay, cropFirst: true, centerX: false, null, new (string, string)[] { ("通用-出战", "按钮-出战") });
	}

	[NodeFrom("出战")]
	[OperationNode("出战确认", NodeMaxRetryTimes = 3)]
	private OperationRoundResult CheckLevel()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "通用-出战", "标题-驱动盘数量已达到可拥有上限");
		if (operationRoundResult.IsSuccess)
		{
			return RoundFail("驱动盘数量已达到可拥有上限");
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(lastScreenshot, "通用-出战", "按钮-队员数量少-确认", preDelay, null, retryDelay);
		if (operationRoundResult2.IsSuccess)
		{
			return RoundWait(operationRoundResult2.Status, null, _retryDelay);
		}
		Mat? lastScreenshot2 = base.LastScreenshot;
		TimeSpan? preDelay2 = _preClickDelay;
		retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult3 = RoundByFindAndClickArea(lastScreenshot2, "通用-出战", "按钮-等级低-确定并出战", preDelay2, null, retryDelay);
		if (operationRoundResult3.IsSuccess)
		{
			return RoundWait(operationRoundResult3.Status, null, _retryDelay);
		}
		return (base.NodeRetryTimes == 2) ? RoundSuccess("无需确认", null, _retryDelay) : RoundRetry("无需确认", null, _retryDelay);
	}
}
