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
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;
using ZzzOd.Gui.Views.FrontierPages.WorldPatrol;

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
					FrontierWorldPatrolPage zzzWorldPatrolAppSettingPage = new FrontierWorldPatrolPage(session.Backend, session.Backend, 2, "daily");
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
					FrontierWorldPatrolPage zzzWorldPatrolAppSettingPage = new FrontierWorldPatrolPage(session.Backend, session.Backend, 2, "daily");
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
	public void FrontierProviderNavigatorOpensDedicatedWorldPatrolInterface()
	{
		string runRoot = CreateRunRoot();
		try
		{
			using BackendSession session = new(runRoot);
			GuiParityAndFacadeTests.RunOnUiThread(() =>
			{
				FrontierAppSettingPageFactory pageFactory = new(session.Backend);
				ZzzAppSettingNavigator navigator = new(session.Backend, pageFactory.Create);
				Control? requested = null;

				bool opened = navigator.Open(
					"world_patrol",
					"daily",
					new Button(),
					content => requested = content);

				Assert.True(opened);
				FrontierWorldPatrolPage page = Assert.IsType<FrontierWorldPatrolPage>(requested);
				page.DisposePage();
			});
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
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
