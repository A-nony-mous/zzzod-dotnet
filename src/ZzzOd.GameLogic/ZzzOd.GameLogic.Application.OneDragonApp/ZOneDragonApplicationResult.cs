namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>一条龙运行中的单个应用结果。</summary>
public sealed record ZOneDragonApplicationResult(int InstanceIndex, string AppId, bool IsSuccess, string? Status);
