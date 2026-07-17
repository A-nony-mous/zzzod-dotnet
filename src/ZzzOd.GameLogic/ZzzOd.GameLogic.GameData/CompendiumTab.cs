using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 手册页签。
/// </summary>
public sealed class CompendiumTab
{
	public string TabName { get; init; } = string.Empty;

	public List<CompendiumCategory> CategoryList { get; init; } = new List<CompendiumCategory>();

	internal void AttachGraph()
	{
		foreach (CompendiumCategory category in CategoryList)
		{
			category.Tab = this;
			category.AttachGraph();
		}
	}
}
