using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>
/// BaselineParity `zzz_od/application` 顶层目录元数据。
/// </summary>
/// <param name="DirectoryName">目录名。</param>
/// <param name="DisplayName">显示名称。</param>
/// <param name="AppIds">目录内应用标识列表。</param>
/// <param name="DefaultGroup">是否属于一条龙默认组。</param>
/// <param name="NeedNotify">是否需要通知。</param>
/// <param name="Priority">默认组排序优先级；为 null 表示无优先级，排序时垫后。</param>
public sealed record ZApplicationDirectoryMetadata(string DirectoryName, string DisplayName, IReadOnlyList<string> AppIds, bool DefaultGroup, bool NeedNotify, int? Priority = null);
