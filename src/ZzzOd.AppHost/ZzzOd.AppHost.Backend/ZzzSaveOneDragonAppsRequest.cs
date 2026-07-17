using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 保存一条龙应用列表请求。
/// </summary>
public sealed record ZzzSaveOneDragonAppsRequest(IReadOnlyList<ZzzOneDragonAppUpdateDto> Apps, int? InstanceIndex = null);
