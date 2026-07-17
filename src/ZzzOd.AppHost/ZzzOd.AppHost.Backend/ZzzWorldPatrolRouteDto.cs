using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>真实路线。</summary>
public sealed record ZzzWorldPatrolRouteDto(string FullId, string EntryId, string AreaId, string AreaName, int Index, string TransportPoint, IReadOnlyList<ZzzWorldPatrolOperationDto> Operations, ZzzWorldPatrolRoutePositionDto? LastPosition)
{
	/// <summary>操作数量。</summary>
	public int OperationCount => Operations.Count;
}
