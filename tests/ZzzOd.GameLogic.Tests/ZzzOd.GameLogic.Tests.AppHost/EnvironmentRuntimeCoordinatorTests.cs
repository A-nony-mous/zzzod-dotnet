using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Context;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Services.Windows;

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

		public static TestHarness Create(bool copyScreenshot = false)
		{
			string runRoot = CreateRunRoot();
			IZzzAppBackend zzzAppBackend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
			RecordingBackendProxy proxy = (RecordingBackendProxy)zzzAppBackend;
			proxy.CopyScreenshot = copyScreenshot;
			ZzzGlobalInputMonitor zzzGlobalInputMonitor = new ZzzGlobalInputMonitor();
			ZzzGuiRunIntentService runIntent = new ZzzGuiRunIntentService();
			RecordingClipboard clipboard = new RecordingClipboard();
			TestHarness harness = null;
			ZzzEnvironmentRuntimeCoordinator coordinator = new ZzzEnvironmentRuntimeCoordinator(zzzAppBackend, zzzGlobalInputMonitor, runIntent, clipboard, runRoot, delegate
			{
				harness.ReinitializeCalls++;
				return ZzzBackendResult<bool>.Ok(value: true);
			}, () => ZzzBackendResult<byte[]>.Ok(proxy.ScreenshotBytes), NullLogger<ZzzEnvironmentRuntimeCoordinator>.Instance);
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

		public ZzzRunState State { get; set; } = ZzzRunState.Idle;

		public bool CopyScreenshot { get; set; }

		public byte[] ScreenshotBytes { get; } = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };

		public int StartCalls { get; private set; }

		public int PauseCalls { get; private set; }

		public int ResumeCalls { get; private set; }

		public int StopCalls { get; private set; }

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			string name = targetMethod.Name;
			if (1 == 0)
			{
			}
			object result = name switch
			{
				"GetConfigScope" => ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(Descriptor, null, null, new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["key_start_running"] = "f9",
					["key_stop_running"] = "f10",
					["key_screenshot"] = "f11",
					["key_debug"] = "f12",
					["copy_screenshot"] = CopyScreenshot
				})), 
				"GetCurrentRun" => ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(State)), 
				"PauseRun" => Pause(), 
				"ResumeRun" => Resume(), 
				"StopRunAsync" => StopAsync(), 
				"StartRunAsync" => StartAsync(), 
				"GetScreenshot" => ZzzBackendResult<ZzzScreenshotDto>.Ok(new ZzzScreenshotDto("image/png", ScreenshotBytes)), 
				_ => throw new NotSupportedException(targetMethod.Name), 
			};
			if (1 == 0)
			{
			}
			return result;
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

		private Task<ZzzBackendResult<ZzzRunStatusDto>> StartAsync()
		{
			StartCalls++;
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

	[Fact]
	public async Task StartHotkeyOnlyTogglesAnExistingRunAndNeverChoosesAnIdleApplication()
	{
		using TestHarness harness = TestHarness.Create();
		List<string> pressed = new List<string>();
		harness.RunIntent.GlobalInputPressed += delegate(object? _, string key)
		{
			pressed.Add(key);
		};
		harness.BackendProxy.State = ZzzRunState.Idle;
		await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		Assert.Equal(0, harness.BackendProxy.PauseCalls);
		Assert.Equal(0, harness.BackendProxy.ResumeCalls);
		Assert.Equal(0, harness.BackendProxy.StartCalls);
		harness.BackendProxy.State = ZzzRunState.Running;
		await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		Assert.Equal(1, harness.BackendProxy.PauseCalls);
		harness.BackendProxy.State = ZzzRunState.Paused;
		await harness.Coordinator.HandleInputPressedForTestAsync("f9");
		Assert.Equal(1, harness.BackendProxy.ResumeCalls);
		Assert.Equal<List<string>>(new List<string>(3) { "f9", "f9", "f9" }, pressed);
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
