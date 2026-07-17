using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地路线操作。
/// </summary>
public sealed class WorldPatrolRouteOperation
{
	/// <summary>操作类型。</summary>
	[YamlMember(Alias = "op_type", ApplyNamingConventions = false)]
	public string OpType { get; set; } = string.Empty;

	/// <summary>操作数据。</summary>
	[YamlMember(Alias = "data", ApplyNamingConventions = false)]
	public List<string> Data { get; set; } = new List<string>();

	/// <summary>
	/// 创建移动操作。
	/// </summary>
	public static WorldPatrolRouteOperation MoveTo(WorldPatrolPoint point)
	{
		WorldPatrolRouteOperation obj = new WorldPatrolRouteOperation
		{
			OpType = "move"
		};
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = point.X.ToString(CultureInfo.InvariantCulture);
		span[1] = point.Y.ToString(CultureInfo.InvariantCulture);
		obj.Data = list;
		return obj;
	}
}
