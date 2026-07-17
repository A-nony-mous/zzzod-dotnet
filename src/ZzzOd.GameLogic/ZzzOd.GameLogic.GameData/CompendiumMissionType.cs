using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 手册副本类型。
/// </summary>
public sealed class CompendiumMissionType
{
	public CompendiumCategory? Category { get; internal set; }

	public string MissionTypeName { get; init; } = string.Empty;

	public string? MissionTypeNameDisplay { get; init; }

	public List<string> AliasList { get; init; } = new List<string>();

	public List<CompendiumMission> MissionList { get; init; } = new List<CompendiumMission>();

	public string DisplayName => string.IsNullOrWhiteSpace(MissionTypeNameDisplay) ? MissionTypeName : MissionTypeNameDisplay;

	public bool IsAgentPlan => MissionTypeName == "代理人方案培养";

	internal void AttachGraph()
	{
		foreach (CompendiumMission mission in MissionList)
		{
			mission.MissionType = this;
		}
	}
}
