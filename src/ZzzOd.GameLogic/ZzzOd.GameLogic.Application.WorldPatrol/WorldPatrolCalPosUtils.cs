using System;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Matcher;
using OneDragon.Core.Utils;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地位置计算工具。
/// </summary>
public static class WorldPatrolCalPosUtils
{
	/// <summary>
	/// 计算小地图在大地图上的匹配位置。
	/// </summary>
	public static MatchResult? CalPos(Mat largeMap, Mat miniMap, WorldPatrolPoint? lastPosition = null)
	{
		ArgumentNullException.ThrowIfNull(largeMap, "largeMap");
		ArgumentNullException.ThrowIfNull(miniMap, "miniMap");
		OneDragon.Core.Abstractions.Geometry.Point? offset;
		using Mat source = CropAroundLastPosition(largeMap, miniMap, lastPosition, out offset);
		MatchResult max = CvImageUtils.MatchTemplate(source, miniMap, 0.1).Max;
		if (max != null && offset.HasValue)
		{
			max.AddOffset(offset.Value);
		}
		return max;
	}

	/// <summary>
	/// 根据 road mask 计算当前位置。
	/// </summary>
	public static WorldPatrolPoint? CalCurrentPosition(Mat largeMap, Mat miniMap, WorldPatrolPoint? lastPosition = null)
	{
		MatchResult matchResult = CalPos(largeMap, miniMap, lastPosition);
		return (matchResult == null) ? ((WorldPatrolPoint?)null) : new WorldPatrolPoint?(new WorldPatrolPoint(matchResult.Center.X, matchResult.Center.Y));
	}

	private static Mat CropAroundLastPosition(Mat largeMap, Mat miniMap, WorldPatrolPoint? lastPosition, out OneDragon.Core.Abstractions.Geometry.Point? offset)
	{
		if (!lastPosition.HasValue)
		{
			offset = null;
			return largeMap.Clone();
		}
		int num = Math.Max(0, lastPosition.Value.X - miniMap.Cols * 2);
		int num2 = Math.Max(0, lastPosition.Value.Y - miniMap.Rows * 2);
		int num3 = Math.Min(largeMap.Cols, lastPosition.Value.X + miniMap.Cols * 2);
		int num4 = Math.Min(largeMap.Rows, lastPosition.Value.Y + miniMap.Rows * 2);
		if (num3 - num < miniMap.Cols || num4 - num2 < miniMap.Rows)
		{
			offset = null;
			return largeMap.Clone();
		}
		offset = new OneDragon.Core.Abstractions.Geometry.Point(num, num2);
		return new Mat(largeMap, new OpenCvSharp.Rect(num, num2, num3 - num, num4 - num2)).Clone();
	}
}
