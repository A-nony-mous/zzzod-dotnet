using System;
using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 恢复电量使用方式。
/// </summary>
public readonly record struct RestoreChargeMode(string DisplayName)
{
	/// <summary>不使用恢复电量。</summary>
	public static RestoreChargeMode None { get; } = new RestoreChargeMode("不使用");

	/// <summary>只使用储蓄电量。</summary>
	public static RestoreChargeMode BackupOnly { get; } = new RestoreChargeMode("使用储蓄电量");

	/// <summary>只使用以太电池。</summary>
	public static RestoreChargeMode EtherOnly { get; } = new RestoreChargeMode("使用以太电池");

	/// <summary>先使用储蓄电量，再使用以太电池。</summary>
	public static RestoreChargeMode Both { get; } = new RestoreChargeMode("同时使用储蓄电量和以太电池");

	/// <summary>
	/// 所有模式。
	/// </summary>
	public static IReadOnlyList<RestoreChargeMode> All { get; } = new RestoreChargeMode[4] { None, BackupOnly, EtherOnly, Both };

	/// <summary>
	/// 按中文配置值解析。
	/// </summary>
	public static RestoreChargeMode FromDisplayName(string? value)
	{
		RestoreChargeMode restoreChargeMode = All.FirstOrDefault((RestoreChargeMode mode) => string.Equals(mode.DisplayName, value, StringComparison.Ordinal));
		string displayName = restoreChargeMode.DisplayName;
		return (displayName != null && displayName.Length > 0) ? restoreChargeMode : None;
	}
}
