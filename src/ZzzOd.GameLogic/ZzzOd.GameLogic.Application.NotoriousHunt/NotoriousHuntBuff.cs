using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎 BUFF 选项。
/// </summary>
public static class NotoriousHuntBuff
{
	/// <summary>BUFF 设置项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("第一个BUFF", 1),
		new ConfigItem("第二个BUFF", 2),
		new ConfigItem("第三个BUFF", 3)
	};
}
