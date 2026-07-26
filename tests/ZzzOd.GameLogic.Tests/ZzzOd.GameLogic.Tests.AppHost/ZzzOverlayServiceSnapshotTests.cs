using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Events;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// ZzzOverlayService 的运行期聚合合同。
/// </summary>
public sealed class ZzzOverlayServiceSnapshotTests
{
	/// <summary>
	/// AppHost 服务应接收当前上下文的视觉事件、运行状态、结构化调试数据和日志，不依赖 GUI。
	/// </summary>
	[Fact]
	public async Task SnapshotMapsRuntimeEventsAndLogFanOutWithoutGui()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(runRoot));
			ZzzBackendEventBus eventBus = new();
			using ZzzLogFanOutLoggerProvider logProvider = new(new ZzzRunRoot(runRoot), eventBus);
			ILogger logger = logProvider.CreateLogger("overlay-test");
			logger.LogInformation("initial log");
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime, eventBus, logProvider);
			runtime.EnsureContext();
			service.GetSnapshot();

			context.EventBus.Publish("Overlay.Vision", new VisionDrawEventPayload(
			[
				new VisionDrawItem("yolo", "target", 10, 20, 60, 80)
				{
					Score = 0.9d,
					TtlSeconds = 20d
				}
			]));
			context.OverlayDebugBus.PublishOperation(new OperationTraceItem(
			"test-app",
			"test-operation",
			"current-node",
			"previous-node",
			"next-node",
			2,
			"success",
			"done",
			DateTimeOffset.UtcNow));
			context.OverlayDebugBus.PublishDecision(new DecisionTraceItem(
			"test",
			"trigger",
			"expression",
			"operation",
			"running",
			DateTimeOffset.UtcNow));
			context.OverlayDebugBus.PublishTimeline(new TimelineItem(
			"operation",
			"node",
			"detail",
			"information",
			DateTimeOffset.UtcNow));
			context.OverlayDebugBus.PublishPerformance(new PerformanceMetricSample("ocr_ms", 12.5d, "ms", DateTimeOffset.UtcNow));
			Assert.True(context.RunContext.StartRunning());
			logger.LogWarning("live log");

			await WaitForAsync(() => service.GetSnapshot().VisionFrame?.Items.Count == 1);
			ZzzOverlaySnapshotDto snapshot = service.GetSnapshot();

			Assert.NotNull(snapshot.VisionFrame);
			Assert.Single(snapshot.VisionFrame.Items);
			Assert.NotNull(snapshot.State);
			Assert.Equal("Running", snapshot.State.RunState);
			Assert.Equal("current-node", snapshot.State.CurrentNode);
			Assert.Equal(2, snapshot.State.NodeRetry);
			Assert.Single(snapshot.Operations);
			Assert.Single(snapshot.Decisions);
			Assert.Single(snapshot.Timeline);
			Assert.Contains(snapshot.Performance, sample => sample.Metric == "ocr_ms" && sample.Value == 12.5d);
			Assert.Contains(snapshot.Logs, entry => entry.Message == "initial log");
			Assert.Contains(snapshot.Logs, entry => entry.Message == "live log");

			context.RunContext.SwitchContextPauseAndRun();
			await WaitForAsync(() => service.GetSnapshot().State?.RunState == "Pause");
			await context.RunContext.StopRunningAsync();
			await WaitForAsync(() => service.GetSnapshot().State?.RunState == "Stop");
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 筛选只影响 GUI 快照；连续且重叠的同类 YOLO 框保留最新项，不同目标和类别继续保留。
	/// </summary>
	[Fact]
	public void SnapshotFiltersSourcesPanelsMetricsAndDeduplicatesYolo()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(runRoot));
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();
			DateTimeOffset now = DateTimeOffset.UtcNow;
			PublishVision(context, "yolo", "enemy", 0, 0, 100, 100, now.AddSeconds(-2));
			PublishVision(context, "yolo", "enemy", 2, 2, 102, 102, now.AddSeconds(-2));
			PublishVision(context, "yolo", "enemy", 400, 400, 500, 500, now.AddSeconds(-1));
			PublishVision(context, "yolo", "elite", 2, 2, 102, 102, now);
			PublishVision(context, "ocr", "text", 100, 100, 200, 200, now);
			context.OverlayDebugBus.PublishVision(new VisionDrawItem("yolo", "expired", 0, 0, 10, 10)
			{
				Created = now.AddSeconds(-2d).ToUnixTimeMilliseconds() / 1000d,
				TtlSeconds = 0.1d
			});
			context.OverlayDebugBus.PublishOperation(new OperationTraceItem("app", "operation", "node", null, null, 0, null, null, now));
			context.OverlayDebugBus.PublishDecision(new DecisionTraceItem("source", "trigger", "expression", "operation", "status", now));
			context.OverlayDebugBus.PublishTimeline(new TimelineItem("kind", "title", "detail", "information", now));
			context.OverlayDebugBus.PublishPerformance(new PerformanceMetricSample("ocr_ms", 1d, "ms", now));
			context.OverlayDebugBus.PublishPerformance(new PerformanceMetricSample("yolo_ms", 2d, "ms", now));
			context.OverlayDebugBus.PublishPerformance(new PerformanceMetricSample("expired_ms", 3d, "ms", now.AddSeconds(-2d), 0.1d));
			service.ConfigureDisplay(new ZzzOverlayDisplayOptionsDto
			{
				VisionLayerEnabled = true,
				ShowYolo = true,
				ShowOcr = false,
				PanelEnabledMap = new Dictionary<string, bool>(StringComparer.Ordinal)
				{
					["state"] = false,
					["battle"] = false,
					["decision"] = false,
					["timeline"] = false,
					["performance"] = true,
					["log"] = false
				},
				PerformanceMetricEnabledMap = new Dictionary<string, bool>(StringComparer.Ordinal)
				{
					["ocr_ms"] = false,
					["yolo_ms"] = true
				}
			});

			ZzzOverlaySnapshotDto snapshot = service.GetSnapshot();

			Assert.NotNull(snapshot.VisionFrame);
			Assert.Equal(3, snapshot.VisionFrame.Items.Count);
			Assert.DoesNotContain(snapshot.VisionFrame.Items, item => item.Id == "ocr:text");
			Assert.DoesNotContain(snapshot.VisionFrame.Items, item => item.Id == "yolo:expired");
			Assert.DoesNotContain(snapshot.VisionFrame.Items, item => item.Id == "yolo:enemy" && item.Bounds.X == 0d);
			Assert.Null(snapshot.State);
			Assert.Empty(snapshot.Operations);
			Assert.Empty(snapshot.Decisions);
			Assert.Empty(snapshot.Timeline);
			Assert.Single(snapshot.Performance);
			Assert.Equal("yolo_ms", snapshot.Performance[0].Metric);
			Assert.DoesNotContain(snapshot.Performance, sample => sample.Metric == "expired_ms");
			Assert.Empty(snapshot.Logs);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 直接配置展示 DTO 时，YOLO 去重阈值也必须处理 NaN 和范围外数值。
	/// </summary>
	[Fact]
	public void SnapshotNormalizesDirectYoloDedupThresholdForDeduplication()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(runRoot));
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();
			DateTimeOffset now = DateTimeOffset.UtcNow;
			PublishVision(context, "yolo", "enemy", 0, 0, 100, 100, now.AddSeconds(-1));
			PublishVision(context, "yolo", "enemy", 25, 0, 125, 100, now);

			service.ConfigureDisplay(new ZzzOverlayDisplayOptionsDto
			{
				YoloDedupIouThreshold = double.NaN,
			});
			Assert.Single(service.GetSnapshot().VisionFrame!.Items);

			service.ConfigureDisplay(new ZzzOverlayDisplayOptionsDto
			{
				YoloDedupIouThreshold = 2d,
			});
			Assert.Equal(2, service.GetSnapshot().VisionFrame!.Items.Count);

			service.ConfigureDisplay(new ZzzOverlayDisplayOptionsDto
			{
				YoloDedupIouThreshold = -1d,
			});
			Assert.Single(service.GetSnapshot().VisionFrame!.Items);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public void SnapshotDisplayFilteringDoesNotChangePublishedVisionOrOperationData()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(runRoot));
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();
			DateTimeOffset now = DateTimeOffset.UtcNow;
			PublishVision(context, "yolo", "target", 10, 20, 30, 40, now);
			PublishVision(context, "ocr", "text", 20, 30, 40, 50, now);
			PublishVision(context, "template", "button", 30, 40, 50, 60, now);
			PublishVision(context, "cv", "pipeline", 40, 50, 60, 70, now);
			context.OverlayDebugBus.PublishOperation(new OperationTraceItem("app", "operation", "node", null, null, 0, "success", "done", now));
			service.ConfigureDisplay(new ZzzOverlayDisplayOptionsDto
			{
				VisionLayerEnabled = false,
				PanelEnabledMap = new Dictionary<string, bool>(StringComparer.Ordinal)
				{
					["state"] = false,
				}
			});

			ZzzOverlaySnapshotDto filtered = service.GetSnapshot();
			OverlayDebugSnapshot published = context.OverlayDebugBus.Snapshot(now);

			Assert.Null(filtered.VisionFrame);
			Assert.Empty(filtered.Operations);
			Assert.Equal(["cv", "ocr", "template", "yolo"], published.VisionItems.Select(item => item.Source).OrderBy(source => source, StringComparer.Ordinal));
			Assert.Single(published.OperationItems);

			service.ConfigureDisplay(new ZzzOverlayDisplayOptionsDto());
			ZzzOverlaySnapshotDto restored = service.GetSnapshot();

			Assert.Equal(["cv", "ocr", "template", "yolo"], restored.VisionFrame!.Items.Select(item => item.Id.Split(':')[0]).OrderBy(source => source, StringComparer.Ordinal));
			Assert.Single(restored.Operations);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public async Task SnapshotBridgesLegacyVisionAndSkipsAlreadyPublishedItems()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(runRoot));
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();
			_ = service.GetSnapshot();
			VisionDrawItem publishedItem = new("compatibility", "already-published", 1, 2, 3, 4)
			{
				TtlSeconds = 30d,
			};
			Assert.True(context.OverlayDebugBus.PublishVision(publishedItem));
			context.EventBus.Publish("Overlay.Vision", new VisionDrawEventPayload([publishedItem], alreadyPublishedToDebugBus: true));
			context.EventBus.Publish("Overlay.Vision", new VisionDrawEventPayload(
			[
				new VisionDrawItem("compatibility", "legacy", 10, 20, 30, 40)
				{
					TtlSeconds = 30d,
				}
			]));

			await WaitForAsync(() => context.OverlayDebugBus.Snapshot().VisionItems.Any(item => item.Label == "legacy"));
			await Task.Delay(50);
			IReadOnlyList<VisionDrawItem> items = context.OverlayDebugBus.Snapshot().VisionItems
				.Where(item => item.Source == "compatibility")
				.ToArray();

			Assert.Equal(2, items.Count);
			Assert.Contains(items, item => item.Label == "already-published");
			Assert.Contains(items, item => item.Label == "legacy");
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 自动战斗上下文未初始化时状态快照保持为空，初始化后映射真实运行状态。
	/// </summary>
	[Fact]
	public void SnapshotMapsAutoBattleStateWithoutInitializingLazyContext()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(runRoot));
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();

			Assert.Null(context.TryGetAutoBattleOverlayStatus());
			Assert.Null(service.GetSnapshot().State!.AutoBattle);
			Assert.Null(context.TryGetAutoBattleOverlayStatus());

			_ = context.AutoBattleContext;
			ZzzOverlayAutoBattleStateDto autoBattle = Assert.IsType<ZzzOverlayAutoBattleStateDto>(service.GetSnapshot().State!.AutoBattle);
			Assert.False(autoBattle.IsRunning);
			Assert.Null(autoBattle.FrontAgentName);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 路径点需要以 GUI 可以稳定解析的坐标文本穿过 AppHost DTO。
	/// </summary>
	[Fact]
	public void SnapshotSerializesPathPointsForGuiRenderer()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(runRoot));
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();
			context.OverlayDebugBus.PublishVision(new VisionDrawItem("path", "route", 10, 20, 50, 60)
			{
				Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
				{
					["path_points"] = new[] { new Point(10, 20), new Point(30, 40), new Point(50, 60) }
				}
			});

			ZzzOverlayDrawItemDto item = Assert.Single(service.GetSnapshot().VisionFrame!.Items);

			Assert.Equal("10,20;30,40;50,60", item.Metadata["path_points"]);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 运行时更换上下文后，服务不得继续返回旧上下文的视觉或性能数据。
	/// </summary>
	[Fact]
	public void SnapshotReleasesOldContextWhenRuntimeIsReinitialized()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext first = new(new OneDragonEnvironment(runRoot));
			ZContext second = new(new OneDragonEnvironment(runRoot));
			ZContext next = first;
			using ZzzRuntimeManager runtime = new(runRoot, NullLogger<ZzzRuntimeManager>.Instance, _ => next);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();
			PublishVision(first, "yolo", "old", 1, 2, 30, 40, DateTimeOffset.UtcNow);
			first.OverlayDebugBus.PublishPerformance(new PerformanceMetricSample("ocr_ms", 1d, "ms", DateTimeOffset.UtcNow));
			Assert.NotNull(service.GetSnapshot().VisionFrame);
			Assert.Single(service.GetSnapshot().Performance);

			next = second;
			Assert.True(runtime.ReinitializeContext().Success);
			ZzzOverlaySnapshotDto snapshot = service.GetSnapshot();

			Assert.Null(snapshot.VisionFrame);
			Assert.Empty(snapshot.Performance);
			Assert.NotNull(snapshot.State);
			Assert.Equal("Stop", snapshot.State.RunState);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 生产接口不再接受匿名绘制帧，快照只能由明确事件源生成。
	/// </summary>
	[Fact]
	public void OverlayServiceInterfaceDoesNotExposeSubmitFrame()
	{
		Assert.Null(typeof(IZzzOverlayService).GetMethod("SubmitFrame"));
		using ZzzOverlayService service = new();
		Assert.Null(service.GetSnapshot().VisionFrame);
	}

	/// <summary>
	/// AppHost 容器应解析完整聚合服务，无需创建或启动 Avalonia 窗口。
	/// </summary>
	[Fact]
	public void AppHostResolvesOverlaySnapshotServiceWithoutGui()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ServiceCollection services = new();
			services.AddLogging();
			services.AddZzzAppHost(runRoot, ZzzHostMode.ApiOnly);
			using ServiceProvider provider = services.BuildServiceProvider();
			IZzzOverlayService service = provider.GetRequiredService<IZzzOverlayService>();

			Assert.IsType<ZzzOverlayService>(service);
			Assert.Null(service.GetSnapshot().State);
			Assert.Empty(service.GetSnapshot().Logs);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	private static void PublishVision(ZContext context, string source, string label, int x1, int y1, int x2, int y2, DateTimeOffset createdAt)
	{
		context.OverlayDebugBus.PublishVision(new VisionDrawItem(source, label, x1, y1, x2, y2)
		{
			Created = createdAt.ToUnixTimeMilliseconds() / 1000d,
			TtlSeconds = 30d
		});
	}

	private static async Task WaitForAsync(Func<bool> condition)
	{
		DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5d);
		while (!condition() && DateTimeOffset.UtcNow < deadline)
		{
			await Task.Delay(20);
		}

		Assert.True(condition());
	}

	private static string CreateTempRoot()
	{
		string root = Path.Combine(Path.GetTempPath(), "zzz-overlay-service-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		return root;
	}
}
