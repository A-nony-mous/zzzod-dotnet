using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>保存路线名单请求。</summary>
public sealed record ZzzSaveWorldPatrolRouteListRequest(int InstanceIndex, string Name, string ListType, IReadOnlyList<string> RouteItems);
