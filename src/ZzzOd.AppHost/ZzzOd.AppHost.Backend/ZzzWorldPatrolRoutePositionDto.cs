namespace ZzzOd.AppHost.Backend;

/// <summary>路线坐标。</summary>
public sealed record ZzzWorldPatrolRoutePositionDto(int X, int Y)
{
	/// <summary>本次真实截图提取的小地图道路图。</summary>
	public ZzzWorldPatrolRecorderImageDto? MiniMapRoad { get; init; }

	/// <summary>包含本次坐标的路线大地图。</summary>
	public ZzzWorldPatrolRecorderImageDto? RouteMap { get; init; }
}
