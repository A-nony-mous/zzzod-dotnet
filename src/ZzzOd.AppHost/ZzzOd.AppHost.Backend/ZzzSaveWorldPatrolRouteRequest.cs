using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>保存路线文件。</summary>
public sealed record ZzzSaveWorldPatrolRouteRequest(int InstanceIndex, string? OriginalFullId, string AreaId, int Index, string TransportPoint, IReadOnlyList<ZzzWorldPatrolOperationDto> Operations);
