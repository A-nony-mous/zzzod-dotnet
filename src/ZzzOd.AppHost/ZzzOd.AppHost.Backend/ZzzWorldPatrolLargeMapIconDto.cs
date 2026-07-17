namespace ZzzOd.AppHost.Backend;

/// <summary>大地图录制图标。</summary>
public sealed record ZzzWorldPatrolLargeMapIconDto(string IconName, string TemplateId, ZzzWorldPatrolRoutePositionDto LargeMapPosition, ZzzWorldPatrolRoutePositionDto TeleportPosition);
