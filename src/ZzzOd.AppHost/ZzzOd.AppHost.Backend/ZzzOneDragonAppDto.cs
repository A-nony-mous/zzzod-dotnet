namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 一条龙应用行。
/// </summary>
public sealed record ZzzOneDragonAppDto(string AppId, string Name, bool Enabled, bool NeedNotify, bool NotifyVisible, bool SettingVisible, bool RunAvailable, string? LastRunTime, int? RunStatus, bool IsMigrated = false);
