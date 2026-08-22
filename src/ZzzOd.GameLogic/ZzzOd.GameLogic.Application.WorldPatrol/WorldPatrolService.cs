using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Configuration;
using OneDragon.Core.Matcher;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using Serilog;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地路线和地图数据服务。
/// </summary>
public sealed class WorldPatrolService
{
	private sealed class MapAreaAllDocument
	{
		[YamlMember(Alias = "full_list", ApplyNamingConventions = false)]
		public List<WorldPatrolEntryDocument> FullList { get; set; } = new List<WorldPatrolEntryDocument>();
	}

	private sealed class WorldPatrolEntryDocument
	{
		[YamlMember(Alias = "entry_name", ApplyNamingConventions = false)]
		public string EntryName { get; set; } = string.Empty;

		[YamlMember(Alias = "entry_id", ApplyNamingConventions = false)]
		public string EntryId { get; set; } = string.Empty;

		[YamlMember(Alias = "area_list", ApplyNamingConventions = false)]
		public List<WorldPatrolAreaDocument> AreaList { get; set; } = new List<WorldPatrolAreaDocument>();
	}

	private sealed class WorldPatrolAreaDocument
	{
		[YamlMember(Alias = "area_name", ApplyNamingConventions = false)]
		public string AreaName { get; set; } = string.Empty;

		[YamlMember(Alias = "area_id", ApplyNamingConventions = false)]
		public string AreaId { get; set; } = string.Empty;

		[YamlMember(Alias = "is_hollow", ApplyNamingConventions = false)]
		public bool IsHollow { get; set; }

		[YamlMember(Alias = "sub_area_list", ApplyNamingConventions = false)]
		public List<WorldPatrolAreaDocument> SubAreaList { get; set; } = new List<WorldPatrolAreaDocument>();
	}

	private static readonly OneDragon.Core.Abstractions.Geometry.Point MiniMapDelta = new OneDragon.Core.Abstractions.Geometry.Point(169, 151);

	private readonly OneDragonEnvironment _environment;

	private readonly YamlOperator _yamlOperator;

	private OneDragon.Core.Abstractions.Geometry.Rect? _miniMapRect;

	private string? _miniMapScreenName;

	/// <summary>入口列表。</summary>
	public IReadOnlyList<WorldPatrolEntry> EntryList { get; private set; } = Array.Empty<WorldPatrolEntry>();

	/// <summary>区域列表。</summary>
	public IReadOnlyList<WorldPatrolArea> AreaList { get; private set; } = Array.Empty<WorldPatrolArea>();

	/// <summary>大地图列表。</summary>
	public IReadOnlyList<WorldPatrolLargeMap> LargeMapList { get; private set; } = Array.Empty<WorldPatrolLargeMap>();

	/// <summary>路线列表缓存。</summary>
	public IReadOnlyList<WorldPatrolRoute> RouteList { get; private set; } = Array.Empty<WorldPatrolRoute>();

	/// <summary>地图区域数据路径。</summary>
	public string MapAreaAllPath => Path.Combine(GameConst.GetGameDataPath(_environment), "map_area_all.yml");

	/// <summary>
	/// 初始化服务。
	/// </summary>
	public WorldPatrolService(OneDragonEnvironment environment, YamlOperator? yamlOperator = null)
	{
		_environment = environment;
		_yamlOperator = yamlOperator ?? new YamlOperator();
	}

	/// <summary>
	/// 加载区域和大地图数据。
	/// </summary>
	public void LoadData()
	{
		LoadArea();
		LoadAreaMap();
	}

	/// <summary>
	/// 加载地图区域。
	/// </summary>
	public void LoadArea()
	{
		EntryList = Array.Empty<WorldPatrolEntry>();
		AreaList = Array.Empty<WorldPatrolArea>();
		if (!File.Exists(MapAreaAllPath))
		{
			return;
		}
		MapAreaAllDocument mapAreaAllDocument = _yamlOperator.Load<MapAreaAllDocument>(MapAreaAllPath);
		List<WorldPatrolEntry> list = new List<WorldPatrolEntry>();
		List<WorldPatrolArea> list2 = new List<WorldPatrolArea>();
		foreach (WorldPatrolEntryDocument full in mapAreaAllDocument.FullList)
		{
			WorldPatrolEntry worldPatrolEntry = new WorldPatrolEntry(full.EntryName, full.EntryId);
			list.Add(worldPatrolEntry);
			foreach (WorldPatrolAreaDocument area in full.AreaList)
			{
				WorldPatrolArea worldPatrolArea = new WorldPatrolArea(worldPatrolEntry, area.AreaName, area.AreaId, area.IsHollow);
				if (area.SubAreaList.Count > 0)
				{
					worldPatrolArea.SubAreaList = new List<WorldPatrolArea>();
					foreach (WorldPatrolAreaDocument subArea in area.SubAreaList)
					{
						WorldPatrolArea item = new WorldPatrolArea(worldPatrolEntry, subArea.AreaName, subArea.AreaId, worldPatrolArea.IsHollow)
						{
							ParentArea = worldPatrolArea
						};
						worldPatrolArea.SubAreaList.Add(item);
						list2.Add(item);
					}
				}
				list2.Add(worldPatrolArea);
			}
		}
		EntryList = list;
		AreaList = list2;
	}

	/// <summary>
	/// 加载大地图图标数据。
	/// </summary>
	public void LoadAreaMap()
	{
		List<WorldPatrolLargeMap> list = new List<WorldPatrolLargeMap>();
		foreach (WorldPatrolArea area in AreaList)
		{
			string text = WorldPatrolPaths.RoadMaskPath(_environment, area);
			if (File.Exists(text))
			{
				using Mat mat = CvImageUtils.ReadImage(text, ImreadModes.Grayscale);
				string text2 = WorldPatrolPaths.IconYamlPath(_environment, area);
				List<WorldPatrolLargeMapIcon> iconList = (File.Exists(text2) ? _yamlOperator.Load<List<WorldPatrolLargeMapIcon>>(text2) : new List<WorldPatrolLargeMapIcon>());
				list.Add(new WorldPatrolLargeMap(area.FullId, text, iconList, mat?.Clone()));
			}
		}
		LargeMapList = list;
	}

	/// <summary>
	/// 按入口获取区域。
	/// </summary>
	public IReadOnlyList<WorldPatrolArea> GetAreaListByEntry(WorldPatrolEntry entry)
	{
		return AreaList.Where((WorldPatrolArea area) => string.Equals(area.Entry.EntryId, entry.EntryId, StringComparison.Ordinal)).ToList();
	}

	/// <summary>
	/// 根据完整区域 id 获取大地图。
	/// </summary>
	public WorldPatrolLargeMap? GetLargeMapByAreaFullId(string areaFullId)
	{
		return LargeMapList.FirstOrDefault((WorldPatrolLargeMap largeMap) => string.Equals(largeMap.AreaFullId, areaFullId, StringComparison.Ordinal));
	}

	/// <summary>
	/// 保存大地图图标数据。
	/// </summary>
	public bool SaveWorldPatrolLargeMap(WorldPatrolArea area, WorldPatrolLargeMap largeMap)
	{
		if (!string.Equals(area.FullId, largeMap.AreaFullId, StringComparison.Ordinal))
		{
			Log.Error("保存区域地图失败，区域 id 不一致 {AreaId} {LargeMapAreaId}", area.FullId, largeMap.AreaFullId);
			return false;
		}
		if (largeMap.RoadMask == null || largeMap.RoadMask.Empty())
		{
			Log.Error("保存区域地图失败，道路掩码为空 {AreaId}", area.FullId);
			return false;
		}
		Directory.CreateDirectory(WorldPatrolPaths.AreaDirectory(_environment, area));
		CvImageUtils.SaveImage(largeMap.RoadMask, WorldPatrolPaths.RoadMaskPath(_environment, area));
		_yamlOperator.Save(WorldPatrolPaths.IconYamlPath(_environment, area), largeMap.IconList);
		LoadAreaMap();
		Log.Information("保存区域地图成功 {AreaId}", area.FullId);
		return true;
	}

	/// <summary>
	/// 删除大地图图标和道路掩码。
	/// </summary>
	public bool DeleteWorldPatrolLargeMap(WorldPatrolArea area)
	{
		WorldPatrolLargeMap largeMapByAreaFullId = GetLargeMapByAreaFullId(area.FullId);
		if (largeMapByAreaFullId == null)
		{
			return false;
		}
		LargeMapList = LargeMapList.Where((WorldPatrolLargeMap largeMap) => !string.Equals(largeMap.AreaFullId, area.FullId, StringComparison.Ordinal)).ToList();
		DeleteIfExists(WorldPatrolPaths.RoadMaskPath(_environment, area));
		DeleteIfExists(WorldPatrolPaths.IconYamlPath(_environment, area));
		return true;
	}

	/// <summary>
	/// 获取所有路线。
	/// </summary>
	public IReadOnlyList<WorldPatrolRoute> GetWorldPatrolRoutes()
	{
		List<WorldPatrolRoute> list = new List<WorldPatrolRoute>();
		foreach (WorldPatrolArea area in AreaList)
		{
			list.AddRange(GetWorldPatrolRoutesByArea(area));
		}
		RouteList = list;
		return list;
	}

	/// <summary>
	/// 获取指定区域的所有路线。
	/// </summary>
	public IReadOnlyList<WorldPatrolRoute> GetWorldPatrolRoutesByArea(WorldPatrolArea area)
	{
		string path = AreaRouteDirectory(area);
		if (!Directory.Exists(path))
		{
			return Array.Empty<WorldPatrolRoute>();
		}
		List<WorldPatrolRoute> list = new List<WorldPatrolRoute>();
		foreach (string item in Directory.EnumerateFiles(path, "*.yml").OrderBy<string, string>((string result) => result, StringComparer.Ordinal))
		{
			try
			{
				WorldPatrolRoute worldPatrolRoute = _yamlOperator.Load<WorldPatrolRoute>(item);
				worldPatrolRoute.AttachArea(area);
				list.Add(worldPatrolRoute);
			}
			catch (Exception exception)
			{
				Log.Error(exception, "加载路线文件失败 {FileName}", Path.GetFileName(item));
			}
		}
		return list.OrderBy((WorldPatrolRoute route) => route.Idx).ToList();
	}

	/// <summary>
	/// 保存路线。
	/// </summary>
	public bool SaveWorldPatrolRoute(WorldPatrolRoute route)
	{
		if (route.TpArea == null)
		{
			return false;
		}
		route.TpAreaId = route.TpArea.FullId;
		string text = AreaRouteDirectory(route.TpArea);
		Directory.CreateDirectory(text);
		_yamlOperator.Save(Path.Combine(text, $"{route.Idx:00}.yml"), route);
		return true;
	}

	/// <summary>
	/// 获取指定区域的下一个路线编号。
	/// </summary>
	public int GetNextRouteIdx(WorldPatrolArea area)
	{
		IReadOnlyList<WorldPatrolRoute> worldPatrolRoutesByArea = GetWorldPatrolRoutesByArea(area);
		return (worldPatrolRoutesByArea.Count == 0) ? 1 : (worldPatrolRoutesByArea.Max((WorldPatrolRoute route) => route.Idx) + 1);
	}

	/// <summary>
	/// 删除路线。
	/// </summary>
	public bool DeleteWorldPatrolRoute(WorldPatrolRoute route)
	{
		if (route.TpArea == null)
		{
			return false;
		}
		string text = Path.Combine(AreaRouteDirectory(route.TpArea), $"{route.Idx:00}.yml");
		if (!File.Exists(text))
		{
			return false;
		}
		File.Delete(text);
		YamlOperator.InvalidateCache(text);
		return true;
	}

	/// <summary>
	/// 获取所有路线列表。
	/// </summary>
	public IReadOnlyList<WorldPatrolRouteList> GetWorldPatrolRouteLists()
	{
		string path = RouteListDirectory();
		if (!Directory.Exists(path))
		{
			return Array.Empty<WorldPatrolRouteList>();
		}
		List<WorldPatrolRouteList> list = new List<WorldPatrolRouteList>();
		foreach (string item in Directory.EnumerateFiles(path, "*.yml").OrderBy<string, string>((string result) => result, StringComparer.Ordinal))
		{
			try
			{
				list.Add(_yamlOperator.Load<WorldPatrolRouteList>(item));
			}
			catch (Exception exception)
			{
				Log.Error(exception, "加载路线列表失败 {FileName}", Path.GetFileName(item));
			}
		}
		return list;
	}

	/// <summary>
	/// 保存路线列表。
	/// </summary>
	public bool SaveWorldPatrolRouteList(WorldPatrolRouteList routeList)
	{
		Directory.CreateDirectory(RouteListDirectory());
		_yamlOperator.Save(Path.Combine(RouteListDirectory(), routeList.Name + ".yml"), routeList);
		return true;
	}

	/// <summary>
	/// 删除路线列表。
	/// </summary>
	public bool DeleteWorldPatrolRouteList(WorldPatrolRouteList routeList)
	{
		string text = Path.Combine(RouteListDirectory(), routeList.Name + ".yml");
		if (!File.Exists(text))
		{
			return false;
		}
		File.Delete(text);
		YamlOperator.InvalidateCache(text);
		return true;
	}

	/// <summary>
	/// 获取路线对应大地图。
	/// </summary>
	public WorldPatrolLargeMap? GetRouteLargeMap(WorldPatrolRoute route)
	{
		return (route.TpArea == null) ? null : GetLargeMapByAreaFullId(route.TpArea.FullId);
	}

	/// <summary>
	/// 获取路线传送点图标。
	/// </summary>
	public WorldPatrolLargeMapIcon? GetRouteTpIcon(WorldPatrolRoute route)
	{
		return GetRouteLargeMap(route)?.IconList.FirstOrDefault((WorldPatrolLargeMapIcon icon) => string.Equals(icon.IconName, route.TpName, StringComparison.Ordinal));
	}

	/// <summary>
	/// 裁剪当前小地图，优先使用动态定位，失败时使用静态区域。
	/// </summary>
	public WorldPatrolMiniMapSnapshot CutMiniMap(ZContext context, Mat? screen)
	{
		return (screen == null) ? new WorldPatrolMiniMapSnapshot(PlayMaskFound: false, null) : CutMiniMapFromScreen(context, screen);
	}

	/// <summary>
	/// 按 BaselineParity `cal_pos()` 顺序计算小地图在大地图上的位置。
	/// </summary>
	public WorldPatrolPoint? CalculateCurrentPosition(ZContext context, WorldPatrolLargeMap largeMap, WorldPatrolMiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Rect largeMapRect)
	{
		return CalculateCurrentPositionByIcon(context, largeMap, miniMap, largeMapRect) ?? CalculateCurrentPositionByRoad(largeMap, miniMap, largeMapRect);
	}

	/// <summary>
	/// 根据小地图上的图标优先定位。
	/// </summary>
	public WorldPatrolPoint? CalculateCurrentPositionByIcon(ZContext context, WorldPatrolLargeMap largeMap, WorldPatrolMiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Rect largeMapRect)
	{
		if (miniMap.Rgb == null || miniMap.RoadMask == null)
		{
			return null;
		}
		List<WorldPatrolLargeMapIcon> list = (from icon in largeMap.IconList
			where PointInside(icon.LargeMapPosition, largeMapRect)
			where !string.IsNullOrWhiteSpace(icon.TemplateId)
			select icon).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		List<(string, OneDragon.Core.Abstractions.Geometry.Point)> list2 = new List<(string, OneDragon.Core.Abstractions.Geometry.Point)>();
		foreach (string item3 in list.Select((WorldPatrolLargeMapIcon icon) => icon.TemplateId).Distinct<string>(StringComparer.Ordinal))
		{
			MatchResultList matchResultList = context.TemplateMatcher.MatchTemplate(
				miniMap.Rgb,
				"map",
				item3,
				"raw",
				0.7,
				null,
				ignoreTemplateMask: false,
				onlyBest: false,
				publishVision: false);
			foreach (MatchResult item4 in matchResultList.Items)
			{
				list2.Add((item3, item4.Center));
			}
		}
		if (list2.Count == 0)
		{
			return null;
		}
		List<MatchResult> list3 = new List<MatchResult>();
		foreach (WorldPatrolLargeMapIcon item5 in list)
		{
			foreach (var item6 in list2)
			{
				string item = item6.Item1;
				OneDragon.Core.Abstractions.Geometry.Point item2 = item6.Item2;
				if (string.Equals(item, item5.TemplateId, StringComparison.Ordinal))
				{
					OneDragon.Core.Abstractions.Geometry.Point point = ToCorePoint(item5.LargeMapPosition) - item2;
					MatchResult candidate = new MatchResult(1.0, point.X, point.Y, miniMap.RoadMask.Cols, miniMap.RoadMask.Rows);
					MatchResult matchResult = list3.FirstOrDefault((MatchResult matchResult3) => CalUtils.DistanceBetween(matchResult3.LeftTop, candidate.LeftTop) < 10.0);
					if (matchResult == null)
					{
						list3.Add(candidate);
					}
					else
					{
						matchResult.Confidence += 1.0;
					}
				}
			}
		}
		if (list3.Count == 0)
		{
			return null;
		}
		double maxConfidence = list3.Max((MatchResult matchResult3) => matchResult3.Confidence);
		List<MatchResult> list4 = list3.Where((MatchResult matchResult3) => Math.Abs(matchResult3.Confidence - maxConfidence) < double.Epsilon).ToList();
		MatchResult matchResult2 = ((list4.Count == 1) ? list4[0] : (SelectBestByRoadMask(largeMap, miniMap, list4) ?? list4[0]));
		return new WorldPatrolPoint(matchResult2.Center.X, matchResult2.Center.Y);
	}

	/// <summary>
	/// 根据道路 mask 兜底定位。
	/// </summary>
	public static WorldPatrolPoint? CalculateCurrentPositionByRoad(WorldPatrolLargeMap largeMap, WorldPatrolMiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Rect largeMapRect)
	{
		if (largeMap.RoadMask == null || miniMap.RoadMask == null)
		{
			return null;
		}
		OneDragon.Core.Abstractions.Geometry.Point offset;
			// 搜索窗钳制后小于模板时抛错，不按普通“本帧无坐标”处理。
		using Mat mat = CropWithOffset(largeMap.RoadMask, largeMapRect, miniMap.RoadMask.Width, miniMap.RoadMask.Height, out offset);
		MatchResultList matchResultList = CvImageUtils.MatchTemplate(mat, miniMap.RoadMask, 0.1);
		if (matchResultList.Max == null)
		{
			return null;
		}
		matchResultList.AddOffset(offset);
		return new WorldPatrolPoint(matchResultList.Max.Center.X, matchResultList.Max.Center.Y);
	}

	/// <summary>
	/// 获取指定操作前所在坐标。
	/// </summary>
	public WorldPatrolPoint? GetRoutePosBeforeOpIdx(WorldPatrolRoute route, int opIdx)
	{
		WorldPatrolLargeMapIcon routeTpIcon = GetRouteTpIcon(route);
		if (routeTpIcon == null)
		{
			return null;
		}
		WorldPatrolPoint value = routeTpIcon.TransportPosition;
		for (int i = 0; i < route.OpList.Count; i++)
		{
			if (i >= opIdx)
			{
				return value;
			}
			WorldPatrolRouteOperation worldPatrolRouteOperation = route.OpList[i];
			if (string.Equals(worldPatrolRouteOperation.OpType, "move", StringComparison.Ordinal) && worldPatrolRouteOperation.Data.Count >= 2 && int.TryParse(worldPatrolRouteOperation.Data[0], out var result) && int.TryParse(worldPatrolRouteOperation.Data[1], out var result2))
			{
				value = new WorldPatrolPoint(result, result2);
			}
		}
		return value;
	}

	private static MatchResult? SelectBestByRoadMask(WorldPatrolLargeMap largeMap, WorldPatrolMiniMapSnapshot miniMap, IReadOnlyList<MatchResult> candidates)
	{
		if (largeMap.RoadMask == null || miniMap.RoadMask == null)
		{
			return null;
		}
		MatchResult result = null;
		double num = double.MinValue;
		foreach (MatchResult candidate in candidates)
		{
			OneDragon.Core.Abstractions.Geometry.Point offset;
				// 候选区域越界时抛错，不静默跳过该候选。
			using Mat mat = CropWithOffset(rect: new OneDragon.Core.Abstractions.Geometry.Rect(candidate.X, candidate.Y, candidate.X + miniMap.RoadMask.Cols, candidate.Y + miniMap.RoadMask.Rows), source: largeMap.RoadMask, minWidth: miniMap.RoadMask.Width, minHeight: miniMap.RoadMask.Height, offset: out offset);
			using Mat mat2 = new Mat();
			Cv2.BitwiseAnd(mat, miniMap.RoadMask, mat2);
			double num2 = Cv2.CountNonZero(mat2);
			if (!(num2 <= num))
			{
				result = candidate;
				num = num2;
			}
		}
		return result;
	}

	/// <summary>
	/// 按大地图边界钳制裁剪区并返回裁剪结果。
	/// </summary>
	/// <remarks>
	/// 钳制后的裁剪区小于模板时抛错。道路匹配的源图小于模板，或图标候选越界后
	/// 与道路掩码尺寸不一致，都会进入“轮内异常 → 状态 <c>异常</c> 重试 → 额度耗尽节点失败”，
	/// 不会被当成普通“本帧无坐标”而静默进入定位失败重启阶梯。
	/// </remarks>
	/// <exception cref="InvalidOperationException">钳制后的裁剪区小于模板尺寸。</exception>
	private static Mat CropWithOffset(Mat source, OneDragon.Core.Abstractions.Geometry.Rect rect, int minWidth, int minHeight, out OneDragon.Core.Abstractions.Geometry.Point offset)
	{
		int num = Math.Max(0, rect.X1);
		int num2 = Math.Max(0, rect.Y1);
		int num3 = Math.Min(source.Cols, rect.X2);
		int num4 = Math.Min(source.Rows, rect.Y2);
		if (num3 - num < minWidth || num4 - num2 < minHeight)
		{
			throw new InvalidOperationException(
				$"裁剪区超出大地图边界：请求 [{rect.X1},{rect.Y1},{rect.X2},{rect.Y2}]，" +
				$"大地图 {source.Cols}x{source.Rows}，钳制后 {num3 - num}x{num4 - num2}，模板 {minWidth}x{minHeight}");
		}
		offset = new OneDragon.Core.Abstractions.Geometry.Point(num, num2);
		return new Mat(source, new OpenCvSharp.Rect(num, num2, num3 - num, num4 - num2)).Clone();
	}

	private static bool PointInside(WorldPatrolPoint point, OneDragon.Core.Abstractions.Geometry.Rect rect)
	{
		return point.X >= rect.X1 && point.X <= rect.X2 && point.Y >= rect.Y1 && point.Y <= rect.Y2;
	}

	private static OneDragon.Core.Abstractions.Geometry.Point ToCorePoint(WorldPatrolPoint point)
	{
		return new OneDragon.Core.Abstractions.Geometry.Point(point.X, point.Y);
	}

	private WorldPatrolMiniMapSnapshot CutMiniMapFromScreen(ZContext context, Mat screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("大世界", "小地图");
		if (area == null)
		{
			return BuildMiniMapSnapshot(screen);
		}
		string currentScreenName = context.ScreenContext.CurrentScreenName;
		bool flag = ((currentScreenName == "大世界-普通" || currentScreenName == "大世界-勘域") ? true : false);
		string text = (flag ? currentScreenName : null);
		if (text != null)
		{
			OneDragon.Core.Abstractions.Geometry.Rect? miniMapRect = _miniMapRect;
			if (miniMapRect.HasValue && string.Equals(_miniMapScreenName, text, StringComparison.Ordinal))
			{
				context.Logger.Debug("[小地图] 复用动态裁剪区域 {Rect} {ScreenName}", _miniMapRect.Value, text);
				return BuildMiniMapSnapshot(CvImageUtils.Crop(screen, _miniMapRect.Value));
			}
		}
		MatchResult matchResult = ScreenUtils.FindTemplateCoordInArea(context, screen, "大世界", "地图");
		if (matchResult != null)
		{
			OneDragon.Core.Abstractions.Geometry.Rect rect = new OneDragon.Core.Abstractions.Geometry.Rect(matchResult.X - MiniMapDelta.X, matchResult.Y - MiniMapDelta.Y, matchResult.X - MiniMapDelta.X + area.Width, matchResult.Y - MiniMapDelta.Y + area.Height);
			WorldPatrolMiniMapSnapshot worldPatrolMiniMapSnapshot = BuildMiniMapSnapshot(CvImageUtils.Crop(screen, rect));
			if (worldPatrolMiniMapSnapshot.PlayMaskFound && text != null)
			{
				_miniMapRect = rect;
				_miniMapScreenName = text;
			}
			context.Logger.Information("[小地图] 动态匹配小地图坐标 ({X1}, {Y1}) - ({X2}, {Y2}) 玩家标记={PlayerMaskFound}", rect.X1, rect.Y1, rect.X2, rect.Y2, worldPatrolMiniMapSnapshot.PlayMaskFound);
			return worldPatrolMiniMapSnapshot;
		}
		context.Logger.Debug("[小地图] 地图按钮模板未命中，使用静态裁剪区域 {Rect}", area.Rect);
		return BuildMiniMapSnapshot(CvImageUtils.Crop(screen, area.Rect));
	}

	private static WorldPatrolMiniMapSnapshot BuildMiniMapSnapshot(Mat croppedBgr)
	{
		using (croppedBgr)
		{
			using Mat rgb = WorldPatrolMiniMapWrapper.ConvertBgrToRgb(croppedBgr);
			using WorldPatrolMiniMapWrapper worldPatrolMiniMapWrapper = new WorldPatrolMiniMapWrapper(rgb);
			return worldPatrolMiniMapWrapper.ToSnapshot();
		}
	}

	/// <summary>
	/// 获取路线最后坐标。
	/// </summary>
	public WorldPatrolPoint? GetRouteLastPos(WorldPatrolRoute route)
	{
		return GetRoutePosBeforeOpIdx(route, route.OpList.Count + 1);
	}

	/// <summary>
	/// 区域路线目录。
	/// </summary>
	public string AreaRouteDirectory(WorldPatrolArea area)
	{
		return _environment.GetPathUnderWorkDir("config", "world_patrol_route", "system", area.Entry.EntryId, area.FullId);
	}

	/// <summary>
	/// 路线列表目录。
	/// </summary>
	public string RouteListDirectory()
	{
		return _environment.GetPathUnderWorkDir("config", "world_patrol_route_list");
	}

	private static void DeleteIfExists(string filePath)
	{
		if (File.Exists(filePath))
		{
			File.Delete(filePath);
			YamlOperator.InvalidateCache(filePath);
		}
	}
}
