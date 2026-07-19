using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.ChallengeMission;

/// <summary>
/// 战斗结束后选择再来一次或完成。
/// </summary>
public sealed class ChooseNextOrFinishAfterBattle : ZOperation
{
	/// <summary>特训目标已达成。</summary>
	public const string StatusAgentPlanFinished = "特训目标已达成";

	private readonly bool _isAgentPlan;

	private readonly ChargePlanConfig _config;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private bool _tryNext;

	/// <summary>
	/// 初始化战斗后选择操作。
	/// </summary>
	public ChooseNextOrFinishAfterBattle(ZContext context, bool tryNext, bool isAgentPlan = false, ChargePlanConfig? config = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "战斗后选择")
	{
		_tryNext = tryNext;
		_isAgentPlan = isAgentPlan;
		_config = config ?? ChargePlanConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.RunContext.CurrentGroupId ?? "one_dragon");
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[NodeFrom("恢复电量", Status = "战斗结果-完成")]
	[OperationNode("判断再来一次", IsStartNode = true)]
	private OperationRoundResult CheckNext()
	{
		if (_tryNext && _isAgentPlan)
		{
			OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "战斗画面", "战斗结果-已达成");
			if (operationRoundResult.IsSuccess)
			{
				OperationRoundResult operationRoundResult2 = ClickFinish();
				return operationRoundResult2.IsSuccess ? RoundSuccess("特训目标已达成") : operationRoundResult2;
			}
		}
		if (_tryNext)
		{
			OperationRoundResult operationRoundResult3 = RoundByFindAndClickArea(base.LastScreenshot, "战斗画面", "战斗结果-再来一次", _preClickDelay, _retryDelay, _retryDelay);
			if (operationRoundResult3.IsSuccess)
			{
				return operationRoundResult3;
			}
		}
		return ClickFinish();
	}

	[NodeFrom("判断再来一次", Status = "战斗结果-再来一次")]
	[OperationNode("恢复电量")]
	private async Task<OperationRoundResult> RestoreChargeAfterRetry()
	{
		OperationRoundResult restoreTitle = RoundByFindArea(base.LastScreenshot, "恢复电量", "标题-恢复电量");
		if (!restoreTitle.IsSuccess)
		{
			return RoundSuccess("战斗结果-再来一次");
		}
		if (_config.IsRestoreChargeEnabled)
		{
			ZContext zContext = base.ZContext;
			ChargePlanConfig config = _config;
			TimeSpan? retryDelay = _retryDelay;
			TimeSpan? preClickDelay = _preClickDelay;
			RestoreCharge operation = new RestoreCharge(zContext, config, retryDelay, preClickDelay)
			{
				IsAfterBattleRetry = true
			};
			OperationResult result = await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (!result.IsSuccess)
			{
				return RoundByOperationResult(result);
			}
			_tryNext = result.Status == "恢复电量成功";
		}
		else
		{
			_tryNext = false;
			OperationRoundResult cancel = RoundByFindAndClickArea(base.LastScreenshot, "恢复电量", "取消", _preClickDelay, TimeSpan.FromSeconds(1L), _retryDelay);
			if (!cancel.IsSuccess)
			{
				return cancel;
			}
		}
		return RoundSuccess("战斗结果-完成", null, TimeSpan.FromMilliseconds(500L));
	}

	private OperationRoundResult ClickFinish()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "战斗画面", "战斗结果-完成", _preClickDelay, TimeSpan.FromSeconds(5L), _retryDelay);
	}
}
