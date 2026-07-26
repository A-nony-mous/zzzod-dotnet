using System;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 区域巡防挑战流程。
/// </summary>
public sealed class AreaPatrol : CompendiumChallengeOperationBase
{
	/// <summary>电量不足。</summary>
	public const string StatusChargeNotEnough = "电量不足";

	/// <summary>战斗超时。</summary>
	public const string StatusFightTimeout = "战斗超时";

	/// <summary>
	/// 初始化区域巡防挑战。
	/// </summary>
	public AreaPatrol(ZContext context, ChargePlanItem plan, ChargePlanConfig? config = null, ChallengeMissionServices? services = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "区域巡防 " + plan.MissionTypeName, plan, config, services, retryDelay, preClickDelay)
	{
	}

	/// <inheritdoc />
	[NodeFrom("等待入口加载")]
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
