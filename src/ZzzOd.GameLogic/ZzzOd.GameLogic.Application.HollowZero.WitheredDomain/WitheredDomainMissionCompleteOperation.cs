using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都通关确认。等待“街区”出现后才写入今日次数。
/// </summary>
internal sealed class WitheredDomainMissionCompleteOperation : ZOperation
{
	private readonly WitheredDomainRunRecord _runRecord;

	public WitheredDomainMissionCompleteOperation(ZContext context, WitheredDomainRunRecord runRecord)
		: base(context, "枯萎之都 通关完成")
	{
		_runRecord = runRecord;
	}

	[OperationNode("通关-完成", IsStartNode = true, NodeMaxRetryTimes = 60)]
	private OperationRoundResult MissionComplete()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "零号空洞-事件", "通关-完成");
		if (operationRoundResult.IsSuccess)
		{
			FindAreaResultEnum findAreaResultEnum = ((base.LastScreenshot != null) ? ScreenUtils.FindArea(base.ZContext, base.LastScreenshot, "零号空洞-战斗", "通关-丁尼奖励") : FindAreaResultEnum.False);
			_runRecord.SetPeriodRewardComplete(findAreaResultEnum != FindAreaResultEnum.True);
			return RoundWait(operationRoundResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "零号空洞-入口", "街区");
		if (operationRoundResult2.IsSuccess)
		{
			_runRecord.AddDailyTimes();
			return RoundSuccess(operationRoundResult2.Status);
		}
		return RoundRetry(operationRoundResult2.Status, null, TimeSpan.FromSeconds(1L));
	}
}
