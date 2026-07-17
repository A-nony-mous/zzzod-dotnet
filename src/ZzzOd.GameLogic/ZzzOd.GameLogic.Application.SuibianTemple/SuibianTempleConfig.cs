using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观配置。
/// </summary>
public sealed class SuibianTempleConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "yum_cha_sin", ApplyNamingConventions = false)]
	public bool YumChaSin { get; set; } = true;

	[YamlMember(Alias = "yum_cha_sin_period_refresh", ApplyNamingConventions = false)]
	public bool YumChaSinPeriodRefresh { get; set; } = true;

	[YamlMember(Alias = "adventure_duration", ApplyNamingConventions = false)]
	public string AdventureDuration { get; set; } = SuibianTempleAdventureDispatchDuration.Hour20.Name;

	[YamlMember(Alias = "adventure_mission_1", ApplyNamingConventions = false)]
	public string AdventureMission1 { get; set; } = SuibianTempleAdventureMission.Research34.Name;

	[YamlMember(Alias = "adventure_mission_2", ApplyNamingConventions = false)]
	public string AdventureMission2 { get; set; } = SuibianTempleAdventureMission.Research24.Name;

	[YamlMember(Alias = "adventure_mission_3", ApplyNamingConventions = false)]
	public string AdventureMission3 { get; set; } = SuibianTempleAdventureMission.Research14.Name;

	[YamlMember(Alias = "adventure_mission_4", ApplyNamingConventions = false)]
	public string AdventureMission4 { get; set; } = SuibianTempleAdventureMission.Community34.Name;

	[YamlMember(Alias = "craft_drag_times", ApplyNamingConventions = false)]
	public int CraftDragTimes { get; set; } = 10;

	[YamlMember(Alias = "good_goods_purchase_enabled", ApplyNamingConventions = false)]
	public bool GoodGoodsPurchaseEnabled { get; set; }

	[YamlMember(Alias = "boo_box_purchase_enabled", ApplyNamingConventions = false)]
	public bool BooBoxPurchaseEnabled { get; set; }

	[YamlMember(Alias = "boo_box_adventure_price", ApplyNamingConventions = false)]
	public string BooBoxAdventurePrice { get; set; } = SuibianTempleBangbooPrice.S4.Name;

	[YamlMember(Alias = "boo_box_craft_price", ApplyNamingConventions = false)]
	public string BooBoxCraftPrice { get; set; } = SuibianTempleBangbooPrice.S4.Name;

	[YamlMember(Alias = "boo_box_sell_price", ApplyNamingConventions = false)]
	public string BooBoxSellPrice { get; set; } = SuibianTempleBangbooPrice.S4.Name;

	[YamlMember(Alias = "pawnshop_omnicoin_enabled", ApplyNamingConventions = false)]
	public bool PawnshopOmnicoinEnabled { get; set; } = true;

	[YamlMember(Alias = "pawnshop_omnicoin_priority", ApplyNamingConventions = false)]
	public List<string> PawnshopOmnicoinPriority { get; set; } = SuibianTemplePawnshopOmnicoinGoods.Options.Select((ConfigItem item) => item.Value?.ToString() ?? string.Empty).ToList();

	[YamlMember(Alias = "pawnshop_crest_enabled", ApplyNamingConventions = false)]
	public bool PawnshopCrestEnabled { get; set; } = true;

	[YamlMember(Alias = "pawnshop_crest_priority", ApplyNamingConventions = false)]
	public List<string> PawnshopCrestPriority { get; set; } = SuibianTemplePawnshopCrestGoods.Options.Select((ConfigItem item) => item.Value?.ToString() ?? string.Empty).ToList();

	[YamlMember(Alias = "pawnshop_crest_unlimited_denny_enabled", ApplyNamingConventions = false)]
	public bool PawnshopCrestUnlimitedDennyEnabled { get; set; }

	[YamlMember(Alias = "auto_manage_enabled", ApplyNamingConventions = false)]
	public bool AutoManageEnabled { get; set; } = true;

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static SuibianTempleConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<SuibianTempleConfig> yamlConfig = new YamlConfig<SuibianTempleConfig>(environment, "suibian_temple", null, instanceIndex, new string[2] { "app_config", groupId });
		SuibianTempleConfig current = yamlConfig.Current;
		current.ConfigureRuntime("suibian_temple", instanceIndex, groupId);
		return current;
	}
}
