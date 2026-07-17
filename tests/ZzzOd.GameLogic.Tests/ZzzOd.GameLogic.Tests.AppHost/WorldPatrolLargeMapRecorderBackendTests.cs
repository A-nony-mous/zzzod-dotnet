using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Matcher;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class WorldPatrolLargeMapRecorderBackendTests
{
	private sealed class BackendSession : IDisposable
	{
		private readonly ZzzRuntimeManager _runtime;

		private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

		private readonly ZzzLogFanOutLoggerProvider _logProvider;

		public ZzzAppBackend Backend { get; }

		public BackendSession(string runRoot, bool forceMissingGameWindow = false)
		{
			_runtime = (forceMissingGameWindow ? new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance, (int instanceIndex) => CreateMissingWindowContext(runRoot, instanceIndex)) : new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance));
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			_battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			_logProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(runRoot), eventBus);
			Backend = new ZzzAppBackend(_runtime, eventBus, _battleAssistantRuntimeSource, _logProvider, new ZzzHostModeOptions(ZzzHostMode.Gui), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
		}

		private static ZContext CreateMissingWindowContext(string runRoot, int instanceIndex)
		{
			ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot), null, instanceIndex);
			ZPcController zPcController = new ZPcController(new GameConfig(), null, 1920, 1080, null, null, null, null, null, null, skipForegroundActivation: true);
			zPcController.SetWindowTitle($"zzzod-test-missing-window-{Guid.NewGuid():N}");
			zContext.AttachController(zPcController);
			return zContext;
		}

		public void Dispose()
		{
			_runtime.Dispose();
			_battleAssistantRuntimeSource.Dispose();
			_logProvider.Dispose();
		}
	}

	[Fact]
	public void LoadMoveScaleHighlightUpdateAndSaveUseRealMapAssets()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> zzzBackendResult = backendSession.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop");
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.True(zzzBackendResult.Value.IsLoaded);
			Assert.True(zzzBackendResult.Value.HasLargeMap);
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(150, 150), zzzBackendResult.Value.CurrentPosition);
			Assert.Equal("咖啡店", Assert.Single(zzzBackendResult.Value.Icons).IconName);
			Assert.NotNull(zzzBackendResult.Value.LargeMap);
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto = AssertSuccess(backendSession.Backend.MoveWorldPatrolLargeMapRecorder(2, 10, -20));
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(160, 130), zzzWorldPatrolLargeMapRecorderStateDto.CurrentPosition);
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto2 = AssertSuccess(backendSession.Backend.SetWorldPatrolLargeMapRecorderPosition(2, 42, 84));
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(42, 84), zzzWorldPatrolLargeMapRecorderStateDto2.CurrentPosition);
			ZzzWorldPatrolLargeMapIconDto item = new ZzzWorldPatrolLargeMapIconDto("改名传送点", "map_icon_01", new ZzzWorldPatrolRoutePositionDto(120, 130), new ZzzWorldPatrolRoutePositionDto(125, 135));
			AssertSuccess(backendSession.Backend.UpdateWorldPatrolLargeMapRecorderIcons(2, new ZzzWorldPatrolLargeMapIconDto[] { item }));
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto3 = AssertSuccess(backendSession.Backend.SelectWorldPatrolLargeMapRecorderIcon(2, 0));
			Assert.Equal(0, zzzWorldPatrolLargeMapRecorderStateDto3.HighlightedIconIndex);
			Assert.NotNull(zzzWorldPatrolLargeMapRecorderStateDto3.LargeMap);
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto4 = AssertSuccess(backendSession.Backend.ScaleWorldPatrolLargeMapRecorder(2, 50));
			using Mat mat = Cv2.ImDecode(zzzWorldPatrolLargeMapRecorderStateDto4.LargeMap.Bytes, ImreadModes.Color);
			Assert.Equal(570, mat.Cols);
			Assert.Equal(570, mat.Rows);
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto5 = AssertSuccess(backendSession.Backend.SaveWorldPatrolLargeMapRecorder(2));
			Assert.Contains("保存区域地图成功", zzzWorldPatrolLargeMapRecorderStateDto5.Status, StringComparison.Ordinal);
			WorldPatrolService worldPatrolService = new WorldPatrolService(new OneDragonEnvironment(text));
			worldPatrolService.LoadData();
			WorldPatrolLargeMap worldPatrolLargeMap = Assert.Single(worldPatrolService.LargeMapList);
			Assert.Equal(570, worldPatrolLargeMap.RoadMask.Cols);
			Assert.Equal("改名传送点", Assert.Single(worldPatrolLargeMap.IconList).IconName);
			Assert.Equal(new WorldPatrolPoint(330, 340), worldPatrolLargeMap.IconList[0].LargeMapPosition);
			Assert.Equal(new WorldPatrolPoint(335, 345), worldPatrolLargeMap.IconList[0].TransportPosition);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DeleteAndCancelMutateOnlyTheSelectedRealMapSession()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			AssertSuccess(backendSession.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop"));
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto = AssertSuccess(backendSession.Backend.DeleteWorldPatrolLargeMapRecorder(2));
			Assert.False(zzzWorldPatrolLargeMapRecorderStateDto.IsLoaded);
			Assert.False(zzzWorldPatrolLargeMapRecorderStateDto.HasLargeMap);
			Assert.Null(zzzWorldPatrolLargeMapRecorderStateDto.AreaId);
			Assert.False(File.Exists(GetRoadMaskPath(text)));
			Assert.False(File.Exists(GetIconPath(text)));
			AssertSuccess(backendSession.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop"));
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto2 = AssertSuccess(backendSession.Backend.CancelWorldPatrolLargeMapRecorder(2));
			Assert.False(zzzWorldPatrolLargeMapRecorderStateDto2.IsLoaded);
			Assert.Null(zzzWorldPatrolLargeMapRecorderStateDto2.AreaId);
			ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> zzzBackendResult = backendSession.Backend.CancelWorldPatrolLargeMapRecorder(2);
			Assert.False(zzzBackendResult.Success);
			Assert.Equal(ZzzBackendErrorCode.NotReady, zzzBackendResult.ErrorCode);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task CaptureReturnsNotReadyWithoutARealGameWindowAndDoesNotCreateSnapshots()
	{
		string runRoot = CreateRunRoot();
		try
		{
			using BackendSession session = new BackendSession(runRoot, forceMissingGameWindow: true);
			AssertSuccess(session.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop"));
			ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> result = await session.Backend.CaptureWorldPatrolLargeMapRecorderAsync(2, 0.7);
			Assert.False(result.Success);
			Assert.Equal(ZzzBackendErrorCode.NotReady, result.ErrorCode);
			Assert.Null(result.Value);
			Assert.Contains("窗口", result.Error ?? string.Empty, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public void PositionMergeAndUndoKeepExplicitIncompleteStateUntilARealSnapshotExists()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			AssertSuccess(backendSession.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop"));
			ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> zzzBackendResult = backendSession.Backend.CalculateWorldPatrolLargeMapRecorderPosition(2, useIcon: true);
			ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> zzzBackendResult2 = backendSession.Backend.MergeWorldPatrolLargeMapRecorder(2);
			ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> zzzBackendResult3 = backendSession.Backend.UndoWorldPatrolLargeMapRecorder(2);
			Assert.False(zzzBackendResult.Success);
			Assert.Equal(ZzzBackendErrorCode.NotReady, zzzBackendResult.ErrorCode);
			Assert.False(zzzBackendResult2.Success);
			Assert.Equal(ZzzBackendErrorCode.NotReady, zzzBackendResult2.ErrorCode);
			Assert.True(zzzBackendResult3.Success, zzzBackendResult3.Error);
			Assert.False(zzzBackendResult3.Value.HasLargeMap);
			Assert.Null(zzzBackendResult3.Value.CurrentPosition);
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto = AssertSuccess(backendSession.Backend.ToggleWorldPatrolLargeMapRecorderOverlap(2));
			Assert.Equal(0, zzzWorldPatrolLargeMapRecorderStateDto.OverlapMode);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void PositionCalculationUsesLargeMapRecorderRoadFallbackWhenNoIconCanLocateTheSnapshot()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			AssertSuccess(backendSession.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop"));
			LargeMapSnapshot sessionLargeMap = GetSessionLargeMap(backendSession.Backend);
			sessionLargeMap.RoadMask.SetTo(Scalar.Black);
			using Mat mat = new Mat(4, 4, MatType.CV_8UC1, Scalar.Black);
			mat.Set(0, 0, 255);
			mat.Set(1, 2, 255);
			mat.Set(3, 1, 255);
			mat.CopyTo(new Mat(sessionLargeMap.RoadMask, new OpenCvSharp.Rect(100, 110, mat.Cols, mat.Rows)));
			SetSessionMiniMap(backendSession.Backend, new MiniMapSnapshot(mat.Clone(), Array.Empty<MiniMapIcon>()));
			AssertSuccess(backendSession.Backend.SetWorldPatrolLargeMapRecorderPosition(2, 102, 112));
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto = AssertSuccess(backendSession.Backend.CalculateWorldPatrolLargeMapRecorderPosition(2, useIcon: true));
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(102, 112), zzzWorldPatrolLargeMapRecorderStateDto.CalculatedPosition);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DeleteClearsANewUnpersistedMapAndRemovesTheChosenSession()
	{
		string text = CreateRunRoot(includeMap: false);
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			AssertSuccess(backendSession.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop"));
			using Mat mat = new Mat(20, 20, MatType.CV_8UC1, Scalar.Black);
			Cv2.Rectangle(mat, new OpenCvSharp.Rect(8, 8, 4, 4), Scalar.White, -1);
			SetSessionMiniMap(backendSession.Backend, new MiniMapSnapshot(mat.Clone(), Array.Empty<MiniMapIcon>()));
			Assert.True(AssertSuccess(backendSession.Backend.MergeWorldPatrolLargeMapRecorder(2)).HasLargeMap);
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto = AssertSuccess(backendSession.Backend.DeleteWorldPatrolLargeMapRecorder(2));
			Assert.False(zzzWorldPatrolLargeMapRecorderStateDto.IsLoaded);
			Assert.False(zzzWorldPatrolLargeMapRecorderStateDto.HasLargeMap);
			Assert.Null(zzzWorldPatrolLargeMapRecorderStateDto.AreaId);
			Assert.False(File.Exists(GetRoadMaskPath(text)));
			Assert.False(File.Exists(GetIconPath(text)));
			ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> zzzBackendResult = backendSession.Backend.SetWorldPatrolLargeMapRecorderPosition(2, 1, 2);
			Assert.False(zzzBackendResult.Success);
			Assert.Equal(ZzzBackendErrorCode.NotReady, zzzBackendResult.ErrorCode);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void FirstMergeCreatesThreeByThreeMapLaterMergeKeepsPythonCopyRoadFalseAndUndoRestoresState()
	{
		string text = CreateRunRoot(includeMap: false);
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto = AssertSuccess(backendSession.Backend.LoadWorldPatrolLargeMapRecorder(2, "sixth_street_coffee_shop"));
			Assert.False(zzzWorldPatrolLargeMapRecorderStateDto.HasLargeMap);
			using Mat mat = new Mat(20, 20, MatType.CV_8UC1, Scalar.Black);
			Cv2.Rectangle(mat, new OpenCvSharp.Rect(8, 8, 4, 4), Scalar.White, -1);
			SetSessionMiniMap(backendSession.Backend, new MiniMapSnapshot(mat.Clone(), new MiniMapIcon[] { new MiniMapIcon("map_icon_01", new OneDragon.Core.Abstractions.Geometry.Point(10, 10)) }));
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto2 = AssertSuccess(backendSession.Backend.MergeWorldPatrolLargeMapRecorder(2));
			Assert.True(zzzWorldPatrolLargeMapRecorderStateDto2.HasLargeMap);
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(30, 30), zzzWorldPatrolLargeMapRecorderStateDto2.CurrentPosition);
			Assert.Single(zzzWorldPatrolLargeMapRecorderStateDto2.Icons);
			Assert.Equal(new ZzzWorldPatrolRoutePositionDto(30, 30), zzzWorldPatrolLargeMapRecorderStateDto2.Icons[0].LargeMapPosition);
			LargeMapSnapshot sessionLargeMap = GetSessionLargeMap(backendSession.Backend);
			Assert.Equal(60, sessionLargeMap.RoadMask.Rows);
			Assert.Equal(60, sessionLargeMap.RoadMask.Cols);
			int expected = Cv2.CountNonZero(sessionLargeMap.RoadMask);
			using Mat mat2 = new Mat(20, 20, MatType.CV_8UC1, Scalar.White);
			SetSessionMiniMap(backendSession.Backend, new MiniMapSnapshot(mat2.Clone(), new MiniMapIcon[] { new MiniMapIcon("map_icon_02", new OneDragon.Core.Abstractions.Geometry.Point(10, 10)) }));
			SetSessionPositionMatch(backendSession.Backend, new MatchResult(1.0, 5, 5, 20, 20));
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto3 = AssertSuccess(backendSession.Backend.MergeWorldPatrolLargeMapRecorder(2));
			Assert.Equal(2, zzzWorldPatrolLargeMapRecorderStateDto3.Icons.Count);
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(15, 15), zzzWorldPatrolLargeMapRecorderStateDto3.CurrentPosition);
			Assert.Equal(expected, Cv2.CountNonZero(GetSessionLargeMap(backendSession.Backend).RoadMask));
			ZzzWorldPatrolLargeMapRecorderStateDto zzzWorldPatrolLargeMapRecorderStateDto4 = AssertSuccess(backendSession.Backend.UndoWorldPatrolLargeMapRecorder(2));
			Assert.Single(zzzWorldPatrolLargeMapRecorderStateDto4.Icons);
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(30, 30), zzzWorldPatrolLargeMapRecorderStateDto4.CurrentPosition);
			Assert.Equal(expected, Cv2.CountNonZero(GetSessionLargeMap(backendSession.Backend).RoadMask));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RouteRecorderVisualComesFromRealMapAndClickUsesUniformImageCoordinates()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzWorldPatrolRouteVisualRequest zzzWorldPatrolRouteVisualRequest = new ZzzWorldPatrolRouteVisualRequest("sixth_street_coffee_shop", "咖啡店", new ZzzWorldPatrolOperationDto[2]
			{
				new ZzzWorldPatrolOperationDto("move", new string[2] { "120", "130" }),
				new ZzzWorldPatrolOperationDto("move", new string[2] { "160", "170" })
			});
			ZzzBackendResult<ZzzWorldPatrolRouteVisualDto> zzzBackendResult = backendSession.Backend.RenderWorldPatrolRouteRecorder(zzzWorldPatrolRouteVisualRequest);
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			using Mat mat = Cv2.ImDecode(zzzBackendResult.Value.LargeMap.Bytes, ImreadModes.Color);
			Assert.Equal(300, mat.Cols);
			Assert.Equal(300, mat.Rows);
			Assert.True(mat.At<Vec3b>(150, 140).Item1 > 0);
			ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> zzzBackendResult2 = backendSession.Backend.ConvertWorldPatrolRouteRecorderClick(new ZzzWorldPatrolRouteMapClickRequest(zzzWorldPatrolRouteVisualRequest.AreaId, zzzWorldPatrolRouteVisualRequest.TransportPoint, zzzWorldPatrolRouteVisualRequest.Operations, 250.0, 100.0, 600.0, 300.0));
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.Equal<ZzzWorldPatrolRoutePositionDto>(new ZzzWorldPatrolRoutePositionDto(100, 100), zzzBackendResult2.Value);
			ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> zzzBackendResult3 = backendSession.Backend.ConvertWorldPatrolRouteRecorderClick(new ZzzWorldPatrolRouteMapClickRequest(zzzWorldPatrolRouteVisualRequest.AreaId, zzzWorldPatrolRouteVisualRequest.TransportPoint, zzzWorldPatrolRouteVisualRequest.Operations, 10.0, 100.0, 600.0, 300.0));
			Assert.False(zzzBackendResult3.Success);
			Assert.Equal(ZzzBackendErrorCode.Validation, zzzBackendResult3.ErrorCode);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static ZzzWorldPatrolLargeMapRecorderStateDto AssertSuccess(ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> result)
	{
		Assert.True(result.Success, result.Error);
		return Assert.IsType<ZzzWorldPatrolLargeMapRecorderStateDto>(result.Value);
	}

	private static object GetRecorderSession(ZzzAppBackend backend)
	{
		FieldInfo field = typeof(ZzzAppBackend).GetField("_worldPatrolLargeMapRecorderSession", BindingFlags.Instance | BindingFlags.NonPublic);
		object value = field.GetValue(backend);
		Assert.NotNull(value);
		return value;
	}

	private static void SetSessionMiniMap(ZzzAppBackend backend, MiniMapSnapshot miniMap)
	{
		object recorderSession = GetRecorderSession(backend);
		PropertyInfo property = recorderSession.GetType().GetProperty("MiniMap");
		(property.GetValue(recorderSession) as IDisposable)?.Dispose();
		property.SetValue(recorderSession, miniMap);
	}

	private static void SetSessionPositionMatch(ZzzAppBackend backend, MatchResult match)
	{
		object recorderSession = GetRecorderSession(backend);
		recorderSession.GetType().GetProperty("PositionMatch").SetValue(recorderSession, match);
	}

	private static LargeMapSnapshot GetSessionLargeMap(ZzzAppBackend backend)
	{
		object recorderSession = GetRecorderSession(backend);
		return Assert.IsType<LargeMapSnapshot>(recorderSession.GetType().GetProperty("LargeMap").GetValue(recorderSession));
	}

	private static string CreateRunRoot(bool includeMap = true)
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-large-map-recorder", Guid.NewGuid().ToString("N"));
		string text2 = Path.Combine(text, "config");
		Directory.CreateDirectory(text2);
		File.WriteAllText(Path.Combine(text2, "one_dragon.yml"), "instance_list:\n  - idx: 2\n    name: '02'\n    active: true\n    active_in_od: true");
		string text3 = Path.Combine(text, "assets", "game_data");
		Directory.CreateDirectory(text3);
		Directory.CreateDirectory(Path.Combine(text, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(text3, "screen_info"));
		File.WriteAllText(Path.Combine(text3, "map_area_all.yml"), "full_list:\n  - entry_name: 城市\n    entry_id: city\n    area_list:\n      - area_name: 六分街\n        area_id: sixth_street\n        sub_area_list:\n          - area_name: 咖啡店\n            area_id: coffee_shop");
		if (includeMap)
		{
			string directoryName = Path.GetDirectoryName(GetRoadMaskPath(text));
			Directory.CreateDirectory(directoryName);
			using (Mat img = new Mat(300, 300, MatType.CV_8UC1, Scalar.Black))
			{
				Cv2.Rectangle(img, new OpenCvSharp.Rect(80, 80, 140, 140), Scalar.White, -1);
				Cv2.ImWrite(GetRoadMaskPath(text), img);
			}
			File.WriteAllText(GetIconPath(text), "- icon_name: 咖啡店\n  template_id: map_icon_01\n  lm_pos: [100, 110]\n  tp_pos: [105, 115]");
		}
		return text;
	}

	private static string GetRoadMaskPath(string runRoot)
	{
		string[] buffer = new string[7];
		buffer[0] = runRoot;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "world_patrol";
		buffer[4] = "city";
		buffer[5] = "sixth_street_coffee_shop";
		buffer[6] = "road_mask.png";
		return Path.Combine(buffer);
	}

	private static string GetIconPath(string runRoot)
	{
		string[] buffer = new string[7];
		buffer[0] = runRoot;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "world_patrol";
		buffer[4] = "city";
		buffer[5] = "sixth_street_coffee_shop";
		buffer[6] = "icon.yml";
		return Path.Combine(buffer);
	}
}
