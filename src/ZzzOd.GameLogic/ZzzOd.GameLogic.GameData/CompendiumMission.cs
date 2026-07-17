namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 手册副本。
/// </summary>
public sealed class CompendiumMission
{
	public CompendiumMissionType? MissionType { get; internal set; }

	public string MissionName { get; init; } = string.Empty;

	public string? MissionNameDisplay { get; init; }

	public string DisplayName => string.IsNullOrWhiteSpace(MissionNameDisplay) ? MissionName : MissionNameDisplay;
}
