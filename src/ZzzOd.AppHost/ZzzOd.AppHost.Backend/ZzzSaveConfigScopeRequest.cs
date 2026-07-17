using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 保存配置 scope 请求。
/// </summary>
/// <param name="Scope">scope 名称。</param>
/// <param name="Values">待保存值。</param>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="GroupId">应用组编号。</param>
public sealed record ZzzSaveConfigScopeRequest(string Scope, IReadOnlyDictionary<string, object?> Values, int? InstanceIndex = null, string? GroupId = null);
