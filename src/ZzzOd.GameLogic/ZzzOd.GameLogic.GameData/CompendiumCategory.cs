using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 手册分类。
/// </summary>
public sealed class CompendiumCategory
{
	public CompendiumTab? Tab { get; internal set; }

	public string CategoryName { get; init; } = string.Empty;

	public List<CompendiumMissionType> MissionTypeList { get; init; } = new List<CompendiumMissionType>();

	internal void AttachGraph()
	{
		foreach (CompendiumMissionType missionType in MissionTypeList)
		{
			missionType.Category = this;
			missionType.AttachGraph();
		}
	}
}
