using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 邦布最低购买价格。
/// </summary>
public static class SuibianTempleBangbooPrice
{
	/// <summary>S4 价格。</summary>
	public static SuibianTempleNamedOption S4 { get; } = new SuibianTempleNamedOption("S4", "25000");

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[5]
	{
		new ConfigItem("40000", "S1"),
		new ConfigItem("35000", "S2"),
		new ConfigItem("30000", "S3"),
		new ConfigItem(S4.Label, S4.Name),
		new ConfigItem("不购买", "NONE")
	};
}
