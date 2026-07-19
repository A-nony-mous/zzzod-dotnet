using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screening;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Context;
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

		public RecordingClipboard Clipboard { get; }

		public ZzzEnvironmentRuntimeCoordinator Coordinator { get; }

		public int ReinitializeCalls { get; private set; }

		private TestHarness(string runRoot, ZzzGlobalInputMonitor monitor, RecordingBackendProxy backendProxy, ZzzGuiRunIntentService runIntent, RecordingClipboard clipboard, ZzzEnvironmentRuntimeCoordinator coordinator)
		{
			RunRoot = runRoot;
			Monitor = monitor;
			BackendProxy = backendProxy;
			RunIntent = runIntent;
			Clipboard = clipboard;
			Coordinator = coordinator;
		}

		public static TestHarness Create(bool copyScreenshot = false, bool patchedCaptureEnabled = false, IOverlayCapturer? overlayCapturer = null)
		{
			string runRoot = CreateRunRoot();
			IZzzAppBackend zzzAppBackend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
			RecordingBackendProxy proxy = (RecordingBackendProxy)zzzAppBackend;
			proxy.CopyScreenshot = copyScreenshot;
			proxy.PatchedCaptureEnabled = patchedCaptureEnabled;
			ZzzGlobalInputMonitor zzzGlobalInputMonitor = new ZzzGlobalInputMonitor();
			ZzzGuiRunIntentService runIntent = new ZzzGuiRunIntentService();
			RecordingClipboard clipboard = new RecordingClipboard();
			TestHarness harness = null;
			ZzzEnvironmentRuntimeCoordinator coordinator = new ZzzEnvironmentRuntimeCoordinator(zzzAppBackend, zzzGlobalInputMonitor, runIntent, clipboard, runRoot, delegate
			{
				harness.ReinitializeCalls++;
				return ZzzBackendResult<bool>.Ok(value: true);
			}, () => ZzzBackendResult<byte[]>.Ok(proxy.ScreenshotBytes), NullLogger<ZzzEnvironmentRuntimeCoordinator>.Instance, overlayCapturer);
			harness = new TestHarness(runRoot, zzzGlobalInputMonitor, proxy, runIntent, clipboard, coordinator);
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

		public ZzzRunState State { get; set; } = ZzzRunState.Idle;

		public string? ActiveAppId { get; set; }

		public bool CopyScreenshot { get; set; }

		public bool PatchedCaptureEnabled { get; set; }

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
			State = ZzzRunState.Paused;
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(State));
		}

		private ZzzBackendResult<ZzzRunStatusDto> Resume()
		{
			ResumeCalls++;
			State = ZzzRunState.Running;
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(State));
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
			State = ZzzRunState.Starting;
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(State)));
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
			Assert.Equal(0, zzzRuntimeManager.TryGetContext()?.InstanceIndex);
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
