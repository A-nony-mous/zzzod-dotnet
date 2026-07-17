using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

internal sealed class WitheredDomainChooseMissionTypeOperation : ZOperation
{
	private readonly string _missionTypeName;

	public WitheredDomainChooseMissionTypeOperation(ZContext context, string missionTypeName)
		: base(context, "枯萎之都 选择副本类型")
	{
		_missionTypeName = missionTypeName;
	}

	[OperationNode("选择副本类型", IsStartNode = true)]
	private OperationRoundResult ChooseMissionType()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "零号空洞-入口", "下一步", null, successDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		Mat? lastScreenshot2 = base.LastScreenshot;
		string missionTypeName = _missionTypeName;
		successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByOcrAndClick(lastScreenshot2, missionTypeName, null, 0.6, null, successDelay, retryDelay);
	}
}
