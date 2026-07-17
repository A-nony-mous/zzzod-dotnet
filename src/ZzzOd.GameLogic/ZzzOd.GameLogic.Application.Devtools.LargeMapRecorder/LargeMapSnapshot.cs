using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 大地图快照。
/// </summary>
public sealed record LargeMapSnapshot(string AreaFullId, Mat RoadMask, IReadOnlyList<LargeMapIcon> IconList, OneDragon.Core.Abstractions.Geometry.Point PositionAfterMerge) : IDisposable
{
	/// <summary>
	/// 创建深拷贝。
	/// </summary>
	public LargeMapSnapshot DeepClone()
	{
		return new LargeMapSnapshot(AreaFullId, RoadMask.Clone(), IconList.Select((LargeMapIcon icon) => icon with { }).ToArray(), PositionAfterMerge);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		RoadMask.Dispose();
	}
}
