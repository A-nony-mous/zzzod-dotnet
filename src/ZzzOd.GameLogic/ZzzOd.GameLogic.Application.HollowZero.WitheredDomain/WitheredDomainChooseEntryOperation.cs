using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

internal sealed class WitheredDomainChooseEntryOperation : ZOperation
{
	private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1L);

	public WitheredDomainChooseEntryOperation(ZContext context)
		: base(context, "前往枯萎之都-入口", 20)
	{
	}

	[OperationNode("前往枯萎之都-入口", IsStartNode = true)]
	private OperationRoundResult ChooseEntry()
	{
		OperationRoundResult entryResult = RoundByFindArea(base.LastScreenshot, "零号空洞-入口", "街区", null, OneSecond);
		if (entryResult.IsSuccess)
		{
			return RoundSuccess("已进入枯萎之都-入口");
		}
		OperationRoundResult result = RoundByOcrAndClick(base.LastScreenshot, "枯萎之都", null, 0.5, null, OneSecond, OneSecond);
		if (result.IsSuccess)
		{
			return RoundRetry("尝试进入枯萎之都-入口", null, OneSecond);
		}
		return RoundRetry(result.Status, null, OneSecond);
	}
}
