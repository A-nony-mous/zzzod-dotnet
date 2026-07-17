using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡触发副本时使用的卡片数量。
/// </summary>
public static class CoffeeCardNum
{
	/// <summary>按游戏内默认数量。</summary>
	public const string Default = "默认数量";

	/// <summary>最少数量。</summary>
	public const string Num1 = "1";

	/// <summary>
	/// 设置项。
	/// </summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[2]
	{
		new ConfigItem("默认数量", "默认数量"),
		new ConfigItem("1", "1")
	};
}
