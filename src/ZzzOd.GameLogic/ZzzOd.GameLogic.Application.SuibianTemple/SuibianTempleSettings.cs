using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观设置元数据。
/// </summary>
public static class SuibianTempleSettings
{
	/// <summary>BaselineParity 设置提供器类型。</summary>
	public const string SettingType = "INTERFACE";

	/// <summary>字段列表。</summary>
	public static IReadOnlyList<SuibianTempleSettingField> Fields { get; } = new SuibianTempleSettingField[19]
	{
		new SuibianTempleSettingField("auto_manage_enabled", "自动托管", SuibianTempleSettingType.Bool, true),
		new SuibianTempleSettingField("yum_cha_sin", "饮茶仙", SuibianTempleSettingType.Bool, true),
		new SuibianTempleSettingField("yum_cha_sin_period_refresh", "饮茶仙-委托刷新", SuibianTempleSettingType.Bool, true),
		new SuibianTempleSettingField("adventure_duration", "派遣-时长", SuibianTempleSettingType.Enum, SuibianTempleAdventureDispatchDuration.Hour20.Name, SuibianTempleAdventureDispatchDuration.Options),
		new SuibianTempleSettingField("adventure_mission_1", "派遣-副本优先级1", SuibianTempleSettingType.Enum, SuibianTempleAdventureMission.Research34.Name, SuibianTempleAdventureMission.Options),
		new SuibianTempleSettingField("adventure_mission_2", "派遣-副本优先级2", SuibianTempleSettingType.Enum, SuibianTempleAdventureMission.Research24.Name, SuibianTempleAdventureMission.Options),
		new SuibianTempleSettingField("adventure_mission_3", "派遣-副本优先级3", SuibianTempleSettingType.Enum, SuibianTempleAdventureMission.Research14.Name, SuibianTempleAdventureMission.Options),
		new SuibianTempleSettingField("adventure_mission_4", "派遣-副本优先级4", SuibianTempleSettingType.Enum, SuibianTempleAdventureMission.Community34.Name, SuibianTempleAdventureMission.Options),
		new SuibianTempleSettingField("craft_drag_times", "制造坊-最大下拉次数", SuibianTempleSettingType.Integer, 10),
		new SuibianTempleSettingField("good_goods_purchase_enabled", "好物铺购买", SuibianTempleSettingType.Bool, false),
		new SuibianTempleSettingField("boo_box_purchase_enabled", "邦巢-购买", SuibianTempleSettingType.Bool, false),
		new SuibianTempleSettingField("boo_box_adventure_price", "邦巢-游历最低购买价格", SuibianTempleSettingType.Enum, SuibianTempleBangbooPrice.S4.Name, SuibianTempleBangbooPrice.Options),
		new SuibianTempleSettingField("boo_box_craft_price", "邦巢-制造最低购买价格", SuibianTempleSettingType.Enum, SuibianTempleBangbooPrice.S4.Name, SuibianTempleBangbooPrice.Options),
		new SuibianTempleSettingField("boo_box_sell_price", "邦巢-售卖最低购买价格", SuibianTempleSettingType.Enum, SuibianTempleBangbooPrice.S4.Name, SuibianTempleBangbooPrice.Options),
		new SuibianTempleSettingField("pawnshop_omnicoin_enabled", "德丰大押-百宝通", SuibianTempleSettingType.Bool, true),
		new SuibianTempleSettingField("pawnshop_omnicoin_priority", "德丰大押-百宝通优先级", SuibianTempleSettingType.MultiEnum, SuibianTemplePawnshopOmnicoinGoods.Options.Select((ConfigItem item) => item.Value?.ToString() ?? string.Empty).ToList(), SuibianTemplePawnshopOmnicoinGoods.Options),
		new SuibianTempleSettingField("pawnshop_crest_enabled", "德丰大押-云纹徽", SuibianTempleSettingType.Bool, true),
		new SuibianTempleSettingField("pawnshop_crest_priority", "德丰大押-云纹徽优先级", SuibianTempleSettingType.MultiEnum, SuibianTemplePawnshopCrestGoods.Options.Select((ConfigItem item) => item.Value?.ToString() ?? string.Empty).ToList(), SuibianTemplePawnshopCrestGoods.Options),
		new SuibianTempleSettingField("pawnshop_crest_unlimited_denny_enabled", "德丰大押-不限购丁尼", SuibianTempleSettingType.Bool, false)
	};
}
