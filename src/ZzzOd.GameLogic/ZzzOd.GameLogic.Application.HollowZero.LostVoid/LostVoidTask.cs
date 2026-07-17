using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地额外任务。
/// </summary>
public static class LostVoidTask
{
	/// <summary>完成悬赏委托。</summary>
	public const string BountyCommission = "完成悬赏委托";

	/// <summary>刷满业绩点。</summary>
	public const string EvalPoint = "刷满业绩点";

	/// <summary>刷满周期奖励。</summary>
	public const string PeriodReward = "刷满周期奖励";

	/// <summary>完成周计划次数。</summary>
	public const string WeeklyPlanTimes = "完成周计划次数";

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[4]
	{
		new ConfigItem("完成悬赏委托", "完成悬赏委托", "完成每周8000积分奖励"),
		new ConfigItem("刷满业绩点", "刷满业绩点", "刷满每周业绩点"),
		new ConfigItem("刷满周期奖励", "刷满周期奖励", "刷满每周丁尼"),
		new ConfigItem("完成周计划次数", "完成周计划次数", "完成配置的每周计划次数")
	};
}
