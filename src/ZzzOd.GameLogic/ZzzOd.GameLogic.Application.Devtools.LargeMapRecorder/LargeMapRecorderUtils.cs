using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Matcher;
using OneDragon.Core.Template;
using OneDragon.Core.Utils;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 大地图记录纯逻辑工具。
/// </summary>
public static class LargeMapRecorderUtils
{
	/// <summary>
	/// 获取小地图圆形区域掩码。
	/// </summary>
	public static Mat GetMiniMapCircleMask(int diameter)
	{
		Mat mat = new Mat(diameter, diameter, MatType.CV_8UC1, Scalar.Black);
		int num = diameter / 2;
		Cv2.Circle(mat, new OpenCvSharp.Point(num, num), Math.Max(0, num - 7), Scalar.White, -1);
		Cv2.Circle(mat, new OpenCvSharp.Point(207, 189), 50, Scalar.Black, -1);
		return mat;
	}

	/// <summary>
	/// 仅保留小地图圆形区域。
	/// </summary>
	public static MiniMapSnapshot GetMiniMapInCircle(MiniMapSnapshot miniMap)
	{
		using Mat mat = GetMiniMapCircleMask(miniMap.RoadMask.Rows);
		Mat mat2 = new Mat();
		Cv2.BitwiseAnd(miniMap.RoadMask, mat, mat2);
		using Mat mask = ConnectionErase(mat2, 50, eraseWhite: false);
		mat2.Dispose();
		mat2 = ConnectionErase(mask, 50, eraseWhite: true);
		return new MiniMapSnapshot(mat2, miniMap.IconList.Select((MiniMapIcon icon) => icon with { }).ToArray());
	}

	/// <summary>
	/// 合并同一位置的多张小地图。
	/// </summary>
	public static MiniMapSnapshot MergeMiniMap(MiniMapSnapshot merge, MiniMapSnapshot newMiniMap)
	{
		Mat mat = new Mat();
		Cv2.BitwiseOr(merge.RoadMask, newMiniMap.RoadMask, mat);
		List<MiniMapIcon> list = new List<MiniMapIcon>();
		foreach (MiniMapIcon icon in newMiniMap.IconList.Concat(merge.IconList))
		{
			if (!list.Any((MiniMapIcon old) => old.TemplateId == icon.TemplateId && Distance(old.Position, icon.Position) <= 10.0))
			{
				list.Add(icon);
			}
		}
		return new MiniMapSnapshot(mat, list);
	}

	/// <summary>
	/// 从原始小地图创建快照，并按 BaselineParity 固定顺序扫描 map_icon_01 到 map_icon_99。
	/// </summary>
	public static MiniMapSnapshot CreateMiniMapSnapshot(TemplateMatcher templateMatcher, Mat rgb, Mat roadMask, double iconThreshold = 0.7)
	{
		ArgumentNullException.ThrowIfNull(templateMatcher, "templateMatcher");
		ArgumentNullException.ThrowIfNull(rgb, "rgb");
		ArgumentNullException.ThrowIfNull(roadMask, "roadMask");
		MatchResultList matchResultList = new MatchResultList(onlyBest: false);
		for (int i = 1; i < 100; i++)
		{
			string templateId = $"map_icon_{i:00}";
			TemplateInfo template = templateMatcher.TemplateLoader.GetTemplate("map", templateId);
			if (template == null)
			{
				break;
			}
			MatchResultList matchResultList2 = templateMatcher.MatchTemplate(rgb, "map", templateId, "raw", iconThreshold, null, ignoreTemplateMask: false, onlyBest: false);
			foreach (MatchResult item in matchResultList2.Items)
			{
				matchResultList.Append(new MatchResult(item.Confidence, item.X, item.Y, item.Width, item.Height, item.TemplateScale, template.TemplateId));
			}
		}
		MiniMapIcon[] iconList = matchResultList.Items.Select((MatchResult match) => new MiniMapIcon((string)match.Data, match.Center)).ToArray();
		return new MiniMapSnapshot(roadMask, iconList);
	}

	/// <summary>
	/// 渲染带模板图标的大地图。图标位置按中心点解释，模板掩码决定写入像素。
	/// </summary>
	public static Mat? GetLargeMapDisplay(TemplateLoader templateLoader, LargeMapSnapshot? largeMap)
	{
		ArgumentNullException.ThrowIfNull(templateLoader, "templateLoader");
		if ((object)largeMap == null)
		{
			return null;
		}
		Mat mat = new Mat();
		Cv2.CvtColor(largeMap.RoadMask, mat, ColorConversionCodes.GRAY2BGR);
		DrawIcons(templateLoader, mat, largeMap.IconList.Select((LargeMapIcon icon) => (TemplateId: icon.TemplateId, LargeMapPosition: icon.LargeMapPosition)));
		return mat;
	}

	/// <summary>
	/// 渲染带模板图标的小地图。图标位置按中心点解释，模板掩码决定写入像素。
	/// </summary>
	public static Mat GetMiniMapDisplay(TemplateLoader templateLoader, MiniMapSnapshot miniMap)
	{
		ArgumentNullException.ThrowIfNull(templateLoader, "templateLoader");
		ArgumentNullException.ThrowIfNull(miniMap, "miniMap");
		Mat mat = new Mat();
		Cv2.CvtColor(miniMap.RoadMask, mat, ColorConversionCodes.GRAY2BGR);
		DrawIcons(templateLoader, mat, miniMap.IconList.Select((MiniMapIcon icon) => (TemplateId: icon.TemplateId, Position: icon.Position)));
		return mat;
	}

	/// <summary>
	/// 合并小地图到大地图。
	/// </summary>
	public static LargeMapSnapshot MergeLargeMap(LargeMapSnapshot? largeMap, MiniMapSnapshot miniMap, MatchResult? position)
	{
		using MiniMapSnapshot miniMapSnapshot = GetMiniMapInCircle(miniMap);
		if ((object)largeMap == null)
		{
			return InitializeLargeMap(miniMapSnapshot);
		}
		if (position == null)
		{
			return largeMap.DeepClone();
		}
		using LargeMapSnapshot largeMap2 = MergeAtPosition(largeMap, miniMapSnapshot, position);
		return ExpandEdgesIfNeeded(largeMap2, miniMapSnapshot.RoadMask.Rows, miniMapSnapshot.RoadMask.Cols);
	}

	/// <summary>
	/// 通过图标计算小地图在大地图的位置。
	/// </summary>
	public static MatchResult? CalculatePositionByIcon(LargeMapSnapshot largeMap, MiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Point lastPosition)
	{
		int num = lastPosition.X - miniMap.RoadMask.Cols * 2;
		int num2 = lastPosition.X + miniMap.RoadMask.Cols * 2;
		int num3 = lastPosition.Y - miniMap.RoadMask.Rows * 2;
		int num4 = lastPosition.Y + miniMap.RoadMask.Rows * 2;
		List<MatchResult> list = new List<MatchResult>();
		foreach (LargeMapIcon icon in largeMap.IconList)
		{
			OneDragon.Core.Abstractions.Geometry.Point largeMapPosition = icon.LargeMapPosition;
			if (largeMapPosition.X < num || largeMapPosition.X > num2 || largeMapPosition.Y < num3 || largeMapPosition.Y > num4)
			{
				continue;
			}
			foreach (MiniMapIcon icon2 in miniMap.IconList)
			{
				if (!(icon2.TemplateId != icon.TemplateId))
				{
					OneDragon.Core.Abstractions.Geometry.Point newPoint = largeMapPosition - icon2.Position;
					MatchResult matchResult = list.FirstOrDefault((MatchResult match) => Distance(match.LeftTop, newPoint) < 10.0);
					if (matchResult != null)
					{
						matchResult.Confidence += 1.0;
					}
					else
					{
						list.Add(new MatchResult(1.0, newPoint.X, newPoint.Y, miniMap.RoadMask.Cols, miniMap.RoadMask.Rows));
					}
				}
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		double maxConfidence = list.Max((MatchResult match) => match.Confidence);
		List<MatchResult> list2 = list.Where((MatchResult match) => Math.Abs(match.Confidence - maxConfidence) < double.Epsilon).ToList();
		if (list2.Count == 1)
		{
			return list2[0];
		}
		using MiniMapSnapshot miniMapSnapshot = GetMiniMapInCircle(miniMap);
		foreach (MatchResult item in list2)
		{
			if (item.X < 0 || item.Y < 0 || item.X + miniMapSnapshot.RoadMask.Cols > largeMap.RoadMask.Cols || item.Y + miniMapSnapshot.RoadMask.Rows > largeMap.RoadMask.Rows)
			{
				item.Confidence = double.NegativeInfinity;
				continue;
			}
			using Mat mat = new Mat(largeMap.RoadMask, new OpenCvSharp.Rect(item.X, item.Y, miniMapSnapshot.RoadMask.Cols, miniMapSnapshot.RoadMask.Rows));
			using Mat mat2 = new Mat();
			Cv2.Absdiff(mat, miniMapSnapshot.RoadMask, mat2);
			item.Confidence = 0.0 - Cv2.Sum(mat2).Val0;
		}
		return list2.MaxBy((MatchResult match) => match.Confidence);
	}

	/// <summary>
	/// 按 BaselineParity cal_pos() 顺序定位，模板图标匹配失败后使用道路掩码匹配。
	/// </summary>
	public static MatchResult? CalculatePosition(LargeMapSnapshot largeMap, MiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Point lastPosition, bool useIcon)
	{
		ArgumentNullException.ThrowIfNull(largeMap, "largeMap");
		ArgumentNullException.ThrowIfNull(miniMap, "miniMap");
		MatchResult matchResult = (useIcon ? CalculatePositionByIcon(largeMap, miniMap, lastPosition) : null);
		return matchResult ?? CalculatePositionByRoad(largeMap.RoadMask, miniMap.RoadMask, lastPosition);
	}

	/// <summary>
	/// 根据道路掩码计算位置，搜索区域与 BaselineParity cal_pos_utils.cal_pos() 相同。
	/// </summary>
	public static MatchResult? CalculatePositionByRoad(Mat largeMap, Mat miniMap, OneDragon.Core.Abstractions.Geometry.Point lastPosition)
	{
		ArgumentNullException.ThrowIfNull(largeMap, "largeMap");
		ArgumentNullException.ThrowIfNull(miniMap, "miniMap");
		int num = Math.Max(0, lastPosition.X - miniMap.Cols * 2);
		int num2 = Math.Max(0, lastPosition.Y - miniMap.Rows * 2);
		int num3 = Math.Min(largeMap.Cols, lastPosition.X + miniMap.Cols * 2);
		int num4 = Math.Min(largeMap.Rows, lastPosition.Y + miniMap.Rows * 2);
		if (num3 - num < miniMap.Cols || num4 - num2 < miniMap.Rows)
		{
			return null;
		}
		using Mat source = new Mat(largeMap, new OpenCvSharp.Rect(num, num2, num3 - num, num4 - num2));
		MatchResult max = CvImageUtils.MatchTemplate(source, miniMap, 0.1).Max;
		max?.AddOffset(new OneDragon.Core.Abstractions.Geometry.Point(num, num2));
		return max;
	}

	private static LargeMapSnapshot InitializeLargeMap(MiniMapSnapshot miniMap)
	{
		int rows = miniMap.RoadMask.Rows;
		int cols = miniMap.RoadMask.Cols;
		Mat mat = new Mat(rows * 3, cols * 3, MatType.CV_8UC1, Scalar.Black);
		OpenCvSharp.Rect roi = new OpenCvSharp.Rect(cols, rows, cols, rows);
		miniMap.RoadMask.CopyTo(new Mat(mat, roi));
		OneDragon.Core.Abstractions.Geometry.Point offset = new OneDragon.Core.Abstractions.Geometry.Point(cols, rows);
		LargeMapIcon[] iconList = miniMap.IconList.Select((MiniMapIcon icon) => new LargeMapIcon(string.Empty, icon.TemplateId, icon.Position + offset)).ToArray();
		OneDragon.Core.Abstractions.Geometry.Point positionAfterMerge = offset + new OneDragon.Core.Abstractions.Geometry.Point(cols / 2, rows / 2);
		return new LargeMapSnapshot(string.Empty, mat, iconList, positionAfterMerge);
	}

	private static LargeMapSnapshot MergeAtPosition(LargeMapSnapshot largeMap, MiniMapSnapshot miniMap, MatchResult position, bool copyRoad = false)
	{
		Mat mat = largeMap.RoadMask.Clone();
		if (copyRoad)
		{
			using Mat mat2 = new Mat(mat, new OpenCvSharp.Rect(position.X, position.Y, miniMap.RoadMask.Cols, miniMap.RoadMask.Rows));
			Cv2.BitwiseOr(mat2, miniMap.RoadMask, mat2);
		}
		List<LargeMapIcon> list = largeMap.IconList.Select((LargeMapIcon largeMapIcon) => largeMapIcon with { }).ToList();
		OneDragon.Core.Abstractions.Geometry.Point point = new OneDragon.Core.Abstractions.Geometry.Point(position.X, position.Y);
		foreach (MiniMapIcon icon in miniMap.IconList)
		{
			OneDragon.Core.Abstractions.Geometry.Point newIconPosition = icon.Position + point;
			if (!list.Any((LargeMapIcon old) => old.TemplateId == icon.TemplateId && Distance(old.LargeMapPosition, newIconPosition) <= 10.0))
			{
				list.Add(new LargeMapIcon(string.Empty, icon.TemplateId, newIconPosition));
			}
		}
		return new LargeMapSnapshot(largeMap.AreaFullId, mat, list, position.Center);
	}

	private static LargeMapSnapshot ExpandEdgesIfNeeded(LargeMapSnapshot largeMap, int maskHeight, int maskWidth)
	{
		Mat roadMask = largeMap.RoadMask;
		int num = Math.Max(1, maskHeight / 2);
		int num2 = Math.Max(1, maskWidth / 2);
		int num3 = ((CountNonZero(roadMask, new OpenCvSharp.Rect(0, 0, roadMask.Cols, num)) > 0) ? maskHeight : 0);
		int num4 = ((CountNonZero(roadMask, new OpenCvSharp.Rect(0, roadMask.Rows - num, roadMask.Cols, num)) > 0) ? maskHeight : 0);
		int num5 = ((CountNonZero(roadMask, new OpenCvSharp.Rect(0, 0, num2, roadMask.Rows)) > 0) ? maskWidth : 0);
		int num6 = ((CountNonZero(roadMask, new OpenCvSharp.Rect(roadMask.Cols - num2, 0, num2, roadMask.Rows)) > 0) ? maskWidth : 0);
		if (num3 == 0 && num4 == 0 && num5 == 0 && num6 == 0)
		{
			return largeMap.DeepClone();
		}
		Mat mat = new Mat(roadMask.Rows + num3 + num4, roadMask.Cols + num5 + num6, MatType.CV_8UC1, Scalar.Black);
		roadMask.CopyTo(new Mat(mat, new OpenCvSharp.Rect(num5, num3, roadMask.Cols, roadMask.Rows)));
		OneDragon.Core.Abstractions.Geometry.Point offset = new OneDragon.Core.Abstractions.Geometry.Point(num5, num3);
		LargeMapIcon[] iconList = largeMap.IconList.Select((LargeMapIcon icon) => icon with
		{
			LargeMapPosition = icon.LargeMapPosition + offset,
			TeleportPosition = ((!icon.TeleportPosition.HasValue) ? ((OneDragon.Core.Abstractions.Geometry.Point?)null) : new OneDragon.Core.Abstractions.Geometry.Point?(icon.TeleportPosition.Value + offset))
		}).ToArray();
		return new LargeMapSnapshot(largeMap.AreaFullId, mat, iconList, largeMap.PositionAfterMerge + offset);
	}

	private static int CountNonZero(Mat source, OpenCvSharp.Rect roi)
	{
		using Mat mat = new Mat(source, roi);
		return Cv2.CountNonZero(mat);
	}

	private static void DrawIcons(TemplateLoader templateLoader, Mat display, IEnumerable<(string TemplateId, OneDragon.Core.Abstractions.Geometry.Point Position)> icons)
	{
		foreach (var icon in icons)
		{
			string item = icon.TemplateId;
			OneDragon.Core.Abstractions.Geometry.Point item2 = icon.Position;
			TemplateInfo template = templateLoader.GetTemplate("map", item);
			if (template != null)
			{
				if (template.Raw == null || template.Mask == null)
				{
					throw new InvalidOperationException("地图图标模板 " + item + " 缺少 raw.png 或 mask.png。");
				}
				int num = item2.X - template.Raw.Cols / 2;
				int num2 = item2.Y - template.Raw.Rows / 2;
				if (num < 0 || num2 < 0 || num + template.Raw.Cols > display.Cols || num2 + template.Raw.Rows > display.Rows)
				{
					throw new ArgumentOutOfRangeException("position", "地图图标模板 " + item + " 的 ROI 超出地图范围。");
				}
				using Mat m = new Mat(display, new OpenCvSharp.Rect(num, num2, template.Raw.Cols, template.Raw.Rows));
				template.Raw.CopyTo(m, template.Mask);
			}
		}
	}

	private static Mat ConnectionErase(Mat mask, int threshold, bool eraseWhite)
	{
		using Mat mat = CreateConnectionSource(mask, eraseWhite);
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		using Mat mat4 = new Mat();
		int num = Cv2.ConnectedComponentsWithStats(mat, mat2, mat3, mat4);
		Mat mat5 = mask.Clone();
		for (int i = 1; i < num; i++)
		{
			int num2 = mat3.At<int>(i, 4);
			if (num2 >= threshold)
			{
				continue;
			}
			byte value = (byte)((!eraseWhite) ? byte.MaxValue : 0);
			for (int j = 0; j < mat2.Rows; j++)
			{
				for (int k = 0; k < mat2.Cols; k++)
				{
					if (mat2.At<int>(j, k) == i)
					{
						mat5.Set(j, k, value);
					}
				}
			}
		}
		return mat5;
	}

	private static Mat CreateConnectionSource(Mat mask, bool eraseWhite)
	{
		if (eraseWhite)
		{
			return mask.Clone();
		}
		Mat mat = new Mat();
		Cv2.BitwiseNot(mask, mat);
		return mat;
	}

	private static double Distance(OneDragon.Core.Abstractions.Geometry.Point first, OneDragon.Core.Abstractions.Geometry.Point second)
	{
		int num = first.X - second.X;
		int num2 = first.Y - second.Y;
		return Math.Sqrt(num * num + num2 * num2);
	}
}
