using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都寻路方式。
/// </summary>
public static class WitheredDomainPathFinding
{
	public const string Default = "默认";

	public const string OnlyBoss = "速通";

	public const string Custom = "自定义";

	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("默认", "默认"),
		new ConfigItem("速通", "速通"),
		new ConfigItem("自定义", "自定义")
	};
}
