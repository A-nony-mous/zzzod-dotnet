using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>大地图录制生产会话状态。</summary>
public sealed record ZzzWorldPatrolLargeMapRecorderStateDto(int InstanceIndex, string? AreaId, bool IsLoaded, bool HasLargeMap, int OverlapMode, ZzzWorldPatrolRoutePositionDto? CurrentPosition, ZzzWorldPatrolRoutePositionDto? CalculatedPosition, ZzzWorldPatrolRecorderImageDto? MiniMap1, ZzzWorldPatrolRecorderImageDto? MiniMap2, ZzzWorldPatrolRecorderImageDto? MiniMapMerged, ZzzWorldPatrolRecorderImageDto? LargeMap, IReadOnlyList<ZzzWorldPatrolLargeMapIconDto> Icons, int HighlightedIconIndex, string Status);
