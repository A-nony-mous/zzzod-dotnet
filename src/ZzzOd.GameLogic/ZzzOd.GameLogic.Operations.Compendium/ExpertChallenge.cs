using System;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 专业挑战室挑战流程。
/// </summary>
public sealed class ExpertChallenge : CompendiumChallengeOperationBase
{
	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>电量不足。</summary>
	public const string StatusChargeNotEnough = "电量不足";

	/// <summary>战斗超时。</summary>
	public const string StatusFightTimeout = "战斗超时";

	/// <summary>
	/// 初始化专业挑战室挑战。
	/// </summary>
	public ExpertChallenge(ZContext context, ChargePlanItem plan, ChargePlanConfig? config = null, ChallengeMissionServices? services = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "专业挑战室 " + plan.MissionTypeName, plan, config, services, retryDelay, preClickDelay)
	{
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[NodeFrom("等待入口加载", Status = "挑战等级")]
	[NodeFrom("等待入口加载")]
	[OperationNode("关闭燃竭模式")]
	private OperationRoundResult CloseBurnoutMode()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "恶名狩猎", "按钮-深度追猎-确认", _preClickDelay, _retryDelay, _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "按钮-深度追猎-ON");
		if (operationRoundResult2.IsSuccess)
		{
			RoundByClickArea("恶名狩猎", "按钮-深度追猎-ON", clickLeftTop: false, _preClickDelay);
			return RoundRetry(operationRoundResult2.Status, null, _retryDelay);
		}
		return RoundSuccess();
	}

	/// <inheritdoc />
	[NodeFrom("关闭燃竭模式")]
	[NodeFrom("恢复电量", Status = "恢复电量成功")]
	[OperationNode("下一步", NodeMaxRetryTimes = 10)]
	protected override OperationRoundResult ClickNext()
	{
		return base.ClickNext();
	}

	/// <inheritdoc />
	[NodeFrom("战斗超时")]
	[OperationNode("点击挑战结果退出")]
	protected override OperationRoundResult ClickResultExit()
	{
		return base.ClickResultExit();
	}
}
