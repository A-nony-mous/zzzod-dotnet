using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 游历派遣时长。
/// </summary>
public static class SuibianTempleAdventureDispatchDuration
{
	/// <summary>20 小时。</summary>
	public static SuibianTempleNamedOption Hour20 { get; } = new SuibianTempleNamedOption("HOUR_20", "20小时");

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[7]
	{
		new ConfigItem("3分钟", "MIN_3"),
		new ConfigItem("15分钟", "MIN_15"),
		new ConfigItem("1小时", "HOUR_1"),
		new ConfigItem("2小时", "HOUR_2"),
		new ConfigItem("6小时", "HOUR_6"),
		new ConfigItem("12小时", "HOUR_12"),
		new ConfigItem(Hour20.Label, Hour20.Name)
	};
}
