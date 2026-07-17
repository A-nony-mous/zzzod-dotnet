using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 小地图快照。
/// </summary>
public sealed record MiniMapSnapshot(Mat RoadMask, IReadOnlyList<MiniMapIcon> IconList) : IDisposable
{
	/// <summary>
	/// 创建深拷贝。
	/// </summary>
	public MiniMapSnapshot DeepClone()
	{
		return new MiniMapSnapshot(RoadMask.Clone(), IconList.Select((MiniMapIcon icon) => icon with { }).ToArray());
	}

	/// <inheritdoc />
	public void Dispose()
	{
		RoadMask.Dispose();
	}
}
