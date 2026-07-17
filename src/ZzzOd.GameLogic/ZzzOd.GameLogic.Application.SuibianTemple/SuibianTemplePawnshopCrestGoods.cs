using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 德丰大押云纹徽商品。
/// </summary>
public static class SuibianTemplePawnshopCrestGoods
{
	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[2]
	{
		new ConfigItem("邦布系统控件", "BANGBOO_SYSTEM_WIDGET"),
		new ConfigItem("丁尼", "DENNY")
	};
}
