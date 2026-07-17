using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地区域。
/// </summary>
public sealed class WorldPatrolArea
{
	/// <summary>所属入口。</summary>
	public WorldPatrolEntry Entry { get; }

	/// <summary>区域名称。</summary>
	public string AreaName { get; }

	/// <summary>区域 id。</summary>
	public string AreaId { get; }

	/// <summary>是否零号空洞区域。</summary>
	public bool IsHollow { get; }

	/// <summary>父区域。</summary>
	public WorldPatrolArea? ParentArea { get; set; }

	/// <summary>子区域列表。</summary>
	public List<WorldPatrolArea>? SubAreaList { get; set; }

	/// <summary>完整区域 id。</summary>
	public string FullId => (ParentArea == null) ? AreaId : (ParentArea.FullId + "_" + AreaId);

	/// <summary>完整区域名称。</summary>
	public string FullName => (ParentArea == null) ? AreaName : (ParentArea.FullName + "_" + AreaName);

	/// <summary>
	/// 初始化区域。
	/// </summary>
	public WorldPatrolArea(WorldPatrolEntry entry, string areaName, string areaId, bool isHollow = false)
	{
		Entry = entry;
		AreaName = areaName;
		AreaId = areaId;
		IsHollow = isHollow;
	}
}
