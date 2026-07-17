using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>从真实游戏截图定位当前路线位置。</summary>
public sealed record ZzzCaptureWorldPatrolRoutePositionRequest(string AreaId, string TransportPoint, IReadOnlyList<ZzzWorldPatrolOperationDto> Operations);
