using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 应用摘要。
/// </summary>
/// <param name="AppId">应用编号。</param>
/// <param name="Name">应用名称。</param>
/// <param name="DefaultGroup">是否默认组应用。</param>
/// <param name="NeedNotify">是否需要通知。</param>
/// <param name="RunAvailable">是否可启动。</param>
/// <param name="SupportsGroup">是否支持应用组。</param>
/// <param name="ConfigScopes">关联配置 scope。</param>
public sealed record ZzzAppDto(string AppId, string Name, bool DefaultGroup, bool NeedNotify, bool RunAvailable = true, bool SupportsGroup = true, IReadOnlyList<string>? ConfigScopes = null);
