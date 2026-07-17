using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 自动开始挑战的星期。
/// </summary>
public static class NotoriousHuntWeekday
{
	/// <summary>自动开始挑战星期设置项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[7]
	{
		new ConfigItem("周一", 1),
		new ConfigItem("周二", 2),
		new ConfigItem("周三", 3),
		new ConfigItem("周四", 4),
		new ConfigItem("周五", 5),
		new ConfigItem("周六", 6),
		new ConfigItem("周日", 7)
	};
}
