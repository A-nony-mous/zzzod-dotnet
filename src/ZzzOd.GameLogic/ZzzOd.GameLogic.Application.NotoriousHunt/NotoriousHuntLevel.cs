using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎难度选项。
/// </summary>
public static class NotoriousHuntLevel
{
	/// <summary>默认难度。</summary>
	public const string Default = "默认等级";

	/// <summary>难度设置项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[6]
	{
		new ConfigItem("默认等级", "默认等级"),
		new ConfigItem("等级Lv.65", "等级Lv.65"),
		new ConfigItem("等级Lv.60", "等级Lv.60"),
		new ConfigItem("等级Lv.50", "等级Lv.50"),
		new ConfigItem("等级Lv.40", "等级Lv.40"),
		new ConfigItem("等级Lv.30", "等级Lv.30")
	};
}
