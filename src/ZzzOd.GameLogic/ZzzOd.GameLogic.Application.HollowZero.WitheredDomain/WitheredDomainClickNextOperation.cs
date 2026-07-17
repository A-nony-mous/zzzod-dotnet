using System;
using System.Threading;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.ScreenArea;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

internal sealed class WitheredDomainClickNextOperation : ZOperation
{
	public WitheredDomainClickNextOperation(ZContext context)
		: base(context, "枯萎之都 下一步")
	{
	}

	[OperationNode("下一步", IsStartNode = true)]
	private OperationRoundResult ClickNext()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "零号空洞-入口", "下一步");
		if (operationRoundResult.IsSuccess)
		{
			Thread.Sleep(TimeSpan.FromMilliseconds(500L));
			base.ZContext.Controller?.MouseMove(ScreenNormalWorldEnum.Uid.Center);
			return RoundSuccess(operationRoundResult.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(base.LastScreenshot, "零号空洞-入口", "行动中-确认");
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status, null, TimeSpan.FromSeconds(1L));
		}
		OperationRoundResult operationRoundResult3 = RoundByFindArea(base.LastScreenshot, "零号空洞-入口", "出战");
		if (operationRoundResult3.IsSuccess)
		{
			return RoundSuccess(operationRoundResult3.Status, null, TimeSpan.FromSeconds(1L));
		}
		OperationRoundResult operationRoundResult4 = RoundByFindAndClickArea(base.LastScreenshot, "零号空洞-入口", "继续-确认");
		if (operationRoundResult4.IsSuccess)
		{
			return RoundSuccess(operationRoundResult4.Status, null, TimeSpan.FromSeconds(1L));
		}
		return RoundRetry("未找到下一步状态", null, TimeSpan.FromSeconds(1L));
	}
}
