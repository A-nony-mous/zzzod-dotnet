using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.Notify;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class ZOperationTests : IDisposable
{
	private sealed class MetadataProbeOperation : ZOperation
	{
		public ZContext TypedContext => base.ZContext;

		public string ExposedOperationName => base.OperationName;

		public int ExposedDefaultNodeMaxRetryTimes => base.DefaultNodeMaxRetryTimes;

		public double ExposedTimeoutSeconds => base.TimeoutSeconds;

		public MetadataProbeOperation(ZContext context, int nodeMaxRetryTimes, string operationName, double timeoutSeconds)
			: base(context, operationName, nodeMaxRetryTimes, timeoutSeconds)
		{
		}

		[OperationNode("完成", IsStartNode = true, ScreenshotBeforeRound = false)]
		private OperationRoundResult Complete()
		{
			return RoundSuccess("完成");
		}
	}

	private sealed class ScreenshotProbeOperation : ZOperation
	{
		public Mat? ExposedLastScreenshot => base.LastScreenshot;

		public DateTimeOffset? ExposedLastScreenshotTimeUtc => base.LastScreenshotTimeUtc;

		public ScreenshotProbeOperation(ZContext context)
			: base(context, "ScreenshotProbeOperation")
		{
		}

		[OperationNode("截图", IsStartNode = true, ScreenshotBeforeRound = true)]
		private OperationRoundResult Capture()
		{
			return (base.LastScreenshot == null) ? RoundRetry("未获取截图") : RoundSuccess("已截图");
		}
	}

	private sealed class HelperProbeOperation : ZOperation
	{
		public bool ExposedNodeClicked => base.NodeClicked;

		public HelperProbeOperation(ZContext context)
			: base(context, "HelperProbeOperation")
		{
		}

		public OperationRoundResult FindArea(Mat screen, string screenName, string areaName)
		{
			return RoundByFindArea(screen, screenName, areaName);
		}

		public OperationRoundResult FindAndClickArea(Mat? screen, string? screenName, string? areaName)
		{
			return RoundByFindAndClickArea(screen, screenName, areaName, TimeSpan.Zero);
		}

		public OperationRoundResult FindAndClickUntilFound(Mat screen, string screenName, string areaName, IReadOnlyList<(string ScreenName, string AreaName)> untilFindAll)
		{
			return RoundByFindAndClickArea(screen, screenName, areaName, TimeSpan.Zero, null, null, cropFirst: true, centerX: false, untilFindAll);
		}

		public OperationRoundResult FindOcr(Mat screen, string targetText, OneDragon.Core.Screen.ScreenArea? area)
		{
			return RoundByOcr(screen, targetText, area);
		}

		public OperationRoundResult FromOperationResult(OperationResult result, string? status = null, bool retryOnFail = false)
		{
			return RoundByOperationResult(result, status, retryOnFail);
		}

		public OperationRoundResult ClickArea(string screenName, string areaName)
		{
			return RoundByClickArea(screenName, areaName);
		}

		public OperationRoundResult ClickArea(string screenName, string areaName, bool clickLeftTop)
		{
			return RoundByClickArea(screenName, areaName, clickLeftTop);
		}

		public OperationRoundResult OcrAndClickByPriority(Mat screen, IReadOnlyList<string> targetTextList, OneDragon.Core.Screen.ScreenArea? area, OneDragon.Core.Abstractions.Geometry.Point? offset = null, double lcsPercent = 0.6, IReadOnlyList<string>? ignoreTextList = null)
		{
			return RoundByOcrAndClickByPriority(screen, targetTextList, area, lcsPercent, offset, null, null, null, cropFirst: true, ignoreTextList);
		}

		public OperationRoundResult GotoScreen(Mat screen, string screenName)
		{
			return RoundByGotoScreen(screen, screenName, TimeSpan.Zero);
		}

		public string? CheckScreen(Mat screen, IReadOnlyList<string>? screenNameList = null)
		{
			return CheckAndUpdateCurrentScreen(screen, screenNameList);
		}

		[OperationNode("完成", IsStartNode = true, ScreenshotBeforeRound = false)]
		private OperationRoundResult Complete()
		{
			return RoundSuccess("完成");
		}
	}

	private sealed class NodeClickedProbeOperation(ZContext context) : ZOperation(context, "NodeClickedProbeOperation")
	{
		public List<bool> NodeClickedSnapshots { get; } = new List<bool>();

		public int RoundCount { get; private set; }

		[OperationNode("点击", IsStartNode = true)]
		private OperationRoundResult Click()
		{
			RoundCount++;
			OperationRoundResult result = RoundByClickArea("点击界面", "确认");
			NodeClickedSnapshots.Add(base.NodeClicked);
			return result;
		}

		[OperationNode("完成")]
		[NodeFrom("点击", Status = "确认", IgnoreStatus = false)]
		private OperationRoundResult Complete()
		{
			RoundCount++;
			NodeClickedSnapshots.Add(base.NodeClicked);
			return RoundSuccess("第二节点完成");
		}
	}

	private sealed class NotificationProbeOperation : ZOperation
	{
		public NotificationProbeOperation(ZContext context)
			: base(context, "节点通知测试")
		{
		}

		[OperationNode("领取", IsStartNode = true, ScreenshotBeforeRound = false)]
		[OperationNodeNotify(OperationNodeNotifyTiming.CurrentSuccess)]
		private OperationRoundResult Claim()
		{
			return RoundSuccess("领取完成");
		}
	}

	private sealed class GameWindowCheckProbeOperation : ZOperation
	{
		private readonly bool _ready;

		public int BodyCount { get; private set; }

		public GameWindowCheckProbeOperation(ZContext context, bool ready, bool needCheckGameWindow = true, Func<CancellationToken, Task<OperationResult>>? enterGameAsync = null)
			: base(context, "游戏窗口检查", needCheckGameWindow: needCheckGameWindow, enterGameAsync: enterGameAsync)
		{
			_ready = ready;
		}

		protected override bool IsGameWindowReady() => _ready;

		[OperationNode("业务", IsStartNode = true, ScreenshotBeforeRound = false)]
		private OperationRoundResult RunBody()
		{
			BodyCount++;
			return RoundSuccess("完成");
		}
	}

	private sealed class NestedGameWindowCheckOperation : ZOperation
	{
		private readonly GameWindowCheckProbeOperation _child;

		public NestedGameWindowCheckOperation(ZContext context, GameWindowCheckProbeOperation child)
			: base(context, "嵌套窗口检查", needCheckGameWindow: false)
		{
			_child = child;
		}

		[OperationNode("执行子操作", IsStartNode = true, ScreenshotBeforeRound = false)]
		private async Task<OperationRoundResult> RunChildAsync()
		{
			return RoundByOperationResult(await _child.ExecuteAsync());
		}
	}

	private sealed class RecordingNotificationService : IPushNotificationService
	{
		public TaskCompletionSource<(string Title, string Content)> Next { get; } = new TaskCompletionSource<(string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);

		public Task<OperationResult> PushAsync(ZContext context, string title, string content, Mat? image, CancellationToken cancellationToken)
		{
			Next.TrySetResult((title, content));
			return Task.FromResult(new OperationResult(IsSuccess: true));
		}
	}

	private sealed class RecordingController(Mat? screenshot = null) : ControllerBase
	{
		public int ScreenshotCount { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPoint { get; private set; }

		public bool LastClickPcAlt { get; private set; }

		public string? LastClickGamepadAction { get; private set; }

		private TimeSpan ScreenshotDelay { get; }

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			LastClickPoint = position;
			LastClickPcAlt = pcAlt;
			LastClickGamepadAction = gamepadAction;
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
			ScreenshotCount++;
			if (ScreenshotDelay > TimeSpan.Zero)
			{
				Thread.Sleep(ScreenshotDelay);
			}
			return screenshot?.Clone();
		}

		public RecordingController(Mat? screenshot, TimeSpan screenshotDelay)
			: this(screenshot)
		{
			ScreenshotDelay = screenshotDelay;
		}
	}

	private sealed class ScreenshotRoundWaitProbeOperation : ZOperation
	{
		public ScreenshotRoundWaitProbeOperation(ZContext context)
			: base(context, "截图轮次等待测试")
		{
		}

		public OperationRoundResult CaptureAndWait(TimeSpan minimumRoundTime)
		{
			Screenshot();
			return RoundWaitForScreenshotRound(minimumRoundTime);
		}
	}

	private sealed class FakeOcrMatcher(IReadOnlyList<OcrMatchResult> results) : IOcrMatcher
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
			return string.Concat(from result in results
				orderby result.Y, result.X
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
			return results.Select((OcrMatchResult result) => new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public ZOperationTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-operation-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
	}

	[Fact]
	public void Constructor_ExposesTypedZContextAndPassesBaseOptions()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		MetadataProbeOperation metadataProbeOperation = new MetadataProbeOperation(zContext, 7, "元数据测试", 12.5);
		Assert.Same(zContext, metadataProbeOperation.TypedContext);
		Assert.Equal("元数据测试", metadataProbeOperation.ExposedOperationName);
		Assert.Equal(7, metadataProbeOperation.ExposedDefaultNodeMaxRetryTimes);
		Assert.Equal(12.5, metadataProbeOperation.ExposedTimeoutSeconds);
	}

	[Fact]
	public async Task ExecuteAsync_ChecksWindowForDirectAndNestedOperations()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		int directEnterCount = 0;
		GameWindowCheckProbeOperation direct = new GameWindowCheckProbeOperation(context, ready: false, enterGameAsync: _ =>
		{
			directEnterCount++;
			return Task.FromResult(new OperationResult(true));
		});
		Assert.True((await direct.ExecuteAsync()).IsSuccess);
		Assert.Equal(1, directEnterCount);
		Assert.Equal(1, direct.BodyCount);

		int nestedEnterCount = 0;
		GameWindowCheckProbeOperation child = new GameWindowCheckProbeOperation(context, ready: false, enterGameAsync: _ =>
		{
			nestedEnterCount++;
			return Task.FromResult(new OperationResult(true));
		});
		Assert.True((await new NestedGameWindowCheckOperation(context, child).ExecuteAsync()).IsSuccess);
		Assert.Equal(1, nestedEnterCount);
		Assert.Equal(1, child.BodyCount);
	}

	[Fact]
	public async Task ExecuteAsync_SkipsEnterWhenReadyAndReturnsEnterFailureBeforeBody()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		int readyEnterCount = 0;
		GameWindowCheckProbeOperation ready = new GameWindowCheckProbeOperation(context, ready: true, enterGameAsync: _ =>
		{
			readyEnterCount++;
			return Task.FromResult(new OperationResult(true));
		});
		Assert.True((await ready.ExecuteAsync()).IsSuccess);
		Assert.Equal(0, readyEnterCount);

		GameWindowCheckProbeOperation failed = new GameWindowCheckProbeOperation(context, ready: false, enterGameAsync: _ => Task.FromResult(new OperationResult(false, "进入游戏失败")));
		OperationResult result = await failed.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("进入游戏失败", result.Status);
		Assert.Equal(0, failed.BodyCount);
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesCancellationFromWindowCheck()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		GameWindowCheckProbeOperation operation = new GameWindowCheckProbeOperation(context, ready: false, enterGameAsync: token => throw new OperationCanceledException(token));
		using CancellationTokenSource cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.ExecuteAsync(cancellation.Token));
		Assert.Equal(0, operation.BodyCount);
	}

	[Fact]
	public async Task ScreenshotBeforeRound_StoresLastScreenshotAndCaptureTime()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		using Mat screenshot = new Mat(8, 9, MatType.CV_8UC3, Scalar.All(11.0));
		RecordingController controller = new RecordingController(screenshot.Clone());
		context.AttachController(controller);
		ScreenshotProbeOperation operation = new ScreenshotProbeOperation(context);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("已截图", result.Status);
		Assert.Equal(1, controller.ScreenshotCount);
		Assert.NotNull(operation.ExposedLastScreenshot);
		Assert.Equal(8, operation.ExposedLastScreenshot.Rows);
		Assert.Equal(9, operation.ExposedLastScreenshot.Cols);
		Assert.NotNull(operation.ExposedLastScreenshotTimeUtc);
	}

	[Fact]
	public async Task ScreenshotBeforeRound_MissingControllerReturnsRetryWithoutThrowing()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		ScreenshotProbeOperation operation = new ScreenshotProbeOperation(context);
		OperationResult result = await operation.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("未获取截图", result.Status);
		Assert.Null(operation.ExposedLastScreenshot);
		Assert.Null(operation.ExposedLastScreenshotTimeUtc);
	}

	/// <summary>
	/// 补足制不再由业务层按截图时刻自行计算，而是交给框架轮循环（锚点在循环顶部）。
	/// 这里只验收通道：填的是 DelayUntilRoundTime 而不是固定 Delay；
	/// 补足量本身由 OneDragon.Core.Tests 的 OperationRoundPacingTests 覆盖。
	/// </summary>
	[Fact]
	public void RoundWaitForScreenshotRound_UsesFrameworkRoundTimeChannel()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		using Mat mat = new Mat(8, 9, MatType.CV_8UC3, Scalar.All(11.0));
		zContext.AttachController(new RecordingController(mat.Clone(), TimeSpan.FromMilliseconds(40L)));
		ScreenshotRoundWaitProbeOperation screenshotRoundWaitProbeOperation = new ScreenshotRoundWaitProbeOperation(zContext);
		OperationRoundResult operationRoundResult = screenshotRoundWaitProbeOperation.CaptureAndWait(TimeSpan.FromMilliseconds(100L));
		Assert.Null(operationRoundResult.Delay);
		Assert.Equal(TimeSpan.FromMilliseconds(100L), operationRoundResult.DelayUntilRoundTime);
	}

	/// <summary>
	/// 业务层不得再自行扣减截图耗时：目标时长必须原样进入补足制通道。
	/// </summary>
	[Fact]
	public void RoundWaitForScreenshotRound_DoesNotSubtractCaptureCostItself()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		using Mat mat = new Mat(8, 9, MatType.CV_8UC3, Scalar.All(11.0));
		zContext.AttachController(new RecordingController(mat.Clone(), TimeSpan.FromMilliseconds(40L)));
		ScreenshotRoundWaitProbeOperation screenshotRoundWaitProbeOperation = new ScreenshotRoundWaitProbeOperation(zContext);
		OperationRoundResult operationRoundResult = screenshotRoundWaitProbeOperation.CaptureAndWait(TimeSpan.FromMilliseconds(20L));
		Assert.Null(operationRoundResult.Delay);
		Assert.Equal(TimeSpan.FromMilliseconds(20L), operationRoundResult.DelayUntilRoundTime);
	}

	[Fact]
	public void RoundByFindArea_ConvertsScreenUtilsResultToRoundResult()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTextScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 6, 5, 20, 8, "领取奖励") });
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.FindArea(screen, "文本界面", "奖励文本");
		OperationRoundResult operationRoundResult2 = helperProbeOperation.FindArea(screen, "文本界面", "不存在文本");
		OperationRoundResult operationRoundResult3 = helperProbeOperation.FindArea(screen, "文本界面", "不存在区域");
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("奖励文本", operationRoundResult.Status);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
		Assert.Equal("未找到 不存在文本", operationRoundResult2.Status);
		Assert.Equal(OperationRoundResultKind.Fail, operationRoundResult3.Kind);
		Assert.Equal("区域未配置 不存在区域", operationRoundResult3.Status);
	}

	[Fact]
	public void RoundByFindAndClickArea_ClicksMatchedAreaAndReportsFailureModes()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTextScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 6, 5, 20, 8, "领取奖励") });
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.FindAndClickArea(screen, "文本界面", "奖励文本");
		OperationRoundResult operationRoundResult2 = helperProbeOperation.FindAndClickArea(screen, "文本界面", "不存在文本");
		OperationRoundResult operationRoundResult3 = helperProbeOperation.FindAndClickArea(screen, "文本界面", "不存在区域");
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("奖励文本", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(17, 10), recordingController.LastClickPoint);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
		Assert.Equal("未找到 不存在文本", operationRoundResult2.Status);
		Assert.Equal(OperationRoundResultKind.Fail, operationRoundResult3.Kind);
		Assert.Equal("区域未配置 不存在区域", operationRoundResult3.Status);
	}

	[Fact]
	public void RoundByOcr_UsesGameTextResolverAndKeepsPythonSourceStatus()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.GameTextResolver = (string text) => (text == "领取奖励") ? "Claim Reward" : text;
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 6, 5, 20, 8, "Claim Reward") });
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.FindOcr(screen, "领取奖励", null);
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("领取奖励", operationRoundResult.Status);
	}

	[Fact]
	public void RoundByFindAndClickArea_WithoutControllerReturnsRetry()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTextScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 6, 5, 20, 8, "领取奖励") });
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.FindAndClickArea(screen, "文本界面", "奖励文本");
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("点击失败 奖励文本", operationRoundResult.Status);
	}

	[Fact]
	public void RoundByFindAndClickArea_WaitsUntilConfiguredAreaAppearsAfterClick()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTextScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 6, 5, 20, 8, "领取奖励") });
		RecordingController controller = new RecordingController();
		zContext.AttachController(controller);
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.FindAndClickUntilFound(screen, "文本界面", "奖励文本", new (string, string)[] { ("文本界面", "确认文本") });
		Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
		Assert.True(helperProbeOperation.ExposedNodeClicked);
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 6, 5, 20, 8, "确认已出现") });
		OperationRoundResult operationRoundResult2 = helperProbeOperation.FindAndClickUntilFound(screen, "文本界面", "奖励文本", new (string, string)[] { ("文本界面", "确认文本") });
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult2.Kind);
		Assert.Equal("奖励文本", operationRoundResult2.Status);
	}

	[Fact]
	public void RoundByOcr_UsesConfiguredAreaAndReturnsTargetText()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 4, 5, 20, 8, "领取奖励") });
		using Mat screen = CreateScreen();
		OneDragon.Core.Screen.ScreenArea area = new OneDragon.Core.Screen.ScreenArea
		{
			AreaName = "奖励文本",
			PcRect = new OneDragon.Core.Abstractions.Geometry.Rect(1, 1, 40, 30)
		};
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.FindOcr(screen, "领取奖励", area);
		OperationRoundResult operationRoundResult2 = helperProbeOperation.FindOcr(screen, "每日任务", area);
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("领取奖励", operationRoundResult.Status);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
		Assert.Equal("找不到 每日任务", operationRoundResult2.Status);
	}

	[Fact]
	public void RoundByOperationResult_MapsFinalResultToRoundResult()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(context);
		object obj = new object();
		OperationRoundResult operationRoundResult = helperProbeOperation.FromOperationResult(new OperationResult(IsSuccess: true, "原状态", obj), "覆盖状态");
		OperationRoundResult operationRoundResult2 = helperProbeOperation.FromOperationResult(new OperationResult(IsSuccess: false, "失败", obj));
		OperationRoundResult operationRoundResult3 = helperProbeOperation.FromOperationResult(new OperationResult(IsSuccess: false, "失败", obj), null, retryOnFail: true);
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("覆盖状态", operationRoundResult.Status);
		Assert.Same(obj, operationRoundResult.Data);
		Assert.Equal(OperationRoundResultKind.Fail, operationRoundResult2.Kind);
		Assert.Equal("失败", operationRoundResult2.Status);
		Assert.Same(obj, operationRoundResult2.Data);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult3.Kind);
		Assert.Equal("失败", operationRoundResult3.Status);
		Assert.Same(obj, operationRoundResult3.Data);
	}

	[Fact]
	public void RoundByClickArea_ClicksConfiguredCenter()
	{
		WriteClickOnlyScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.ClickArea("点击界面", "确认");
		OperationRoundResult operationRoundResult2 = helperProbeOperation.ClickArea("点击界面", "不存在区域");
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("确认", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(30, 40), recordingController.LastClickPoint);
		Assert.Equal(OperationRoundResultKind.Fail, operationRoundResult2.Kind);
		Assert.Equal("区域未配置 不存在区域", operationRoundResult2.Status);
	}

	[Fact]
	public async Task ExecuteAsync_FollowsStatusEdgesAndResetsNodeClickedPerNode()
	{
		WriteClickOnlyScreenYaml();
		using ZContext context = CreateContextWithScreenConfig();
		RecordingController controller = new RecordingController();
		context.AttachController(controller);
		NodeClickedProbeOperation operation = new NodeClickedProbeOperation(context);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("第二节点完成", result.Status);
		Assert.Equal(new List<bool>(2) { true, false }, operation.NodeClickedSnapshots);
		Assert.Equal(2, operation.RoundCount);
	}

	[Fact]
	public async Task ExecuteAsync_DispatchesCurrentSuccessNotificationAfterSelectingNodeResult()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		RecordingNotificationService push = (RecordingNotificationService)(context.PushNotificationService = new RecordingNotificationService());
		NotificationProbeOperation operation = new NotificationProbeOperation(context);
		OperationResult result = await operation.ExecuteAsync();
		var (title, content) = await push.Next.Task.WaitAsync(TimeSpan.FromSeconds(2L));
		Assert.True(result.IsSuccess);
		Assert.Equal("一条龙运行通知", title);
		Assert.Equal("任务「节点通知测试」节点「领取」" + Environment.NewLine + "运行「成功」", content);
	}

	[Fact]
	public void RoundByClickArea_ClicksLeftTopAndAppliesGotoList()
	{
		WriteClickOnlyScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.ClickArea("点击界面", "确认", clickLeftTop: true);
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("确认", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(10, 20), recordingController.LastClickPoint);
		Assert.Equal("结果界面", zContext.ScreenContext.CurrentScreenName);
		Assert.True(helperProbeOperation.ExposedNodeClicked);
	}

	[Fact]
	public void RoundByOcrAndClickByPriority_ClicksFirstMatchedPriorityWithOffset()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 4, 6, 10, 8, "普通项"),
			new OcrMatchResult(0.99, 25, 30, 20, 10, "特选奖")
		});
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		using Mat screen = CreateScreen();
		OneDragon.Core.Screen.ScreenArea area = new OneDragon.Core.Screen.ScreenArea
		{
			AreaName = "奖励列表",
			PcRect = new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 80, 60),
			PcAlt = true,
			GamepadKey = "confirm"
		};
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.OcrAndClickByPriority(screen, new string[2] { "特选奖", "普通项" }, area, new OneDragon.Core.Abstractions.Geometry.Point(3, -2));
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("特选奖", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(38, 33), recordingController.LastClickPoint);
		Assert.True(recordingController.LastClickPcAlt);
		Assert.Equal("confirm", recordingController.LastClickGamepadAction);
	}

	[Fact]
	public void RoundByOcrAndClickByPriority_IgnoresStatusTextWithoutHidingTargetText()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 4, 6, 40, 10, "自动托管中"),
			new OcrMatchResult(0.99, 25, 30, 40, 10, "经营方针")
		});
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		IReadOnlyList<string> targetTextList = new string[4] { "托管中", "自动托管中", "经营方针", "经营" };
		IReadOnlyList<string> ignoreTextList = new string[2] { "自动托管中", "经营" };
		OperationRoundResult operationRoundResult = helperProbeOperation.OcrAndClickByPriority(screen, targetTextList, null, null, 0.6, ignoreTextList);
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("经营方针", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(45, 35), recordingController.LastClickPoint);
	}

	[Fact]
	public void RoundByOcrAndClickByPriority_ClicksBestTargetInsteadOfEarlierPartialMatch()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 440, 380, 56, 20, "托管效率"),
			new OcrMatchResult(0.96, 1410, 920, 90, 32, "停止托管")
		});
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.OcrAndClickByPriority(screen, new string[] { "停止托管" }, null);
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("停止托管", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1455, 936), recordingController.LastClickPoint);
	}

	[Fact]
	public void RoundByOcrAndClickByPriority_UsesSuppliedExactLcsThreshold()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 4, 6, 10, 8, "10O") });
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.OcrAndClickByPriority(screen, new string[] { "100" }, null, null, 1.0);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("找不到 100", operationRoundResult.Status);
		Assert.Null(recordingController.LastClickPoint);
	}

	[Fact]
	public void RoundByOcrAndClickByPriority_EmptyTargetsOrMissingControllerReturnsRetry()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 4, 6, 10, 8, "奖励") });
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.OcrAndClickByPriority(screen, Array.Empty<string>(), null);
		OperationRoundResult operationRoundResult2 = helperProbeOperation.OcrAndClickByPriority(screen, new string[] { "奖励" }, null);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("未指定 OCR 文本", operationRoundResult.Status);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
		Assert.Equal("点击失败", operationRoundResult2.Status);
	}

	[Fact]
	public void RoundByGotoScreen_ClicksFirstRouteNodeAndUpdatesCurrentScreen()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGotoScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 2, 2, 20, 8, "CURMARK"),
			new OcrMatchResult(0.99, 10, 8, 18, 8, "OPENNEXT")
		});
		RecordingController recordingController = new RecordingController();
		zContext.AttachController(recordingController);
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.GotoScreen(screen, "目标界面");
		Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
		Assert.Equal("跳转按钮", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(39, 24), recordingController.LastClickPoint);
		Assert.Equal("目标界面", zContext.ScreenContext.CurrentScreenName);
	}

	[Fact]
	public void RoundByGotoScreen_ReturnsFailWhenRouteIsMissing()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGotoScreenYaml(includeRoute: false);
		using ZContext zContext = CreateContextWithScreenConfig();
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 2, 2, 20, 8, "CURMARK") });
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);
		OperationRoundResult operationRoundResult = helperProbeOperation.GotoScreen(screen, "目标界面");
		Assert.Equal(OperationRoundResultKind.Fail, operationRoundResult.Kind);
		Assert.Equal("无法从 当前界面 前往 目标界面", operationRoundResult.Status);
	}

	[Fact]
	public void ScreenRecognitionFailure_ClearsStaleCurrentScreen()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGotoScreenYaml();
		using ZContext zContext = CreateContextWithScreenConfig();
		zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
		using Mat screen = CreateScreen();
		HelperProbeOperation helperProbeOperation = new HelperProbeOperation(zContext);

		zContext.ScreenContext.UpdateCurrentScreenName("当前界面");
		OperationRoundResult operationRoundResult = helperProbeOperation.GotoScreen(screen, "目标界面");
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("未能识别当前画面", operationRoundResult.Status);
		Assert.Null(zContext.ScreenContext.CurrentScreenName);

		zContext.ScreenContext.UpdateCurrentScreenName("当前界面");
		Assert.Null(helperProbeOperation.CheckScreen(screen, new string[] { "目标界面" }));
		Assert.Null(zContext.ScreenContext.CurrentScreenName);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	private ZContext CreateContextWithScreenConfig()
	{
		ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.ScreenContext.Reload();
		return zContext;
	}

	private void WriteTextScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: text_screen\n  screen_name: 文本界面\n  area_list:\n    - area_name: 奖励文本\n      id_mark: true\n      pc_rect: [1, 1, 40, 30]\n      text: 领取奖励\n      lcs_percent: 0.6\n    - area_name: 不存在文本\n      id_mark: true\n      pc_rect: [1, 1, 40, 30]\n      text: 每日任务\n      lcs_percent: 0.9\n    - area_name: 确认文本\n      id_mark: true\n      pc_rect: [1, 1, 40, 30]\n      text: 确认已出现\n      lcs_percent: 0.6");
	}

	private void WriteClickOnlyScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: click_screen\n  screen_name: 点击界面\n  area_list:\n    - area_name: 确认\n      pc_rect: [10, 20, 50, 60]\n      goto_list: [结果界面]\n- screen_id: result_screen\n  screen_name: 结果界面\n  area_list: []");
	}

	private void WriteGotoScreenYaml(bool includeRoute = true)
	{
		string text = (includeRoute ? "      goto_list: [目标界面]\n" : string.Empty);
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: current_screen\n  screen_name: 当前界面\n  area_list:\n    - area_name: 当前标识\n      id_mark: true\n      pc_rect: [0, 0, 24, 12]\n      text: CURMARK\n      lcs_percent: 0.6\n    - area_name: 跳转按钮\n      pc_rect: [20, 12, 60, 40]\n      text: OPENNEXT\n      lcs_percent: 0.6\n" + text + "- screen_id: target_screen\n  screen_name: 目标界面\n  area_list:\n    - area_name: 目标标识\n      id_mark: true\n      pc_rect: [0, 0, 24, 12]\n      text: TARGETMARK\n      lcs_percent: 0.6");
	}

	private static Mat CreateScreen()
	{
		return new Mat(new Size(80, 60), MatType.CV_8UC3, Scalar.Black);
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
