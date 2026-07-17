using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 大地图图标。
/// </summary>
public sealed class WorldPatrolLargeMapIcon
{
	/// <summary>图标名称。</summary>
	[YamlMember(Alias = "icon_name", ApplyNamingConventions = false)]
	public string IconName { get; set; } = string.Empty;

	/// <summary>模板 id。</summary>
	[YamlMember(Alias = "template_id", ApplyNamingConventions = false)]
	public string TemplateId { get; set; } = string.Empty;

	/// <summary>大地图坐标。</summary>
	[YamlMember(Alias = "lm_pos", ApplyNamingConventions = false)]
	public List<int> LmPos { get; set; } = new List<int>();

	/// <summary>传送落地坐标，为空时使用大地图坐标。</summary>
	[YamlMember(Alias = "tp_pos", ApplyNamingConventions = false)]
	public List<int>? TpPos { get; set; }

	/// <summary>大地图坐标。</summary>
	[YamlIgnore]
	public WorldPatrolPoint LargeMapPosition => ToPoint(LmPos);

	/// <summary>传送落地坐标。</summary>
	[YamlIgnore]
	public WorldPatrolPoint TransportPosition
	{
		get
		{
			List<int> tpPos = TpPos;
			return (tpPos != null && tpPos.Count >= 2) ? ToPoint(TpPos) : LargeMapPosition;
		}
	}

	/// <summary>
	/// 创建图标。
	/// </summary>
	public static WorldPatrolLargeMapIcon Create(string iconName, string templateId, WorldPatrolPoint largeMapPosition, WorldPatrolPoint? transportPosition = null)
	{
		WorldPatrolPoint worldPatrolPoint = transportPosition ?? largeMapPosition;
		WorldPatrolLargeMapIcon obj = new WorldPatrolLargeMapIcon
		{
			IconName = iconName,
			TemplateId = templateId
		};
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = largeMapPosition.X;
		span[1] = largeMapPosition.Y;
		obj.LmPos = list;
		num = 2;
		List<int> list2 = new List<int>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<int> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = worldPatrolPoint.X;
		span2[1] = worldPatrolPoint.Y;
		obj.TpPos = list2;
		return obj;
	}

	private static WorldPatrolPoint ToPoint(IReadOnlyList<int> values)
	{
		return (values.Count >= 2) ? new WorldPatrolPoint(values[0], values[1]) : new WorldPatrolPoint(0, 0);
	}
}
