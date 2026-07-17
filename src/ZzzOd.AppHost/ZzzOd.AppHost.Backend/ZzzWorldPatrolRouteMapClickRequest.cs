using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>路线大地图点击坐标换算请求。</summary>
public sealed record ZzzWorldPatrolRouteMapClickRequest(string AreaId, string TransportPoint, IReadOnlyList<ZzzWorldPatrolOperationDto> Operations, double ClickX, double ClickY, double ViewportWidth, double ViewportHeight);
