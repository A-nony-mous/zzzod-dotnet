using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地大地图。
/// </summary>
public sealed class WorldPatrolLargeMap : IDisposable
{
	/// <summary>区域完整 id。</summary>
	public string AreaFullId { get; }

	/// <summary>道路掩码图片路径。</summary>
	public string RoadMaskPath { get; }

	/// <summary>道路掩码。</summary>
	public Mat? RoadMask { get; }

	/// <summary>图标列表。</summary>
	public List<WorldPatrolLargeMapIcon> IconList { get; }

	/// <summary>
	/// 初始化大地图。
	/// </summary>
	public WorldPatrolLargeMap(string areaFullId, string roadMaskPath, IReadOnlyList<WorldPatrolLargeMapIcon>? iconList = null, Mat? roadMask = null)
	{
		AreaFullId = areaFullId;
		RoadMaskPath = roadMaskPath;
		IconList = iconList?.ToList() ?? new List<WorldPatrolLargeMapIcon>();
		RoadMask = roadMask;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		RoadMask?.Dispose();
	}
}
