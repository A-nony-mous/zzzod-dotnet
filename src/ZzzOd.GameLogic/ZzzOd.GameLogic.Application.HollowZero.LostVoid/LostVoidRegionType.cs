using System;
using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地区域类型。
/// </summary>
public static class LostVoidRegionType
{
	/// <summary>入口。</summary>
	public const string Entry = "入口";

	/// <summary>战斗-鸣徽。</summary>
	public const string CombatResonium = "战斗-鸣徽";

	/// <summary>战斗-武备。</summary>
	public const string CombatGear = "战斗-武备";

	/// <summary>战斗-硬币。</summary>
	public const string CombatCoin = "战斗-硬币";

	/// <summary>挑战-无伤。</summary>
	public const string ChallengeFlawless = "挑战-无伤";

	/// <summary>挑战-限时。</summary>
	public const string ChallengeTimeTrial = "挑战-限时";

	/// <summary>挑战-收割。</summary>
	public const string ChallengeEnemyTrial = "挑战-收割";

	/// <summary>偶遇事件。</summary>
	public const string Encounter = "偶遇事件";

	/// <summary>代价之间。</summary>
	public const string PriceDifference = "代价之间";

	/// <summary>休憩调息。</summary>
	public const string Rest = "休憩调息";

	/// <summary>邦布商店。</summary>
	public const string BangbooStore = "邦布商店";

	/// <summary>挚交会谈。</summary>
	public const string FriendlyTalk = "挚交会谈";

	/// <summary>战斗-道中危机。</summary>
	public const string Elite = "战斗-道中危机";

	/// <summary>战斗-终结之役。</summary>
	public const string Boss = "战斗-终结之役";

	/// <summary>全部区域类型。</summary>
	public static IReadOnlyList<string> All { get; } = new string[14]
	{
		"入口", "战斗-鸣徽", "战斗-武备", "战斗-硬币", "挑战-无伤", "挑战-限时", "挑战-收割", "偶遇事件", "代价之间", "休憩调息",
		"邦布商店", "挚交会谈", "战斗-道中危机", "战斗-终结之役"
	};

	/// <summary>
	/// 转换为已知区域类型。
	/// </summary>
	public static string FromValue(string? value)
	{
		return All.Contains<string>(value, StringComparer.Ordinal) ? value : "入口";
	}
}
