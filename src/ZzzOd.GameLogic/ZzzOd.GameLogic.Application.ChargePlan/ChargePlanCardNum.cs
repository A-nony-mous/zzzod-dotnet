using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 实战模拟室卡片数量配置。
/// </summary>
public static class ChargePlanCardNum
{
	/// <summary>默认数量。</summary>
	public const string Default = "默认数量";

	/// <summary>1 张卡片。</summary>
	public const string Num1 = "1";

	/// <summary>2 张卡片。</summary>
	public const string Num2 = "2";

	/// <summary>3 张卡片。</summary>
	public const string Num3 = "3";

	/// <summary>4 张卡片。</summary>
	public const string Num4 = "4";

	/// <summary>5 张卡片。</summary>
	public const string Num5 = "5";

	/// <summary>
	/// 设置项。
	/// </summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[6]
	{
		new ConfigItem("默认数量", "默认数量"),
		new ConfigItem("1张卡片", "1"),
		new ConfigItem("2张卡片", "2"),
		new ConfigItem("3张卡片", "3"),
		new ConfigItem("4张卡片", "4"),
		new ConfigItem("5张卡片", "5")
	};
}
