using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地路线列表。
/// </summary>
public sealed class WorldPatrolRouteList
{
	/// <summary>列表名称。</summary>
	[YamlMember(Alias = "name", ApplyNamingConventions = false)]
	public string Name { get; set; } = string.Empty;

	/// <summary>列表类型。</summary>
	[YamlMember(Alias = "list_type", ApplyNamingConventions = false)]
	public string ListType { get; set; } = "whitelist";

	/// <summary>路线 id 列表。</summary>
	[YamlMember(Alias = "route_items", ApplyNamingConventions = false)]
	public List<string> RouteItems { get; set; } = new List<string>();

	/// <summary>
	/// 添加路线。
	/// </summary>
	public void AddRoute(string routeFullId)
	{
		RouteItems.Add(routeFullId);
	}

	/// <summary>
	/// 移除路线。
	/// </summary>
	public void RemoveRoute(string routeFullId)
	{
		RouteItems.Remove(routeFullId);
	}

	/// <summary>
	/// 移动路线顺序。
	/// </summary>
	public void MoveRoute(int fromIndex, int toIndex)
	{
		if (fromIndex >= 0 && fromIndex < RouteItems.Count && toIndex >= 0 && toIndex < RouteItems.Count)
		{
			string item = RouteItems[fromIndex];
			RouteItems.RemoveAt(fromIndex);
			RouteItems.Insert(toIndex, item);
		}
	}
}
