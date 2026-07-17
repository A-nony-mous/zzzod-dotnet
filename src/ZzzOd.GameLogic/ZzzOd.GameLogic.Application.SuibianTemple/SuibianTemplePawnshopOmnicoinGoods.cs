using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 德丰大押百宝通商品。
/// </summary>
public static class SuibianTemplePawnshopOmnicoinGoods
{
	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[5]
	{
		new ConfigItem("高保真母盘", "HIFI_MASTER_COPY"),
		new ConfigItem("资深调查员记录", "SENIOR_INVESTIGATOR_LOG"),
		new ConfigItem("音擎能源模块", "W_ENGINE_ENERGY_MODULE"),
		new ConfigItem("以太镀剂", "ETHER_PLATING_AGENT"),
		new ConfigItem("储值电卡", "PREPAID_POWER_CARD")
	};
}
