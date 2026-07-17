using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>路线名单。</summary>
public sealed record ZzzWorldPatrolRouteListDto(string Name, string ListType, IReadOnlyList<string> RouteItems);
