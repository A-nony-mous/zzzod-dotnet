namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 一条龙应用顺序和启用状态。
/// </summary>
public sealed record ZzzOneDragonAppUpdateDto(string AppId, bool Enabled);
