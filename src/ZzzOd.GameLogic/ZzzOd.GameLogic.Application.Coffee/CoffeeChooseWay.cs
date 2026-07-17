using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡选择方式。
/// </summary>
public static class CoffeeChooseWay
{
	/// <summary>优先选择符合体力计划的咖啡。</summary>
	public const string PlanPriority = "优先体力计划";

	/// <summary>只选择汀曼特调。</summary>
	public const string TinmanOnly = "汀曼特调";

	/// <summary>只选择浓缩咖啡。</summary>
	public const string EspressoOnly = "浓缩咖啡";

	/// <summary>
	/// 设置项。
	/// </summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("优先体力计划", "优先体力计划"),
		new ConfigItem("汀曼特调", "汀曼特调"),
		new ConfigItem("浓缩咖啡", "浓缩咖啡")
	};
}
