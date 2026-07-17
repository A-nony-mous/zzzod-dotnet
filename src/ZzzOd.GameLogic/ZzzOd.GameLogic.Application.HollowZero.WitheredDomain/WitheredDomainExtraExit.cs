using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 额外任务退出时机。
/// </summary>
public static class WitheredDomainExtraExit
{
	/// <summary>通关。</summary>
	public const string Complete = "通关";

	/// <summary>2层业绩后退出。</summary>
	public const string Level2Eva = "2层业绩后退出";

	/// <summary>3层业绩后退出。</summary>
	public const string Level3Eva = "3层业绩后退出";

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("通关", "通关"),
		new ConfigItem("2层业绩后退出", "2层业绩后退出"),
		new ConfigItem("3层业绩后退出", "3层业绩后退出")
	};
}
