using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Events;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;
using ZzzOd.GameLogic.Context;
using ZzzOd.Gui.Overlay;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// Overlay 设置页和 BaselineParity overlay.yml 的定向合同。
/// </summary>
public sealed class OverlaySettingsParityTests
{
	private class OverlayBackendProxy : DispatchProxy
	{
		public ZzzConfigScopeService Scopes { get; set; } = null;

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			if (args == null)
			{
				args = Array.Empty<object>();
			}
			string name = targetMethod.Name;
			if (1 == 0)
			{
			}
			ZzzBackendResult<ZzzConfigScopeValuesDto> result;
			if (!(name == "GetConfigScope"))
			{
				if (!(name == "SaveConfigScope"))
				{
					throw new NotSupportedException(targetMethod.Name);
				}
				result = Scopes.Save((ZzzSaveConfigScopeRequest)args[0]);
			}
			else
			{
				result = Scopes.Read((string)args[0], (int?)args[1], (string)args[2]);
			}
			if (1 == 0)
			{
			}
			return result;
		}
	}

	private static readonly string RepoRoot = FindRepoRoot();

	/// <summary>
	/// 配置缺失时应返回 BaselineParity 底层默认值，同时保持真实文件未创建状态。
	/// </summary>
	[Fact]
	public void OverlayScopeReadsPythonDefaultsWithoutCreatingDemoConfig()
	{
		string text = CreateTempRoot();
		try
		{
			ZzzConfigScopeService zzzConfigScopeService = new ZzzConfigScopeService(text);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = zzzConfigScopeService.Read("overlay", null, null);
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			IReadOnlyDictionary<string, object> values = zzzBackendResult.Value.Values;
			Assert.False(Assert.IsType<bool>(values["enabled"]));
			Assert.True(Assert.IsType<bool>(values["visible"]));
			Assert.True(Assert.IsType<bool>(values["anti_capture"]));
			Assert.Equal("o", values["toggle_hotkey"]);
			Assert.Equal(12, values["font_size"]);
			Assert.Equal(70, values["panel_opacity"]);
			Assert.Equal(12, values["log_fade_seconds"]);
			Assert.Equal(200, values["state_poll_interval_ms"]);
			Assert.True(Assert.IsType<bool>(values["timeline_panel_enabled"]));
			Dictionary<string, bool> dictionary = Assert.IsType<Dictionary<string, bool>>(values["performance_metric_enabled_map"]);
			Assert.All(dictionary.Values, Assert.True);
			Assert.False(File.Exists(Path.Combine(text, "config", "overlay.yml")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// 保存应写入 overlay 根节点，执行 BaselineParity 规范化并保留未知数据。
	/// </summary>
	[Fact]
	public void OverlayScopeWritesNestedYamlNormalizesValuesAndPreservesUnknownData()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config");
			Directory.CreateDirectory(text2);
			string path = Path.Combine(text2, "overlay.yml");
			File.WriteAllText(path, "root_marker: keep\noverlay:\n  unknown_key: keep\n  performance_metric_enabled_map:\n    custom_ms: false\n");
			ZzzConfigScopeService zzzConfigScopeService = new ZzzConfigScopeService(text);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = zzzConfigScopeService.Save(new ZzzSaveConfigScopeRequest("overlay", new Dictionary<string, object>
			{
				["enabled"] = true,
				["vision_scale_x"] = 9.0,
				["font_size"] = 100,
				["panel_text_color"] = "invalid",
				["patched_capture_suffix"] = "capture",
				["performance_metric_enabled_map"] = new Dictionary<string, bool> { ["ocr_ms"] = false }
			}));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			IReadOnlyDictionary<string, object> values = zzzBackendResult.Value.Values;
			Assert.True(Assert.IsType<bool>(values["enabled"]));
			Assert.Equal(1.5, Assert.IsType<double>(values["vision_scale_x"]));
			Assert.Equal(28, values["font_size"]);
			Assert.Equal("#f2f2f2", values["panel_text_color"]);
			Assert.Equal("_capture", values["patched_capture_suffix"]);
			Dictionary<string, bool> dictionary = Assert.IsType<Dictionary<string, bool>>(values["performance_metric_enabled_map"]);
			Assert.False(dictionary["ocr_ms"]);
			Assert.False(dictionary["custom_ms"]);
			string actualString = File.ReadAllText(path);
			Assert.Contains("root_marker: keep", actualString, StringComparison.Ordinal);
			Assert.Contains("unknown_key: keep", actualString, StringComparison.Ordinal);
			Assert.Contains("overlay:", actualString, StringComparison.Ordinal);
			Assert.DoesNotContain("overlay_layout.json", actualString, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// Controller 初始化时应直接使用真实 overlay scope，不依赖打开设置页或旧 JSON 布局文件。
	/// </summary>
	[Fact]
	public void OverlayControllerLoadsYamlScopeBeforeSettingsPageIsOpened()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "overlay.yml"), "overlay:\n  enabled: true\n  visible: false\n  toggle_hotkey: k\n  font_size: 18\n  panel_opacity: 55\n  vision_offset_x: 23\n  log_panel_enabled: false");
			ZzzConfigScopeService scopes = new ZzzConfigScopeService(text);
			IZzzAppBackend backend = CreateBackend(scopes);
			ZzzOverlayService zzzOverlayService = new ZzzOverlayService();
			ZzzOverlayController zzzOverlayController = new ZzzOverlayController(zzzOverlayService, backend);
			Assert.True(zzzOverlayController.Settings.Enabled);
			Assert.False(zzzOverlayController.Settings.ShowByDefault);
			Assert.Equal("k", zzzOverlayController.Settings.Hotkey);
			Assert.Equal(18.0, zzzOverlayController.Settings.FontSize);
			Assert.Equal(0.55, zzzOverlayController.Settings.Opacity);
			Assert.Equal(23, zzzOverlayController.Settings.Visual.OffsetX);
			Assert.False(zzzOverlayController.Settings.Panels.Single((ZzzOverlayPanelSettings panel) => panel.Id == "log").Enabled);
			Assert.True(zzzOverlayService.GetStatus().Enabled);
			Assert.False(File.Exists(Path.Combine(text2, "overlay_layout.json")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// Controller 保存和重置位置应继续走 overlay scope，并保留 YAML 未知数据。
	/// </summary>
	[Fact]
	public void OverlayControllerSavesAndResetsGeometryThroughYamlScope()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config");
			Directory.CreateDirectory(text2);
			string path = Path.Combine(text2, "overlay.yml");
			File.WriteAllText(path, "root_marker: keep\noverlay:\n  unknown_key: keep\n");
			ZzzConfigScopeService scopes = new ZzzConfigScopeService(text);
			IZzzAppBackend backend = CreateBackend(scopes);
			ZzzOverlayController zzzOverlayController = new ZzzOverlayController(new ZzzOverlayService(), backend);
			Dictionary<string, object> dictionary = ZzzOverlaySettingsMapper.DefaultPanelGeometry();
			Dictionary<string, object> dictionary2 = Assert.IsType<Dictionary<string, object>>(dictionary["log_panel"]);
			dictionary2["x"] = 777;
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = zzzOverlayController.SaveConfiguration(new Dictionary<string, object>
			{
				["font_size"] = 100,
				["panel_geometry"] = dictionary
			});
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.Equal(28.0, zzzOverlayController.Settings.FontSize);
			Assert.Equal(777.0, zzzOverlayController.Settings.Panels.Single((ZzzOverlayPanelSettings panel) => panel.Id == "log").X);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = zzzOverlayController.ResetPanelGeometry();
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.Equal(100.0, zzzOverlayController.Settings.Panels.Single((ZzzOverlayPanelSettings panel) => panel.Id == "log").X);
			IReadOnlyDictionary<string, object?> resetGeometry = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(zzzBackendResult2.Value.Values["panel_geometry"]);
			IReadOnlyDictionary<string, object?> resetLogPanel = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(resetGeometry["log_panel"]);
			Assert.Equal(2, Convert.ToInt32(resetLogPanel["layout_version"]));
			Assert.Equal(0d, Convert.ToDouble(resetLogPanel["locked_x"]));
			Assert.Equal(0d, Convert.ToDouble(resetLogPanel["locked_y"]));
			Assert.Equal(0d, Convert.ToDouble(resetLogPanel["locked_w"]));
			Assert.Equal(0d, Convert.ToDouble(resetLogPanel["locked_h"]));
			Assert.Equal(100d, Convert.ToDouble(resetLogPanel["free_x"]));
			Assert.Equal(100d, Convert.ToDouble(resetLogPanel["free_y"]));
			Assert.Equal(480d, Convert.ToDouble(resetLogPanel["free_w"]));
			Assert.Equal(200d, Convert.ToDouble(resetLogPanel["free_h"]));
			Assert.Equal(96u, Convert.ToUInt32(resetLogPanel["free_dpi"]));
			string actualString = File.ReadAllText(path);
			Assert.Contains("root_marker: keep", actualString, StringComparison.Ordinal);
			Assert.Contains("unknown_key: keep", actualString, StringComparison.Ordinal);
			Assert.False(File.Exists(Path.Combine(text2, "overlay_layout.json")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// 无有效游戏窗口时，全局模式切换只保存目标模式和延迟转换标记，不能用默认屏幕坐标猜测锁定布局。
	/// </summary>
	[Fact]
	public void OverlayControllerDefersGlobalPanelModeConversionUntilGameGeometryIsAvailable()
	{
		string root = CreateTempRoot();
		try
		{
			ZzzOverlayController controller = new ZzzOverlayController(
				new ZzzOverlayService(),
				CreateBackend(new ZzzConfigScopeService(root)));

			ZzzBackendResult<ZzzConfigScopeValuesDto> saved = controller.SaveConfiguration(new Dictionary<string, object?>
			{
				["panel_lock_to_game_window"] = false,
			});

			Assert.True(saved.Success, saved.Error);
			Assert.False(controller.Settings.PanelLockToGameWindow);
			Assert.All(controller.Settings.Panels, panel =>
			{
				Assert.True(panel.IsFreeMode);
				Assert.False(panel.PendingSourceIsFreeMode ?? true);
			});
			IReadOnlyDictionary<string, object?> geometry = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(saved.Value.Values["panel_geometry"]);
			IReadOnlyDictionary<string, object?> logPanel = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(geometry["log_panel"]);
			Assert.False(Convert.ToBoolean(logPanel["pending_source_free_mode"]));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// 保存 Overlay scope 后，当前运行时快照应立即按同一份配置过滤，不需要重启 GUI。
	/// </summary>
	[Fact]
	public void SavingOverlayDisplaySettingsReloadsTheLiveSnapshot()
	{
		string root = CreateTempRoot();
		try
		{
			ZContext context = new(new OneDragonEnvironment(root));
			using ZzzRuntimeManager runtime = new(root, NullLogger<ZzzRuntimeManager>.Instance, _ => context);
			using ZzzOverlayService service = new(runtime);
			runtime.EnsureContext();
			service.GetSnapshot();
			context.OverlayDebugBus.PublishVision(new VisionDrawItem("yolo", "target", 10, 20, 60, 80)
			{
				TtlSeconds = 20d,
			});

			ZzzConfigScopeService scopes = new(root);
			ZzzOverlayController controller = new(service, CreateBackend(scopes));
			Assert.NotNull(service.GetSnapshot().VisionFrame);

			ZzzBackendResult<ZzzConfigScopeValuesDto> saved = controller.SaveConfiguration(new Dictionary<string, object>
			{
				["vision_yolo_enabled"] = false,
			});

			Assert.True(saved.Success, saved.Error);
			Assert.False(controller.Settings.Visual.ShowYolo);
			Assert.Null(service.GetSnapshot().VisionFrame);
			ZzzBackendResult<ZzzConfigScopeValuesDto> reread = scopes.Read("overlay", null, null);
			Assert.True(reread.Success, reread.Error);
			Assert.False(Assert.IsType<bool>(reread.Value.Values["vision_yolo_enabled"]));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// 产品接线不应继续注册旧布局存储或保留不可达的动态 Overlay 设置页。
	/// </summary>
	[Fact]
	public void GuiWiringDoesNotContainLegacyOverlayLayoutStoreOrDynamicSettingsPath()
	{
		string text = Path.Combine(RepoRoot, "src", "ZzzOd.Gui");
		string actualString = File.ReadAllText(Path.Combine(text, "Program.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(text, "Overlay", "ZzzOverlayController.cs"));
		Assert.DoesNotContain("ZzzOverlayLayoutStore", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzOverlayLayoutStore", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("overlay_layout.json", Directory.GetFiles(text, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Aggregate(string.Empty, string.Concat), StringComparison.Ordinal);
	}

	/// <summary>
	/// Overlay 可见字符串不得带入迁移、实现或测试说明。
	/// </summary>
	[Fact]
	public void OverlaySourcesDoNotContainExplanationPlaceholders()
	{
		string guiRoot = Path.Combine(RepoRoot, "src", "ZzzOd.Gui");
		string[] files = Directory
			.EnumerateFiles(guiRoot, "*.*", SearchOption.AllDirectories)
			.Where(path => path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
				path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
				path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
			.Where(path => path.Contains($"{Path.DirectorySeparatorChar}Overlay{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
				path.EndsWith($"{Path.DirectorySeparatorChar}ZzzOverlaySettingsPage.axaml", StringComparison.OrdinalIgnoreCase) ||
				path.EndsWith($"{Path.DirectorySeparatorChar}ZzzOverlaySettingsPage.cs", StringComparison.OrdinalIgnoreCase))
			.ToArray();
		string[] prohibited =
		[
			"显示运行日志和最近事件",
			"显示当前任务",
			"对应 Python 的",
			"后端尚未实现",
			"当前使用 fallback",
			"用于截图验证",
			"已通过测试",
		];

		Assert.NotEmpty(files);
		foreach (string file in files)
		{
			string source = File.ReadAllText(file);
			foreach (string text in prohibited)
			{
				Assert.DoesNotContain(text, source, StringComparison.Ordinal);
			}
		}
	}

	/// <summary>
	/// GUI 宿主应启动真实 Overlay 生命周期、周期刷新和 Ctrl+Alt 配置热键。
	/// </summary>
	[Fact]
	public void GuiHostWiresOverlayLifecycleRefreshAndConfiguredHotkey()
	{
		string path = Path.Combine(RepoRoot, "src", "ZzzOd.Gui");
		string actualString = File.ReadAllText(Path.Combine(path, "Shell", "ZzzShellWindowRuntime.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Overlay", "ZzzOverlayController.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "Overlay", "ZzzOverlayTechnicalWindow.cs"));
		Assert.Contains("_overlayController.Start()", actualString, StringComparison.Ordinal);
		Assert.Contains("_globalInputMonitor.InputPressed += OnGlobalInputPressed", actualString, StringComparison.Ordinal);
		Assert.Contains("TryToggleFromHotkey(key)", actualString, StringComparison.Ordinal);
		Assert.Contains("Settings.StatePollIntervalMs", actualString2, StringComparison.Ordinal);
		Assert.Contains("GetAsyncKeyState", actualString2, StringComparison.Ordinal);
		Assert.Contains("Refresh(null)", actualString2, StringComparison.Ordinal);
		Assert.Contains("overlay_refresh_ms", actualString2, StringComparison.Ordinal);
		Assert.Contains("GetSnapshot()", actualString2, StringComparison.Ordinal);
		Assert.Contains("ConfigureDisplay", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("显示识别耗时、操作耗时和刷新间隔。", actualString3, StringComparison.Ordinal);
	}

	/// <summary>
	/// 生产 Overlay 服务应接收当前 ZContext 的真实性能事件，并按核心顺序保留最新值。
	/// </summary>
	[Fact]
	public async Task OverlayServiceBindsRuntimePerformanceEventsAndDropsExpiredSamples()
	{
		string runRoot = CreateTempRoot();
		try
		{
			ZContext context = new ZContext(new OneDragonEnvironment(runRoot));
			try
			{
				using ZzzRuntimeManager runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance, (int _) => context);
				using ZzzOverlayService service = new ZzzOverlayService(runtime);
				runtime.EnsureContext();
				service.GetPerformanceSamples();
				context.EventBus.Publish("Overlay.Performance", new PerformanceMetricEventPayload(new PerformanceMetricSample("operation_round_ms", 8.5, "ms", DateTimeOffset.UtcNow)));
				context.EventBus.Publish("Overlay.Performance", new PerformanceMetricEventPayload(new PerformanceMetricSample("ocr_ms", 12.25, "ms", DateTimeOffset.UtcNow)));
				DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5.0);
				while (service.GetPerformanceSamples().Count != 2 && DateTimeOffset.UtcNow < deadline)
				{
					await Task.Delay(20);
				}
				Assert.Equal(2, service.GetPerformanceSamples().Count);
				IReadOnlyList<ZzzOverlayPerformanceSampleDto> samples = service.GetPerformanceSamples();
				Assert.Equal(new string[2] { "ocr_ms", "operation_round_ms" }, samples.Select((ZzzOverlayPerformanceSampleDto item) => item.Metric));
				Assert.Equal(12.25, samples[0].Value);
				service.SubmitPerformanceSample(new ZzzOverlayPerformanceSampleDto("yolo_ms", 4.0, "ms", DateTimeOffset.UtcNow.AddSeconds(-2.0), 0.1));
				Assert.DoesNotContain((IEnumerable<ZzzOverlayPerformanceSampleDto>)service.GetPerformanceSamples(), (Predicate<ZzzOverlayPerformanceSampleDto>)((ZzzOverlayPerformanceSampleDto item) => item.Metric == "yolo_ms"));
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 性能面板只显示有真实采样且当前启用的指标。
	/// </summary>
	[Fact]
	public void OverlayPerformancePanelFiltersDisabledAndMissingMetricsWithoutPlaceholders()
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		ZzzOverlayPerformanceSampleDto[] samples = new ZzzOverlayPerformanceSampleDto[2]
		{
			new ZzzOverlayPerformanceSampleDto("ocr_ms", 12.345, "ms", utcNow.AddMilliseconds(-20.0)),
			new ZzzOverlayPerformanceSampleDto("yolo_ms", 6.5, "ms", utcNow.AddMilliseconds(-10.0))
		};
		Dictionary<string, bool> enabledMetricMap = new Dictionary<string, bool>(StringComparer.Ordinal)
		{
			["ocr_ms"] = true,
			["yolo_ms"] = false,
			["cv_pipeline_ms"] = true
		};
		string text = ZzzOverlayTechnicalWindow.FormatPerformancePanelText(samples, enabledMetricMap, utcNow);
		Assert.Equal("ocr_ms: 12.35 ms (20ms ago)", text);
		Assert.DoesNotContain("yolo_ms", text, StringComparison.Ordinal);
		Assert.DoesNotContain("cv_pipeline_ms", text, StringComparison.Ordinal);
		Assert.DoesNotContain("N/A", text, StringComparison.Ordinal);
		Assert.DoesNotContain("显示识别耗时", text, StringComparison.Ordinal);
	}

	/// <summary>
	/// 不可见窗口没有发生刷新时不得生成 overlay_refresh_ms。
	/// </summary>
	[Fact]
	public void OverlayControllerDoesNotPublishRefreshMetricWithoutVisibleWindow()
	{
		string text = CreateTempRoot();
		try
		{
			ZzzConfigScopeService scopes = new ZzzConfigScopeService(text);
			ZzzOverlayService zzzOverlayService = new ZzzOverlayService();
			ZzzOverlayController zzzOverlayController = new ZzzOverlayController(zzzOverlayService, CreateBackend(scopes));
			zzzOverlayController.Refresh(null);
			Assert.Empty(zzzOverlayService.GetPerformanceSamples());
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// 当前运行上下文丢失时应移除旧订阅和旧实例采样。
	/// </summary>
	[Fact]
	public void OverlayServiceClearsSamplesWhenRuntimeContextBecomesUnavailable()
	{
		string runRoot = CreateTempRoot();
		int createCount = 0;
		try
		{
			using ZzzRuntimeManager zzzRuntimeManager = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance, delegate
			{
				if (++createCount != 1)
				{
					throw new InvalidOperationException("context unavailable");
				}
				return new ZContext(new OneDragonEnvironment(runRoot));
			});
			using ZzzOverlayService zzzOverlayService = new ZzzOverlayService(zzzRuntimeManager);
			zzzRuntimeManager.EnsureContext();
			zzzOverlayService.GetPerformanceSamples();
			zzzOverlayService.SubmitPerformanceSample(new ZzzOverlayPerformanceSampleDto("ocr_ms", 1.0, "ms", DateTimeOffset.UtcNow));
			Assert.Single(zzzOverlayService.GetPerformanceSamples());
			ZzzBackendResult<bool> zzzBackendResult = zzzRuntimeManager.ReinitializeContext();
			Assert.False(zzzBackendResult.Success);
			Assert.Empty(zzzOverlayService.GetPerformanceSamples());
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), $"zzz-overlay-{Guid.NewGuid():N}");
		Directory.CreateDirectory(text);
		return text;
	}

	private static IZzzAppBackend CreateBackend(ZzzConfigScopeService scopes)
	{
		IZzzAppBackend zzzAppBackend = DispatchProxy.Create<IZzzAppBackend, OverlayBackendProxy>();
		((OverlayBackendProxy)zzzAppBackend).Scopes = scopes;
		return zzzAppBackend;
	}

	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "ZzzOd.slnx")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("找不到 zzzod-dotnet 仓库根目录。");
	}
}
