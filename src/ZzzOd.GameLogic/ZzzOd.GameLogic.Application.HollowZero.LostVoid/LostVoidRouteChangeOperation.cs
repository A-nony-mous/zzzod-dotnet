using System;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidRouteChangeOperation : ZOperation
{
	public LostVoidRouteChangeOperation(ZContext context)
		: base(context, "迷失之地-路径迭换")
	{
	}

	[OperationNode("返回", IsStartNode = true, NodeMaxRetryTimes = 5)]
	public OperationRoundResult BackToWorld()
	{
		if (base.LastScreenshot != null && LostVoidMoveByDetectionService.Instance.IsInNormalWorld(base.ZContext, base.LastScreenshot))
		{
			return RoundSuccess();
		}
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "迷失之地-路径迭换", "按钮-返回");
		return RoundRetry(operationRoundResult.Status, null, TimeSpan.FromSeconds(1L));
	}
}
