using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>路线录制可视化请求。</summary>
public sealed record ZzzWorldPatrolRouteVisualRequest(string AreaId, string TransportPoint, IReadOnlyList<ZzzWorldPatrolOperationDto> Operations);
