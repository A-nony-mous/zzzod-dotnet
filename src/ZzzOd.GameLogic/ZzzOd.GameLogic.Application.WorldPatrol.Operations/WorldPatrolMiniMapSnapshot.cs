using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 小地图快照。
/// </summary>
public sealed record WorldPatrolMiniMapSnapshot(bool PlayMaskFound, double? ViewAngle, int Size = 120, Mat? RoadMask = null, Mat? Rgb = null);
