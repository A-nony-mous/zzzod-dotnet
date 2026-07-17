using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地运行记录 YAML 数据。
/// </summary>
public sealed class WorldPatrolRunRecordData
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

	/// <summary>当日已完成路线。</summary>
	[YamlMember(Alias = "finished", ApplyNamingConventions = false)]
	public List<string> Finished { get; set; } = new List<string>();

	/// <summary>当日已完成轮数。</summary>
	[YamlMember(Alias = "completed_rounds", ApplyNamingConventions = false)]
	public int CompletedRounds { get; set; }

	/// <summary>本次任务每轮路线数。</summary>
	[YamlMember(Alias = "routes_per_round", ApplyNamingConventions = false)]
	public int RoutesPerRound { get; set; }
}
