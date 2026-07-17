using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class WorldPatrolAppTests
{
	private sealed class RecordingWorldPatrolFlow : IWorldPatrolAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, WorldPatrolConfig config, WorldPatrolRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			runRecord.SetRoutesPerRound(9);
			return Task.FromResult(new OperationResult(IsSuccess: true, "锄大地完成"));
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesWorldPatrolApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			WorldPatrolFactory worldPatrolFactory = zContext.ApplicationFactoryRegistry.CreateWorldPatrolFactory();
			IApplication application = worldPatrolFactory.CreateApplication(0, "default");
			IApplicationConfig config = worldPatrolFactory.GetConfig(0, "default");
			IApplicationRunRecord runRecord = worldPatrolFactory.GetRunRecord(0);
			Assert.Equal("world_patrol", worldPatrolFactory.AppId);
			Assert.Equal("锄大地", worldPatrolFactory.AppName);
			Assert.Equal("default", worldPatrolFactory.GroupId);
			Assert.True(worldPatrolFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<WorldPatrolApp>(application);
			WorldPatrolConfig worldPatrolConfig = Assert.IsType<WorldPatrolConfig>(config);
			Assert.Equal("全配队通用", worldPatrolConfig.AutoBattle);
			WorldPatrolRunRecord worldPatrolRunRecord = Assert.IsType<WorldPatrolRunRecord>(runRecord);
			Assert.Equal("world_patrol", worldPatrolRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersWorldPatrolAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterWorldPatrolApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("world_patrol"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("world_patrol"));
			Assert.Contains("world_patrol", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Config_LoadsPythonFieldsAndSettingMetadata()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "default");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "world_patrol.yml"), "auto_battle: 自定义战斗\nroute_list: 常用路线\nui_disappear_action: restart_and_retry\nui_disappear_seconds: 1234\nroute_retry_times: 3\nroute_retry_action: retry_on_stuck_again\ndaily_loop_count: 2\nloop_interval_seconds: 600");
			WorldPatrolConfig worldPatrolConfig = WorldPatrolConfig.Load(new OneDragonEnvironment(text), 0, "default");
			Assert.Equal("world_patrol", worldPatrolConfig.AppId);
			Assert.Equal(0, worldPatrolConfig.InstanceIndex);
			Assert.Equal("default", worldPatrolConfig.GroupId);
			Assert.Equal("自定义战斗", worldPatrolConfig.AutoBattle);
			Assert.Equal("常用路线", worldPatrolConfig.RouteList);
			Assert.Equal("restart_and_retry", worldPatrolConfig.UiDisappearAction);
			Assert.Equal(999, worldPatrolConfig.UiDisappearSeconds);
			Assert.Equal(3, worldPatrolConfig.RouteRetryTimes);
			Assert.Equal("retry_on_stuck_again", worldPatrolConfig.RouteRetryAction);
			Assert.Equal(2, worldPatrolConfig.DailyLoopCount);
			Assert.Equal(600, worldPatrolConfig.LoopIntervalSeconds);
			Assert.Equal("INTERFACE", "INTERFACE");
			Assert.Contains((IEnumerable<WorldPatrolSettingField>)WorldPatrolSettings.Fields, (Predicate<WorldPatrolSettingField>)((WorldPatrolSettingField field) => field.Key == "auto_battle" && field.DefaultValue.Equals("全配队通用")));
			Assert.Contains((IEnumerable<WorldPatrolSettingField>)WorldPatrolSettings.Fields, (Predicate<WorldPatrolSettingField>)((WorldPatrolSettingField field) => field.Key == "ui_disappear_action" && field.Options.Any((ConfigItem option) => object.Equals(option.Value, "restart_and_skip"))));
			Assert.Contains((IEnumerable<WorldPatrolSettingField>)WorldPatrolSettings.Fields, (Predicate<WorldPatrolSettingField>)((WorldPatrolSettingField field) => field.Key == "route_retry_action" && field.Options.Any((ConfigItem option) => object.Equals(option.Value, "retry_on_stuck_again"))));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_TracksFinishedRoundsAndRuntimeTiming()
	{
		string text = CreateTempRoot();
		try
		{
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
			WorldPatrolRunRecord worldPatrolRunRecord = WorldPatrolRunRecord.Load(new OneDragonEnvironment(text), 0, 4, () => now);
			worldPatrolRunRecord.SetRoutesPerRound(4);
			worldPatrolRunRecord.IncCompletedRounds();
			worldPatrolRunRecord.AddRecord("area_1");
			worldPatrolRunRecord.RoundStartTime = 100.0;
			worldPatrolRunRecord.RoundWaitSeconds = 12.0;
			worldPatrolRunRecord.RoundWaitStartTime = 120.0;
			worldPatrolRunRecord.ResetRoundTiming();
			Assert.Equal("world_patrol", worldPatrolRunRecord.AppId);
			Assert.Equal("20260706", worldPatrolRunRecord.Dt);
			Assert.Equal(4, worldPatrolRunRecord.RoutesPerRound);
			Assert.Equal(1, worldPatrolRunRecord.CompletedRounds);
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "area_1";
			Assert.Equal<List<string>>(list, worldPatrolRunRecord.Finished);
			Assert.Contains("area_1", (IEnumerable<string>)worldPatrolRunRecord.TimeCost.Keys);
			Assert.Null(worldPatrolRunRecord.RoundStartTime);
			Assert.Equal(0.0, worldPatrolRunRecord.RoundWaitSeconds);
			Assert.Null(worldPatrolRunRecord.RoundWaitStartTime);
			WorldPatrolRunRecord worldPatrolRunRecord2 = WorldPatrolRunRecord.Load(new OneDragonEnvironment(text), 0, 4, () => now);
			num = 1;
			List<string> list2 = new List<string>(num);
			CollectionsMarshal.SetCount(list2, num);
			CollectionsMarshal.AsSpan(list2)[0] = "area_1";
			Assert.Equal<List<string>>(list2, worldPatrolRunRecord2.Finished);
			Assert.Equal(1, worldPatrolRunRecord2.CompletedRounds);
			Assert.Equal(4, worldPatrolRunRecord2.RoutesPerRound);
			worldPatrolRunRecord2.ResetFinished();
			worldPatrolRunRecord2.ResetRecord();
			Assert.Empty(worldPatrolRunRecord2.Finished);
			Assert.Equal(0, worldPatrolRunRecord2.CompletedRounds);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Models_PreservePythonAreaRouteAndRouteListSemantics()
	{
		WorldPatrolEntry entry = new WorldPatrolEntry("六分街", "liufen_street");
		WorldPatrolArea worldPatrolArea = new WorldPatrolArea(entry, "主区域", "main", isHollow: true);
		WorldPatrolArea worldPatrolArea2 = new WorldPatrolArea(entry, "子区域", "sub", worldPatrolArea.IsHollow)
		{
			ParentArea = worldPatrolArea
		};
		int num = 1;
		List<WorldPatrolArea> list = new List<WorldPatrolArea>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = worldPatrolArea2;
		worldPatrolArea.SubAreaList = list;
		WorldPatrolLargeMapIcon worldPatrolLargeMapIcon = WorldPatrolLargeMapIcon.Create("传送点", "map_icon_01", new WorldPatrolPoint(10, 20));
		WorldPatrolRoute worldPatrolRoute = new WorldPatrolRoute(worldPatrolArea2, "传送点", 7);
		worldPatrolRoute.AddMoveOperation(new WorldPatrolPoint(30, 40));
		WorldPatrolRouteList worldPatrolRouteList = new WorldPatrolRouteList
		{
			Name = "常用",
			ListType = "whitelist"
		};
		worldPatrolRouteList.AddRoute(worldPatrolRoute.FullId);
		worldPatrolRouteList.AddRoute("other_1");
		worldPatrolRouteList.MoveRoute(1, 0);
		worldPatrolRouteList.RemoveRoute("other_1");
		Assert.Equal("main_sub", worldPatrolArea2.FullId);
		Assert.Equal("主区域_子区域", worldPatrolArea2.FullName);
		Assert.True(worldPatrolArea2.IsHollow);
		Assert.Equal(new WorldPatrolPoint(10, 20), worldPatrolLargeMapIcon.TransportPosition);
		Assert.Equal("main_sub_7", worldPatrolRoute.FullId);
		Assert.Equal("move", worldPatrolRoute.OpList[0].OpType);
		num = 2;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<string> span = CollectionsMarshal.AsSpan(list2);
		span[0] = "30";
		span[1] = "40";
		Assert.Equal<List<string>>(list2, worldPatrolRoute.OpList[0].Data);
		num = 1;
		List<string> list3 = new List<string>(num);
		CollectionsMarshal.SetCount(list3, num);
		CollectionsMarshal.AsSpan(list3)[0] = worldPatrolRoute.FullId;
		Assert.Equal<List<string>>(list3, worldPatrolRouteList.RouteItems);
	}

	[Fact]
	public void Service_LoadsAreasLargeMapsRoutesAndRouteLists()
	{
		string text = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(text);
			WorldPatrolService worldPatrolService = new WorldPatrolService(new OneDragonEnvironment(text));
			worldPatrolService.LoadData();
			Assert.Equal(new string[] { "city" }, worldPatrolService.EntryList.Select((WorldPatrolEntry entry) => entry.EntryId));
			WorldPatrolArea worldPatrolArea = Assert.Single(worldPatrolService.AreaList, (WorldPatrolArea area) => area.FullId == "sixth_street");
			WorldPatrolArea worldPatrolArea2 = Assert.Single(worldPatrolService.AreaList, (WorldPatrolArea area) => area.FullId == "sixth_street_coffee_shop");
			Assert.Equal<WorldPatrolArea>(worldPatrolArea, worldPatrolArea2.ParentArea);
			Assert.Equal(new WorldPatrolArea[2] { worldPatrolArea2, worldPatrolArea }, worldPatrolService.GetAreaListByEntry(worldPatrolService.EntryList[0]));
			WorldPatrolLargeMap worldPatrolLargeMap = Assert.Single(worldPatrolService.LargeMapList);
			Assert.Equal(worldPatrolArea2.FullId, worldPatrolLargeMap.AreaFullId);
			Assert.Equal(new WorldPatrolPoint(100, 200), worldPatrolLargeMap.IconList[0].LargeMapPosition);
			Assert.Equal(new WorldPatrolPoint(110, 220), worldPatrolLargeMap.IconList[0].TransportPosition);
			WorldPatrolRoute worldPatrolRoute = new WorldPatrolRoute(worldPatrolArea2, "咖啡店", worldPatrolService.GetNextRouteIdx(worldPatrolArea2));
			worldPatrolRoute.AddMoveOperation(new WorldPatrolPoint(300, 400));
			Assert.True(worldPatrolService.SaveWorldPatrolRoute(worldPatrolRoute));
			Assert.Equal(2, worldPatrolService.GetNextRouteIdx(worldPatrolArea2));
			WorldPatrolRoute worldPatrolRoute2 = Assert.Single(worldPatrolService.GetWorldPatrolRoutesByArea(worldPatrolArea2));
			Assert.Equal("sixth_street_coffee_shop_1", worldPatrolRoute2.FullId);
			Assert.Equal("咖啡店", worldPatrolRoute2.TpName);
			Assert.Equal(new WorldPatrolPoint(110, 220), worldPatrolService.GetRoutePosBeforeOpIdx(worldPatrolRoute2, 0));
			Assert.Equal(new WorldPatrolPoint(300, 400), worldPatrolService.GetRouteLastPos(worldPatrolRoute2));
			Assert.Equal("咖啡店", worldPatrolService.GetRouteTpIcon(worldPatrolRoute2).IconName);
			Assert.Single(worldPatrolService.GetWorldPatrolRoutes());
			WorldPatrolRouteList obj = new WorldPatrolRouteList
			{
				Name = "常用路线",
				ListType = "blacklist"
			};
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = worldPatrolRoute2.FullId;
			obj.RouteItems = list;
			WorldPatrolRouteList routeList = obj;
			Assert.True(worldPatrolService.SaveWorldPatrolRouteList(routeList));
			WorldPatrolRouteList worldPatrolRouteList = Assert.Single(worldPatrolService.GetWorldPatrolRouteLists());
			Assert.Equal("常用路线", worldPatrolRouteList.Name);
			Assert.Equal("blacklist", worldPatrolRouteList.ListType);
			num = 1;
			List<string> list2 = new List<string>(num);
			CollectionsMarshal.SetCount(list2, num);
			CollectionsMarshal.AsSpan(list2)[0] = worldPatrolRoute2.FullId;
			Assert.Equal<List<string>>(list2, worldPatrolRouteList.RouteItems);
			Assert.True(worldPatrolService.DeleteWorldPatrolRoute(worldPatrolRoute2));
			Assert.Empty(worldPatrolService.GetWorldPatrolRoutesByArea(worldPatrolArea2));
			Assert.True(worldPatrolService.DeleteWorldPatrolRouteList(worldPatrolRouteList));
			Assert.Empty(worldPatrolService.GetWorldPatrolRouteLists());
			Assert.True(worldPatrolService.DeleteWorldPatrolLargeMap(worldPatrolArea2));
			Assert.Empty(worldPatrolService.LargeMapList);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Service_SkipsMalformedRouteAndRouteListFiles()
	{
		string text = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(text);
			WorldPatrolService worldPatrolService = new WorldPatrolService(new OneDragonEnvironment(text));
			worldPatrolService.LoadData();
			WorldPatrolArea worldPatrolArea = Assert.Single(worldPatrolService.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute worldPatrolRoute = new WorldPatrolRoute(worldPatrolArea, "咖啡店", 1);
			worldPatrolRoute.AddMoveOperation(new WorldPatrolPoint(10, 20));
			Assert.True(worldPatrolService.SaveWorldPatrolRoute(worldPatrolRoute));
			File.WriteAllText(Path.Combine(worldPatrolService.AreaRouteDirectory(worldPatrolArea), "99.yml"), "op_list: [");
			WorldPatrolRouteList obj = new WorldPatrolRouteList
			{
				Name = "常用路线",
				ListType = "whitelist"
			};
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = worldPatrolRoute.FullId;
			obj.RouteItems = list;
			WorldPatrolRouteList worldPatrolRouteList = obj;
			Assert.True(worldPatrolService.SaveWorldPatrolRouteList(worldPatrolRouteList));
			File.WriteAllText(Path.Combine(worldPatrolService.RouteListDirectory(), "损坏.yml"), "route_items: [");
			WorldPatrolRoute worldPatrolRoute2 = Assert.Single(worldPatrolService.GetWorldPatrolRoutesByArea(worldPatrolArea));
			WorldPatrolRouteList worldPatrolRouteList2 = Assert.Single(worldPatrolService.GetWorldPatrolRouteLists());
			Assert.Equal(worldPatrolRoute.FullId, worldPatrolRoute2.FullId);
			Assert.Equal(worldPatrolRouteList.Name, worldPatrolRouteList2.Name);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Service_RejectsLargeMapWithoutRoadMask()
	{
		string text = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(text);
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			WorldPatrolService worldPatrolService = new WorldPatrolService(environment);
			worldPatrolService.LoadArea();
			WorldPatrolArea worldPatrolArea = Assert.Single(worldPatrolService.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			string text2 = WorldPatrolPaths.RoadMaskPath(environment, worldPatrolArea);
			File.Delete(text2);
			using WorldPatrolLargeMap largeMap = new WorldPatrolLargeMap(worldPatrolArea.FullId, text2, new WorldPatrolLargeMapIcon[] { WorldPatrolLargeMapIcon.Create("咖啡店", "map_icon_01", new WorldPatrolPoint(10, 20)) });
			bool condition = worldPatrolService.SaveWorldPatrolLargeMap(worldPatrolArea, largeMap);
			Assert.False(condition);
			Assert.False(File.Exists(text2));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ProductionAssetsAndRoutesFormACompleteExecutableContract()
	{
		string text = FindWorkspaceRoot();
		string text2 = CreateProductionWorldPatrolRunRoot(text);
		try
		{
			Assert.Empty(EnumerateConfigCaches(text));
			OneDragonEnvironment environment = new OneDragonEnvironment(text2);
			YamlOperator yamlOperator = new YamlOperator();
			WorldPatrolService worldPatrolService = new WorldPatrolService(environment, yamlOperator);
			List<string> list = new List<string>();
			try
			{
				worldPatrolService.LoadArea();
			}
			catch (Exception ex)
			{
				list.Add("assets/game_data/map_area_all.yml: 无法加载 " + ex.Message);
			}
			if (worldPatrolService.AreaList.Count == 0)
			{
				list.Add("assets/game_data/map_area_all.yml: 未加载到任何锄大地区域");
			}
			string text3 = Path.Combine(text2, "config", "world_patrol_route", "system");
			string[] array = (Directory.Exists(text3) ? Directory.EnumerateFiles(text3, "*.yml", SearchOption.AllDirectories).Order<string>(StringComparer.Ordinal).ToArray() : Array.Empty<string>());
			if (array.Length == 0)
			{
				list.Add("config/world_patrol_route/system: 未找到生产路线");
			}
			string[] array2 = array;
			foreach (string text4 in array2)
			{
				string relativePath = Path.GetRelativePath(text2, text4);
				WorldPatrolRoute route = null;
				try
				{
					route = yamlOperator.Load<WorldPatrolRoute>(text4);
				}
				catch (Exception ex2)
				{
					list.Add(relativePath + ": YAML 无法反序列化 " + ex2.Message);
					continue;
				}
				string relativePath2 = Path.GetRelativePath(text3, text4);
				char[] buffer = new char[2];
				buffer[0] = Path.DirectorySeparatorChar;
				buffer[1] = Path.AltDirectorySeparatorChar;
				string[] array3 = relativePath2.Split(buffer);
				if (array3.Length < 3)
				{
					list.Add(relativePath + ": 路径必须包含入口目录和区域目录");
					continue;
				}
				string entryId = array3[0];
				string areaFullId = array3[1];
				List<WorldPatrolArea> list2 = worldPatrolService.AreaList.Where((WorldPatrolArea worldPatrolArea) => string.Equals(worldPatrolArea.Entry.EntryId, entryId, StringComparison.Ordinal) && string.Equals(worldPatrolArea.FullId, areaFullId, StringComparison.Ordinal) && string.Equals(worldPatrolArea.FullId, route.TpAreaId, StringComparison.Ordinal)).ToList();
				if (list2.Count != 1)
				{
					list.Add($"{relativePath}: tp_area_id={route.TpAreaId} 在入口 {entryId}/区域目录 {areaFullId} 中匹配到 {list2.Count} 个区域");
					continue;
				}
				WorldPatrolArea area = list2[0];
				string text5 = WorldPatrolPaths.RoadMaskPath(environment, area);
				using Mat mat = (File.Exists(text5) ? Cv2.ImRead(text5, ImreadModes.Grayscale) : new Mat());
				if (mat.Empty())
				{
					list.Add(relativePath + ": road_mask.png 缺失或无法解码 " + Path.GetRelativePath(text2, text5));
					continue;
				}
				string text6 = WorldPatrolPaths.IconYamlPath(environment, area);
				List<WorldPatrolLargeMapIcon> source;
				try
				{
					source = (File.Exists(text6) ? yamlOperator.Load<List<WorldPatrolLargeMapIcon>>(text6) : new List<WorldPatrolLargeMapIcon>());
				}
				catch (Exception ex3)
				{
					list.Add(relativePath + ": icon.yml 无法加载 " + ex3.Message);
					goto end_IL_02de;
				}
				if (!File.Exists(text6))
				{
					list.Add(relativePath + ": 缺少 " + Path.GetRelativePath(text2, text6));
					continue;
				}
				List<WorldPatrolLargeMapIcon> list3 = source.Where((WorldPatrolLargeMapIcon icon) => !string.IsNullOrWhiteSpace(icon.IconName) && string.Equals(icon.IconName, route.TpName, StringComparison.Ordinal)).ToList();
				if (list3.Count != 1)
				{
					list.Add($"{relativePath}: tp_name={route.TpName} 匹配到 {list3.Count} 个非空 icon_name");
				}
				else
				{
					WorldPatrolLargeMapIcon worldPatrolLargeMapIcon = list3[0];
					ValidateIcon(relativePath, "lm_pos", worldPatrolLargeMapIcon.LmPos, mat, list);
					if (worldPatrolLargeMapIcon.TpPos != null)
					{
						ValidateIcon(relativePath, "tp_pos", worldPatrolLargeMapIcon.TpPos, mat, list);
					}
					if (string.IsNullOrWhiteSpace(worldPatrolLargeMapIcon.TemplateId))
					{
						list.Add(relativePath + ": 传送图标 " + worldPatrolLargeMapIcon.IconName + " 的 template_id 为空");
					}
					else
					{
						string[] buffer2 = new string[5];
						buffer2[0] = text;
						buffer2[1] = "assets";
						buffer2[2] = "template";
						buffer2[3] = "map";
						buffer2[4] = worldPatrolLargeMapIcon.TemplateId;
						string path = Path.Combine(buffer2);
						ValidateImage(relativePath, Path.Combine(path, "raw.png"), text, "template raw.png", list);
						string text7 = Path.Combine(path, "mask.png");
						if (File.Exists(text7))
						{
							ValidateImage(relativePath, text7, text, "template mask.png", list);
						}
					}
				}
				int num = 0;
				for (int num2 = 0; num2 < route.OpList.Count; num2++)
				{
					WorldPatrolRouteOperation worldPatrolRouteOperation = route.OpList[num2];
					if (!string.Equals(worldPatrolRouteOperation.OpType, "move", StringComparison.Ordinal))
					{
						list.Add($"{relativePath}: op_list[{num2}].op_type={worldPatrolRouteOperation.OpType} 不是受支持的 move");
						continue;
					}
					if (worldPatrolRouteOperation.Data.Count < 2 || !int.TryParse(worldPatrolRouteOperation.Data[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || !int.TryParse(worldPatrolRouteOperation.Data[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
					{
						list.Add($"{relativePath}: op_list[{num2}].data 不是有效二维整数坐标");
						continue;
					}
					num++;
					if (!PointInside(result, result2, mat))
					{
						list.Add($"{relativePath}: op_list[{num2}].data=({result},{result2}) 超出 road_mask {mat.Cols}x{mat.Rows}");
					}
				}
				if (num == 0)
				{
					list.Add(relativePath + ": 没有有效 move 操作");
				}
				end_IL_02de:;
			}
			Assert.True(list.Count == 0, string.Join(Environment.NewLine, list));
			Assert.Empty(EnumerateConfigCaches(text));
		}
		finally
		{
			Directory.Delete(text2, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			WorldPatrolConfig config = new WorldPatrolConfig();
			WorldPatrolRunRecord runRecord = new WorldPatrolRunRecord();
			RecordingWorldPatrolFlow flow = new RecordingWorldPatrolFlow();
			WorldPatrolApp app = new WorldPatrolApp(context, config, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("锄大地完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(9, runRecord.RoutesPerRound);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "zzzod-dotnet";
			buffer[2] = "src";
			buffer[3] = "ZzzOd.GameLogic";
			buffer[4] = "ZzzOd.GameLogic.csproj";
			if (File.Exists(Path.Combine(buffer)) && Directory.Exists(Path.Combine(directoryInfo.FullName, "config", "world_patrol_route", "system")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "assets", "game_data")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("无法从测试输出目录定位包含根 config 和 assets 的工作区目录");
	}

	private static string CreateProductionWorldPatrolRunRoot(string workspaceRoot)
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-world-patrol-production-contract", Guid.NewGuid().ToString("N"));
		string text2 = Path.Combine(text, "assets", "game_data");
		Directory.CreateDirectory(text2);
		File.Copy(Path.Combine(workspaceRoot, "assets", "game_data", "map_area_all.yml"), Path.Combine(text2, "map_area_all.yml"));
		CopyDirectory(Path.Combine(workspaceRoot, "assets", "game_data", "world_patrol"), Path.Combine(text2, "world_patrol"));
		CopyDirectory(Path.Combine(workspaceRoot, "config", "world_patrol_route"), Path.Combine(text, "config", "world_patrol_route"));
		return text;
	}

	private static void CopyDirectory(string sourceDirectory, string targetDirectory)
	{
		foreach (string item in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, item)));
		}
		Directory.CreateDirectory(targetDirectory);
		foreach (string item2 in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			string text = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, item2));
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item2, text);
		}
	}

	private static IEnumerable<string> EnumerateConfigCaches(string workspaceRoot)
	{
		return from path in Directory.EnumerateFiles(Path.Combine(workspaceRoot, "config"), "*", SearchOption.AllDirectories)
			where path.EndsWith(".yml_cache", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".pkl", StringComparison.OrdinalIgnoreCase)
			select path;
	}

	private static void ValidateIcon(string routePath, string fieldName, IReadOnlyList<int> coordinates, Mat roadMask, ICollection<string> errors)
	{
		if (coordinates.Count < 2)
		{
			errors.Add(routePath + ": " + fieldName + " 缺少二维坐标");
		}
		else if (!PointInside(coordinates[0], coordinates[1], roadMask))
		{
			errors.Add($"{routePath}: {fieldName}=({coordinates[0]},{coordinates[1]}) 超出 road_mask {roadMask.Cols}x{roadMask.Rows}");
		}
	}

	private static bool PointInside(int x, int y, Mat roadMask)
	{
		return x >= 0 && y >= 0 && x < roadMask.Cols && y < roadMask.Rows;
	}

	private static void ValidateImage(string routePath, string imagePath, string repositoryRoot, string fieldName, ICollection<string> errors)
	{
		using Mat mat = (File.Exists(imagePath) ? Cv2.ImRead(imagePath, ImreadModes.Unchanged) : new Mat());
		if (mat.Empty())
		{
			errors.Add($"{routePath}: {fieldName} 缺失或无法解码 {Path.GetRelativePath(repositoryRoot, imagePath)}");
		}
	}

	private static void WriteWorldPatrolAreaData(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "map_area_all.yml"), "full_list:\n  - entry_name: 城市\n    entry_id: city\n    area_list:\n      - area_name: 六分街\n        area_id: sixth_street\n        is_hollow: false\n        sub_area_list:\n          - area_name: 咖啡店\n            area_id: coffee_shop");
		string text2 = Path.Combine(text, "world_patrol", "city", "sixth_street_coffee_shop");
		Directory.CreateDirectory(text2);
		File.WriteAllBytes(Path.Combine(text2, "road_mask.png"), (ReadOnlySpan<byte>)new byte[3] { 1, 2, 3 });
		File.WriteAllText(Path.Combine(text2, "icon.yml"), "- icon_name: 咖啡店\n  template_id: map_icon_01\n  lm_pos:\n    - 100\n    - 200\n  tp_pos:\n    - 110\n    - 220");
	}
}
