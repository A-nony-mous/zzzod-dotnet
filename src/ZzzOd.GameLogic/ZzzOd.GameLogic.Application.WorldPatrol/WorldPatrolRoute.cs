using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地路线。
/// </summary>
public sealed class WorldPatrolRoute
{
	/// <summary>传送区域。</summary>
	[YamlIgnore]
	public WorldPatrolArea? TpArea { get; set; }

	/// <summary>传送区域 id。</summary>
	[YamlMember(Alias = "tp_area_id", ApplyNamingConventions = false)]
	public string TpAreaId { get; set; } = string.Empty;

	/// <summary>传送点名称。</summary>
	[YamlMember(Alias = "tp_name", ApplyNamingConventions = false)]
	public string TpName { get; set; } = string.Empty;

	/// <summary>路线编号。</summary>
	[YamlMember(Alias = "idx", ApplyNamingConventions = false)]
	public int Idx { get; set; }

	/// <summary>操作列表。</summary>
	[YamlMember(Alias = "op_list", ApplyNamingConventions = false)]
	public List<WorldPatrolRouteOperation> OpList { get; set; } = new List<WorldPatrolRouteOperation>();

	/// <summary>完整路线 id。</summary>
	[YamlIgnore]
	public string FullId => $"{TpArea?.FullId ?? TpAreaId}_{Idx}";

	/// <summary>
	/// 初始化 YAML 反序列化使用的路线。
	/// </summary>
	public WorldPatrolRoute()
	{
	}

	/// <summary>
	/// 初始化路线。
	/// </summary>
	public WorldPatrolRoute(WorldPatrolArea tpArea, string tpName, int idx = 0, IReadOnlyList<WorldPatrolRouteOperation>? opList = null)
	{
		TpArea = tpArea;
		TpAreaId = tpArea.FullId;
		TpName = tpName;
		Idx = idx;
		OpList = opList?.ToList() ?? new List<WorldPatrolRouteOperation>();
	}

	/// <summary>
	/// 添加移动操作。
	/// </summary>
	public void AddMoveOperation(WorldPatrolPoint point)
	{
		OpList.Add(WorldPatrolRouteOperation.MoveTo(point));
	}

	internal void AttachArea(WorldPatrolArea area)
	{
		TpArea = area;
		if (string.IsNullOrWhiteSpace(TpAreaId))
		{
			TpAreaId = area.FullId;
		}
	}
}
