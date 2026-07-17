using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战运行记录 YAML 数据。
/// </summary>
public sealed class ShiyuDefenseRunRecordData
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

	/// <summary>已完成剧变节点。</summary>
	[YamlMember(Alias = "critical_history", ApplyNamingConventions = false)]
	public List<int> CriticalHistory { get; set; } = new List<int>();
}
