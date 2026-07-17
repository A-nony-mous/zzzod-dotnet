namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 咖啡定义。
/// </summary>
public sealed class Coffee
{
	public required string CoffeeName { get; init; }

	public CompendiumTab? Tab { get; init; }

	public CompendiumCategory? Category { get; init; }

	public CompendiumMissionType? MissionType { get; init; }

	public CompendiumMission? Mission { get; init; }

	public bool Extra { get; init; }

	public string DisplayName
	{
		get
		{
			if (MissionType == null)
			{
				return CoffeeName;
			}
			return (Mission == null) ? MissionType.DisplayName : (MissionType.DisplayName + " - " + Mission.DisplayName);
		}
	}

	public bool WithoutBenefit => MissionType == null;
}
