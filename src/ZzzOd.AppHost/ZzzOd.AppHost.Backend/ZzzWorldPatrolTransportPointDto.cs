namespace ZzzOd.AppHost.Backend;

/// <summary>大地图中真实存在的传送点。</summary>
public sealed record ZzzWorldPatrolTransportPointDto(string AreaId, string Name, ZzzWorldPatrolRoutePositionDto Position);
