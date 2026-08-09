using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screening;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Context;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Services.Windows;
using GeometryRect = OneDragon.Core.Abstractions.Geometry.Rect;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class EnvironmentRuntimeCoordinatorTests
{
	private sealed class TestHarness : IDisposable
	{
		public string RunRoot { get; }

		public ZzzGlobalInputMonitor Monitor { get; }

		public RecordingBackendProxy BackendProxy { get; }

		public ZzzGuiRunIntentService RunIntent { get; }

		public ZzzDialogService DialogService { get; }

		public RecordingClipboard Clipboard { get; }

		public ZzzEnvironmentRuntimeCoordinator Coordinator { get; }

		public int ReinitializeCalls { get; private set; }

		private TestHarness(string runRoot, ZzzGlobalInputMonitor monitor, RecordingBackendProxy backendProxy, ZzzGuiRunIntentService runIntent, ZzzDialogService dialogService, RecordingClipboard clipboard, ZzzEnvironmentRuntimeCoordinator coordinator)
		{
			RunRoot = runRoot;
			Monitor = monitor;
			BackendProxy = backendProxy;
			RunIntent = runIntent;
			DialogService = dialogService;
			Clipboard = clipboard;
			Coordinator = coordinator;
		}

		public static TestHarness Create(
			bool copyScreenshot = false,
			bool patchedCaptureEnabled = false,
			IOverlayCapturer? overlayCapturer = null,
			TimeSpan? runStateObservationTimeout = null)
		{
			string runRoot = CreateRunRoot();
			IZzzAppBackend zzzAppBackend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
			RecordingBackendProxy proxy = (RecordingBackendProxy)zzzAppBackend;
			proxy.CopyScreenshot = copyScreenshot;
			proxy.PatchedCaptureEnabled = patchedCaptureEnabled;
			ZzzGlobalInputMonitor zzzGlobalInputMonitor = new ZzzGlobalInputMonitor();
			ZzzGuiRunIntentService runIntent = new ZzzGuiRunIntentService();
			ZzzDialogService dialogService = new ZzzDialogService();
			RecordingClipboard clipboard = new RecordingClipboard();
			TestHarness harness = null;
			ZzzEnvironmentRuntimeCoordinator coordinator = new ZzzEnvironmentRuntimeCoordinator(zzzAppBackend, zzzGlobalInputMonitor, runIntent, dialogService, clipboard, runRoot, delegate
			{
				harness.ReinitializeCalls++;
				return ZzzBackendResult<bool>.Ok(value: true);
			}, () => ZzzBackendResult<byte[]>.Ok(proxy.ScreenshotBytes), NullLogger<ZzzEnvironmentRuntimeCoordinator>.Instance, overlayCapturer, runStateObservationTimeout);
			harness = new TestHarness(runRoot, zzzGlobalInputMonitor, proxy, runIntent, dialogService, clipboard, coordinator);
			return harness;
		}

		public void Dispose()
		{
			Coordinator.Dispose();
			Monitor.Dispose();
			Directory.Delete(RunRoot, recursive: true);
		}
	}

	public class RecordingBackendProxy : DispatchProxy
	{
		private static readonly ZzzConfigScopeDescriptorDto Descriptor = new ZzzConfigScopeDescriptorDto("env", "脚本环境", InstanceBound: false, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>());

		private static readonly ZzzConfigScopeDescriptorDto OverlayDescriptor = new ZzzConfigScopeDescriptorDto("overlay", "Overlay", InstanceBound: false, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>());

		private readonly Channel<ZzzBackendEvent> _events = Channel.CreateUnbounded<ZzzBackendEvent>();

		public ZzzRunState State { get; set; } = ZzzRunState.Idle;

		public string? ActiveAppId { get; set; }

		public bool CopyScreenshot { get; set; }

		public bool PatchedCaptureEnabled { get; set; }

		public ZzzBackendResult<ZzzRunStatusDto>? StartResult { get; set; }

		public ZzzBackendResult<ZzzRunStatusDto>? PauseResult { get; set; }

		public ZzzBackendResult<ZzzRunStatusDto>? ResumeResult { get; set; }

		public Exception? StartException { get; set; }

		public bool PublishRunEvents { get; set; }

		public byte[] ScreenshotBytes { get; set; } = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };

		public int StartCalls { get; private set; }

		public int PauseCalls { get; private set; }

		public int ResumeCalls { get; private set; }

		public int StopCalls { get; private set; }

		public string? StartedAppId { get; private set; }

		public string? StartedGroupId { get; private set; }

		public int? StartedInstanceIndex { get; private set; }

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			string name = targetMethod.Name;
			if (1 == 0)
			{
			}
			object result = name switch
			{
				"GetConfigScope" => GetConfigScope((string)args![0]!),
				"GetCurrentRun" => ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(State)), 
				"PauseRun" => Pause(), 
				"ResumeRun" => Resume(), 
				"StopRunAsync" => StopAsync(), 
				"StartRunAsync" => StartAsync((ZzzStartRunRequest)args![0]!),
					"SubscribeEvents" => _events.Reader,
					"UnsubscribeEvents" => null,
				"GetScreenshot" => ZzzBackendResult<ZzzScreenshotDto>.Ok(new ZzzScreenshotDto("image/png", ScreenshotBytes)), 
				_ => throw new NotSupportedException(targetMethod.Name), 
			};
			if (1 == 0)
			{
			}
			return result;
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> GetConfigScope(string scope)
		{
			if (string.Equals(scope, "overlay", StringComparison.Ordinal))
			{
				return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(OverlayDescriptor, null, null, new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["patched_capture_enabled"] = PatchedCaptureEnabled,
					["patched_capture_suffix"] = "_overlay",
				}));
			}

			if (string.Equals(scope, "standalone-app", StringComparison.Ordinal))
			{
				return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
					new ZzzConfigScopeDescriptorDto("standalone-app", "独立运行", false, false, true, Array.Empty<ZzzConfigSettingDescriptorDto>()),
					null,
					null,
					new Dictionary<string, object>(StringComparer.Ordinal)
					{
						["active_app_id"] = ActiveAppId ?? string.Empty,
					}));
			}

			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(Descriptor, null, null, new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["key_start_running"] = "f9",
				["key_stop_running"] = "f10",
				["key_screenshot"] = "f11",
				["key_debug"] = "f12",
				["copy_screenshot"] = CopyScreenshot
			}));
		}

		private ZzzBackendResult<ZzzRunStatusDto> Pause()
		{
			PauseCalls++;
			if (PauseResult is not null)
			{
				return PauseResult;
			}

			State = ZzzRunState.Paused;
			ZzzRunStatusDto status = new(State);
			PublishRunStateIfEnabled(status);
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(status);
		}

		private ZzzBackendResult<ZzzRunStatusDto> Resume()
		{
			ResumeCalls++;
			if (ResumeResult is not null)
			{
				return ResumeResult;
			}

			State = ZzzRunState.Running;
			ZzzRunStatusDto status = new(State);
			PublishRunStateIfEnabled(status);
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(status);
		}

		private Task<ZzzBackendResult<ZzzRunStatusDto>> StopAsync()
		{
			StopCalls++;
			State = ZzzRunState.Cancelled;
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(State)));
		}

		private Task<ZzzBackendResult<ZzzRunStatusDto>> StartAsync(ZzzStartRunRequest request)
		{
			StartCalls++;
			StartedAppId = request.AppId;
			StartedGroupId = request.GroupId;
			StartedInstanceIndex = request.InstanceIndex;
			if (StartException is not null)
			{
				throw StartException;
			}
			if (StartResult is not null)
			{
				return Task.FromResult(StartResult);
			}

			State = ZzzRunState.Starting;
			ZzzRunStatusDto status = new(State, request.AppId, request.AppId, request.InstanceIndex, request.GroupId, "request-started");
			PublishRunStateIfEnabled(status);
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(status));
		}

		public void PublishRunState(ZzzRunStatusDto status)
		{
			_events.Writer.TryWrite(new ZzzBackendEvent("run.stateChanged", DateTimeOffset.UtcNow, status));
		}

		private void PublishRunStateIfEnabled(ZzzRunStatusDto status)
		{
			if (PublishRunEvents)
			{
				PublishRunState(status);
			}
		}
	}

	private sealed class RecordingClipboard : IZzzImageClipboardService
	{
		public byte[]? LastPngBytes { get; private set; }

		public Task CopyPngAsync(byte[] pngBytes, CancellationToken cancellationToken)
		{
			LastPngBytes = pngBytes.ToArray();
			return Task.CompletedTask;
		}
	}

	private sealed class StaticOverlayCapturer : IOverlayCapturer
	{
		public IReadOnlyList<OverlayCaptureFrame> CaptureFrames()
		{
			Mat image = new(new Size(1, 1), MatType.CV_8UC4, new Scalar(0, 0, 255, 255));
			return [new OverlayCaptureFrame(image, new GeometryRect(1, 1, 2, 2))];
		}
	}

	[Fact]
	public void RunIntentRegistersAndClearsTheCurrentTargetByOwner()
	{
		ZzzGuiRunIntentService runIntent = new();
		object owner = new();
		object otherOwner = new();

		runIntent.RegisterRunTarget(owner, "coffee", "daily", 2);

		Assert.Equal(new ZzzGuiRunTarget("coffee", "daily", 2), runIntent.CurrentRunTarget);
		runIntent.ClearRunTarget(otherOwner);
		Assert.NotNull(runIntent.CurrentRunTarget);
		runIntent.ClearRunTarget(owner);
		Assert.Null(runIntent.CurrentRunTarget);
	}

	[Fact]
	public async Task StartHotkeyStartsTheRegisteredPageTargetWhenIdle()
	{
		using TestHarness harness = TestHarness.Create();
		object owner = new();
		harness.RunIntent.RegisterRunTarget(owner, "coffee", "daily", 2);

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(1, harness.BackendProxy.StartCalls);
		Assert.Equal("coffee", harness.BackendProxy.StartedAppId);
		Assert.Equal("daily", harness.BackendProxy.StartedGroupId);
		Assert.Equal(2, harness.BackendProxy.StartedInstanceIndex);
	}

	[Fact]
	public async Task StartHotkeyUsesTheStandaloneActiveAppWithoutAVisiblePageTarget()
	{
		using TestHarness harness = TestHarness.Create();
		harness.BackendProxy.ActiveAppId = "scratch_card";

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(1, harness.BackendProxy.StartCalls);
		Assert.Equal("scratch_card", harness.BackendProxy.StartedAppId);
		Assert.Equal("one_dragon", harness.BackendProxy.StartedGroupId);
		Assert.Null(harness.BackendProxy.StartedInstanceIndex);
	}

	[Fact]
	public async Task InputMonitorPublishesOneF9ActionToTheCoordinator()
	{
		using TestHarness harness = TestHarness.Create();
		harness.RunIntent.RegisterRunTarget(new object(), "coffee");
		await harness.Coordinator.StartAsync(CancellationToken.None);

		harness.Monitor.PublishForTest("f9");

		Assert.True(SpinWait.SpinUntil(() => harness.BackendProxy.StartCalls == 1, TimeSpan.FromSeconds(3)));
		Assert.Equal(1, harness.BackendProxy.StartCalls);
	}

	[Fact]
	public async Task StartHotkeyShowsErrorWhenNoRunTargetExists()
	{
		using TestHarness harness = TestHarness.Create();
		ZzzToastRequest? toast = null;
		harness.DialogService.ToastRequested += (_, request) => toast = request;

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(0, harness.BackendProxy.StartCalls);
		Assert.NotNull(toast);
		Assert.Equal("启动失败", toast.Title);
		Assert.Equal("未选择运行应用。", toast.Message);
		Assert.Equal(FluentAvalonia.UI.Controls.FAInfoBarSeverity.Error, toast.Severity);
	}

	[Theory]
	[InlineData(ZzzBackendErrorCode.Conflict, "上一轮运行仍在退出中，请稍后重试。")]
	[InlineData(ZzzBackendErrorCode.NotFound, "应用未注册 coffee")]
	[InlineData(ZzzBackendErrorCode.Validation, "运行参数无效")]
	public async Task StartHotkeyShowsBackendFailureWithoutChangingState(ZzzBackendErrorCode code, string message)
	{
		using TestHarness harness = TestHarness.Create();
		harness.RunIntent.RegisterRunTarget(new object(), "coffee");
		harness.BackendProxy.StartResult = ZzzBackendResult<ZzzRunStatusDto>.Fail(code, message);
		ZzzToastRequest? toast = null;
		harness.DialogService.ToastRequested += (_, request) => toast = request;

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(ZzzRunState.Idle, harness.BackendProxy.State);
		Assert.Equal(message, toast?.Message);
	}

	[Fact]
	public async Task StartHotkeyShowsThrownBackendException()
	{
		using TestHarness harness = TestHarness.Create();
		harness.RunIntent.RegisterRunTarget(new object(), "coffee");
		harness.BackendProxy.StartException = new InvalidOperationException("backend disconnected");
		ZzzToastRequest? toast = null;
		harness.DialogService.ToastRequested += (_, request) => toast = request;

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal("backend disconnected", toast?.Message);
		Assert.Equal(ZzzRunState.Idle, harness.BackendProxy.State);
	}

	[Theory]
	[InlineData(ZzzRunState.Running, ZzzBackendErrorCode.Conflict, "当前没有运行中的应用。")]
	[InlineData(ZzzRunState.Paused, ZzzBackendErrorCode.NotReady, "运行上下文未初始化。")]
	public async Task PauseAndResumeFailuresShowErrorsWithoutChangingState(
		ZzzRunState state,
		ZzzBackendErrorCode code,
		string message)
	{
		using TestHarness harness = TestHarness.Create();
		harness.BackendProxy.State = state;
		if (state == ZzzRunState.Running)
		{
			harness.BackendProxy.PauseResult = ZzzBackendResult<ZzzRunStatusDto>.Fail(code, message);
		}
		else
		{
			harness.BackendProxy.ResumeResult = ZzzBackendResult<ZzzRunStatusDto>.Fail(code, message);
		}
		ZzzToastRequest? toast = null;
		harness.DialogService.ToastRequested += (_, request) => toast = request;

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(state, harness.BackendProxy.State);
		Assert.Equal(message, toast?.Message);
	}

	[Fact]
	public async Task AcceptedStartWithoutStateEventShowsSynchronizationFailure()
	{
		using TestHarness harness = TestHarness.Create(runStateObservationTimeout: TimeSpan.FromMilliseconds(30));
		harness.RunIntent.RegisterRunTarget(new object(), "coffee");
		ZzzToastRequest? toast = null;
		harness.DialogService.ToastRequested += (_, request) => toast = request;
		await harness.Coordinator.StartAsync(CancellationToken.None);

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.True(SpinWait.SpinUntil(() => toast?.Title == "运行状态同步失败", TimeSpan.FromSeconds(3)));
		Assert.Equal(ZzzRunState.Starting, harness.BackendProxy.State);
	}

	[Fact]
	public async Task AcceptedStartStateEventPreventsSynchronizationFailure()
	{
		using TestHarness harness = TestHarness.Create(runStateObservationTimeout: TimeSpan.FromMilliseconds(100));
		harness.RunIntent.RegisterRunTarget(new object(), "coffee");
		harness.BackendProxy.PublishRunEvents = true;
		List<ZzzToastRequest> toasts = new();
		harness.DialogService.ToastRequested += (_, request) => toasts.Add(request);
		await harness.Coordinator.StartAsync(CancellationToken.None);

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		await Task.Delay(200);

		Assert.DoesNotContain(toasts, request => request.Title == "运行状态同步失败");
	}

	[Fact]
	public async Task DelayedStateEventDoesNotCreateASecondSynchronizationWarning()
	{
		using TestHarness harness = TestHarness.Create(runStateObservationTimeout: TimeSpan.FromMilliseconds(30));
		harness.RunIntent.RegisterRunTarget(new object(), "coffee");
		List<ZzzToastRequest> toasts = new();
		harness.DialogService.ToastRequested += (_, request) => toasts.Add(request);
		await harness.Coordinator.StartAsync(CancellationToken.None);

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		Assert.True(SpinWait.SpinUntil(() => toasts.Any(request => request.Title == "运行状态同步失败"), TimeSpan.FromSeconds(3)));
		harness.BackendProxy.PublishRunState(new ZzzRunStatusDto(ZzzRunState.Running, "coffee", "咖啡店"));
		await Task.Delay(100);

		Assert.Single(toasts, request => request.Title == "运行状态同步失败");
	}

	[Fact]
	public async Task StartHotkeyDoesNotStartWithoutARealTarget()
	{
		using TestHarness harness = TestHarness.Create();

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(0, harness.BackendProxy.StartCalls);
	}

	[Fact]
	public async Task StartHotkeyPausesAndResumesAnExistingRun()
	{
		using TestHarness harness = TestHarness.Create();
		List<string> pressed = new();
		harness.RunIntent.GlobalInputPressed += delegate(object? _, string key)
		{
			pressed.Add(key);
		};

		harness.BackendProxy.State = ZzzRunState.Running;
		await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		harness.BackendProxy.State = ZzzRunState.Paused;
		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(1, harness.BackendProxy.PauseCalls);
		Assert.Equal(1, harness.BackendProxy.ResumeCalls);
		Assert.Equal(new[] { "f9", "f9" }, pressed);
	}

	[Theory]
	[InlineData(ZzzRunState.Starting)]
	[InlineData(ZzzRunState.Stopping)]
	public async Task StartHotkeyIgnoresDuplicateRequestsDuringTransitions(ZzzRunState state)
	{
		using TestHarness harness = TestHarness.Create();
		harness.BackendProxy.State = state;
		harness.RunIntent.RegisterRunTarget(new object(), "coffee");

		await harness.Coordinator.HandleInputPressedForTestAsync("f9");

		Assert.Equal(0, harness.BackendProxy.StartCalls);
		Assert.Equal(0, harness.BackendProxy.PauseCalls);
		Assert.Equal(0, harness.BackendProxy.ResumeCalls);
	}

	[Fact]
	public async Task StopHotkeyStopsOnlyAnActiveRun()
	{
		using TestHarness harness = TestHarness.Create();
		harness.BackendProxy.State = ZzzRunState.Idle;
		await harness.Coordinator.HandleInputPressedForTestAsync("f10");
		Assert.Equal(0, harness.BackendProxy.StopCalls);
		harness.BackendProxy.State = ZzzRunState.Running;
		await harness.Coordinator.HandleInputPressedForTestAsync("f10");
		Assert.Equal(1, harness.BackendProxy.StopCalls);
	}

	[Fact]
	public async Task ScreenshotHotkeySavesRealBackendBytesAndHonorsClipboardSetting()
	{
		using TestHarness harness = TestHarness.Create(copyScreenshot: true);
		await harness.Coordinator.HandleInputPressedForTestAsync("f11");
		string[] files = Directory.GetFiles(Path.Combine(harness.RunRoot, ".debug", "images"), "*.png");
		string file = Assert.Single(files);
		byte[] screenshotBytes = harness.BackendProxy.ScreenshotBytes;
		Assert.Equal(screenshotBytes, await File.ReadAllBytesAsync(file));
		Assert.Equal(harness.BackendProxy.ScreenshotBytes, harness.Clipboard.LastPngBytes);
	}

	[Fact]
	public async Task ScreenshotHotkeyKeepsOriginalAndWritesConfiguredPatchedOverlayImage()
	{
		using TestHarness harness = TestHarness.Create(patchedCaptureEnabled: true, overlayCapturer: new StaticOverlayCapturer());
		using (Mat original = new Mat(new Size(3, 3), MatType.CV_8UC3, Scalar.Black))
		{
			Cv2.ImEncode(".png", original, out byte[] png);
			harness.BackendProxy.ScreenshotBytes = png;
		}

		await harness.Coordinator.HandleInputPressedForTestAsync("f11");

		string[] files = Directory.GetFiles(Path.Combine(harness.RunRoot, ".debug", "images"), "*.png");
		Assert.Equal(2, files.Length);
		string originalPath = Assert.Single(files, path => !Path.GetFileNameWithoutExtension(path).EndsWith("_overlay", StringComparison.Ordinal));
		string patchedPath = Assert.Single(files, path => Path.GetFileNameWithoutExtension(path).EndsWith("_overlay", StringComparison.Ordinal));
		Assert.Equal(harness.BackendProxy.ScreenshotBytes, await File.ReadAllBytesAsync(originalPath));
		using Mat patched = Cv2.ImRead(patchedPath, ImreadModes.Unchanged);
		Assert.Equal(new Vec3b(0, 0, 0), patched.At<Vec3b>(0, 0));
		Assert.Equal(new Vec3b(0, 0, 255), patched.At<Vec3b>(1, 1));
	}

	[Fact]
	public async Task DebugHotkeyIsDispatchedAndDebugModeCanReinitializeTheCurrentRuntime()
	{
		using TestHarness harness = TestHarness.Create();
		string pressed = null;
		harness.RunIntent.GlobalInputPressed += delegate(object? _, string key)
		{
			pressed = key;
		};
		await harness.Coordinator.HandleInputPressedForTestAsync("f12");
		ZzzBackendResult<bool> result = await harness.Coordinator.ReinitializeContextAsync();
		Assert.Equal("f12", pressed);
		Assert.True(result.Success);
		Assert.Equal(1, harness.ReinitializeCalls);
	}

	[Fact]
	public async Task CapturingAReplacementHotkeySuspendsProductionActions()
	{
		using TestHarness harness = TestHarness.Create();
		harness.BackendProxy.State = ZzzRunState.Running;
		using (harness.Coordinator.SuspendHotkeyActions())
		{
			await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		}
		Assert.Equal(0, harness.BackendProxy.PauseCalls);
		await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		Assert.Equal(1, harness.BackendProxy.PauseCalls);
	}

	[Fact]
	public void RuntimeManagerRecreatesTheCurrentInstanceContext()
	{
		string runRoot = CreateRunRoot();
		int createCalls = 0;
		try
		{
			using ZzzRuntimeManager zzzRuntimeManager = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance, delegate(int instanceIndex)
			{
				createCalls++;
				return new ZContext(new OneDragonEnvironment(runRoot), null, instanceIndex);
			});
			ZContext expected = zzzRuntimeManager.EnsureContext();
			ZzzBackendResult<bool> zzzBackendResult = zzzRuntimeManager.ReinitializeContext();
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.Equal(2, createCalls);
			Assert.NotSame(expected, zzzRuntimeManager.TryGetContext());
			Assert.Equal(1, zzzRuntimeManager.TryGetContext()?.InstanceIndex);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	private static string CreateRunRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzz-env-runtime-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
