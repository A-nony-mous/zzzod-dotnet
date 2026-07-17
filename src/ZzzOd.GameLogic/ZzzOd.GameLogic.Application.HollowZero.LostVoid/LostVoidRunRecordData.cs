using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地运行记录 YAML 数据。
/// </summary>
public sealed class LostVoidRunRecordData
{
	/// <summary>记录日期。</summary>
	[YamlMember(Alias = "dt", ApplyNamingConventions = false)]
	public string Dt { get; set; } = string.Empty;

	/// <summary>运行时间文本。</summary>
	[YamlMember(Alias = "run_time", ApplyNamingConventions = false)]
	public string RunTime { get; set; } = "-";

	/// <summary>运行时间戳。</summary>
	[YamlMember(Alias = "run_time_float", ApplyNamingConventions = false)]
	public double RunTimeFloat { get; set; }

	/// <summary>运行状态。</summary>
	[YamlMember(Alias = "run_status", ApplyNamingConventions = false)]
	public int RunStatus { get; set; } = 0;

	/// <summary>今日完成次数。</summary>
	[YamlMember(Alias = "daily_run_times", ApplyNamingConventions = false)]
	public int DailyRunTimes { get; set; }

	/// <summary>本周完成次数。</summary>
	[YamlMember(Alias = "weekly_run_times", ApplyNamingConventions = false)]
	public int WeeklyRunTimes { get; set; }

	/// <summary>悬赏委托已完成。</summary>
	[YamlMember(Alias = "bounty_commission_complete", ApplyNamingConventions = false)]
	public bool BountyCommissionComplete { get; set; }

	/// <summary>业绩点已刷满。</summary>
	[YamlMember(Alias = "eval_point_complete", ApplyNamingConventions = false)]
	public bool EvalPointComplete { get; set; }

	/// <summary>周期奖励已刷满。</summary>
	[YamlMember(Alias = "period_reward_complete", ApplyNamingConventions = false)]
	public bool PeriodRewardComplete { get; set; }

	/// <summary>已使用 UP 代理人完成特遣调查。</summary>
	[YamlMember(Alias = "complete_task_force_with_up", ApplyNamingConventions = false)]
	public bool CompleteTaskForceWithUp { get; set; }
}
