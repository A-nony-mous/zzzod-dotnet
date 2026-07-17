using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解等级。
/// </summary>
public static class DismantleLevel
{
	/// <summary>B 级。</summary>
	public const string LevelB = "B";

	/// <summary>A 级及以下。</summary>
	public const string LevelAAndBelow = "A及以下";

	/// <summary>S 级及以下。</summary>
	public const string LevelSAndBelow = "S及以下";

	/// <summary>等级设置项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("B", "B"),
		new ConfigItem("A及以下", "A及以下"),
		new ConfigItem("S及以下", "S及以下")
	};
}
