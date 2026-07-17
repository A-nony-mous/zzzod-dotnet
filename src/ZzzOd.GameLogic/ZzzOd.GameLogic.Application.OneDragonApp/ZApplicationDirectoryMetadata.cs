using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>
/// BaselineParity `zzz_od/application` 顶层目录元数据。
/// </summary>
public sealed record ZApplicationDirectoryMetadata(string DirectoryName, string DisplayName, IReadOnlyList<string> AppIds, bool DefaultGroup, bool NeedNotify);
