namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用 factory 元数据。
/// </summary>
public sealed record ZApplicationFactoryMetadata(string AppId, string AppName, string GroupId = "default", bool NeedNotify = false);
