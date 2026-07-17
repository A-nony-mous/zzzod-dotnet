using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 游历任务。
/// </summary>
public static class SuibianTempleAdventureMission
{
	/// <summary>科研院旧址 3-4。</summary>
	public static SuibianTempleNamedOption Research34 { get; } = new SuibianTempleNamedOption("RESEARCH_3_4", "科研院旧址3-4");

	/// <summary>科研院旧址 2-4。</summary>
	public static SuibianTempleNamedOption Research24 { get; } = new SuibianTempleNamedOption("RESEARCH_2_4", "科研院旧址2-4");

	/// <summary>科研院旧址 1-4。</summary>
	public static SuibianTempleNamedOption Research14 { get; } = new SuibianTempleNamedOption("RESEARCH_1_4", "科研院旧址1-4");

	/// <summary>社区旧址 3-4。</summary>
	public static SuibianTempleNamedOption Community34 { get; } = new SuibianTempleNamedOption("COMMUNITY_3_4", "社区旧址3-4");

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[36]
	{
		new ConfigItem("制造区1-1", "CRAFT_1_1"),
		new ConfigItem("制造区1-2", "CRAFT_1_2"),
		new ConfigItem("制造区1-3", "CRAFT_1_3"),
		new ConfigItem("制造区1-4", "CRAFT_1_4"),
		new ConfigItem("制造区2-1", "CRAFT_2_1"),
		new ConfigItem("制造区2-2", "CRAFT_2_2"),
		new ConfigItem("制造区2-3", "CRAFT_2_3"),
		new ConfigItem("制造区2-4", "CRAFT_2_4"),
		new ConfigItem("制造区3-1", "CRAFT_3_1"),
		new ConfigItem("制造区3-2", "CRAFT_3_2"),
		new ConfigItem("制造区3-3", "CRAFT_3_3"),
		new ConfigItem("制造区3-4", "CRAFT_3_4"),
		new ConfigItem("社区旧址1-1", "COMMUNITY_1_1"),
		new ConfigItem("社区旧址1-2", "COMMUNITY_1_2"),
		new ConfigItem("社区旧址1-3", "COMMUNITY_1_3"),
		new ConfigItem("社区旧址1-4", "COMMUNITY_1_4"),
		new ConfigItem("社区旧址2-1", "COMMUNITY_2_1"),
		new ConfigItem("社区旧址2-2", "COMMUNITY_2_2"),
		new ConfigItem("社区旧址2-3", "COMMUNITY_2_3"),
		new ConfigItem("社区旧址2-4", "COMMUNITY_2_4"),
		new ConfigItem("社区旧址3-1", "COMMUNITY_3_1"),
		new ConfigItem("社区旧址3-2", "COMMUNITY_3_2"),
		new ConfigItem("社区旧址3-3", "COMMUNITY_3_3"),
		new ConfigItem(Community34.Label, Community34.Name),
		new ConfigItem("科研院旧址1-1", "RESEARCH_1_1"),
		new ConfigItem("科研院旧址1-2", "RESEARCH_1_2"),
		new ConfigItem("科研院旧址1-3", "RESEARCH_1_3"),
		new ConfigItem(Research14.Label, Research14.Name),
		new ConfigItem("科研院旧址2-1", "RESEARCH_2_1"),
		new ConfigItem("科研院旧址2-2", "RESEARCH_2_2"),
		new ConfigItem("科研院旧址2-3", "RESEARCH_2_3"),
		new ConfigItem(Research24.Label, Research24.Name),
		new ConfigItem("科研院旧址3-1", "RESEARCH_3_1"),
		new ConfigItem("科研院旧址3-2", "RESEARCH_3_2"),
		new ConfigItem("科研院旧址3-3", "RESEARCH_3_3"),
		new ConfigItem(Research34.Label, Research34.Name)
	};
}
