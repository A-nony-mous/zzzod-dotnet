using System.Collections.Generic;

namespace ZzzOd.GameLogic.Const;

/// <summary>
/// ZZZ 业务应用 id。
/// </summary>
public static class ZzzApplicationIds
{
	public const string AutoBattle = "auto_battle";

	public const string ChargePlan = "charge_plan";

	public const string CityFund = "city_fund";

	public const string Coffee = "coffee";

	public const string CommissionAssistant = "commission_assistant";

	public const string DodgeAssistant = "dodge_assistant";

	public const string DriveDiscDismantle = "drive_disc_dismantle";

	public const string Email = "email";

	public const string EngagementReward = "engagement_reward";

	public const string HouHouBakery = "hou_hou_bakery";

	public const string IntelBoard = "intel_board";

	public const string LifeOnLine = "life_on_line";

	public const string LostVoid = "lost_void";

	public const string MouseSensitivityChecker = "mouse_sensitivity_checker";

	public const string NotoriousHunt = "notorious_hunt";

	public const string Notify = "notify";

	public const string OneDragon = "one_dragon";

	public const string OperationDebug = "operation_debug";

	public const string PredefinedTeamChecker = "predefined_team_checker";

	public const string RandomPlay = "random_play";

	public const string RedemptionCode = "redemption_code";

	public const string RiduWeekly = "ridu_weekly";

	public const string ScratchCard = "scratch_card";

	public const string ScreenshotHelper = "screenshot_helper";

	public const string ShiyuDefense = "shiyu_defense";

	public const string SuibianTemple = "suibian_temple";

	public const string TrigramsCollection = "trigrams_collection";

	public const string WitheredDomain = "withered_domain";

	public const string WorldPatrol = "world_patrol";

	public static IReadOnlyList<string> All { get; } = new string[29]
	{
		"auto_battle", "charge_plan", "city_fund", "coffee", "commission_assistant", "dodge_assistant", "drive_disc_dismantle", "email", "engagement_reward", "hou_hou_bakery",
		"intel_board", "life_on_line", "lost_void", "mouse_sensitivity_checker", "notorious_hunt", "notify", "one_dragon", "operation_debug", "predefined_team_checker", "random_play",
		"redemption_code", "ridu_weekly", "scratch_card", "screenshot_helper", "shiyu_defense", "suibian_temple", "trigrams_collection", "withered_domain", "world_patrol"
	};
}
