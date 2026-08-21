using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Input;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class WorldPatrolOperationTests
{
	private sealed class RecordingController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot;

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public List<bool> PcAltClicks { get; } = new List<bool>();

		public List<string> GamepadActions { get; } = new List<string>();

		public List<(OneDragon.Core.Abstractions.Geometry.Point End, OneDragon.Core.Abstractions.Geometry.Point? Start)> Drags { get; } = new List<(OneDragon.Core.Abstractions.Geometry.Point, OneDragon.Core.Abstractions.Geometry.Point?)>();

		public List<bool> ScreenshotIndependentFlags { get; } = new List<bool>();

		public RecordingController(Mat? screenshot = null)
		{
			_screenshot = screenshot?.Clone() ?? new Mat(10, 10, MatType.CV_8UC3, Scalar.Black);
		}

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			if (position.HasValue)
			{
				Clicks.Add(position.Value);
			}
			PcAltClicks.Add(pcAlt);
			if (gamepadAction != null)
			{
				GamepadActions.Add(gamepadAction);
			}
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
			Drags.Add((end, start));
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}

		public void Dispose()
		{
			_screenshot.Dispose();
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			ScreenshotIndependentFlags.Add(independent);
			return _screenshot.Clone();
		}
	}

	private sealed class RecordingButtonController : IButtonController
	{
		public List<string> Taps { get; } = new List<string>();

		public List<(string Key, TimeSpan? PressTime)> Presses { get; } = new List<(string, TimeSpan?)>();

		public List<string> Releases { get; } = new List<string>();

		public int ResetCount { get; private set; }

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
			ResetCount++;
		}
	}

	private sealed class RecordingInputController(IButtonController buttonController) : IInputController
	{
		public IButtonController ButtonController { get; } = buttonController;

		public bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool primary = true)
		{
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

	private sealed class SizeAwareOcrMatcher(Func<int, int, IReadOnlyList<OcrMatchResult>> resultsFactory) : IOcrMatcher
	{
		public void UpdateUseGpu(bool useGpu)
		{
		}

		public bool IsUseGpu()
		{
			return false;
		}

		public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
		{
			return true;
		}

		public string RunOcrSingleLine(Mat image, double? threshold = null, bool strictOneLine = true)
		{
			return string.Join(string.Empty, from result in Ocr(image, threshold.GetValueOrDefault())
				select result.Text);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in Ocr(image, threshold.GetValueOrDefault(), mergeLineDistance))
			{
				if (!dictionary.TryGetValue(item.Text, out var value))
				{
					value = new MatchResultList(onlyBest: false);
					dictionary[item.Text] = value;
				}
				value.Append(item, autoMerge: false);
			}
			return dictionary;
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return (from result in resultsFactory(image.Width, image.Height)
				select new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	private sealed class RecordingTransportBy3dMapServices : ITransportBy3dMapServices
	{
		public List<string> Calls { get; } = new List<string>();

		public string OpenFilterStatus { get; set; } = "筛选";

		public string CloseFilterStatus { get; set; } = "3D地图";

		public string? CheckCurrentScreen(ZContext context, Mat? screen, IReadOnlyList<string> screenNameList)
		{
			return null;
		}

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}

		public bool Open3dMap(ZContext context, Mat? screen)
		{
			Calls.Add("open_map");
			return true;
		}

		public OperationResult ChooseArea(ZContext context, Mat? screen, string areaName, WorldPatrolArea targetArea)
		{
			Calls.Add("choose_area:" + areaName);
			return new OperationResult(IsSuccess: true, areaName);
		}

		public OperationResult ExpandSubArea(ZContext context, Mat? screen)
		{
			Calls.Add("expand_sub_area");
			return new OperationResult(IsSuccess: true, "展开");
		}

		public OperationResult ChooseSubArea(ZContext context, Mat? screen, string areaName)
		{
			Calls.Add("choose_sub_area:" + areaName);
			return new OperationResult(IsSuccess: true, areaName);
		}

		public OperationResult OpenFilter(ZContext context, Mat? screen)
		{
			Calls.Add("open_filter");
			return new OperationResult(IsSuccess: true, OpenFilterStatus);
		}

		public OperationResult ChooseFilter(ZContext context, Mat? screen, string targetWord)
		{
			Calls.Add("choose_filter:" + targetWord);
			return new OperationResult(IsSuccess: true, targetWord);
		}

		public OperationResult CloseFilter(ZContext context, Mat? screen)
		{
			Calls.Add("close_filter");
			return new OperationResult(IsSuccess: true, CloseFilterStatus);
		}

		public OperationResult ClickMiniScale(ZContext context)
		{
			Calls.Add("mini_scale");
			return new OperationResult(IsSuccess: true, "最小缩放");
		}

		public OperationResult InitTransportPointSearch(ZContext context, WorldPatrolArea targetArea, string targetTransportName)
		{
			Calls.Add("init_search:" + targetTransportName);
			return new OperationResult(IsSuccess: true, targetTransportName);
		}

		public OperationResult SearchTransportPoint(ZContext context, Mat? screen, string targetTransportName, CancellationToken cancellationToken)
		{
			Calls.Add("search:" + targetTransportName);
			return new OperationResult(IsSuccess: true, targetTransportName);
		}

		public void CloseAreaInfoPopup(ZContext context, Mat? screen)
		{
			Calls.Add("close_popup");
		}

		public OperationResult ClickGo(ZContext context, Mat? screen)
		{
			Calls.Add("click_go");
			return new OperationResult(IsSuccess: true, "前往");
		}

		public Task<OperationResult> WaitNormalWorldAfterTransportAsync(ZContext context, WorldPatrolArea targetArea, CancellationToken cancellationToken)
		{
			Calls.Add($"wait_world:{targetArea.IsHollow}");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}
	}

	private sealed class ExecutingTransportBy3dMapServices : ITransportBy3dMapServices
	{
		private bool _mapOpened;

		private bool _filterOpened;

		private bool _filterClosed;

		public int OpenMapChecks { get; private set; }

		public int OpenFilterChecks { get; private set; }

		public int CloseFilterChecks { get; private set; }

		public int ClickGoCount { get; private set; }

		public CancellationToken BackCancellationToken { get; private set; }

		public CancellationToken WaitCancellationToken { get; private set; }

		public string? CheckCurrentScreen(ZContext context, Mat? screen, IReadOnlyList<string> screenNameList)
		{
			if (screenNameList.Contains<string>("3D地图", StringComparer.Ordinal))
			{
				OpenMapChecks++;
				return _mapOpened ? "3D地图" : null;
			}
			return null;
		}

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context, CancellationToken cancellationToken)
		{
			BackCancellationToken = cancellationToken;
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}

		public bool Open3dMap(ZContext context, Mat? screen)
		{
			_mapOpened = true;
			return true;
		}

		public OperationResult ChooseArea(ZContext context, Mat? screen, string areaName, WorldPatrolArea targetArea)
		{
			return new OperationResult(IsSuccess: true, areaName);
		}

		public OperationResult ExpandSubArea(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: true, "展开子区域列表");
		}

		public OperationResult ChooseSubArea(ZContext context, Mat? screen, string areaName)
		{
			return new OperationResult(IsSuccess: true, areaName);
		}

		public OperationResult OpenFilter(ZContext context, Mat? screen)
		{
			OpenFilterChecks++;
			if (_filterOpened)
			{
				return new OperationResult(IsSuccess: true, "标题-标识点筛选");
			}
			_filterOpened = true;
			return new OperationResult(IsSuccess: true, "按钮-筛选");
		}

		public OperationResult ChooseFilter(ZContext context, Mat? screen, string targetWord)
		{
			return new OperationResult(IsSuccess: true, targetWord);
		}

		public OperationResult CloseFilter(ZContext context, Mat? screen)
		{
			CloseFilterChecks++;
			if (_filterClosed)
			{
				return new OperationResult(IsSuccess: true, "3D地图");
			}
			_filterClosed = true;
			return new OperationResult(IsSuccess: true, "关闭筛选");
		}

		public OperationResult ClickMiniScale(ZContext context)
		{
			return new OperationResult(IsSuccess: true, "最小缩放");
		}

		public OperationResult InitTransportPointSearch(ZContext context, WorldPatrolArea targetArea, string targetTransportName)
		{
			return new OperationResult(IsSuccess: true, targetTransportName);
		}

		public OperationResult SearchTransportPoint(ZContext context, Mat? screen, string targetTransportName, CancellationToken cancellationToken)
		{
			return new OperationResult(IsSuccess: true, targetTransportName);
		}

		public void CloseAreaInfoPopup(ZContext context, Mat? screen)
		{
		}

		public OperationResult ClickGo(ZContext context, Mat? screen)
		{
			ClickGoCount++;
			return new OperationResult(IsSuccess: true, "按钮-前往");
		}

		public Task<OperationResult> WaitNormalWorldAfterTransportAsync(ZContext context, WorldPatrolArea targetArea, CancellationToken cancellationToken)
		{
			WaitCancellationToken = cancellationToken;
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}
	}

	private sealed class RecordingRunRouteServices : IWorldPatrolRunRouteServices
	{
		private DateTimeOffset _now = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

		private readonly WorldPatrolArea _area;

		private readonly WorldPatrolRoute _route;

		public List<string> Calls { get; } = new List<string>();

		public WorldPatrolMiniMapSnapshot MiniMap { get; set; } = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0);

		public Queue<WorldPatrolMiniMapSnapshot> MiniMapSequence { get; } = new Queue<WorldPatrolMiniMapSnapshot>();

		public WorldPatrolPoint? NextPosition { get; set; } = new WorldPatrolPoint(0, 0);

		public WorldPatrolBattleCheckResult BattleCheck { get; set; } = new WorldPatrolBattleCheckResult(InBattle: true);

		public bool HasInteractionResult { get; set; }

		public DateTimeOffset Now => _now;

		public TimeSpan BattleWaitDelay => TimeSpan.Zero;

		public CancellationToken BackCancellationToken { get; private set; }

		public CancellationToken TransportCancellationToken { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Rect? LastPossibleRect { get; private set; }

		public RecordingRunRouteServices(WorldPatrolArea area, WorldPatrolRoute route)
		{
			_area = area;
			_route = route;
		}

		public void Advance(TimeSpan span)
		{
			_now += span;
		}

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context, CancellationToken cancellationToken)
		{
			BackCancellationToken = cancellationToken;
			Calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}

		public Task<OperationResult> TransportAsync(ZContext context, WorldPatrolRoute route, CancellationToken cancellationToken)
		{
			TransportCancellationToken = cancellationToken;
			Calls.Add("transport");
			return Task.FromResult(new OperationResult(IsSuccess: true, route.TpName));
		}

		public WorldPatrolPoint? GetRoutePosBeforeOpIdx(ZContext context, WorldPatrolRoute route, int opIdx)
		{
			Calls.Add($"route_pos:{opIdx}");
			return new WorldPatrolPoint(0, 0);
		}

		public WorldPatrolLargeMap? GetRouteLargeMap(ZContext context, WorldPatrolRoute route)
		{
			return new WorldPatrolLargeMap(_area.FullId, "road_mask.png", new WorldPatrolLargeMapIcon[] { WorldPatrolLargeMapIcon.Create(_route.TpName, "map_icon_01", new WorldPatrolPoint(0, 0)) });
		}

		public WorldPatrolMiniMapSnapshot CutMiniMap(ZContext context, Mat? screen)
		{
			WorldPatrolMiniMapSnapshot worldPatrolMiniMapSnapshot = ((MiniMapSequence.Count == 0) ? MiniMap : MiniMapSequence.Dequeue());
			Calls.Add($"cut_minimap:{worldPatrolMiniMapSnapshot.PlayMaskFound}");
			return worldPatrolMiniMapSnapshot;
		}

		public WorldPatrolPoint? CalculateCurrentPosition(ZContext context, WorldPatrolLargeMap largeMap, WorldPatrolMiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Rect possibleRect)
		{
			LastPossibleRect = possibleRect;
			Calls.Add($"calculate_position:{NextPosition?.X},{NextPosition?.Y}");
			return NextPosition;
		}

		public void StopMovingForward(ZContext context)
		{
			Calls.Add("stop_forward");
		}

		public void StartMovingForward(ZContext context)
		{
			Calls.Add("start_forward");
		}

		public void TurnVerticalByDistance(ZContext context, double distance)
		{
			Calls.Add($"turn_vertical:{distance}");
		}

		public void TurnByAngleDiff(ZContext context, double angleDiff)
		{
			Calls.Add($"turn:{angleDiff:0.##}");
		}

		public void SwitchToBestAgentForMoving(ZContext context)
		{
			Calls.Add("switch_best");
		}

		public void SwitchNextForUnstuck(ZContext context)
		{
			Calls.Add("switch_next_unstuck");
		}

		public void MoveUnstuck(ZContext context, int direction, string tag)
		{
			Calls.Add($"unstuck:{tag}:{direction}");
		}

		public void InitAutoBattle(ZContext context, string autoBattleName)
		{
			Calls.Add("init_auto:" + autoBattleName);
		}

		public void StartAutoBattle(ZContext context)
		{
			Calls.Add("start_auto");
		}

		public void StopAutoBattle(ZContext context)
		{
			Calls.Add("stop_auto");
		}

		public void WaitAfterAutoBattleStop(ZContext context)
		{
			Calls.Add("wait_after_auto_stop");
		}

		public WorldPatrolBattleCheckResult CheckBattleState(ZContext context, Mat? screen, DateTimeOffset screenshotTime)
		{
			Calls.Add($"check_battle:{BattleCheck.InBattle}");
			return BattleCheck;
		}

		public bool HasInteraction(ZContext context, Mat? screen)
		{
			Calls.Add($"has_interaction:{HasInteractionResult}");
			return HasInteractionResult;
		}
	}

	private sealed class RecordingRouteRunner : IWorldPatrolRouteRunner
	{
		public bool IsRunning { get; private set; }

		public int RunCount { get; private set; }

		public List<bool> RestartFlags { get; } = new List<bool>();

		public Task<OperationResult> RunRouteAsync(ZContext context, WorldPatrolConfig config, WorldPatrolRoute route, bool isRestarted, CancellationToken cancellationToken)
		{
			IsRunning = true;
			RunCount++;
			RestartFlags.Add(isRestarted);
			IsRunning = false;
			return Task.FromResult(new OperationResult(IsSuccess: true, route.FullId));
		}

		public void Pause()
		{
			IsRunning = false;
		}

		public void Resume()
		{
		}

		public void Stop()
		{
			IsRunning = false;
		}
	}

	private sealed class ScriptedRouteRunner(params OperationResult[] results) : IWorldPatrolRouteRunner
	{
		private int _index;

		public bool IsRunning { get; private set; }

		public List<bool> RestartFlags { get; } = new List<bool>();

		public Task<OperationResult> RunRouteAsync(ZContext context, WorldPatrolConfig config, WorldPatrolRoute route, bool isRestarted, CancellationToken cancellationToken)
		{
			IsRunning = true;
			RestartFlags.Add(isRestarted);
			OperationResult result = ((_index < results.Length) ? results[_index] : results[^1]);
			_index++;
			IsRunning = false;
			return Task.FromResult(result);
		}

		public void Pause()
		{
			IsRunning = false;
		}

		public void Resume()
		{
		}

		public void Stop()
		{
			IsRunning = false;
		}
	}

	private sealed class BlockingRouteRunner : IWorldPatrolRouteRunner
	{
		private readonly TaskCompletionSource<OperationResult> _completion = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

		public TaskCompletionSource<bool> Started { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		public bool IsRunning { get; private set; }

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public async Task<OperationResult> RunRouteAsync(ZContext context, WorldPatrolConfig config, WorldPatrolRoute route, bool isRestarted, CancellationToken cancellationToken)
		{
			IsRunning = true;
			Started.TrySetResult(result: true);
			try
			{
				return await _completion.Task.WaitAsync(cancellationToken);
			}
			finally
			{
				IsRunning = false;
			}
		}

		public void Pause()
		{
			PauseCount++;
		}

		public void Resume()
		{
			ResumeCount++;
		}

		public void Stop()
		{
			StopCount++;
		}

		public void Complete(OperationResult result)
		{
			_completion.TrySetResult(result);
		}
	}

	[Fact]
	public async Task TransportBy3dMap_UsesInjectedServicesWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			WorldPatrolArea area = CreateRouteArea().Area;
			RecordingTransportBy3dMapServices services = new RecordingTransportBy3dMapServices();
			TransportBy3dMap operation = new TransportBy3dMap(context, area, "咖啡店", services);
			OperationRoundResult back = await operation.BackAtFirst().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult openMap = operation.OpenMap();
			OperationRoundResult chooseArea = operation.ChooseArea();
			OperationRoundResult expand = operation.ExpandSubArea();
			OperationRoundResult chooseSub = operation.ChooseSubArea();
			OperationRoundResult openFilter = operation.OpenFilter();
			OperationRoundResult chooseFilter = operation.ChooseFilter();
			OperationRoundResult closeFilter = operation.CloseFilter();
			OperationRoundResult scale = operation.ClickMiniScale();
			OperationRoundResult initSearch = operation.InitTransportPointSearch();
			OperationRoundResult search = operation.SearchTransportPointLoop();
			OperationRoundResult closePopup = operation.CloseAreaInfoPopup();
			// 点击前往改为直接调用框架 RoundByFindAndClickArea，不再经过注入的 services；
			// 本用例没有附加控制器/截图，因此这一步会因"未获取截图"而 Retry。
			OperationRoundResult go = operation.ClickGo();
			OperationRoundResult wait = await operation.BackAtLast().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(back.IsSuccess);
			Assert.Equal(OperationRoundResultKind.Wait, openMap.Kind);
			Assert.True(chooseArea.IsSuccess);
			Assert.True(expand.IsSuccess);
			Assert.True(chooseSub.IsSuccess);
			Assert.Equal(OperationRoundResultKind.Retry, openFilter.Kind);
			Assert.True(chooseFilter.IsSuccess);
			Assert.True(closeFilter.IsSuccess);
			Assert.True(scale.IsSuccess);
			Assert.True(initSearch.IsSuccess);
			Assert.True(search.IsSuccess);
			Assert.True(closePopup.IsSuccess);
			Assert.Equal(OperationRoundResultKind.Retry, go.Kind);
			Assert.True(wait.IsSuccess);
			Assert.Equal<List<string>>(new List<string>(13)
			{
				"back", "open_map", "choose_area:六分街", "expand_sub_area", "choose_sub_area:咖啡店", "open_filter", "choose_filter:传送", "close_filter", "mini_scale", "init_search:咖啡店",
				"search:咖啡店", "close_popup", "wait_world:False"
			}, services.Calls);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void TransportBy3dMap_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = GetNodes<TransportBy3dMap>();
		Assert.Equal(new string[14]
		{
			"初始回到大世界", "打开3D地图", "选择区域", "展开子区域列表", "选择子区域", "打开筛选", "筛选传送点", "关闭筛选", "最小缩放", "初始化传送点搜索",
			"搜索传送点循环", "关闭区域信息弹窗", "点击前往", "等待画面加载"
		}, nodes.Keys);
		Assert.True(nodes["初始回到大世界"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(20, nodes["选择区域"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(6, nodes["选择子区域"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(8, nodes["搜索传送点循环"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Contains(nodes["选择区域"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "初始回到大世界" && edge.Status == "3D地图");
		Assert.Contains(nodes["选择区域"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "选择子区域" && !edge.Success);
		Assert.Contains(nodes["关闭区域信息弹窗"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "搜索传送点循环" && !edge.Success);
		Assert.Contains(nodes["等待画面加载"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "点击前往");
	}

	[Fact]
	public void TransportBy3dMap_FilterNodesWaitForNextScreenshotConfirmation()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			WorldPatrolArea item = CreateRouteArea().Area;
			RecordingTransportBy3dMapServices recordingTransportBy3dMapServices = new RecordingTransportBy3dMapServices
			{
				OpenFilterStatus = "按钮-筛选",
				CloseFilterStatus = "关闭筛选"
			};
			TransportBy3dMap transportBy3dMap = new TransportBy3dMap(context, item, "咖啡店", recordingTransportBy3dMapServices);
			Assert.Equal(OperationRoundResultKind.Retry, transportBy3dMap.OpenFilter().Kind);
			recordingTransportBy3dMapServices.OpenFilterStatus = "标题-标识点筛选";
			Assert.True(transportBy3dMap.OpenFilter().IsSuccess);
			Assert.Equal(OperationRoundResultKind.Wait, transportBy3dMap.CloseFilter().Kind);
			recordingTransportBy3dMapServices.CloseFilterStatus = "3D地图";
			Assert.True(transportBy3dMap.CloseFilter().IsSuccess);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_CloseFilterUpdatesRecognizedScreenState()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (width != 100 || height != 40)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "标识点筛选") };
					result = readOnlyList;
				}
				return result;
			});
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultTransportBy3dMapServices.CloseFilter(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("3D地图", operationResult.Status);
			Assert.Equal("3D地图", zContext.ScreenContext.CurrentScreenName);
			Assert.Empty(recordingController.Clicks);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task TransportBy3dMap_ExecuteAsyncFollowsPythonConfirmationLoops()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(rootDirectory);
			// "按钮-前往" pc_rect 是 [220, 0, 320, 80]，需要一张能完整容纳该区域的截图，
			// 否则裁剪会退化成空区域，命中不到下面伪造的 OCR 结果。
			using RecordingController controller = new RecordingController(new Mat(500, 500, MatType.CV_8UC3, Scalar.Black));
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(controller);
			context.ScreenContext.Reload();
			int clickGoOcrCalls = 0;
			context.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				if (width == 100 && height == 80)
				{
					clickGoOcrCalls++;
					// 点击前往改为直接调用框架的持续补点击语义：第一次识别到"前往"触发点击，
					// 第二次识别不到即视为已跳转，从而结束该节点。
					return clickGoOcrCalls == 1
						? new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 60, 20, "前往") }
						: Array.Empty<OcrMatchResult>();
				}
				return Array.Empty<OcrMatchResult>();
			});
			WorldPatrolArea area = CreateRouteArea().Area;
			ExecutingTransportBy3dMapServices services = new ExecutingTransportBy3dMapServices();
			TransportBy3dMap operation = new TransportBy3dMap(context, area, "咖啡店", services);
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15L));
			OperationResult result = await operation.ExecuteAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(15L));
			Assert.True(result.IsSuccess);
			Assert.Equal("大世界-普通", result.Status);
			Assert.Equal(3, services.OpenMapChecks);
			Assert.Equal(2, services.OpenFilterChecks);
			Assert.Equal(2, services.CloseFilterChecks);
			// 点击前往不再经过注入的 services，真实识别/点击走 ScreenContext + OcrService。
			Assert.Equal(0, services.ClickGoCount);
			Assert.Equal(cts.Token, services.BackCancellationToken);
			Assert.Equal(cts.Token, services.WaitCancellationToken);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_ChooseAreaClicksDifflibBestCandidateInsteadOfFirstLcsCandidate()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteWorldPatrolAreaOrderData(text);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.WorldPatrolService.LoadData();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (width != 100 || height != 300)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[2]
					{
						new OcrMatchResult(0.99, 10, 10, 40, 20, "ABCDEFGHXX"),
						new OcrMatchResult(0.99, 10, 80, 40, 20, "ABCDEFGHIX")
					};
					result = readOnlyList;
				}
				return result;
			});
			WorldPatrolArea targetArea = Assert.Single(zContext.WorldPatrolService.AreaList, (WorldPatrolArea area) => area.AreaName == "三区");
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultTransportBy3dMapServices.ChooseArea(zContext, screen, "ABCDEFGHIJ", targetArea);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(30, 90), Assert.Single(recordingController.Clicks));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_Open3dMapClicksMiniMapAreaWhenPlayerMaskIsVisible()
	{
		string text = CreateTempRoot();
		try
		{
			WriteMiniMapScreenInfo(text);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			using Mat screen = CreateWorldScreenWithMiniMapPlayerMask();
			List<TimeSpan> list = new List<TimeSpan>();
			Action<TimeSpan> sleep = list.Add;
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices(null, null, null, sleep);
			bool condition = defaultTransportBy3dMapServices.Open3dMap(zContext, screen);
			Assert.True(condition);
			Assert.Empty(list);
			int num = 1;
			List<OneDragon.Core.Abstractions.Geometry.Point> list2 = new List<OneDragon.Core.Abstractions.Geometry.Point>(num);
			CollectionsMarshal.SetCount(list2, num);
			CollectionsMarshal.AsSpan(list2)[0] = new OneDragon.Core.Abstractions.Geometry.Point(120, 120);
			Assert.Equal(list2, recordingController.Clicks);
			num = 1;
			List<bool> list3 = new List<bool>(num);
			CollectionsMarshal.SetCount(list3, num);
			CollectionsMarshal.AsSpan(list3)[0] = true;
			Assert.Equal(list3, recordingController.PcAltClicks);
			num = 1;
			List<string> list4 = new List<string>(num);
			CollectionsMarshal.SetCount(list4, num);
			CollectionsMarshal.AsSpan(list4)[0] = "minimap";
			Assert.Equal<List<string>>(list4, recordingController.GamepadActions);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_Open3dMapDoesNotClickWhenMiniMapMaskIsMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteMiniMapScreenInfo(text);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			using Mat screen = new Mat(240, 240, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			bool condition = defaultTransportBy3dMapServices.Open3dMap(zContext, screen);
			Assert.False(condition);
			Assert.Empty(recordingController.Clicks);
			Assert.Empty(recordingController.GamepadActions);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_OnlyOcrAndAreaRecognitionClicksUsePythonPreDelay()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteAreaInfoCloseTemplate(text);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				if (width == 100 && height == 300)
				{
					return new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 60, 20, "咖啡店") };
				}
				if (width == 120 && height == 300)
				{
					return new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 60, 20, "传送") };
				}
				return (width == 100 && height == 80) ? ((IReadOnlyList<OcrMatchResult>)new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 60, 20, "前往") }) : ((IReadOnlyList<OcrMatchResult>)Array.Empty<OcrMatchResult>());
			});
			List<TimeSpan> list = new List<TimeSpan>();
			TimeSpan? clickPreDelay = TimeSpan.FromMilliseconds(300L);
			Action<TimeSpan> sleep = list.Add;
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices(null, null, clickPreDelay, sleep);
			using Mat screen = Create3dMapScreenWithAreaInfoClose();
			WorldPatrolEntry entry = new WorldPatrolEntry("城市", "city");
			WorldPatrolArea targetArea = new WorldPatrolArea(entry, "咖啡店", "coffee");
			Assert.True(defaultTransportBy3dMapServices.ChooseArea(zContext, screen, "咖啡店", targetArea).IsSuccess);
			Assert.True(defaultTransportBy3dMapServices.ExpandSubArea(zContext, screen).IsSuccess);
			Assert.True(defaultTransportBy3dMapServices.ChooseSubArea(zContext, screen, "咖啡店").IsSuccess);
			Assert.True(defaultTransportBy3dMapServices.OpenFilter(zContext, screen).IsSuccess);
			Assert.True(defaultTransportBy3dMapServices.ChooseFilter(zContext, screen, "传送").IsSuccess);
			Assert.True(defaultTransportBy3dMapServices.CloseFilter(zContext, screen).IsSuccess);
			defaultTransportBy3dMapServices.CloseAreaInfoPopup(zContext, screen);
			zContext.OcrService.ClearCache();
			OperationResult operationResult = defaultTransportBy3dMapServices.ClickGo(zContext, screen);
			Assert.True(operationResult.IsSuccess, operationResult.Status);
			Assert.Equal(4, list.Count);
			Assert.All(list, delegate(TimeSpan delay)
			{
				Assert.Equal(TimeSpan.FromMilliseconds(300L), delay);
			});
			Assert.Equal(8, recordingController.Clicks.Count);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_ClosePopupAndGoRequirePythonAreaRecognition()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher((int _, int _) => Array.Empty<OcrMatchResult>());
			List<TimeSpan> list = new List<TimeSpan>();
			TimeSpan? clickPreDelay = TimeSpan.FromMilliseconds(300L);
			Action<TimeSpan> sleep = list.Add;
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices(null, null, clickPreDelay, sleep);
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			defaultTransportBy3dMapServices.CloseAreaInfoPopup(zContext, screen);
			OperationResult operationResult = defaultTransportBy3dMapServices.ClickGo(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("找不到 按钮-前往", operationResult.Status);
			Assert.Empty(recordingController.Clicks);
			Assert.Empty(list);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_ChooseAreaUsesConfiguredOcrRegionAndPythonScrollDirection()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteWorldPatrolAreaOrderData(text);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.WorldPatrolService.LoadData();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (width != 100 || height != 300)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 40, 20, "一区") };
					result = readOnlyList;
				}
				return result;
			});
			WorldPatrolArea worldPatrolArea = Assert.Single(zContext.WorldPatrolService.AreaList, (WorldPatrolArea area) => area.AreaName == "三区");
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultTransportBy3dMapServices.ChooseArea(zContext, screen, worldPatrolArea.AreaName, worldPatrolArea);
			Assert.False(operationResult.IsSuccess);
			var (actual, actual2) = Assert.Single(recordingController.Drags);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(50, 150), actual2);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(50, -250), actual);
			Assert.Empty(recordingController.Clicks);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_ChooseAreaUsesTranslatedAreaOrderForPythonScrollDirection()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteWorldPatrolAreaOrderData(text);
			WriteGameLanguage(text, "en");
			WriteGameTextCatalog(text, "en", "一区", "Zone One");
			AppendGameTextCatalog(text, "en", "三区", "Zone Three");
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.WorldPatrolService.LoadData();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (width != 100 || height != 300)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 40, 20, "Zone One") };
					result = readOnlyList;
				}
				return result;
			});
			WorldPatrolArea worldPatrolArea = Assert.Single(zContext.WorldPatrolService.AreaList, (WorldPatrolArea area) => area.AreaName == "三区");
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultTransportBy3dMapServices.ChooseArea(zContext, screen, worldPatrolArea.AreaName, worldPatrolArea);
			Assert.False(operationResult.IsSuccess);
			var (actual, actual2) = Assert.Single(recordingController.Drags);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(50, 150), actual2);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(50, -250), actual);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { "cn", "zh", "六分街" })]
	[InlineData(new object[] { "en", "en", "Sixth Street" })]
	public void DefaultTransportBy3dMapServices_UsesCurrentGameLanguageForAreaOcr(string gameLanguage, string catalogLanguage, string ocrText)
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteWorldPatrolAreaData(text);
			WriteGameLanguage(text, gameLanguage);
			WriteGameTextCatalog(text, catalogLanguage, "六分街", ocrText);
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.WorldPatrolService.LoadData();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (width != 100 || height != 300)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, ocrText) };
					result = readOnlyList;
				}
				return result;
			});
			WorldPatrolArea worldPatrolArea = Assert.Single(zContext.WorldPatrolService.AreaList, (WorldPatrolArea area) => area.AreaName == "六分街");
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultTransportBy3dMapServices.ChooseArea(zContext, screen, worldPatrolArea.AreaName, worldPatrolArea);
			Assert.True(operationResult.IsSuccess, operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(50, 20), Assert.Single(recordingController.Clicks));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_UsesCurrentGameLanguageForTransportPointOcr()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteTransportPointTemplate(text);
			WriteTransportPointAreaData(text);
			WriteGameLanguage(text, "en");
			WriteGameTextCatalog(text, "en", "旧点", "Old Point");
			using Mat mat = Create3dMapScreenWithTransportIcon();
			using RecordingController recordingController = new RecordingController(mat);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.WorldPatrolService.LoadData();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				if (width == 100 && height == 80)
				{
					return new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 40, 20, "前往") };
				}
				IReadOnlyList<OcrMatchResult> result;
				if (width != 200 || height != 60)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "Old Point") };
					result = readOnlyList;
				}
				return result;
			});
			WorldPatrolArea targetArea = Assert.Single(zContext.WorldPatrolService.AreaList);
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices(TimeSpan.Zero, (int _) => 0);
			Assert.True(defaultTransportBy3dMapServices.InitTransportPointSearch(zContext, targetArea, "目标点").IsSuccess);
			OperationResult operationResult = defaultTransportBy3dMapServices.SearchTransportPoint(zContext, mat, "目标点", CancellationToken.None);
			Assert.False(operationResult.IsSuccess);
			var (actual, actual2) = Assert.Single(recordingController.Drags);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(200, 200), actual2);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(-100, 200), actual);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_UsesCurrentGameLanguageForScreenRecognition()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteGameLanguage(text, "en");
			WriteGameTextCatalog(text, "en", "标识点筛选", "Marker Filter");
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (width != 100 || height != 40)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "Marker Filter") };
					result = readOnlyList;
				}
				return result;
			});
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultTransportBy3dMapServices.OpenFilter(zContext, screen);
			Assert.True(operationResult.IsSuccess, operationResult.Status);
			Assert.Equal("标题-标识点筛选", operationResult.Status);
			Assert.Empty(recordingController.Clicks);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_UsesCurrentGameLanguageForFilterOcr()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteGameLanguage(text, "en");
			WriteGameTextCatalog(text, "en", "传送", "Teleport");
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (width != 120 || height != 300)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "Teleport") };
					result = readOnlyList;
				}
				return result;
			});
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices();
			using Mat screen = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultTransportBy3dMapServices.ChooseFilter(zContext, screen, "传送");
			Assert.True(operationResult.IsSuccess, operationResult.Status);
			Assert.Equal("传送", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(290, 20), Assert.Single(recordingController.Clicks));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultTransportBy3dMapServices_SearchTransportPointUsesIconRecognitionAndDirectionalDrag()
	{
		string text = CreateTempRoot();
		try
		{
			Write3dMapScreenInfo(text);
			WriteTransportPointTemplate(text);
			WriteTransportPointAreaData(text);
			using Mat mat = Create3dMapScreenWithTransportIcon();
			using RecordingController recordingController = new RecordingController(mat);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(recordingController);
			zContext.ScreenContext.Reload();
			zContext.WorldPatrolService.LoadData();
			zContext.OcrService.Matcher = new SizeAwareOcrMatcher(delegate(int width, int height)
			{
				if (width == 100 && height == 80)
				{
					return new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 40, 20, "前往") };
				}
				return (width == 200 && height == 60) ? ((IReadOnlyList<OcrMatchResult>)new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "旧点") }) : ((IReadOnlyList<OcrMatchResult>)Array.Empty<OcrMatchResult>());
			});
			WorldPatrolArea targetArea = Assert.Single(zContext.WorldPatrolService.AreaList);
			DefaultTransportBy3dMapServices defaultTransportBy3dMapServices = new DefaultTransportBy3dMapServices(TimeSpan.Zero, (int _) => 0);
			Assert.True(defaultTransportBy3dMapServices.InitTransportPointSearch(zContext, targetArea, "目标点").IsSuccess);
			OperationResult operationResult = defaultTransportBy3dMapServices.SearchTransportPoint(zContext, mat, "目标点", CancellationToken.None);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(146, 146), Assert.Single(recordingController.Clicks));
			Assert.Contains(false, (IEnumerable<bool>)recordingController.ScreenshotIndependentFlags);
			var (actual, actual2) = Assert.Single(recordingController.Drags);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(200, 200), actual2);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(-100, 200), actual);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolRunRoute_RunsMoveAndBattleBranches()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			var (area, route) = CreateRouteArea();
			route.AddMoveOperation(new WorldPatrolPoint(10, 0));
			route.AddMoveOperation(new WorldPatrolPoint(20, 0));
			RecordingRunRouteServices services = new RecordingRunRouteServices(area, route)
			{
				MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0),
				NextPosition = new WorldPatrolPoint(10, 0)
			};
			WorldPatrolRunRoute operation = new WorldPatrolRunRoute(context, route, new WorldPatrolConfig(), 0, isRestarted: false, services);
			Assert.True((await operation.BackAtFirst().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(operation.SetStartIdx().IsSuccess);
			OperationRoundResult move = operation.RunOp();
			Assert.Equal(OperationRoundResultKind.Wait, move.Kind);
			Assert.Equal(1, operation.CurrentIdx);
			Assert.Equal(new WorldPatrolPoint(10, 0), operation.CurrentPos);
			Assert.Contains("stop_forward", (IEnumerable<string>)services.Calls);
			Assert.Contains("switch_best", (IEnumerable<string>)services.Calls);
			Assert.Contains("start_forward", (IEnumerable<string>)services.Calls);
			services.MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: false, null);
			OperationRoundResult enterBattle = operation.RunOp();
			Assert.True(enterBattle.IsSuccess);
			Assert.Equal("进入战斗", enterBattle.Status);
			Assert.True(operation.InitAutoBattle().IsSuccess);
			Assert.True(operation.InBattle);
			OperationRoundResult afterBattle = operation.AfterAutoBattle();
			Assert.True(afterBattle.IsSuccess);
			Assert.False(operation.InBattle);
			operation.HandlePause();
			operation.HandleResume();
			Assert.Contains("init_auto:全配队通用", (IEnumerable<string>)services.Calls);
			Assert.Contains("start_auto", (IEnumerable<string>)services.Calls);
			Assert.Contains("stop_auto", (IEnumerable<string>)services.Calls);
			Assert.Contains("wait_after_auto_stop", (IEnumerable<string>)services.Calls);
			int stopAutoIndex = services.Calls.FindLastIndex((string call) => call == "stop_auto");
			int waitAfterStopIndex = services.Calls.IndexOf("wait_after_auto_stop");
			int afterBattleSwitchIndex = services.Calls.FindLastIndex((string call) => call == "switch_best");
			int afterBattleTurnIndex = services.Calls.FindLastIndex((string call) => call == "turn_vertical:300");
			Assert.True(stopAutoIndex < waitAfterStopIndex);
			Assert.True(waitAfterStopIndex < afterBattleSwitchIndex);
			Assert.True(afterBattleSwitchIndex < afterBattleTurnIndex);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolRunRoute_ExecuteAsyncRunsCompleteMoveRoute()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			var (area, route) = CreateRouteArea();
			route.AddMoveOperation(new WorldPatrolPoint(0, 0));
			RecordingRunRouteServices services = new RecordingRunRouteServices(area, route)
			{
				MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0),
				NextPosition = new WorldPatrolPoint(0, 0)
			};
			WorldPatrolRunRoute operation = new WorldPatrolRunRoute(context, route, new WorldPatrolConfig(), 0, isRestarted: false, services);
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5L));
			OperationResult result = await operation.ExecuteAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(5L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部指令已完成", result.Status);
			Assert.Equal(1, operation.CurrentIdx);
			Assert.Contains("back", (IEnumerable<string>)services.Calls);
			Assert.Contains("transport", (IEnumerable<string>)services.Calls);
			Assert.Contains("start_forward", (IEnumerable<string>)services.Calls);
			Assert.Equal(cts.Token, services.BackCancellationToken);
			Assert.Equal(cts.Token, services.TransportCancellationToken);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolRunRoute_ExecuteAsyncRunsCompleteMoveBattleResumeStateGraph()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			var (area, route) = CreateRouteArea();
			route.AddMoveOperation(new WorldPatrolPoint(0, 0));
			RecordingRunRouteServices services = new RecordingRunRouteServices(area, route)
			{
				BattleCheck = new WorldPatrolBattleCheckResult(InBattle: false),
				HasInteractionResult = false,
				NextPosition = new WorldPatrolPoint(0, 0)
			};
			services.MiniMapSequence.Enqueue(new WorldPatrolMiniMapSnapshot(PlayMaskFound: false, null));
			services.MiniMapSequence.Enqueue(new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0));
			services.MiniMapSequence.Enqueue(new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0));
			WorldPatrolRunRoute operation = new WorldPatrolRunRoute(context, route, new WorldPatrolConfig(), 0, isRestarted: false, services);
			services.Calls.Clear();
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5L));
			OperationResult result = await operation.ExecuteAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(5L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部指令已完成", result.Status);
			Assert.Equal(1, operation.CurrentIdx);
			Assert.Equal<List<string>>(new List<string>(21)
			{
				"back", "transport", "route_pos:0", "stop_forward", "switch_best", "turn_vertical:300", "cut_minimap:False", "stop_forward", "init_auto:全配队通用", "start_auto",
				"check_battle:False", "has_interaction:False", "cut_minimap:True", "stop_auto", "wait_after_auto_stop", "switch_best", "turn_vertical:300", "cut_minimap:True", "calculate_position:0,0", "start_forward",
				"stop_forward"
			}, services.Calls);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void DefaultWorldPatrolRunRouteServices_SwitchToBestAgentUsesAutoBattleContext()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateZPcContext(out buttons);
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ASTRA_YAO.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		DefaultWorldPatrolRunRouteServices defaultWorldPatrolRunRouteServices = new DefaultWorldPatrolRunRouteServices(zContext);
		defaultWorldPatrolRunRouteServices.SwitchToBestAgentForMoving(zContext);
		Assert.Equal("安比", zContext.AutoBattleContext.AgentContext.Team.Agents[0].Agent.AgentName);
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = "space";
		Assert.Equal<List<string>>(list, buttons.Taps);
	}

	[Fact]
	public void DefaultWorldPatrolRunRouteServices_SwitchNextForUnstuckUpdatesAutoBattleTeam()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateZPcContext(out buttons);
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		DefaultWorldPatrolRunRouteServices defaultWorldPatrolRunRouteServices = new DefaultWorldPatrolRunRouteServices(zContext);
		defaultWorldPatrolRunRouteServices.SwitchNextForUnstuck(zContext);
		Assert.Equal("妮可", zContext.AutoBattleContext.AgentContext.Team.Agents[0].Agent.AgentName);
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = "space";
		Assert.Equal<List<string>>(list, buttons.Taps);
	}

	[Fact]
	public void WorldPatrolRunRoute_ConsumesAsynchronousBattleEndResultOnNextRound()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			(WorldPatrolArea Area, WorldPatrolRoute Route) tuple = CreateRouteArea();
			WorldPatrolArea item = tuple.Area;
			WorldPatrolRoute item2 = tuple.Route;
			RecordingRunRouteServices recordingRunRouteServices = new RecordingRunRouteServices(item, item2)
			{
				MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: false, null),
				BattleCheck = new WorldPatrolBattleCheckResult(InBattle: false)
			};
			WorldPatrolRunRoute worldPatrolRunRoute = new WorldPatrolRunRoute(zContext, item2, new WorldPatrolConfig(), 0, isRestarted: false, recordingRunRouteServices);
			Assert.True(worldPatrolRunRoute.InitAutoBattle().IsSuccess);
			OperationRoundResult operationRoundResult = worldPatrolRunRoute.AutoBattle();
			zContext.AutoBattleContext.LastCheckEndResult = "战斗结束-完成";
			OperationRoundResult operationRoundResult2 = worldPatrolRunRoute.AutoBattle();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.True(operationRoundResult2.IsSuccess);
			Assert.Equal("战斗结束-完成", operationRoundResult2.Status);
			Assert.Equal(1, recordingRunRouteServices.Calls.Count((string call) => call == "stop_auto"));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void WorldPatrolRunRoute_FailsWhenPositionMissingTooLong()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			var (area, worldPatrolRoute) = CreateRouteArea();
			worldPatrolRoute.AddMoveOperation(new WorldPatrolPoint(10, 0));
			RecordingRunRouteServices recordingRunRouteServices = new RecordingRunRouteServices(area, worldPatrolRoute)
			{
				MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0),
				NextPosition = null
			};
			WorldPatrolRunRoute worldPatrolRunRoute = new WorldPatrolRunRoute(context, worldPatrolRoute, new WorldPatrolConfig(), 0, isRestarted: false, recordingRunRouteServices);
			Assert.True(worldPatrolRunRoute.SetStartIdx().IsSuccess);
			OperationRoundResult operationRoundResult = worldPatrolRunRoute.RunOp();
			recordingRunRouteServices.Advance(TimeSpan.FromSeconds(14L));
			OperationRoundResult operationRoundResult2 = worldPatrolRunRoute.RunOp();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.True(operationRoundResult2.IsFail);
			Assert.Equal("坐标计算失败，重启当前路线", operationRoundResult2.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void WorldPatrolRunRoute_FiltersLargePositionJumpAsNoPosition()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			var (area, worldPatrolRoute) = CreateRouteArea();
			worldPatrolRoute.AddMoveOperation(new WorldPatrolPoint(10, 0));
			RecordingRunRouteServices recordingRunRouteServices = new RecordingRunRouteServices(area, worldPatrolRoute)
			{
				MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0, 20),
				NextPosition = new WorldPatrolPoint(1000, 1000)
			};
			WorldPatrolRunRoute worldPatrolRunRoute = new WorldPatrolRunRoute(context, worldPatrolRoute, new WorldPatrolConfig(), 0, isRestarted: false, recordingRunRouteServices);
			Assert.True(worldPatrolRunRoute.SetStartIdx().IsSuccess);
			OperationRoundResult operationRoundResult = worldPatrolRunRoute.RunOp();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Contains("坐标计算失败", operationRoundResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Rect(-70, -70, 70, 70), recordingRunRouteServices.LastPossibleRect);
			Assert.Equal(new WorldPatrolPoint(0, 0), worldPatrolRunRoute.CurrentPos);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void WorldPatrolRunRoute_BacktracksBeforeUnstuckWhenPositionIsStuck()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			var (area, worldPatrolRoute) = CreateRouteArea();
			worldPatrolRoute.AddMoveOperation(new WorldPatrolPoint(10, 0));
			worldPatrolRoute.AddMoveOperation(new WorldPatrolPoint(20, 0));
			RecordingRunRouteServices recordingRunRouteServices = new RecordingRunRouteServices(area, worldPatrolRoute)
			{
				MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0),
				NextPosition = new WorldPatrolPoint(0, 0)
			};
			WorldPatrolRunRoute worldPatrolRunRoute = new WorldPatrolRunRoute(context, worldPatrolRoute, new WorldPatrolConfig(), 0, isRestarted: false, recordingRunRouteServices);
			Assert.True(worldPatrolRunRoute.SetStartIdx().IsSuccess);
			OperationRoundResult operationRoundResult = worldPatrolRunRoute.RunOp();
			recordingRunRouteServices.Advance(TimeSpan.FromSeconds(3L));
			OperationRoundResult operationRoundResult2 = worldPatrolRunRoute.RunOp();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult2.Kind);
			Assert.DoesNotContain((IEnumerable<string>)recordingRunRouteServices.Calls, (Predicate<string>)((string call) => call.StartsWith("unstuck:", StringComparison.Ordinal)));
			Assert.Contains("start_forward", (IEnumerable<string>)recordingRunRouteServices.Calls);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void WorldPatrolRunRoute_FailsWhenBattleUiDisappearsTooLong()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			(WorldPatrolArea Area, WorldPatrolRoute Route) tuple = CreateRouteArea();
			WorldPatrolArea item = tuple.Area;
			WorldPatrolRoute item2 = tuple.Route;
			RecordingRunRouteServices recordingRunRouteServices = new RecordingRunRouteServices(item, item2)
			{
				MiniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: false, null),
				BattleCheck = new WorldPatrolBattleCheckResult(InBattle: false),
				HasInteractionResult = false
			};
			WorldPatrolRunRoute worldPatrolRunRoute = new WorldPatrolRunRoute(context, item2, new WorldPatrolConfig
			{
				UiDisappearSeconds = 1
			}, 0, isRestarted: false, recordingRunRouteServices);
			Assert.Equal(OperationRoundResultKind.Wait, worldPatrolRunRoute.AutoBattle().Kind);
			recordingRunRouteServices.Advance(TimeSpan.FromSeconds(2L));
			OperationRoundResult operationRoundResult = worldPatrolRunRoute.AutoBattle();
			Assert.True(operationRoundResult.IsFail);
			Assert.Equal("疑似界面消失卡死", operationRoundResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void WorldPatrolRunRoute_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = GetNodes<WorldPatrolRunRoute>();
		Assert.Equal(new string[7] { "初始回到大世界", "传送", "设置起始坐标", "运行指令", "初始化自动战斗", "自动战斗", "自动战斗结束" }, nodes.Keys);
		Assert.True(nodes["初始回到大世界"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.True(nodes["自动战斗"].GetCustomAttribute<OperationNodeAttribute>().Mute);
		Assert.Contains(nodes["设置起始坐标"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "初始回到大世界" && edge.Status == "DEBUG");
		Assert.Contains(nodes["初始化自动战斗"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "运行指令" && edge.Status == "进入战斗");
		Assert.Contains(nodes["自动战斗结束"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "自动战斗");
	}

	[Fact]
	public void WorldPatrolAppOperation_DeclaresPythonOuterFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = GetNodes<WorldPatrolAppOperation>();
		Assert.Equal(new string[10] { "初始化", "开始前返回大世界", "前往绳网", "停止追踪", "停止追踪后返回大世界", "执行路线", "轮次结束判定", "传送回录像店", "轮间等待", "准备下一轮" }, nodes.Keys);
		Assert.True(nodes["初始化"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(nodes["执行路线"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "前往绳网" && edge.Status == "无任务追踪");
		Assert.Contains(nodes["执行路线"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "准备下一轮" && edge.Status == "进入下一轮");
		Assert.Contains(nodes["轮次结束判定"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "执行路线" && edge.Status == "路线已全部完成");
		Assert.Contains(nodes["传送回录像店"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "轮次结束判定" && edge.Status == "进入轮间等待");
	}

	[Fact]
	public async Task WorldPatrolAppOperation_EmptyRouteListStillStartsPythonRoundTiming()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			WorldPatrolRunRecord record = new WorldPatrolRunRecord();
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig
			{
				DailyLoopCount = 1
			}, record, context.WorldPatrolService, new RecordingRouteRunner());
			Assert.True(operation.InitializeWorldPatrol().IsSuccess);
			Assert.Null(record.RoundStartTime);
			OperationRoundResult result = await operation.RunRouteAsync();
			Assert.True(result.IsSuccess);
			Assert.Equal("路线已全部完成", result.Status);
			Assert.NotNull(record.RoundStartTime);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolAppOperation_ExecuteAsyncRunsCompletePythonOuterFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			WriteMiniMapScreenInfo(rootDirectory);
			using Mat screen = CreateWorldScreenWithMiniMapPlayerMask();
			using RecordingController controller = new RecordingController(screen);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(controller);
			context.ScreenContext.Reload();
			context.OcrService.Matcher = CreateNoTrackingOcrMatcher();
			context.WorldPatrolService.LoadData();
			WorldPatrolArea area = Assert.Single(context.WorldPatrolService.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(context.WorldPatrolService.SaveWorldPatrolRoute(route));
			RecordingRouteRunner runner = new RecordingRouteRunner();
			WorldPatrolRunRecord record = new WorldPatrolRunRecord();
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig
			{
				DailyLoopCount = 1
			}, record, context.WorldPatrolService, runner);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(5L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部完成", result.Status);
			Assert.Equal(1, runner.RunCount);
			Assert.Equal(1, record.CompletedRounds);
			Assert.Equal<List<string>>(new List<string>(1) { "sixth_street_coffee_shop_1" }, record.Finished);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolApp_DefaultFlowExecutesRealOuterOperation()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			WriteMiniMapScreenInfo(rootDirectory);
			using Mat screen = CreateWorldScreenWithMiniMapPlayerMask();
			using RecordingController controller = new RecordingController(screen);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(controller);
			context.ScreenContext.Reload();
			context.OcrService.Matcher = CreateNoTrackingOcrMatcher();
			context.WorldPatrolService.LoadData();
			WorldPatrolArea area = Assert.Single(context.WorldPatrolService.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(context.WorldPatrolService.SaveWorldPatrolRoute(route));
			RecordingRouteRunner runner = new RecordingRouteRunner();
			WorldPatrolRunRecord record = new WorldPatrolRunRecord();
			WorldPatrolApp app = new WorldPatrolApp(context, new WorldPatrolConfig
			{
				DailyLoopCount = 2,
				LoopIntervalSeconds = 0
			}, record, null, (CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: true, "游戏窗口已就绪")), runner, (TimeSpan _, CancellationToken _) => Task.CompletedTask);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部完成", result.Status);
			Assert.Equal(2, runner.RunCount);
			Assert.Equal(2, record.CompletedRounds);
			Assert.Equal(2, record.CurrentRound);
			Assert.Equal<List<string>>(new List<string>(1) { "sixth_street_coffee_shop_1" }, record.Finished);
			Assert.Equal(1, record.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolAppOperation_ExecuteAsyncRunsTwoRoundsAndReturnsToVideoShopBetweenRounds()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			WriteMiniMapScreenInfo(rootDirectory);
			using Mat screen = CreateWorldScreenWithMiniMapPlayerMask();
			using RecordingController controller = new RecordingController(screen);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(controller);
			context.ScreenContext.Reload();
			context.OcrService.Matcher = CreateNoTrackingOcrMatcher();
			context.WorldPatrolService.LoadData();
			WorldPatrolArea area = Assert.Single(context.WorldPatrolService.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(context.WorldPatrolService.SaveWorldPatrolRoute(route));
			RecordingRouteRunner runner = new RecordingRouteRunner();
			WorldPatrolRunRecord record = new WorldPatrolRunRecord();
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig
			{
				DailyLoopCount = 2,
				LoopIntervalSeconds = 0
			}, record, context.WorldPatrolService, runner);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(5L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部完成", result.Status);
			Assert.Equal(2, runner.RunCount);
			Assert.Equal(2, record.CompletedRounds);
			Assert.Equal(2, record.CurrentRound);
			Assert.Equal<List<string>>(new List<string>(1) { "sixth_street_coffee_shop_1" }, record.Finished);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolAppOperation_PauseResumeAndStopReachActiveRoute()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			WriteMiniMapScreenInfo(rootDirectory);
			using Mat screen = CreateWorldScreenWithMiniMapPlayerMask();
			using RecordingController controller = new RecordingController(screen);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(controller);
			context.ScreenContext.Reload();
			context.OcrService.Matcher = CreateNoTrackingOcrMatcher();
			context.WorldPatrolService.LoadData();
			WorldPatrolArea area = Assert.Single(context.WorldPatrolService.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(context.WorldPatrolService.SaveWorldPatrolRoute(route));
			BlockingRouteRunner runner = new BlockingRouteRunner();
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig(), new WorldPatrolRunRecord(), context.WorldPatrolService, runner);
			Task<OperationResult> executeTask = operation.ExecuteAsync();
			await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5L));
			operation.HandlePause();
			operation.HandleResume();
			operation.HandleStop();
			runner.Complete(new OperationResult(IsSuccess: true, route.FullId));
			Assert.True((await executeTask.WaitAsync(TimeSpan.FromSeconds(5L))).IsSuccess);
			Assert.Equal(1, runner.PauseCount);
			Assert.Equal(1, runner.ResumeCount);
			Assert.True(runner.StopCount >= 1);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolAppOperation_CancellationStopsActiveRoute()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			WriteMiniMapScreenInfo(rootDirectory);
			using Mat screen = CreateWorldScreenWithMiniMapPlayerMask();
			using RecordingController controller = new RecordingController(screen);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(controller);
			context.ScreenContext.Reload();
			context.OcrService.Matcher = CreateNoTrackingOcrMatcher();
			context.WorldPatrolService.LoadData();
			WorldPatrolArea area = Assert.Single(context.WorldPatrolService.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(context.WorldPatrolService.SaveWorldPatrolRoute(route));
			BlockingRouteRunner runner = new BlockingRouteRunner();
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig(), new WorldPatrolRunRecord(), context.WorldPatrolService, runner);
			using CancellationTokenSource cts = new CancellationTokenSource();
			Task<OperationResult> executeTask = operation.ExecuteAsync(cts.Token);
			await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5L));
			cts.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executeTask);
			Assert.True(runner.StopCount >= 1);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolAppOperation_UiDisappearRestartUsesInjectedEnterGame()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			using RecordingController controller = new RecordingController();
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(controller);
			WorldPatrolService service = new WorldPatrolService(context.Environment);
			service.LoadData();
			WorldPatrolArea area = Assert.Single(service.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(service.SaveWorldPatrolRoute(route));
			ScriptedRouteRunner runner = new ScriptedRouteRunner(new OperationResult(IsSuccess: false, "疑似界面消失卡死"));
			int enterCount = 0;
			CancellationToken enterToken = default(CancellationToken);
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig
			{
				UiDisappearAction = "restart_and_skip"
			}, new WorldPatrolRunRecord(), service, runner, delegate(CancellationToken cancellationToken)
			{
				enterCount++;
				enterToken = cancellationToken;
				return Task.FromResult(new OperationResult(IsSuccess: true, "进入游戏"));
			}, (TimeSpan _, CancellationToken _) => Task.CompletedTask);
			Assert.True(operation.InitializeWorldPatrol().IsSuccess);
			OperationRoundResult result = await operation.RunRouteAsync();
			Assert.Equal(OperationRoundResultKind.Wait, result.Kind);
			Assert.Contains("已重开游戏并跳过路线", result.Status);
			Assert.Equal(1, enterCount);
			Assert.Equal(CancellationToken.None, enterToken);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolAppOperation_RunsRoutesAndRecordsFinishedRoutes()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			WorldPatrolService service = new WorldPatrolService(context.Environment);
			service.LoadData();
			WorldPatrolArea area = Assert.Single(service.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(service.SaveWorldPatrolRoute(route));
			RecordingRouteRunner runner = new RecordingRouteRunner();
			WorldPatrolRunRecord record = new WorldPatrolRunRecord();
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig(), record, service, runner);
			Assert.True(operation.InitializeWorldPatrol().IsSuccess);
			Assert.Equal(OperationRoundResultKind.Wait, (await operation.RunRouteAsync()).Kind);
			Assert.Equal("路线已全部完成", (await operation.RunRouteAsync()).Status);
			OperationRoundResult result = operation.DecideNextRound();
			Assert.True(result.IsSuccess);
			Assert.Equal("全部完成", result.Status);
			Assert.Equal(1, runner.RunCount);
			Assert.Equal<List<string>>(new List<string>(1) { "sixth_street_coffee_shop_1" }, record.Finished);
			Assert.Equal(1, record.RoutesPerRound);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task WorldPatrolAppOperation_RetriesRestartRouteFailureAndContinuesToNextRoute()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteWorldPatrolAreaData(rootDirectory);
			WriteWorldPatrolAutoBattleConfig(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			WorldPatrolService service = new WorldPatrolService(context.Environment);
			service.LoadData();
			WorldPatrolArea area = Assert.Single(service.AreaList, (WorldPatrolArea item) => item.FullId == "sixth_street_coffee_shop");
			WorldPatrolRoute route = new WorldPatrolRoute(area, "咖啡店", 1);
			route.AddMoveOperation(new WorldPatrolPoint(1, 1));
			Assert.True(service.SaveWorldPatrolRoute(route));
			ScriptedRouteRunner runner = new ScriptedRouteRunner(new OperationResult(IsSuccess: false, "坐标计算失败，重启当前路线"), new OperationResult(IsSuccess: false, "坐标计算失败，重启当前路线"));
			WorldPatrolRunRecord record = new WorldPatrolRunRecord();
			WorldPatrolAppOperation operation = new WorldPatrolAppOperation(context, new WorldPatrolConfig
			{
				RouteRetryTimes = 1
			}, record, service, runner);
			Assert.True(operation.InitializeWorldPatrol().IsSuccess);
			OperationRoundResult result = await operation.RunRouteAsync();
			Assert.Equal(OperationRoundResultKind.Wait, result.Kind);
			Assert.Empty(record.Finished);
			Assert.Equal(new List<bool>(2) { false, true }, runner.RestartFlags);
			Assert.Contains("重试 1 次后仍卡住", result.Status);
			Assert.Contains("sixth_street_coffee_shop_1", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static IReadOnlyDictionary<string, MethodInfo> GetNodes<T>()
	{
		return (from method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
	}

	private static (WorldPatrolArea Area, WorldPatrolRoute Route) CreateRouteArea()
	{
		WorldPatrolEntry entry = new WorldPatrolEntry("城市", "city");
		WorldPatrolArea worldPatrolArea = new WorldPatrolArea(entry, "六分街", "sixth_street");
		WorldPatrolArea worldPatrolArea2 = new WorldPatrolArea(entry, "咖啡店", "coffee_shop")
		{
			ParentArea = worldPatrolArea
		};
		int num = 1;
		List<WorldPatrolArea> list = new List<WorldPatrolArea>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = worldPatrolArea2;
		worldPatrolArea.SubAreaList = list;
		WorldPatrolRoute item = new WorldPatrolRoute(worldPatrolArea2, "咖啡店", 1);
		return (Area: worldPatrolArea2, Route: item);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static ZContext CreateZPcContext(out RecordingButtonController buttons)
	{
		buttons = new RecordingButtonController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(buttons), null, buttons, null, null, skipForegroundActivation: true);
		ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(controller);
		return zContext;
	}

	private static void WriteWorldPatrolAreaData(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "map_area_all.yml"), "full_list:\n  - entry_name: 城市\n    entry_id: city\n    area_list:\n      - area_name: 六分街\n        area_id: sixth_street\n        sub_area_list:\n          - area_name: 咖啡店\n            area_id: coffee_shop");
		string text2 = Path.Combine(text, "world_patrol", "city", "sixth_street_coffee_shop");
		Directory.CreateDirectory(text2);
		File.WriteAllBytes(Path.Combine(text2, "road_mask.png"), (ReadOnlySpan<byte>)new byte[3] { 1, 2, 3 });
		File.WriteAllText(Path.Combine(text2, "icon.yml"), "- icon_name: 咖啡店\n  template_id: map_icon_01\n  lm_pos:\n    - 100\n    - 200");
	}

	private static void WriteGameLanguage(string rootDirectory, string language)
	{
		string text = Path.Combine(rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "game_account.yml"), "game_language: " + language + "\n");
	}

	private static void WriteGameTextCatalog(string rootDirectory, string language, string source, string translation)
	{
		string text = Path.Combine(rootDirectory, "assets", "text", "game");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, language + ".po"), $"msgid \"\"\nmsgstr \"\"\n\"Content-Type: text/plain; charset=UTF-8\\\\n\"\n\nmsgid \"{source}\"\nmsgstr \"{translation}\"");
	}

	private static void AppendGameTextCatalog(string rootDirectory, string language, string source, string translation)
	{
		string[] buffer = new string[5];
		buffer[0] = rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "text";
		buffer[3] = "game";
		buffer[4] = language + ".po";
		string path = Path.Combine(buffer);
		File.AppendAllText(path, $"\nmsgid \"{source}\"\nmsgstr \"{translation}\"");
	}

	private static void WriteWorldPatrolAutoBattleConfig(string rootDirectory)
	{
		LinkFlashClassifierModels(rootDirectory);
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "全配队通用.yml"), "scenes:\n  - triggers: [\"自定义-锄大地测试\"]\n    priority: 1\n    interval: 0\n    handlers:\n      - states: \"[自定义-锄大地测试, 0, 1]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-锄大地测试完成\"");
	}

	private static void LinkFlashClassifierModels(string rootDirectory)
	{
		string pathToTarget = Path.Combine(FindWorkspaceRoot(), "assets", "models", "flash_classifier");
		string text = Path.Combine(rootDirectory, "assets", "models");
		string path = Path.Combine(text, "flash_classifier");
		Directory.CreateDirectory(text);
		if (!Directory.Exists(path))
		{
			Directory.CreateSymbolicLink(path, pathToTarget);
		}
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string path = Path.Combine(directoryInfo.FullName, "assets", "models", "flash_classifier");
			if (Directory.Exists(path))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到锄大地测试所需的真实闪光模型目录。");
	}

	private static void WriteMiniMapScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		ScreenSeed.WriteScreens(text, "- screen_id: normal_world\n  screen_name: 大世界\n  pc_alt: true\n  area_list:\n    - area_name: 小地图\n      pc_rect: [0, 0, 240, 240]\n      gamepad_key: minimap\n    - area_name: 任务追踪\n      pc_rect: [0, 0, 240, 240]\n      text: 按自己的步调度过这一天");
	}

	private static SizeAwareOcrMatcher CreateNoTrackingOcrMatcher()
	{
		return new SizeAwareOcrMatcher(delegate(int width, int height)
		{
			IReadOnlyList<OcrMatchResult> result;
			if (width != 240 || height != 240)
			{
				IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
				result = readOnlyList;
			}
			else
			{
				IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 180, 20, "按自己的步调度过这一天") };
				result = readOnlyList;
			}
			return result;
		});
	}

	private static void Write3dMapScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		ScreenSeed.WriteScreens(text, "- screen_id: map_3d\n  screen_name: 3D地图\n  area_list:\n    - area_name: 区域-区域列表\n      pc_rect: [0, 0, 100, 300]\n    - area_name: 区域-子区域列表\n      pc_rect: [120, 0, 220, 300]\n    - area_name: 区域-筛选选项\n      pc_rect: [240, 0, 360, 300]\n    - area_name: 区域-地图\n      pc_rect: [100, 100, 300, 300]\n    - area_name: 按钮-当前子区域\n      pc_rect: [0, 320, 100, 360]\n    - area_name: 标题-标识点筛选\n      id_mark: true\n      pc_rect: [110, 320, 210, 360]\n      text: 标识点筛选\n    - area_name: 按钮-筛选\n      pc_rect: [220, 320, 260, 360]\n    - area_name: 按钮-关闭筛选\n      pc_rect: [270, 320, 310, 360]\n    - area_name: 按钮-区域信息-关闭\n      pc_rect: [320, 320, 360, 360]\n      template_sub_dir: normal_world_investigation\n      template_id: btn_area_info_close\n    - area_name: 按钮-前往\n      pc_rect: [220, 0, 320, 80]\n      text: 前往\n    - area_name: 标题-当前选择传送点\n      pc_rect: [220, 100, 420, 160]");
	}

	private static void WriteAreaInfoCloseTemplate(string rootDirectory)
	{
		string[] buffer = new string[5];
		buffer[0] = rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "normal_world_investigation";
		buffer[4] = "btn_area_info_close";
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		using Mat mat = CreateAreaInfoCloseTemplate();
		using Mat img = new Mat(mat.Rows, mat.Cols, MatType.CV_8UC1, Scalar.White);
		Cv2.ImWrite(Path.Combine(text, "raw.png"), mat);
		Cv2.ImWrite(Path.Combine(text, "mask.png"), img);
	}

	private static Mat Create3dMapScreenWithAreaInfoClose()
	{
		Mat mat = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
		using Mat mat2 = CreateAreaInfoCloseTemplate();
		mat2.CopyTo(new Mat(mat, new OpenCvSharp.Rect(325, 325, mat2.Cols, mat2.Rows)));
		return mat;
	}

	private static Mat CreateAreaInfoCloseTemplate()
	{
		Mat mat = new Mat(12, 12, MatType.CV_8UC3, Scalar.Black);
		Cv2.Line(mat, new OpenCvSharp.Point(1, 1), new OpenCvSharp.Point(10, 10), Scalar.White, 2);
		Cv2.Line(mat, new OpenCvSharp.Point(10, 1), new OpenCvSharp.Point(1, 10), new Scalar(128.0, 64.0, 255.0), 2);
		return mat;
	}

	private static void WriteWorldPatrolAreaOrderData(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "map_area_all.yml"), "full_list:\n  - entry_name: 城市\n    entry_id: city\n    area_list:\n      - area_name: 一区\n        area_id: area_1\n      - area_name: 二区\n        area_id: area_2\n      - area_name: 三区\n        area_id: area_3");
	}

	private static void WriteTransportPointAreaData(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "map_area_all.yml"), "full_list:\n  - entry_name: 城市\n    entry_id: city\n    area_list:\n      - area_name: 测试区\n        area_id: test_area");
		string text2 = Path.Combine(text, "world_patrol", "city", "test_area");
		Directory.CreateDirectory(text2);
		using Mat img = new Mat(160, 160, MatType.CV_8UC1, Scalar.Black);
		Cv2.ImWrite(Path.Combine(text2, "road_mask.png"), img);
		File.WriteAllText(Path.Combine(text2, "icon.yml"), "- icon_name: 旧点\n  template_id: old_icon\n  lm_pos:\n    - 0\n    - 0\n- icon_name: 目标点\n  template_id: target_icon\n  lm_pos:\n    - 100\n    - 0");
	}

	private static void WriteTransportPointTemplate(string rootDirectory)
	{
		string[] buffer = new string[5];
		buffer[0] = rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "map";
		buffer[4] = "3d_map_tp_icon_1";
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		using Mat img = CreateTransportPointIconTemplate();
		Cv2.ImWrite(Path.Combine(text, "raw.png"), img);
	}

	private static Mat Create3dMapScreenWithTransportIcon()
	{
		Mat mat = new Mat(500, 500, MatType.CV_8UC3, Scalar.Black);
		using Mat mat2 = CreateTransportPointIconTemplate();
		mat2.CopyTo(new Mat(mat, new OpenCvSharp.Rect(140, 140, mat2.Cols, mat2.Rows)));
		return mat;
	}

	private static Mat CreateTransportPointIconTemplate()
	{
		Mat mat = new Mat(12, 12, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(1, 1, 4, 4), new Scalar(255.0, 255.0, 255.0), -1);
		Cv2.Circle(mat, new OpenCvSharp.Point(8, 8), 3, new Scalar(128.0, 64.0, 255.0), -1);
		return mat;
	}

	private static Mat CreateWorldScreenWithMiniMapPlayerMask()
	{
		Mat mat = new Mat(240, 240, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
		Cv2.Circle(mat, new OpenCvSharp.Point(70, 70), 10, new Scalar(0.0, 160.0, 255.0), -1);
		return mat;
	}
}
