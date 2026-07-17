using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>锄大地页面目录。</summary>
public sealed record ZzzWorldPatrolCatalogDto(IReadOnlyList<ZzzWorldPatrolEntryDto> Entries, IReadOnlyList<ZzzWorldPatrolAreaDto> Areas, IReadOnlyList<ZzzWorldPatrolRouteDto> Routes, IReadOnlyList<ZzzWorldPatrolRouteListDto> RouteLists, IReadOnlyList<ZzzWorldPatrolTransportPointDto> TransportPoints, IReadOnlyList<string> AutoBattleConfigs, ZzzWorldPatrolRunRecordDto RunRecord);
