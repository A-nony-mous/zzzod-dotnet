using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Context;
using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class WorldPatrolAppSettingPageTests
{
	private sealed class BackendSession : IDisposable
	{
		private readonly ZzzRuntimeManager _runtime;

		private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

		private readonly ZzzLogFanOutLoggerProvider _logProvider;

		public ZzzAppBackend Backend { get; }

		public BackendSession(string runRoot)
		{
			_runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance, (int instanceIndex) => new ZContext(new OneDragonEnvironment(runRoot), null, instanceIndex));
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			_battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			_logProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(runRoot), eventBus);
			Backend = new ZzzAppBackend(_runtime, eventBus, _battleAssistantRuntimeSource, _logProvider, new ZzzHostModeOptions(ZzzHostMode.Gui), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
		}

		public void Dispose()
		{
			_runtime.Dispose();
			_battleAssistantRuntimeSource.Dispose();
			_logProvider.Dispose();
		}
	}

	[Fact]
	public void PageUsesAxamlForAllFourPythonWorldPatrolSettingSlices()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzWorldPatrolAppSettingPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzWorldPatrolAppSettingPage.axaml.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "ZzzWorldPatrolLargeMapIconEditorWindow.axaml"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "ZzzWorldPatrolLargeMapIconEditorWindow.axaml.cs"));
		string actualString4 = File.ReadAllText(Path.Combine(path, "ZzzWorldPatrolImageViewer.axaml"));
		string actualString5 = File.ReadAllText(Path.Combine(path, "ZzzWorldPatrolImageViewer.axaml.cs"));
		AssertOrder(text, "锄大地配置", "路线列表", "大地图录制", "锄地路线录制");
		AssertOrder(text, "自动战斗", "界面消失预警时间", "单条路线重试上限", "锄地每日循环次数", "运行记录", "路线名单", "界面消失处理方式", "路线重试处理方式", "每轮最少占用时长（敌人刷新时间）");
		Assert.Contains("fa:TabView", text, StringComparison.Ordinal);
		Assert.Contains("fa:SettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:CommandBar", text, StringComparison.Ordinal);
		Assert.Contains("当前区域可用路线", text, StringComparison.Ordinal);
		Assert.Contains("GetWorldPatrolCatalog", actualString, StringComparison.Ordinal);
		Assert.Contains("ResetWorldPatrolRunRecord", actualString, StringComparison.Ordinal);
		Assert.Contains("SaveWorldPatrolRoute", actualString, StringComparison.Ordinal);
		Assert.Contains("DeleteWorldPatrolRoute", actualString, StringComparison.Ordinal);
		Assert.Contains("DebugWorldPatrolRouteAsync", actualString, StringComparison.Ordinal);
		Assert.Contains("CaptureWorldPatrolRoutePosition", actualString, StringComparison.Ordinal);
		Assert.Contains("LoadWorldPatrolLargeMapRecorder", actualString, StringComparison.Ordinal);
		Assert.Contains("CaptureWorldPatrolLargeMapRecorderAsync", actualString, StringComparison.Ordinal);
		Assert.Contains("CalculateWorldPatrolLargeMapRecorderPosition", actualString, StringComparison.Ordinal);
		Assert.Contains("ToggleWorldPatrolLargeMapRecorderOverlap", actualString, StringComparison.Ordinal);
		Assert.Contains("MergeWorldPatrolLargeMapRecorder", actualString, StringComparison.Ordinal);
		Assert.Contains("UndoWorldPatrolLargeMapRecorder", actualString, StringComparison.Ordinal);
		Assert.Contains("UpdateWorldPatrolLargeMapRecorderIcons", actualString, StringComparison.Ordinal);
		Assert.Contains("RenderWorldPatrolRouteRecorder", actualString, StringComparison.Ordinal);
		Assert.Contains("ConvertWorldPatrolRouteRecorderClick", actualString, StringComparison.Ordinal);
		Assert.Contains("_appliedLargeMapIconThreshold", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzLogDisplayCard(_backend)", actualString, StringComparison.Ordinal);
		Assert.Contains("ZzzWorldPatrolLargeMapIconEditorWindow", actualString, StringComparison.Ordinal);
		Assert.Contains("ScreenshotHelperGlobalInputSource.Subscribe", actualString, StringComparison.Ordinal);
		Assert.Contains("\"1\" => () => _ = CaptureLargeMapAsync()", actualString, StringComparison.Ordinal);
		Assert.Contains("\"2\" => CalculateLargeMapPosition", actualString, StringComparison.Ordinal);
		Assert.Contains("\"3\" => ToggleLargeMapOverlap", actualString, StringComparison.Ordinal);
		Assert.Contains("\"4\" => MergeLargeMap", actualString, StringComparison.Ordinal);
		Assert.Contains("\"1\" => CaptureAndAppendMove", actualString, StringComparison.Ordinal);
		Assert.Contains("\"2\" => null", actualString, StringComparison.Ordinal);
		Assert.Contains("\"3\" => null", actualString, StringComparison.Ordinal);
		Assert.Contains("\"4\" =>", actualString, StringComparison.Ordinal);
		Assert.Contains("\"5\" => UndoLastOperation", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"RouteOperationList\"", text, StringComparison.Ordinal);
		Assert.Contains("Content=\"截图(1)\"", text, StringComparison.Ordinal);
		Assert.Contains("Content=\"添加移动(4)\"", text, StringComparison.Ordinal);
		Assert.Contains("Content=\"回退(5)\"", text, StringComparison.Ordinal);
		Assert.Contains("Label=\"定位(2)\"", text, StringComparison.Ordinal);
		Assert.Contains("Label=\"重叠(3)\"", text, StringComparison.Ordinal);
		Assert.Contains("Label=\"合并(4)\"", text, StringComparison.Ordinal);
		Assert.Contains("Label=\"编辑图标\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"LargeMapMiniMap1Image\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"LargeMapMiniMap2Image\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"LargeMapMiniMapMergedImage\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"LargeMapViewer\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"RouteMiniMapRoadImage\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"RouteMapViewer\"", text, StringComparison.Ordinal);
		Assert.Contains("Content=\"点击后自动追加移动\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"LargeMapLogHost\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"RouteRecorderLogHost\"", text, StringComparison.Ordinal);
		Assert.Contains("Label=\"添加操作\"", text, StringComparison.Ordinal);
		Assert.Contains("Label=\"删除选中\"", text, StringComparison.Ordinal);
		Assert.Contains("PrimaryButtonText=\"保存\"", text, StringComparison.Ordinal);
		Assert.Contains("CloseButtonText=\"取消\"", text, StringComparison.Ordinal);
		Assert.Contains("PrimaryButtonClick=\"OnSaveRouteOperationsClicked\"", text, StringComparison.Ordinal);
		Assert.Contains("_routeOperationDraft = CloneOperations(_routeOperations)", actualString, StringComparison.Ordinal);
		Assert.Contains("double.TryParse(operation.Data1", actualString, StringComparison.Ordinal);
		Assert.Contains("args.Cancel = true", actualString, StringComparison.Ordinal);
		Assert.Contains("确定要删除第{index}个操作吗？", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Class=\"ZzzOd.Gui.Pages.ApplicationSettings.ZzzWorldPatrolLargeMapIconEditorWindow\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("Show()", actualString, StringComparison.Ordinal);
		Assert.Contains("Activate()", actualString, StringComparison.Ordinal);
		Assert.Contains("Saved", actualString3, StringComparison.Ordinal);
		Assert.Contains("x:Class=\"ZzzOd.Gui.Pages.ApplicationSettings.ZzzWorldPatrolImageViewer\"", actualString4, StringComparison.Ordinal);
		Assert.Contains("StrokeDashArray=\"4,2\"", actualString4, StringComparison.Ordinal);
		Assert.Contains("Label=\"适应窗口\"", actualString4, StringComparison.Ordinal);
		Assert.Contains("SetScaleFactor(scale", actualString5, StringComparison.Ordinal);
		Assert.Contains("previousOffset", actualString5, StringComparison.Ordinal);
		Assert.Contains("PointClicked?.Invoke", actualString5, StringComparison.Ordinal);
		Assert.DoesNotContain("六分街 01", text, StringComparison.Ordinal);
		Assert.DoesNotContain("默认白名单", text, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
	}

	[Fact]
	public void RouteRecorderHotkeysAppendUndoAndPersistRealMoveOperations()
	{
		string text = CreateRunRoot();
		try
		{
			BackendSession session = new BackendSession(text);
			try
			{
				string fullId = Assert.Single(session.Backend.GetWorldPatrolCatalog(2).Value.Routes).FullId;
				GuiParityAndFacadeTests.RunOnUiThread(delegate
				{
					ZzzWorldPatrolAppSettingPage zzzWorldPatrolAppSettingPage = new ZzzWorldPatrolAppSettingPage(session.Backend, session.Backend, 2, "daily");
					zzzWorldPatrolAppSettingPage.LoadRouteForTest(fullId);
					Assert.Equal(2, zzzWorldPatrolAppSettingPage.RouteOperationCountForTest);
					Assert.True(zzzWorldPatrolAppSettingPage.HandleRouteRecorderKeyForTest("4"));
					Assert.Equal(3, zzzWorldPatrolAppSettingPage.RouteOperationCountForTest);
					Assert.False(zzzWorldPatrolAppSettingPage.HandleRouteRecorderKeyForTest("2"));
					Assert.False(zzzWorldPatrolAppSettingPage.HandleRouteRecorderKeyForTest("3"));
					Assert.Equal(3, zzzWorldPatrolAppSettingPage.RouteOperationCountForTest);
					Assert.True(zzzWorldPatrolAppSettingPage.HandleRouteRecorderKeyForTest("5"));
					Assert.Equal(2, zzzWorldPatrolAppSettingPage.RouteOperationCountForTest);
					Assert.True(zzzWorldPatrolAppSettingPage.HandleRouteRecorderKeyForTest("4"));
					zzzWorldPatrolAppSettingPage.SaveRouteForTest();
				});
				ZzzWorldPatrolRouteDto zzzWorldPatrolRouteDto = Assert.Single(session.Backend.GetWorldPatrolCatalog(2).Value.Routes);
				Assert.Equal(3, zzzWorldPatrolRouteDto.OperationCount);
				IReadOnlyList<string> expected = new string[2] { "300", "400" };
				IReadOnlyList<ZzzWorldPatrolOperationDto> operations = zzzWorldPatrolRouteDto.Operations;
				Assert.Equal(expected, operations[operations.Count - 1].Data);
			}
			finally
			{
				if (session != null)
				{
					((IDisposable)session).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RouteRecorderCaptureUsesProductionWindowAndDoesNotInventAPositionWhenUnavailable()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzWorldPatrolRouteDto zzzWorldPatrolRouteDto = Assert.Single(backendSession.Backend.GetWorldPatrolCatalog(2).Value.Routes);
			ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> zzzBackendResult = backendSession.Backend.CaptureWorldPatrolRoutePosition(new ZzzCaptureWorldPatrolRoutePositionRequest(zzzWorldPatrolRouteDto.AreaId, zzzWorldPatrolRouteDto.TransportPoint, zzzWorldPatrolRouteDto.Operations));
			Assert.False(zzzBackendResult.Success);
			Assert.Equal(ZzzBackendErrorCode.NotReady, zzzBackendResult.ErrorCode);
			Assert.Null(zzzBackendResult.Value);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void BackendReadsRealRoutesPersistsRouteListCrudAndResetsRunRecord()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<ZzzWorldPatrolCatalogDto> worldPatrolCatalog = backendSession.Backend.GetWorldPatrolCatalog(2);
			Assert.True(worldPatrolCatalog.Success, worldPatrolCatalog.Error);
			ZzzWorldPatrolCatalogDto value = worldPatrolCatalog.Value;
			Assert.Equal("city", Assert.Single(value.Entries).Id);
			Assert.Equal("sixth_street_coffee_shop", Assert.Single(value.Areas, (ZzzWorldPatrolAreaDto area) => area.Id.EndsWith("coffee_shop", StringComparison.Ordinal)).Id);
			ZzzWorldPatrolRouteDto zzzWorldPatrolRouteDto = Assert.Single(value.Routes);
			Assert.Equal("sixth_street_coffee_shop_1", zzzWorldPatrolRouteDto.FullId);
			Assert.Equal("咖啡店", zzzWorldPatrolRouteDto.TransportPoint);
			Assert.Equal(2, zzzWorldPatrolRouteDto.OperationCount);
			Assert.Equal(new string[] { "实战配置" }, value.AutoBattleConfigs);
			Assert.Equal(1, value.RunRecord.CompletedRounds);
			ZzzBackendResult<ZzzWorldPatrolCatalogDto> zzzBackendResult = backendSession.Backend.SaveWorldPatrolRouteList(new ZzzSaveWorldPatrolRouteListRequest(2, "常用路线", "blacklist", new string[] { zzzWorldPatrolRouteDto.FullId }));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			ZzzWorldPatrolRouteListDto zzzWorldPatrolRouteListDto = Assert.Single(zzzBackendResult.Value.RouteLists);
			Assert.Equal("blacklist", zzzWorldPatrolRouteListDto.ListType);
			Assert.Equal(new string[] { zzzWorldPatrolRouteDto.FullId }, zzzWorldPatrolRouteListDto.RouteItems);
			Assert.True(File.Exists(Path.Combine(text, "config", "world_patrol_route_list", "常用路线.yml")));
			ZzzBackendResult<ZzzWorldPatrolRunRecordDto> zzzBackendResult2 = backendSession.Backend.ResetWorldPatrolRunRecord(2);
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.Empty(zzzBackendResult2.Value.Finished);
			Assert.Equal(0, zzzBackendResult2.Value.CompletedRounds);
			Assert.Equal(2, zzzBackendResult2.Value.RoutesPerRound);
			WorldPatrolRunRecord worldPatrolRunRecord = WorldPatrolRunRecord.Load(new OneDragonEnvironment(text), 2);
			Assert.Empty(worldPatrolRunRecord.Finished);
			Assert.Equal(0, worldPatrolRunRecord.CompletedRounds);
			ZzzBackendResult<ZzzWorldPatrolCatalogDto> zzzBackendResult3 = backendSession.Backend.DeleteWorldPatrolRouteList(2, "常用路线");
			Assert.True(zzzBackendResult3.Success, zzzBackendResult3.Error);
			Assert.Empty(zzzBackendResult3.Value.RouteLists);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void PageUsesRealBackendForRouteListSaveAndRecordReset()
	{
		string text = CreateRunRoot();
		try
		{
			BackendSession session = new BackendSession(text);
			try
			{
				GuiParityAndFacadeTests.RunOnUiThread(delegate
				{
					ZzzWorldPatrolAppSettingPage zzzWorldPatrolAppSettingPage = new ZzzWorldPatrolAppSettingPage(session.Backend, session.Backend, 2, "daily");
					zzzWorldPatrolAppSettingPage.BeginNewListForTest("页面名单");
					Assert.Equal("页面名单", zzzWorldPatrolAppSettingPage.CurrentRouteList.Name);
					zzzWorldPatrolAppSettingPage.SaveListForTest();
					zzzWorldPatrolAppSettingPage.ResetRecordForTest();
				});
				Assert.True(File.Exists(Path.Combine(text, "config", "world_patrol_route_list", "页面名单.yml")));
				WorldPatrolRunRecord worldPatrolRunRecord = WorldPatrolRunRecord.Load(new OneDragonEnvironment(text), 2);
				Assert.Empty(worldPatrolRunRecord.Finished);
				Assert.Equal(0, worldPatrolRunRecord.CompletedRounds);
			}
			finally
			{
				if (session != null)
				{
					((IDisposable)session).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void BackendPersistsCompleteRouteOperationListAndDeletesRouteFile()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzWorldPatrolRouteDto zzzWorldPatrolRouteDto = Assert.Single(backendSession.Backend.GetWorldPatrolCatalog(2).Value.Routes);
			ZzzBackendResult<ZzzWorldPatrolCatalogDto> zzzBackendResult = backendSession.Backend.SaveWorldPatrolRoute(new ZzzSaveWorldPatrolRouteRequest(2, zzzWorldPatrolRouteDto.FullId, zzzWorldPatrolRouteDto.AreaId, zzzWorldPatrolRouteDto.Index, zzzWorldPatrolRouteDto.TransportPoint, new ZzzWorldPatrolOperationDto[2]
			{
				new ZzzWorldPatrolOperationDto("move", new string[2] { "11", "22" }),
				new ZzzWorldPatrolOperationDto("move", new string[2] { "33", "44" })
			}));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			ZzzWorldPatrolRouteDto zzzWorldPatrolRouteDto2 = Assert.Single(zzzBackendResult.Value.Routes);
			Assert.Equal(new string[2] { "11", "22" }, zzzWorldPatrolRouteDto2.Operations[0].Data);
			Assert.Equal(new string[2] { "33", "44" }, zzzWorldPatrolRouteDto2.Operations[1].Data);
			string[] buffer = new string[7];
			buffer[0] = text;
			buffer[1] = "config";
			buffer[2] = "world_patrol_route";
			buffer[3] = "system";
			buffer[4] = "city";
			buffer[5] = "sixth_street_coffee_shop";
			buffer[6] = "01.yml";
			string path = Path.Combine(buffer);
			string actualString = File.ReadAllText(path);
			Assert.Contains("op_list:", actualString, StringComparison.Ordinal);
			Assert.Contains("11", actualString, StringComparison.Ordinal);
			Assert.Contains("44", actualString, StringComparison.Ordinal);
			ZzzBackendResult<ZzzWorldPatrolCatalogDto> zzzBackendResult2 = backendSession.Backend.DeleteWorldPatrolRoute(2, zzzWorldPatrolRouteDto.FullId);
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.Empty(zzzBackendResult2.Value.Routes);
			Assert.False(File.Exists(path));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void BackendRejectsIncompleteOrUnknownRouteOperations()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzWorldPatrolRouteDto zzzWorldPatrolRouteDto = Assert.Single(backendSession.Backend.GetWorldPatrolCatalog(2).Value.Routes);
			ZzzBackendResult<ZzzWorldPatrolCatalogDto> zzzBackendResult = backendSession.Backend.SaveWorldPatrolRoute(new ZzzSaveWorldPatrolRouteRequest(2, zzzWorldPatrolRouteDto.FullId, zzzWorldPatrolRouteDto.AreaId, zzzWorldPatrolRouteDto.Index, zzzWorldPatrolRouteDto.TransportPoint, new ZzzWorldPatrolOperationDto[] { new ZzzWorldPatrolOperationDto("move", new string[] { "11" }) }));
			ZzzBackendResult<ZzzWorldPatrolCatalogDto> zzzBackendResult2 = backendSession.Backend.SaveWorldPatrolRoute(new ZzzSaveWorldPatrolRouteRequest(2, zzzWorldPatrolRouteDto.FullId, zzzWorldPatrolRouteDto.AreaId, zzzWorldPatrolRouteDto.Index, zzzWorldPatrolRouteDto.TransportPoint, new ZzzWorldPatrolOperationDto[] { new ZzzWorldPatrolOperationDto("click", new string[2] { "11", "22" }) }));
			Assert.False(zzzBackendResult.Success);
			Assert.Equal(ZzzBackendErrorCode.Validation, zzzBackendResult.ErrorCode);
			Assert.False(zzzBackendResult2.Success);
			Assert.Equal(ZzzBackendErrorCode.Validation, zzzBackendResult2.ErrorCode);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task DebugRunnerRejectsStartIndexOutsideRealRouteOperations()
	{
		string runRoot = CreateRunRoot();
		try
		{
			using BackendSession session = new BackendSession(runRoot);
			ZzzWorldPatrolRouteDto route = Assert.Single(session.Backend.GetWorldPatrolCatalog(2).Value.Routes);
			ZzzBackendResult<ZzzWorldPatrolRouteDebugDto> result = await session.Backend.DebugWorldPatrolRouteAsync(new ZzzDebugWorldPatrolRouteRequest(2, "daily", route.FullId, route.OperationCount + 1));
			Assert.False(result.Success);
			Assert.Equal(ZzzBackendErrorCode.Validation, result.ErrorCode);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public void ProviderNavigatorOpensRealWorldPatrolInterfaceForCurrentInstanceAndGroup()
	{
		string text = CreateRunRoot();
		try
		{
			BackendSession session = new BackendSession(text);
			try
			{
				GuiParityAndFacadeTests.RunOnUiThread(delegate
				{
					ZzzAppSettingNavigator zzzAppSettingNavigator = new ZzzAppSettingNavigator(session.Backend);
					Control requested = null;
					bool condition = zzzAppSettingNavigator.Open("world_patrol", "daily", new Button(), delegate(Control control)
					{
						requested = control;
					});
					Assert.True(condition);
					ZzzWorldPatrolAppSettingPage zzzWorldPatrolAppSettingPage = Assert.IsType<ZzzWorldPatrolAppSettingPage>(requested);
					zzzWorldPatrolAppSettingPage.OnPageShown();
				});
			}
			finally
			{
				if (session != null)
				{
					((IDisposable)session).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static void AssertOrder(string text, params string[] markers)
	{
		int num = -1;
		foreach (string text2 in markers)
		{
			int num2 = text.IndexOf(text2, StringComparison.Ordinal);
			Assert.True(num2 > num, "未按顺序找到 " + text2 + "。");
			num = num2;
		}
	}

	private static string FindDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "ApplicationSettings";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到应用设置目录。");
	}

	private static string CreateRunRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-world-patrol-settings", Guid.NewGuid().ToString("N"));
		string text2 = Path.Combine(text, "config");
		Directory.CreateDirectory(text2);
		File.WriteAllText(Path.Combine(text2, "one_dragon.yml"), "instance_list:\n  - idx: 2\n    name: '02'\n    active: true\n    active_in_od: true");
		string text3 = Path.Combine(text, "assets", "game_data");
		Directory.CreateDirectory(text3);
		Directory.CreateDirectory(Path.Combine(text, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(text3, "screen_info"));
		File.WriteAllText(Path.Combine(text3, "map_area_all.yml"), "full_list:\n  - entry_name: 城市\n    entry_id: city\n    area_list:\n      - area_name: 六分街\n        area_id: sixth_street\n        sub_area_list:\n          - area_name: 咖啡店\n            area_id: coffee_shop");
		string[] buffer = new string[6];
		buffer[0] = text;
		buffer[1] = "config";
		buffer[2] = "world_patrol_route";
		buffer[3] = "system";
		buffer[4] = "city";
		buffer[5] = "sixth_street_coffee_shop";
		string text4 = Path.Combine(buffer);
		Directory.CreateDirectory(text4);
		File.WriteAllText(Path.Combine(text4, "01.yml"), "tp_area_id: sixth_street_coffee_shop\ntp_name: 咖啡店\nidx: 1\nop_list:\n  - op_type: move\n    data: ['100', '200']\n  - op_type: move\n    data: ['300', '400']");
		string text5 = Path.Combine(text3, "world_patrol", "city", "sixth_street_coffee_shop");
		Directory.CreateDirectory(text5);
		using (Mat img = new Mat(600, 800, MatType.CV_8UC1, Scalar.Black))
		{
			Cv2.ImWrite(Path.Combine(text5, "road_mask.png"), img);
		}
		File.WriteAllText(Path.Combine(text5, "icon.yml"), "- icon_name: 咖啡店\n  template_id: map_icon_01\n  lm_pos: [100, 200]\n  tp_pos: [90, 190]");
		string text6 = Path.Combine(text, "config", "auto_battle");
		Directory.CreateDirectory(text6);
		File.WriteAllText(Path.Combine(text6, "实战配置.yml"), "scene_handler_interval: 0.5\n");
		string text7 = Path.Combine(text, "config", "02", "daily");
		Directory.CreateDirectory(text7);
		File.WriteAllText(Path.Combine(text7, "world_patrol.yml"), "auto_battle: 实战配置\nroute_list: ''\nui_disappear_action: silent_fail\nui_disappear_seconds: 10\nroute_retry_times: 1\nroute_retry_action: skip_on_stuck_again\ndaily_loop_count: 2\nloop_interval_seconds: 1800");
		string text8 = Path.Combine(text, "config", "02", "app_run_record");
		Directory.CreateDirectory(text8);
		File.WriteAllText(Path.Combine(text8, "world_patrol.yml"), "dt: '20260713'\nfinished: [sixth_street_coffee_shop_1]\ncompleted_rounds: 1\nroutes_per_round: 2");
		return text;
	}
}
