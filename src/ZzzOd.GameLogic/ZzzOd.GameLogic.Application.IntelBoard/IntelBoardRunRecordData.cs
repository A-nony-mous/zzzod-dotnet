using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板运行记录 YAML 数据。
/// </summary>
public sealed class IntelBoardRunRecordData
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

	/// <summary>本周期进度是否已满。</summary>
	[YamlMember(Alias = "progress_complete", ApplyNamingConventions = false)]
	public bool ProgressComplete { get; set; }

	/// <summary>本周期恶名狩猎完成次数。</summary>
	[YamlMember(Alias = "notorious_hunt_count", ApplyNamingConventions = false)]
	public int NotoriousHuntCount { get; set; }

	/// <summary>本周期专业挑战室完成次数。</summary>
	[YamlMember(Alias = "expert_challenge_count", ApplyNamingConventions = false)]
	public int ExpertChallengeCount { get; set; }

	/// <summary>根据 OCR 进度估算的基础经验值。</summary>
	[YamlMember(Alias = "base_exp", ApplyNamingConventions = false)]
	public int BaseExp { get; set; }
}
