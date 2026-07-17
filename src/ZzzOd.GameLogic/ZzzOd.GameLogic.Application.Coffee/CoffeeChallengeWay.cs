using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡后是否挑战副本。
/// </summary>
public static class CoffeeChallengeWay
{
	/// <summary>全都挑战。</summary>
	public const string All = "全都挑战";

	/// <summary>只挑战体力计划。</summary>
	public const string OnlyPlan = "只挑战体力计划";

	/// <summary>不挑战。</summary>
	public const string None = "不挑战";

	/// <summary>
	/// 设置项。
	/// </summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("全都挑战", "全都挑战"),
		new ConfigItem("只挑战体力计划", "只挑战体力计划"),
		new ConfigItem("不挑战", "不挑战")
	};
}
