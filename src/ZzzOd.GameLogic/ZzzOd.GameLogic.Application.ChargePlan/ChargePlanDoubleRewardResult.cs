using System;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划双倍奖励检查结果。
/// </summary>
/// <param name="Kind">结果类型。</param>
/// <param name="Status">节点状态。</param>
/// <param name="Plan">命中的临时计划。</param>
/// <param name="Delay">重试等待时间。</param>
public sealed record ChargePlanDoubleRewardResult(ChargePlanDoubleRewardResultKind Kind, string Status, ChargePlanItem? Plan = null, TimeSpan? Delay = null)
{
	/// <summary>
	/// 创建无双倍活动结果。
	/// </summary>
	public static ChargePlanDoubleRewardResult NoActivity()
	{
		return new ChargePlanDoubleRewardResult(ChargePlanDoubleRewardResultKind.Success, "无双倍活动");
	}

	/// <summary>
	/// 创建命中双倍活动计划结果。
	/// </summary>
	public static ChargePlanDoubleRewardResult WithPlan(ChargePlanItem plan)
	{
		return new ChargePlanDoubleRewardResult(ChargePlanDoubleRewardResultKind.Success, string.Empty, plan);
	}

	/// <summary>
	/// 创建重试结果。
	/// </summary>
	public static ChargePlanDoubleRewardResult Retry(string status)
	{
		return new ChargePlanDoubleRewardResult(ChargePlanDoubleRewardResultKind.Retry, status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 创建失败结果。
	/// </summary>
	public static ChargePlanDoubleRewardResult Fail(string status)
	{
		return new ChargePlanDoubleRewardResult(ChargePlanDoubleRewardResultKind.Fail, status);
	}
}
