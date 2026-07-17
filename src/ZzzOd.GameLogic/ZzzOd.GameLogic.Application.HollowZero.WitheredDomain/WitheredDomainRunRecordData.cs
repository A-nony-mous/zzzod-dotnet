using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都运行记录 YAML 数据。
/// </summary>
public sealed class WitheredDomainRunRecordData
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

	/// <summary>本周运行次数。</summary>
	[YamlMember(Alias = "weekly_run_times", ApplyNamingConventions = false)]
	public int WeeklyRunTimes { get; set; }

	/// <summary>今日进入次数。</summary>
	[YamlMember(Alias = "daily_run_times", ApplyNamingConventions = false)]
	public int DailyRunTimes { get; set; }

	/// <summary>业绩点已空。</summary>
	[YamlMember(Alias = "no_eval_point", ApplyNamingConventions = false)]
	public bool NoEvalPoint { get; set; }

	/// <summary>周期奖励已满。</summary>
	[YamlMember(Alias = "period_reward_complete", ApplyNamingConventions = false)]
	public bool PeriodRewardComplete { get; set; }
}
