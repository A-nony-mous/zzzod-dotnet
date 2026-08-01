using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Controller;
using OneDragon.Core.Events;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using YamlDotNet.Serialization;
using Xunit;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class LostVoidAppTests
{
	private sealed class RecordingLostVoidRunner(OperationResult result) : ILostVoidRunner
	{
		public int RunCount { get; private set; }

		public string? LastRegionType { get; private set; }

		public CancellationToken LastCancellationToken { get; private set; }

		public Task<OperationResult> RunLevelAsync(ZContext context, LostVoidConfig config, LostVoidRunRecord runRecord, string regionType, CancellationToken cancellationToken)
		{
			RunCount++;
			LastRegionType = regionType;
			LastCancellationToken = cancellationToken;
			return Task.FromResult(result);
		}
	}

	private sealed class MatrixSelectionController : ControllerBase, IDisposable
	{
		private readonly Mat _screen = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.Black);

		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPoint { get; private set; }

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			LastClickPoint = position;
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return _screen.Clone();
		}

		public void Dispose()
		{
			_screen.Dispose();
		}
	}

	private sealed class MatrixSelectionOcrMatcher : IOcrMatcher
	{
		private static readonly IReadOnlyList<OcrMatchResult> Results = new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 100, 70, 40, 20, "目标配队"),
			new OcrMatchResult(0.99, 100, 70, 40, 20, "主战")
		};

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
			return string.Concat(Results.Select((OcrMatchResult result) => result.Text));
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult result in Results)
			{
				MatchResultList matchResultList = new MatchResultList(onlyBest: false);
				matchResultList.Append(result, autoMerge: false);
				dictionary[result.Text] = matchResultList;
			}
			return dictionary;
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return Results;
		}
	}

	[Theory]
	[InlineData(new object[] { "迷失之地-大世界", false, false, "迷失之地-大世界" })]
	[InlineData(new object[] { "菜单", true, true, "可前往副本画面" })]
	[InlineData(new object[] { "迷失之地-入口-周期", false, true, "可前往快捷手册" })]
	[InlineData(new object[] { "迷失之地-入口-常规", false, false, "未识别初始画面" })]
	[InlineData(new object[] { "大世界-普通", false, true, "可前往快捷手册" })]
	[InlineData(new object[] { null, false, false, "未识别初始画面" })]
	public void InitialScreenStatus_UsesBaselineRoutePriority(string? currentScreen, bool canGoMission, bool canGoCompendium, string expected)
	{
		string actual = LostVoidAppOperation.ResolveInitialScreenStatus(currentScreen, "迷失之地-特遣调查", canGoMission, canGoCompendium);
		Assert.Equal(expected, actual);
	}

	private sealed class CountingLostVoidController : ControllerBase, IZzzControllerActions
	{
		public int InputCount { get; private set; }

		public override bool IsGameWindowReady => true;

		public override bool InitBeforeContextRun() => true;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			InputCount++;
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null) => InputCount++;

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null) => InputCount++;

		public override void InputText(string text) => InputCount++;

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position) => InputCount++;

		public void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false) => InputCount++;

		public void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false) => InputCount++;

		public void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false) => InputCount++;

		public void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false) => InputCount++;

		public void Interact(bool press = false, TimeSpan? pressTime = null, bool release = false) => InputCount++;

		public void TurnByDistance(float distance) => InputCount++;

		protected override Mat? GetScreenshot(bool independent = false) => null;
	}

	private sealed class RecordingLogSink : ILogEventSink
	{
		private readonly ConcurrentQueue<LogEvent> _events = new();

		public IReadOnlyList<LogEvent> Events => _events.ToArray();

		public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
	}

	[Theory]
	[InlineData("success")]
	[InlineData("failure")]
	[InlineData("cancel")]
	public async Task AppScope_IsActiveDuringFlowAndRestoredForEveryExit(string outcome)
	{
		string root = CreateTempRoot();
		try
		{
			string screenDirectory = Path.Combine(root, "screens");
			Directory.CreateDirectory(screenDirectory);
			File.WriteAllText(
				Path.Combine(screenDirectory, "lost_void.yml"),
				"screen_id: lost_void_scope_test\nscreen_name: 迷失之地-作用域测试\napp_id: lost_void\narea_list: []\n");
			File.WriteAllText(
				Path.Combine(screenDirectory, "global.yml"),
				"screen_id: global_scope_test\nscreen_name: 全局-作用域测试\narea_list: []\n");
			using ZContext context = new ZContext(new OneDragonEnvironment(root));
			context.ScreenContext.LoadExtraScreenDir(screenDirectory);
			context.AttachController(new ReadyController());
			ScopeProbeLostVoidFlow flow = new ScopeProbeLostVoidFlow(outcome);
			LostVoidConfig config = new LostVoidConfig();
			LostVoidApp app = new LostVoidApp(context, config, new LostVoidRunRecord(config), flow);

			try
			{
				_ = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			}
			catch (OperationCanceledException) when (string.Equals(outcome, "cancel", StringComparison.Ordinal))
			{
			}

			Assert.True(flow.SawLostVoidScope);
			Assert.Null(context.ScreenContext.ActiveScreenNames);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void InteractionScreenCandidates_FollowPythonUnrestrictedCalls()
	{
		Assert.Null(LostVoidBangbooStoreOperation.CurrentScreenCandidates);
		Assert.Null(LostVoidChooseGearOperation.CurrentScreenCandidates);
		Assert.Null(LostVoidLotteryOperation.CurrentScreenCandidates);
	}

	[Fact]
	public async Task RunLevel_PublishesNextRegionBusinessStateWithContractTtl()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		LostVoidConfig config = new LostVoidConfig();
		LostVoidAppOperation operation = new LostVoidAppOperation(
			context,
			config,
			new LostVoidRunRecord(config),
			new RecordingLostVoidRunner(new OperationResult(true, "通关")));
		MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(typeof(LostVoidAppOperation).GetMethod("RunLevel", BindingFlags.Instance | BindingFlags.NonPublic));

		_ = await Assert.IsAssignableFrom<Task<OperationRoundResult>>(method.Invoke(operation, null));

		BusinessStateItem state = Assert.Single(
			context.OverlayDebugBus.Snapshot().BusinessStateItems,
			item => item.Key == "迷失之地-下一层");
		Assert.Equal("入口", state.Value);
		Assert.Equal(nameof(LostVoidAppOperation), state.Source);
		Assert.Equal(60d, state.TtlSeconds);
	}

	[Fact]
	public void SupportPanel_UsesLcsFallbackWhenDifflibDoesNotMatch()
	{
		Assert.True(LostVoidAppOperation.IsSupportPanelVisible(new[] { "代xxxxxxxx理" }, "代理人"));
		Assert.False(LostVoidAppOperation.IsSupportPanelVisible(new[] { "完全无关" }, "代理人"));
	}

	[Fact]
	public void StrategyDirection_UsesPointSixDifflibBoundary()
	{
		string[] ordered = new[] { "abcde", "target" };
		Assert.True(LostVoidAppOperation.IsStrategyAfterOcr("target", ordered, new[] { "abcXY" }, static text => text));
		Assert.False(LostVoidAppOperation.IsStrategyAfterOcr("target", ordered, new[] { "abXYZ" }, static text => text));
	}

	[Fact]
	public void SpecialTalk_UsesPointThreeLcsBoundary()
	{
		Assert.True(ScreenLostVoidRunLevelRuntime.IsSpecialTalkText("abc", new[] { "abcdefghij" }));
		Assert.False(ScreenLostVoidRunLevelRuntime.IsSpecialTalkText("ab", new[] { "abcdefghij" }));
	}

	[Fact]
	public void ChooseTitle_DetectsGearMarkerOnlyAfterTitleRulesMiss()
	{
		int detectorCalls = 0;
		LostVoidChooseTitleState exact = LostVoidChooseCommonOperation.ResolveChooseTitle(
			LostVoidInteractService.Instance,
			new[] { "获得武备" },
			() =>
			{
				detectorCalls++;
				return true;
			});
		LostVoidChooseTitleState fallback = LostVoidChooseCommonOperation.ResolveChooseTitle(
			LostVoidInteractService.Instance,
			new[] { "未知标题" },
			() =>
			{
				detectorCalls++;
				return true;
			});

		Assert.Equal("GEAR_GAIN", exact.RuleId);
		Assert.Equal("fallback:gear_marker", fallback.RuleId);
		Assert.Equal(1, detectorCalls);
	}

	[Fact]
	public void OuterOperation_DeclaresCompleteBaselineNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(LostVoidAppOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = (method.GetCustomAttributes(typeof(OperationNodeAttribute), inherit: false).FirstOrDefault() as OperationNodeAttribute)
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		string[] source = new string[28]
		{
			"初始化加载", "识别初始画面", "前往迷失之地-入口", "开始前等待入口加载", "识别悬赏委托完成进度", "矩阵行动-前往入口", "矩阵行动-点击预备编队",
			"矩阵行动-选择预备编队", "矩阵行动-选择代理人", "矩阵行动-点击协战代理人", "矩阵行动-等待代理人列表", "矩阵行动-选择协战代理人", "矩阵行动-开始挑战", "前往副本画面", "副本画面识别",
			"打开调查战略列表", "选择调查战略", "选择周期增益", "下一步", "检查预备编队", "选择预备编队", "出战", "加载自动战斗配置", "层间移动", "通关后处理",
			"打开悬赏委托", "全部领取", "完成后返回"
		};
		Assert.Equal(source.Order<string>(StringComparer.Ordinal), readOnlyDictionary.Keys.Order<string>(StringComparer.Ordinal));
		string[] source2 = new string[37]
		{
			Edge("初始化加载", "识别初始画面", success: true, "继续挑战"),
			Edge("识别初始画面", "前往迷失之地-入口", success: true, "可前往快捷手册"),
			Edge("识别初始画面", "前往迷失之地-入口", success: true, "未能识别当前画面"),
			Edge("识别初始画面", "前往迷失之地-入口", success: true, "未识别初始画面"),
			Edge("识别初始画面", "开始前等待入口加载", success: true, "可前往副本画面"),
			Edge("前往迷失之地-入口", "开始前等待入口加载"),
			Edge("开始前等待入口加载", "识别悬赏委托完成进度"),
			Edge("通关后处理", "识别悬赏委托完成进度"),
			Edge("识别悬赏委托完成进度", "矩阵行动-前往入口", success: true, "继续挑战-矩阵行动"),
			Edge("矩阵行动-前往入口", "矩阵行动-点击预备编队"),
			Edge("矩阵行动-点击预备编队", "矩阵行动-选择预备编队"),
			Edge("矩阵行动-点击预备编队", "矩阵行动-选择代理人", success: true, "手动选取角色"),
			Edge("矩阵行动-选择预备编队", "矩阵行动-点击协战代理人"),
			Edge("矩阵行动-选择代理人", "矩阵行动-点击协战代理人"),
			Edge("矩阵行动-点击协战代理人", "矩阵行动-等待代理人列表"),
			Edge("矩阵行动-等待代理人列表", "矩阵行动-选择协战代理人"),
			Edge("矩阵行动-选择协战代理人", "矩阵行动-开始挑战"),
			Edge("识别悬赏委托完成进度", "前往副本画面", success: true, "继续挑战"),
			Edge("前往副本画面", "副本画面识别"),
			Edge("副本画面识别", "打开调查战略列表"),
			Edge("打开调查战略列表", "选择调查战略"),
			Edge("选择调查战略", "选择周期增益"),
			Edge("选择周期增益", "下一步"),
			Edge("下一步", "检查预备编队"),
			Edge("检查预备编队", "选择预备编队", success: true, "需选择预备编队"),
			Edge("检查预备编队", "出战", success: true, "无需选择预备编队"),
			Edge("选择预备编队", "出战"),
			Edge("识别初始画面", "加载自动战斗配置", success: true, "迷失之地-大世界"),
			Edge("出战", "加载自动战斗配置"),
			Edge("矩阵行动-开始挑战", "加载自动战斗配置"),
			Edge("加载自动战斗配置", "层间移动"),
			Edge("层间移动", "层间移动"),
			Edge("层间移动", "通关后处理", success: true, "通关"),
			Edge("识别悬赏委托完成进度", "打开悬赏委托", success: true, "完成通关次数"),
			Edge("打开悬赏委托", "全部领取"),
			Edge("全部领取", "完成后返回"),
			Edge("全部领取", "完成后返回", success: false)
		};
		string[] actual = readOnlyDictionary.SelectMany((KeyValuePair<string, MethodInfo> pair) => from NodeFromAttribute edge in pair.Value.GetCustomAttributes(typeof(NodeFromAttribute), inherit: false)
			select Edge(edge.FromName, pair.Key, edge.Success, edge.Status)).Order<string>(StringComparer.Ordinal).ToArray();
		Assert.Equal<string[]>(source2.Order<string>(StringComparer.Ordinal).ToArray(), actual);
	}

	[Fact]
	public void RunLevel_DeclaresCompletePythonNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(LostVoidRunLevel).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = (method.GetCustomAttributes(typeof(OperationNodeAttribute), inherit: false).FirstOrDefault() as OperationNodeAttribute)
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		string[] source = new string[18]
		{
			"等待加载", "区域类型初始化", "非战斗画面识别", "更新优先级", "追加代理人类型优先级", "下层入口处理", "尝试交互", "交互处理", "交互后处理", "准备自动战斗",
			"战斗中", "挑战结果处理确定", "挑战结果处理完成", "处理寻路失败", "保存错误信息", "失败退出空洞", "处理战斗失败", "点击失败退出完成"
		};
		Assert.Equal(source.Order<string>(StringComparer.Ordinal), readOnlyDictionary.Keys.Order<string>(StringComparer.Ordinal));
		string[] source2 = new string[40]
		{
			Edge("非战斗画面识别", "等待加载", success: true, "未在大世界"),
			Edge("非战斗画面识别", "等待加载", success: true, "按钮-挑战-确认"),
			Edge("处理寻路失败", "等待加载", success: true, "准备重试"),
			Edge("等待加载", "区域类型初始化"),
			Edge("区域类型初始化", "非战斗画面识别", success: true, "非战斗区域"),
			Edge("非战斗画面识别", "非战斗画面识别", success: true, "0001-距离"),
			Edge("非战斗画面识别", "非战斗画面识别", success: true, "需要重新识别"),
			Edge("交互后处理", "非战斗画面识别", success: true, "大世界"),
			Edge("战斗中", "非战斗画面识别", success: true, "识别需移动交互"),
			Edge("尝试交互", "非战斗画面识别", success: false),
			Edge("更新优先级", "非战斗画面识别"),
			Edge("追加代理人类型优先级", "非战斗画面识别", success: true, "非战斗区域"),
			Edge("非战斗画面识别", "更新优先级", success: true, "需要更新优先级"),
			Edge("更新优先级", "追加代理人类型优先级", success: true, "需要追加代理人类型优先级"),
			Edge("非战斗画面识别", "下层入口处理", success: true, "xxxx-入口"),
			Edge("非战斗画面识别", "尝试交互", success: true, "0000-感叹号"),
			Edge("下层入口处理", "尝试交互"),
			Edge("等待加载", "交互处理", success: true, "识别正在交互"),
			Edge("尝试交互", "交互处理", success: true, "交互成功"),
			Edge("战斗中", "交互处理", success: true, "识别正在交互"),
			Edge("交互处理", "交互后处理", success: true, "迷失之地-大世界"),
			Edge("交互处理", "交互后处理", success: true, "迷失之地-挑战结果"),
			Edge("交互处理", "交互后处理", success: true, "进入下层"),
			Edge("交互处理", "交互后处理", success: false, "未知画面"),
			Edge("非战斗画面识别", "准备自动战斗", success: true, "进入战斗"),
			Edge("非战斗画面识别", "准备自动战斗", success: true, "遭遇战斗"),
			Edge("区域类型初始化", "准备自动战斗", success: true, "战斗区域"),
			Edge("准备自动战斗", "战斗中"),
			Edge("交互后处理", "挑战结果处理确定", success: true, "挑战结果-确定"),
			Edge("交互后处理", "挑战结果处理完成", success: true, "挑战结果-完成"),
			Edge("非战斗画面识别", "处理寻路失败", success: false, "处理寻路失败"),
			Edge("非战斗画面识别", "保存错误信息", success: false, "执行超时"),
			Edge("非战斗画面识别", "保存错误信息", success: false, "节点超时"),
			Edge("战斗中", "保存错误信息", success: false, "执行超时"),
			Edge("战斗中", "保存错误信息", success: false, "节点超时"),
			Edge("处理寻路失败", "保存错误信息", success: true, "准备最终退出"),
			Edge("保存错误信息", "失败退出空洞", success: false),
			Edge("战斗中", "处理战斗失败", success: true, "迷失之地-战斗失败"),
			Edge("失败退出空洞", "点击失败退出完成"),
			Edge("处理战斗失败", "点击失败退出完成")
		};
		string[] actual = readOnlyDictionary.SelectMany((KeyValuePair<string, MethodInfo> pair) => from NodeFromAttribute edge in pair.Value.GetCustomAttributes(typeof(NodeFromAttribute), inherit: false)
			select Edge(edge.FromName, pair.Key, edge.Success, edge.Status)).Order<string>(StringComparer.Ordinal).ToArray();
		Assert.Equal<string[]>(source2.Order<string>(StringComparer.Ordinal).ToArray(), actual);
	}

	[Fact]
	public void OuterOperation_RepeatsRunLevelForAnyNonCompleteSuccessLikePython()
	{
		MethodInfo methodInfo = Assert.IsAssignableFrom<MethodInfo>(typeof(LostVoidAppOperation).GetMethod("RunLevel", BindingFlags.Instance | BindingFlags.NonPublic));
		IReadOnlyList<NodeFromAttribute> collection = methodInfo.GetCustomAttributes(typeof(NodeFromAttribute), inherit: false).Cast<NodeFromAttribute>().ToArray();
		Assert.Contains((IEnumerable<NodeFromAttribute>)collection, (Predicate<NodeFromAttribute>)((NodeFromAttribute edge) => edge.FromName == "层间移动" && edge.Status == null && edge.IgnoreStatus));
	}

	[Fact]
	public void EntryNavigation_RemovedManualOcrClickNodesInFavorOfScreenRoute()
	{
		string[] removedMethodNames = ["ClickPeriodInEntry", "MatrixGotoChallenge", "MatrixClickNextStep", "ClickRegularInMatrixExplore", "ClickTargetMissionInMatrixExplore", "IsMatrixExploreMission", "ClickEntryNavigation"];
		foreach (string methodName in removedMethodNames)
		{
			Assert.Null(typeof(LostVoidAppOperation).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic));
		}

		Assert.DoesNotContain(typeof(LostVoidAppOperation).GetFields(BindingFlags.Instance | BindingFlags.NonPublic), field => field.Name.Contains("entryNavigation", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesLostVoidApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			LostVoidAppFactory lostVoidAppFactory = zContext.ApplicationFactoryRegistry.CreateLostVoidFactory();
			IApplication application = lostVoidAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = lostVoidAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = lostVoidAppFactory.GetRunRecord(0);
			Assert.Equal("lost_void", lostVoidAppFactory.AppId);
			Assert.Equal("迷失之地", lostVoidAppFactory.AppName);
			Assert.Equal("one_dragon", "one_dragon");
			Assert.Equal("one_dragon", lostVoidAppFactory.GroupId);
			Assert.True(lostVoidAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<LostVoidApp>(application);
			Assert.IsType<LostVoidConfig>(config);
			LostVoidRunRecord lostVoidRunRecord = Assert.IsType<LostVoidRunRecord>(runRecord);
			Assert.Equal("lost_void", lostVoidRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersLostVoidAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterLostVoidApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("lost_void"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("lost_void"));
			Assert.Contains("lost_void", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
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
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "lost_void.yml"), "daily_plan_times: 4\nweekly_plan_times: 3\nextra_task: 刷满业绩点\nmission_name: 特遣调查\nchallenge_config: 自定义挑战");
			LostVoidConfig lostVoidConfig = LostVoidConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("lost_void", lostVoidConfig.AppId);
			Assert.Equal(4, lostVoidConfig.DailyPlanTimes);
			Assert.Equal(3, lostVoidConfig.WeeklyPlanTimes);
			Assert.Equal("刷满业绩点", lostVoidConfig.ExtraTask);
			Assert.False(lostVoidConfig.IsBountyCommissionMode);
			Assert.Equal("特遣调查", lostVoidConfig.MissionName);
			Assert.Equal("自定义挑战", lostVoidConfig.ChallengeConfig);
			Assert.Equal("INTERFACE", "INTERFACE");
			Assert.Contains((IEnumerable<LostVoidSettingField>)LostVoidSettings.Fields, (Predicate<LostVoidSettingField>)((LostVoidSettingField field) => field.Key == "extra_task" && field.Options.Any((ConfigItem option) => object.Equals(option.Value, "完成悬赏委托"))));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChallengeConfig_LoadsPythonFieldsAndDiscoversConfigNames()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "lost_void_challenge");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "默认-成就模式.yml"), "predefined_team_idx: 2\nchoose_team_by_priority: true\nmanually_choose_agent: true\nteam_info:\n  - ellen\n  - anby\n  - nicole\nauto_battle: 自定义战斗\nartifact_priority_new: true\nartifact_priority:\n  - 优先一\nartifact_priority_2:\n  - 优先二\nregion_type_priority:\n  - 战斗-鸣徽\nperiod_buff_no: 第二个\nbuy_only_priority_1: 4\nbuy_only_priority_2: 999\nstore_gold: false\nstore_blood: true\nstore_blood_min: 70\ninvestigation_strategy: 战略A\nchase_new_mode: true");
			File.WriteAllText(Path.Combine(text2, "自定义-02.yml"), "auto_battle: B\n");
			File.WriteAllText(Path.Combine(text2, "自定义-03.sample.yml"), "auto_battle: C\n");
			File.WriteAllText(Path.Combine(text2, "自定义-04.yml"), "predefined_team_idx: [\n");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			LostVoidChallengeConfig lostVoidChallengeConfig = LostVoidChallengeConfig.Load(environment, "默认-成就模式");
			Assert.Equal(2, lostVoidChallengeConfig.PredefinedTeamIdx);
			Assert.True(lostVoidChallengeConfig.ChooseTeamByPriority);
			Assert.True(lostVoidChallengeConfig.ManuallyChooseAgent);
			int num = 3;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<string> span = CollectionsMarshal.AsSpan(list);
			span[0] = "ellen";
			span[1] = "anby";
			span[2] = "nicole";
			Assert.Equal<List<string>>(list, lostVoidChallengeConfig.TeamInfo);
			Assert.Equal("自定义战斗", lostVoidChallengeConfig.AutoBattle);
			Assert.True(lostVoidChallengeConfig.ArtifactPriorityNew);
			Assert.Equal("优先一", lostVoidChallengeConfig.ArtifactPriorityText);
			Assert.Equal("优先二", lostVoidChallengeConfig.ArtifactPriority2Text);
			Assert.Equal("战斗-鸣徽", lostVoidChallengeConfig.RegionTypePriority[0]);
			Assert.Equal("第二个", lostVoidChallengeConfig.PeriodBuffNo);
			Assert.Equal(4, lostVoidChallengeConfig.BuyOnlyPriority1);
			Assert.Equal(999, lostVoidChallengeConfig.BuyOnlyPriority2);
			Assert.False(lostVoidChallengeConfig.StoreGold);
			Assert.True(lostVoidChallengeConfig.StoreBlood);
			Assert.Equal(70, lostVoidChallengeConfig.StoreBloodMin);
			Assert.Equal("战略A", lostVoidChallengeConfig.InvestigationStrategy);
			Assert.True(lostVoidChallengeConfig.ChaseNewMode);
			List<(string ModuleName, Exception Error)> invalidConfigs = new List<(string ModuleName, Exception Error)>();
			Assert.Equal(new string[3] { "自定义-02", "自定义-03", "默认-成就模式" }, LostVoidChallengeConfig.GetAllModuleNames(environment, onInvalidConfig: (moduleName, error) => invalidConfigs.Add((moduleName, error))));
			(string moduleName, Exception error) = Assert.Single(invalidConfigs);
			Assert.Equal("自定义-04", moduleName);
			Assert.NotNull(error);
			Assert.Equal("自定义-05", LostVoidChallengeConfig.GetNewModuleName(environment));
			Assert.Equal("入口", LostVoidRegionType.FromValue("不存在"));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_TracksCompletionBySelectedTask()
	{
		string text = CreateTempRoot();
		try
		{
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
			LostVoidConfig lostVoidConfig = new LostVoidConfig
			{
				DailyPlanTimes = 2,
				WeeklyPlanTimes = 3,
				ExtraTask = "完成周计划次数"
			};
			LostVoidRunRecord lostVoidRunRecord = LostVoidRunRecord.Load(new OneDragonEnvironment(text), lostVoidConfig, 0, 4, () => now);
			lostVoidRunRecord.AddCompleteTimes();
			lostVoidRunRecord.AddCompleteTimes();
			Assert.False(lostVoidRunRecord.IsFinishedByWeek());
			Assert.True(lostVoidRunRecord.IsFinishedByDay());
			lostVoidRunRecord.AddCompleteTimes();
			Assert.True(lostVoidRunRecord.IsFinishedByWeek());
			lostVoidConfig.ExtraTask = "完成悬赏委托";
			Assert.False(lostVoidRunRecord.IsFinishedByWeek());
			lostVoidRunRecord.BountyCommissionComplete = true;
			Assert.True(lostVoidRunRecord.IsFinishedByWeek());
			lostVoidRunRecord.EvalPointComplete = true;
			lostVoidRunRecord.PeriodRewardComplete = true;
			lostVoidRunRecord.CompleteTaskForceWithUp = true;
			LostVoidRunRecord lostVoidRunRecord2 = LostVoidRunRecord.Load(new OneDragonEnvironment(text), lostVoidConfig, 0, 4, () => now);
			Assert.Equal(3, lostVoidRunRecord2.WeeklyRunTimes);
			Assert.Equal(3, lostVoidRunRecord2.DailyRunTimes);
			Assert.True(lostVoidRunRecord2.BountyCommissionComplete);
			Assert.True(lostVoidRunRecord2.EvalPointComplete);
			Assert.True(lostVoidRunRecord2.PeriodRewardComplete);
			Assert.True(lostVoidRunRecord2.CompleteTaskForceWithUp);
			Assert.Equal(1, lostVoidRunRecord2.RunStatusUnderNow);
			now = now.AddDays(7.0);
			Assert.Equal(0, lostVoidRunRecord2.RunStatusUnderNow);
			lostVoidRunRecord2.CheckAndUpdateStatus();
			Assert.Equal(0, lostVoidRunRecord2.WeeklyRunTimes);
			Assert.Equal(0, lostVoidRunRecord2.DailyRunTimes);
			Assert.False(lostVoidRunRecord2.BountyCommissionComplete);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private sealed class ScopeProbeLostVoidFlow(string outcome) : ILostVoidAppFlow
	{
		public bool SawLostVoidScope { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, LostVoidConfig config, LostVoidRunRecord runRecord, CancellationToken cancellationToken)
		{
			SawLostVoidScope = context.ScreenContext.ActiveScreenNames?.Contains("迷失之地-作用域测试") ?? false;
			return outcome switch
			{
				"success" => Task.FromResult(new OperationResult(true, "完成")),
				"failure" => Task.FromResult(new OperationResult(false, "失败")),
				"cancel" => Task.FromCanceled<OperationResult>(new CancellationToken(canceled: true)),
				_ => throw new ArgumentOutOfRangeException(nameof(outcome))
			};
		}
	}

	[Fact]
	public void YamlForwardCompatibility_PreservesUnknownFieldsForAllLostVoidDocuments()
	{
		string root = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(root);
			string appConfigPath = Path.Combine(root, "config", "00", "one_dragon", "lost_void.yml");
			Directory.CreateDirectory(Path.GetDirectoryName(appConfigPath)!);
			File.WriteAllText(Path.ChangeExtension(appConfigPath, ".sample.yml"), "daily_plan_times: 4\nfuture_root: keep-app\nfuture_nested:\n  child: app-child\nfuture_list:\n- future-a\n- future-b\n");
			YamlConfig<LostVoidConfig> appConfig = new YamlConfig<LostVoidConfig>(environment, "lost_void", null, 0, new string[] { "app_config", "one_dragon" }, sample: true);
			appConfig.Current.DailyPlanTimes = 6;
			appConfig.Save();

			string challengePath = LostVoidChallengeConfig.GetUserFilePath(environment, "前向兼容");
			Directory.CreateDirectory(Path.GetDirectoryName(challengePath)!);
			File.WriteAllText(challengePath, "store_gold: true\nfuture_root: keep-challenge\nfuture_nested:\n  child: challenge-child\nfuture_list:\n- future-a\n- future-b\n");
			LostVoidChallengeConfig challengeConfig = LostVoidChallengeConfig.Load(environment, "前向兼容");
			challengeConfig.StoreGold = false;
			LostVoidChallengeConfig.Save(environment, "前向兼容", challengeConfig);
			string copiedChallengePath = LostVoidChallengeConfig.GetUserFilePath(environment, "前向兼容-copy");
			LostVoidChallengeConfig.Save(environment, "前向兼容-copy", challengeConfig);

			string runRecordPath = Path.Combine(root, "config", "00", "app_run_record", "lost_void.yml");
			Directory.CreateDirectory(Path.GetDirectoryName(runRecordPath)!);
			File.WriteAllText(runRecordPath, "daily_run_times: 1\nfuture_root: keep-record\nfuture_nested:\n  child: record-child\nfuture_list:\n- future-a\n- future-b\n");
			LostVoidRunRecord runRecord = LostVoidRunRecord.Load(environment, new LostVoidConfig(), 0);
			runRecord.AddCompleteTimes();

			AssertForwardCompatibleDocument(appConfigPath, "keep-app", "app-child");
			Assert.Equal(6, Convert.ToInt32(ReadYamlMap(appConfigPath)["daily_plan_times"]));
			AssertForwardCompatibleDocument(challengePath, "keep-challenge", "challenge-child");
			Assert.False(Convert.ToBoolean(ReadYamlMap(challengePath)["store_gold"]));
			AssertForwardCompatibleDocument(copiedChallengePath, "keep-challenge", "challenge-child");
			AssertForwardCompatibleDocument(runRecordPath, "keep-record", "record-child");
			Assert.Equal(2, Convert.ToInt32(ReadYamlMap(runRecordPath)["daily_run_times"]));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void LostVoidConstruction_DoesNotCreateOrInitializeDetector()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		Assert.Null(context.LostVoid.Detector);

		_ = new LostVoidApp(
			context,
			new LostVoidConfig(),
			new LostVoidRunRecord(new LostVoidConfig()));

		Assert.Null(context.LostVoid.Detector);
	}

	[Fact]
	public void AppInitialization_PreparesModelBeforeReturningContinueStatus()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		LostVoidConfig config = new LostVoidConfig();
		bool prepared = false;
		LostVoidAppOperation operation = new LostVoidAppOperation(
			context,
			config,
			new LostVoidRunRecord(config),
			new RecordingLostVoidRunner(new OperationResult(true, "通关")),
			ctx =>
			{
				prepared = true;
				return LostVoidModelPreparationResult.Success(ctx.LostVoid.Detector!.CoreDetector.Config.ModelPath);
			});
		MethodInfo initialize = typeof(LostVoidAppOperation).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)!;

		OperationRoundResult result = Assert.IsType<OperationRoundResult>(initialize.Invoke(operation, null));

		Assert.True(prepared);
		Assert.True(result.IsSuccess);
		Assert.Equal(LostVoidApp.StatusAgain, result.Status);
	}

	[Theory]
	[InlineData("YOLO 模型文件缺失: model.onnx", "模型文件检查")]
	[InlineData("YOLO 模型下载失败: connection refused", "模型下载")]
	public void ModelPreparation_PreservesMissingAndDownloadFailures(string originalError, string expectedStage)
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));

		LostVoidModelPreparationResult result = context.LostVoid.PrepareLostVoidDetectorModel(
			(_, _, progress) =>
			{
				progress?.Invoke(1d, originalError);
				return false;
			});

		Assert.False(result.IsSuccess);
		Assert.Equal(expectedStage, result.Stage);
		Assert.Equal(originalError, result.ErrorMessage);
		Assert.Contains("model.onnx", result.ModelPath, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ModelPreparation_PreservesOnnxInitializationException()
	{
		RecordingLogSink sink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"), logger);
		InvalidOperationException original = new InvalidOperationException("onnx session original error");

		LostVoidModelPreparationResult result = context.LostVoid.PrepareLostVoidDetectorModel(
			(_, _, _) => throw original);

		Assert.False(result.IsSuccess);
		Assert.Equal("ONNX 初始化", result.Stage);
		Assert.Same(original, result.Exception);
		Assert.Equal(original.Message, result.ErrorMessage);
		LogEvent failure = Assert.Single(
			sink.Events,
			eventItem => eventItem.MessageTemplate.Text.StartsWith("迷失之地模型准备失败", StringComparison.Ordinal));
		Assert.Equal(LogEventLevel.Error, failure.Level);
		Assert.Same(original, failure.Exception);
		Assert.Equal("ONNX 初始化", Assert.IsType<ScalarValue>(failure.Properties["Stage"]).Value);
		Assert.Contains("model.onnx", Convert.ToString(Assert.IsType<ScalarValue>(failure.Properties["ModelPath"]).Value), StringComparison.OrdinalIgnoreCase);
		Assert.Equal(original.Message, Assert.IsType<ScalarValue>(failure.Properties["Error"]).Value);
	}

	[Theory]
	[InlineData("personal", "http://127.0.0.1:7890", "https://gh.example", "http://127.0.0.1:7890", null)]
	[InlineData("ghproxy", "http://127.0.0.1:7890", "https://gh.example", null, "https://gh.example")]
	[InlineData("None", "http://127.0.0.1:7890", "https://gh.example", null, null)]
	public void ModelPreparation_UsesOnlyTheConfiguredProxy(
		string proxyType,
		string personalProxy,
		string ghProxy,
		string? expectedPersonalProxy,
		string? expectedGhProxy)
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.EnvConfig.ProxyType = proxyType;
		context.EnvConfig.PersonalProxy = personalProxy;
		context.EnvConfig.GhProxyUrl = ghProxy;
		string? actualPersonalProxy = null;
		string? actualGhProxy = null;

		_ = context.LostVoid.PrepareLostVoidDetectorModel(
			(personal, gh, progress) =>
			{
				actualPersonalProxy = personal;
				actualGhProxy = gh;
				progress?.Invoke(1d, "YOLO 模型文件缺失: model.onnx");
				return false;
			});

		Assert.Equal(expectedPersonalProxy, actualPersonalProxy);
		Assert.Equal(expectedGhProxy, actualGhProxy);
	}

	[Theory]
	[InlineData("模型文件检查", "model.onnx missing")]
	[InlineData("模型下载", "download original error")]
	[InlineData("ONNX 初始化", "onnx original error")]
	public async Task AppInitialization_ModelFailureEndsCurrentRunAndKeepsHostAlive(string stage, string originalError)
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		CountingLostVoidController controller = new CountingLostVoidController();
		context.AttachController(controller);
		LostVoidConfig config = new LostVoidConfig();
		RecordingLostVoidRunner runner = new RecordingLostVoidRunner(new OperationResult(true, "通关"));
		LostVoidAppOperation operation = new LostVoidAppOperation(
			context,
			config,
			new LostVoidRunRecord(config),
			runner,
			_ => LostVoidModelPreparationResult.Failure(stage, @"C:\models\lost_void\model.onnx", originalError));

		OperationResult result = await operation.ExecuteAsync(CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Contains(stage, result.Status, StringComparison.Ordinal);
		Assert.Contains(originalError, result.Status, StringComparison.Ordinal);
		Assert.NotNull(context.RunContext);
		Assert.NotNull(context.ModelConfig);
		Assert.Equal(0, runner.RunCount);
		Assert.Equal(0, controller.InputCount);
	}

	private static void AssertForwardCompatibleDocument(string path, string rootValue, string nestedValue)
	{
		Dictionary<object, object> yaml = ReadYamlMap(path);
		Assert.Equal(rootValue, Convert.ToString(yaml["future_root"]));
		Dictionary<object, object> nested = Assert.IsType<Dictionary<object, object>>(yaml["future_nested"]);
		Assert.Equal(nestedValue, Convert.ToString(nested["child"]));
		Assert.Equal(
			new[] { "future-a", "future-b" },
			Assert.IsAssignableFrom<IEnumerable<object>>(yaml["future_list"]).Select(value => Convert.ToString(value)).ToArray());
	}

	private static Dictionary<object, object> ReadYamlMap(string path)
	{
		return new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(File.ReadAllText(path));
	}

	private static string Edge(string from, string to, bool success = true, string? status = null)
	{
		return $"{from}|{to}|{success}|{status ?? "<null>"}";
	}

	[Fact]
	public void ChaseNewStrategy_PrefersNoLevelRingFromConfiguredArea()
	{
		using Mat mat = new Mat(1080, 1920, MatType.CV_8UC3, BgrFromHsv(170, byte.MaxValue, 50));
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(200, 250, 60, 60), BgrFromHsv(179, 59, 63), -1);
		OneDragon.Core.Abstractions.Geometry.Point? actual = LostVoidAppOperation.FindChaseNewNoLevelTarget(mat, new OneDragon.Core.Abstractions.Geometry.Rect(2, 206, 1922, 358));
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(229, 279), actual);
	}

	[Fact]
	public void ChaseNewStrategy_SkipsFrameContainingLevelDigit()
	{
		using Mat mat = new Mat(1080, 1920, MatType.CV_8UC3, BgrFromHsv(170, byte.MaxValue, 50));
		OneDragon.Core.Abstractions.Geometry.Rect area = new OneDragon.Core.Abstractions.Geometry.Rect(2, 206, 1922, 358);
		Scalar color = BgrFromHsv(75, 5, 150);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(100, 250, 60, 60), color, -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(300, 250, 60, 60), color, -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(114, 264, 34, 34), BgrFromHsv(0, 0, 200), -1);
		IReadOnlyList<OpenCvSharp.Point[]> readOnlyList = LostVoidAppOperation.FindChaseNewLevelFrames(mat, area);
		IReadOnlyList<OpenCvSharp.Point[]> readOnlyList2 = LostVoidAppOperation.FindChaseNewLevelDigits(mat, area);
		Assert.Equal(2, readOnlyList.Count);
		Assert.Single(readOnlyList2);
		OneDragon.Core.Abstractions.Geometry.Point? actual = LostVoidAppOperation.FindChaseNewFrameWithoutDigitTarget(readOnlyList, readOnlyList2, area);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(329, 279), actual);
	}

	[Fact]
	public void MatrixSelectAgent_ReturnsToTopWithBaselineSwipeCount()
	{
		Assert.Equal(0, LostVoidAppOperation.GetMatrixAgentReturnToTopSwipeCount(0, foundAll: true));
		Assert.Equal(2, LostVoidAppOperation.GetMatrixAgentReturnToTopSwipeCount(2, foundAll: true));
		Assert.Equal(4, LostVoidAppOperation.GetMatrixAgentReturnToTopSwipeCount(4, foundAll: true));
		Assert.Equal(5, LostVoidAppOperation.GetMatrixAgentReturnToTopSwipeCount(1, foundAll: false));
	}

	[Fact]
	public void MatrixSelectTeam_UsesMatrixOcrThenVerifiesMainTeamSlot()
	{
		string text = CreateTempRoot();
		try
		{
			WriteMatrixTeamScreenInfo(text);
			string text2 = Path.Combine(text, "config", "lost_void_challenge");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "默认-成就模式.yml"), "predefined_team_idx: 0\n");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			using MatrixSelectionController matrixSelectionController = new MatrixSelectionController();
			zContext.AttachController(matrixSelectionController);
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.TeamConfig.TeamList[0].Name = "目标 配队";
			zContext.LostVoid.LoadChallengeConfig();
			zContext.OcrService.Matcher = new MatrixSelectionOcrMatcher();
			LostVoidAppOperation operation = new LostVoidAppOperation(zContext, new LostVoidConfig(), new LostVoidRunRecord(new LostVoidConfig()), new RecordingLostVoidRunner(new OperationResult(IsSuccess: true, "通关")));
			CaptureOperationScreenshot(operation);
			OperationRoundResult operationRoundResult = InvokeMatrixSelectTeam(operation);
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("已选择配队", operationRoundResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(120, 80), matrixSelectionController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static Scalar BgrFromHsv(byte hue, byte saturation, byte value)
	{
		using Mat mat = new Mat(1, 1, MatType.CV_8UC3, new Scalar((int)hue, (int)saturation, (int)value));
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.HSV2BGR);
		Vec3b vec3b = mat2.At<Vec3b>(0, 0);
		return new Scalar((int)vec3b.Item0, (int)vec3b.Item1, (int)vec3b.Item2);
	}

	private static void CaptureOperationScreenshot(LostVoidAppOperation operation)
	{
		MethodInfo method = typeof(ZOperation).GetMethod("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic);
		method.Invoke(operation, new object[1] { false });
	}

	private static OperationRoundResult InvokeMatrixSelectTeam(LostVoidAppOperation operation)
	{
		MethodInfo method = typeof(LostVoidAppOperation).GetMethod("MatrixSelectTeam", BindingFlags.Instance | BindingFlags.NonPublic);
		return Assert.IsType<OperationRoundResult>(method.Invoke(operation, null));
	}

	private static void WriteMatrixTeamScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "lost_void_matrix.yml"), "screen_id: lost_void_matrix\nscreen_name: 迷失之地-矩阵行动-编队选择\narea_list:\n- area_name: 编队列表\n  pc_rect: [100, 100, 800, 800]\n- area_name: 主战编队槽\n  pc_rect: [900, 100, 1400, 500]");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
