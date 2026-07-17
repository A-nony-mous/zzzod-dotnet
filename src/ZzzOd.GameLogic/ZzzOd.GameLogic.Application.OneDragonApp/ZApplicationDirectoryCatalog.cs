using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>
/// 内置应用目录列表。
/// </summary>
public static class ZApplicationDirectoryCatalog
{
	/// <summary>
	/// 与 BaselineParity `zzz_od/application` 当前 25 个顶层目录保持一致。
	/// </summary>
	public static IReadOnlyList<ZApplicationDirectoryMetadata> BuiltInDirectories { get; } = new ZApplicationDirectoryMetadata[25]
	{
		new ZApplicationDirectoryMetadata("battle_assistant", "战斗助手", new string[2] { "auto_battle", "dodge_assistant" }, DefaultGroup: false, NeedNotify: false),
		new ZApplicationDirectoryMetadata("charge_plan", "体力刷本", new string[] { "charge_plan" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("city_fund", "丽都城募", new string[] { "city_fund" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("coffee", "咖啡店", new string[] { "coffee" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("commission_assistant", "委托助手", new string[] { "commission_assistant" }, DefaultGroup: false, NeedNotify: false),
		new ZApplicationDirectoryMetadata("devtools", "开发工具", new string[2] { "operation_debug", "screenshot_helper" }, DefaultGroup: false, NeedNotify: false),
		new ZApplicationDirectoryMetadata("drive_disc_dismantle", "驱动盘拆解", new string[] { "drive_disc_dismantle" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("email_app", "邮件", new string[] { "email" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("engagement_reward", "活跃度奖励", new string[] { "engagement_reward" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("game_config_checker", "游戏配置检查", new string[2] { "mouse_sensitivity_checker", "predefined_team_checker" }, DefaultGroup: false, NeedNotify: false),
		new ZApplicationDirectoryMetadata("hollow_zero", "零号空洞", new string[2] { "lost_void", "withered_domain" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("hou_hou_bakery", "吼吼饼铺", new string[] { "hou_hou_bakery" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("intel_board", "情报板", new string[] { "intel_board" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("life_on_line", "真·拿命验收", new string[] { "life_on_line" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("notify", "通知", new string[] { "notify" }, DefaultGroup: true, NeedNotify: false),
		new ZApplicationDirectoryMetadata("notorious_hunt", "恶名狩猎", new string[] { "notorious_hunt" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("one_dragon_app", "一条龙", new string[] { "one_dragon" }, DefaultGroup: false, NeedNotify: false),
		new ZApplicationDirectoryMetadata("random_play", "录像店营业", new string[] { "random_play" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("redemption_code", "兑换码", new string[] { "redemption_code" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("ridu_weekly", "丽都周纪 (领奖励)", new string[] { "ridu_weekly" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("scratch_card", "刮刮卡", new string[] { "scratch_card" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("shiyu_defense", "式舆防卫战", new string[] { "shiyu_defense" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("suibian_temple", "随便观", new string[] { "suibian_temple" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("trigrams_collection", "卦象集录", new string[] { "trigrams_collection" }, DefaultGroup: true, NeedNotify: true),
		new ZApplicationDirectoryMetadata("world_patrol", "锄大地", new string[] { "world_patrol" }, DefaultGroup: true, NeedNotify: true)
	};
}
