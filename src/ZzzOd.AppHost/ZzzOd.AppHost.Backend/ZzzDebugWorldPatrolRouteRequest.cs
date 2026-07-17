namespace ZzzOd.AppHost.Backend;

/// <summary>调试单条路线。</summary>
public sealed record ZzzDebugWorldPatrolRouteRequest(int InstanceIndex, string GroupId, string FullId, int StartIndex);
