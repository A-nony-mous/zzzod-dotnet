using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Input;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.Tests.HollowZero;

public sealed class HollowRunnerTests
{
	private sealed class ScriptedHollowEventSource : IHollowEventSource
	{
		private readonly Queue<string> _events;

		public int Calls { get; private set; }

		public ScriptedHollowEventSource(params string[] events)
		{
			_events = new Queue<string>(events);
		}

		public Task<HollowEventDetection?> DetectAsync(CancellationToken cancellationToken)
		{
			Calls++;
			if (_events.Count == 0)
			{
				return Task.FromResult<HollowEventDetection>(null);
			}
			HollowEventDetection result = new HollowEventDetection(_events.Dequeue(), 0.99, DateTimeOffset.UtcNow, (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
			return Task.FromResult(result);
		}
	}

	private sealed class FrameHollowEventSource : IHollowEventSource
	{
		public Task<HollowEventDetection?> DetectAsync(CancellationToken cancellationToken)
		{
			Mat screen = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			return Task.FromResult(new HollowEventDetection(HollowZeroSpecialEvent.HollowInside.EventName, 1.0, utcNow, (double)utcNow.ToUnixTimeMilliseconds() / 1000.0, screen));
		}
	}

	private sealed class FrameAwareMapSource(HollowZeroMap map) : IHollowMapSource
	{
		public bool ReceivedDetectionFrame { get; private set; }

		public Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken)
		{
			ReceivedDetectionFrame = detection?.Screen != null;
			return Task.FromResult(map);
		}
	}

	private sealed class FrameAwareMapNavigator : IHollowMapNavigator
	{
		public bool ReceivedDetectionFrame { get; private set; }

		public HollowMapMoveResult? MoveNext(HollowZeroMap map, Mat? screen)
		{
			ReceivedDetectionFrame = screen != null;
			HollowZeroMapNode hollowZeroMapNode = map.Nodes[1];
			return new HollowMapMoveResult(hollowZeroMapNode, hollowZeroMapNode.Pos.Center, Clicked: true);
		}
	}

	private sealed class ScriptedHollowMapSource : IHollowMapSource
	{
		private readonly HollowZeroMap? _map;

		public ScriptedHollowMapSource(HollowZeroMap? map)
		{
			_map = map;
		}

		public Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken)
		{
			return Task.FromResult(_map);
		}
	}

	private sealed class MutableHollowMapSource(HollowZeroMap? map) : IHollowMapSource
	{
		public HollowZeroMap? Map { get; set; } = map;

		public Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken)
		{
			return Task.FromResult(Map);
		}
	}

	private sealed class ScriptedHollowMapNavigator(HollowMapMoveResult result) : IHollowMapNavigator
	{
		public HollowMapMoveResult? MoveNext(HollowZeroMap map, Mat? screen)
		{
			return result;
		}
	}

	private sealed class RecordingInputController : IInputController
	{
		public RecordingButtonController ButtonRecorder { get; } = new RecordingButtonController();

		public IButtonController ButtonController => ButtonRecorder;

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool primary = true)
		{
			Clicks.Add(position ?? new OneDragon.Core.Abstractions.Geometry.Point(0, 0));
			return true;
		}

		public void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
		}

		public void Scroll(int clicks, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public void InputText(string text)
		{
		}

		public void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}
	}

	private sealed class RecordingButtonController : IButtonController
	{
		public List<string> Taps { get; } = new List<string>();

		public List<(string Key, TimeSpan? PressTime)> Presses { get; } = new List<(string, TimeSpan?)>();

		public List<string> Releases { get; } = new List<string>();

		public void Tap(string key)
		{
			Taps.Add(key);
		}

		public void TapCombo(IReadOnlyList<string> keys)
		{
			Taps.Add(string.Join("+", keys));
		}

		public void Press(string key, TimeSpan? pressTime = null)
		{
			Presses.Add((key, pressTime));
		}

		public void Release(string key)
		{
			Releases.Add(key);
		}

		public void Reset()
		{
		}
	}

	[Fact]
	public async Task CheckScreenOnceAsync_DispatchesEventFromEventSource()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		ScriptedHollowEventSource eventSource = new ScriptedHollowEventSource(HollowZeroSpecialEvent.ResoniumChoose.EventName);
		using HollowRunner runner = new HollowRunner(context, eventSource);
		await runner.CheckScreenOnceAsync();
		Assert.Equal(1, eventSource.Calls);
		Assert.Equal(HollowZeroSpecialEvent.ResoniumChoose.EventName, runner.LastDetection?.EventName);
		Assert.Contains(HollowZeroSpecialEvent.ResoniumChoose.EventName, (IEnumerable<string>)runner.HandledEvents);
	}

	[Fact]
	public async Task CheckScreenOnceAsync_StopsRunnerWhenMissionCompleteDetected()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		ScriptedHollowEventSource eventSource = new ScriptedHollowEventSource(HollowZeroSpecialEvent.MissionComplete.EventName, HollowZeroSpecialEvent.MissionComplete.EventName);
		using HollowRunner runner = new HollowRunner(context, eventSource);
		runner.StartRunningAsync();
		await runner.CheckScreenOnceAsync();
		Assert.False(runner.IsRunning);
		Assert.Equal(HollowZeroSpecialEvent.MissionComplete.EventName, runner.LastDetection?.EventName);
	}

	[Fact]
	public async Task CheckScreenOnceAsync_ClicksNextMapNodeWhenInsideHollow()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		RecordingInputController input = new RecordingInputController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, input, null, input.ButtonController, null, null, skipForegroundActivation: true);
		context.AttachController(controller);
		using HollowRunner runner = new HollowRunner(mapSource: new ScriptedHollowMapSource(CreateTwoNodeMap()), ctx: context, eventSource: new ScriptedHollowEventSource(HollowZeroSpecialEvent.HollowInside.EventName));
		await runner.CheckScreenOnceAsync();
		Assert.NotNull(runner.LastMoveResult);
		Assert.True(runner.LastMoveResult.Clicked);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(120, 220), runner.LastMoveResult.ClickPosition);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(120, 220), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)input.Clicks);
	}

	[Fact]
	public async Task CheckScreenOnceAsync_PassesOneDetectedFrameToMapAndNavigator()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		FrameHollowEventSource eventSource = new FrameHollowEventSource();
		FrameAwareMapSource mapSource = new FrameAwareMapSource(CreateTwoNodeMap());
		FrameAwareMapNavigator navigator = new FrameAwareMapNavigator();
		using HollowRunner runner = new HollowRunner(context, eventSource, mapSource, navigator);
		await runner.CheckScreenOnceAsync();
		Assert.True(mapSource.ReceivedDetectionFrame);
		Assert.True(navigator.ReceivedDetectionFrame);
	}

	[Theory]
	[InlineData(new object[] { "进入商店" })]
	[InlineData(new object[] { "开门" })]
	public async Task CheckScreenOnceAsync_MovesByMapForEntryEvents(string eventName)
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		RecordingInputController input = new RecordingInputController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, input, null, input.ButtonController, null, null, skipForegroundActivation: true);
		context.AttachController(controller);
		using HollowRunner runner = new HollowRunner(mapSource: new ScriptedHollowMapSource(CreateTwoNodeMap()), ctx: context, eventSource: new ScriptedHollowEventSource(eventName));
		await runner.CheckScreenOnceAsync();
		Assert.NotNull(runner.LastMoveResult);
		Assert.True(runner.LastMoveResult.Clicked);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(120, 220), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)input.Clicks);
	}

	[Fact]
	public async Task CheckScreenOnceAsync_ClearsLastMoveResultWhenMapDetectionReturnsNull()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		MutableHollowMapSource mapSource = new MutableHollowMapSource(CreateTwoNodeMap());
		using HollowRunner runner = new HollowRunner(context, new ScriptedHollowEventSource(HollowZeroSpecialEvent.HollowInside.EventName, HollowZeroSpecialEvent.HollowInside.EventName), mapSource, new ScriptedHollowMapNavigator(new HollowMapMoveResult(CreateTwoNodeMap().Nodes[1], new OneDragon.Core.Abstractions.Geometry.Point(120, 220), Clicked: true)));
		await runner.CheckScreenOnceAsync();
		mapSource.Map = null;
		await runner.CheckScreenOnceAsync();
		Assert.Null(runner.LastMoveResult);
		Assert.Equal("未识别到空洞地图", runner.LastMapFailureReason);
	}

	[Fact]
	public async Task CheckScreenOnceAsync_RecordsFailureWhenMapMovementUsesDefaultEmptySource()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		using HollowRunner runner = new HollowRunner(context, new ScriptedHollowEventSource(HollowZeroSpecialEvent.HollowInside.EventName));
		await runner.CheckScreenOnceAsync();
		Assert.True(runner.UsesEmptyMapSource);
		Assert.Null(runner.LastMoveResult);
		Assert.Equal("地图源未配置", runner.LastMapFailureReason);
	}

	[Fact]
	public async Task CheckScreenOnceAsync_StartsAutoBattleWhenBattleDetected()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		using HollowRunner runner = new HollowRunner(context, new ScriptedHollowEventSource(HollowZeroSpecialEvent.InBattle.EventName));
		await runner.CheckScreenOnceAsync();
		Assert.True(context.AutoBattleContext.IsRuntimeRunning);
		Assert.Equal(HollowEventOutcomeKind.BattleStarted, runner.LastEventResult?.Outcome);
		context.AutoBattleContext.StopContext();
	}

	[Fact]
	public async Task CheckScreenOnceAsync_InteractsWhenInteractionDetected()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		RecordingInputController input = new RecordingInputController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, input, null, input.ButtonRecorder, null, null, skipForegroundActivation: true);
		context.AttachController(controller);
		using HollowRunner runner = new HollowRunner(context, new ScriptedHollowEventSource(HollowZeroSpecialEvent.NeedInteract.EventName));
		await runner.CheckScreenOnceAsync();
		Assert.Equal(HollowEventOutcomeKind.Interacted, runner.LastEventResult?.Outcome);
		Assert.Contains<(string, TimeSpan?)>(input.ButtonRecorder.Presses, ((string Key, TimeSpan? PressTime) press) => press.Key == "f" && press.PressTime == TimeSpan.FromMilliseconds(200L));
	}

	[Fact]
	public async Task CheckScreenOnceAsync_MarksRewardEventPending()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		using HollowRunner runner = new HollowRunner(context, new ScriptedHollowEventSource(HollowZeroSpecialEvent.ResoniumConfirm1.EventName));
		await runner.CheckScreenOnceAsync();
		Assert.Equal(HollowEventOutcomeKind.RewardPending, runner.LastEventResult?.Outcome);
		Assert.True(runner.LastEventResult?.Success);
	}

	[Fact]
	public void Dispose_StopsPeriodicRunner()
	{
		using ZContext ctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		HollowRunner hollowRunner = new HollowRunner(ctx, new ScriptedHollowEventSource());
		hollowRunner.StartRunningAsync();
		hollowRunner.Dispose();
		Assert.False(hollowRunner.IsRunning);
	}

	[Fact]
	public async Task HollowEventDispatcher_ReturnsControllerNotReadyWhenInteractWithoutController()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		HollowEventDispatcher dispatcher = new HollowEventDispatcher(context);
		HollowEventHandleResult result = await dispatcher.DispatchAsync(HollowZeroSpecialEvent.NeedInteract.EventName, CancellationToken.None);
		Assert.Equal(HollowEventOutcomeKind.Interacted, result.Outcome);
		Assert.False(result.Success);
		Assert.Equal("controller-not-ready", result.Message);
	}

	[Fact]
	public async Task HollowEventDispatcher_ReturnsUnhandledForUnknownEvent()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		HollowEventDispatcher dispatcher = new HollowEventDispatcher(context);
		HollowEventHandleResult result = await dispatcher.DispatchAsync("未知事件", CancellationToken.None);
		Assert.Equal(HollowEventOutcomeKind.Unhandled, result.Outcome);
		Assert.False(result.Success);
		Assert.Equal("unhandled-event", result.Message);
	}

	private static HollowZeroMap CreateTwoNodeMap()
	{
		HollowZeroMapNode hollowZeroMapNode = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 40, 40), new HollowZeroEntry("0000-当前"));
		HollowZeroMapNode hollowZeroMapNode2 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(100, 200, 140, 240), new HollowZeroEntry("0001-目标"));
		int num = 2;
		List<HollowZeroMapNode> list = new List<HollowZeroMapNode>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<HollowZeroMapNode> span = CollectionsMarshal.AsSpan(list);
		span[0] = hollowZeroMapNode;
		span[1] = hollowZeroMapNode2;
		int? currentIdx = 0;
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		num = 1;
		List<int> list2 = new List<int>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = 1;
		dictionary[0] = list2;
		num = 1;
		List<int> list3 = new List<int>(num);
		CollectionsMarshal.SetCount(list3, num);
		CollectionsMarshal.AsSpan(list3)[0] = 0;
		dictionary[1] = list3;
		return new HollowZeroMap(list, currentIdx, dictionary);
	}
}
