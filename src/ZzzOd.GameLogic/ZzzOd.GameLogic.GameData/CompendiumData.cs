using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 手册数据根对象。
/// </summary>
public sealed class CompendiumData
{
	public List<CompendiumTab> TabList { get; init; } = new List<CompendiumTab>();

	internal void AttachGraph()
	{
		foreach (CompendiumTab tab in TabList)
		{
			tab.AttachGraph();
		}
	}
}
