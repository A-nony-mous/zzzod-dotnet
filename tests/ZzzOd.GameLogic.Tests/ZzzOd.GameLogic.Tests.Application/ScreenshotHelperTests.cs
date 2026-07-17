using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

[Collection("Screenshot helper global input source")]
public sealed class ScreenshotHelperTests
{
	private sealed class SequenceCaptureSource : IScreenshotHelperCaptureSource, IDisposable
	{
		private readonly Queue<ScreenshotHelperFrame> _frames;

		public SequenceCaptureSource(params ScreenshotHelperFrame[] frames)
		{
			_frames = new Queue<ScreenshotHelperFrame>(frames);
		}

		public ScreenshotHelperFrame? Capture()
		{
			if (_frames.Count == 0)
			{
				return null;
			}
			ScreenshotHelperFrame screenshotHelperFrame = _frames.Dequeue();
			return new ScreenshotHelperFrame(screenshotHelperFrame.CaptureTimeUtc, screenshotHelperFrame.Image.Clone());
		}

		public void Dispose()
		{
			while (_frames.Count > 0)
			{
				_frames.Dequeue().Dispose();
			}
		}
	}

	private sealed class EmptyCaptureSource : IScreenshotHelperCaptureSource
	{
		public ScreenshotHelperFrame? Capture()
		{
			return null;
		}
	}

	private sealed class BlockingReadyController : ControllerBase
	{
		public TaskCompletionSource ScreenshotCaptured { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public override bool IsGameWindowReady => true;

		public override bool InitBeforeContextRun()
		{
			return true;
		}

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
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
			ScreenshotCaptured.TrySetResult();
			return new Mat(4, 4, MatType.CV_8UC3, new Scalar(12.0, 24.0, 36.0));
		}
	}

	private sealed class StaticDodgeDetector(bool checkFlash, bool checkAudio = false) : IScreenshotHelperDodgeDetector
	{
		public bool CheckDodgeFlash(Mat screen, DateTimeOffset captureTimeUtc)
		{
			return checkFlash;
		}

		public bool CheckDodgeAudio(DateTimeOffset captureTimeUtc)
		{
			return checkAudio;
		}
	}

	private sealed class StaticMiniMapAngleDetector(bool shouldSave = true) : IScreenshotHelperMiniMapAngleDetector
	{
		public bool ShouldSaveForMissingAngle(Mat screen)
		{
			return shouldSave;
		}
	}

	[Fact]
	public void Config_LoadsPythonCompatibleDefaultsAndSnakeCaseYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "screenshot_helper.yml"), "frequency_second: 0.25\nlength_second: 2\nkey_save: \"space\"\ndodge_detect: false\nscreenshot_before_key: false\nmini_map_angle_detect: true");
			ScreenshotHelperConfig screenshotHelperConfig = ScreenshotHelperConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("screenshot_helper", screenshotHelperConfig.AppId);
			Assert.Equal(0.25, screenshotHelperConfig.FrequencySecond);
			Assert.Equal(2.0, screenshotHelperConfig.LengthSecond);
			Assert.Equal("space", screenshotHelperConfig.KeySave);
			Assert.False(screenshotHelperConfig.DodgeDetect);
			Assert.False(screenshotHelperConfig.ScreenshotBeforeKey);
			Assert.True(screenshotHelperConfig.MiniMapAngleDetect);
			Assert.Equal(9, screenshotHelperConfig.CacheMaxCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Service_SavesCachedScreenshotsWhenConfiguredBeforeKey()
	{
		string text = CreateTempRoot();
		try
		{
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
			ScreenshotHelperConfig config = new ScreenshotHelperConfig
			{
				FrequencySecond = 0.1,
				LengthSecond = 0.2,
				KeySave = "1",
				DodgeDetect = false,
				ScreenshotBeforeKey = true
			};
			SequenceCaptureSource captureSource = new SequenceCaptureSource(CreateFrame(now, 10), CreateFrame(now.AddMilliseconds(100.0), 20), CreateFrame(now.AddMilliseconds(200.0), 30), CreateFrame(now.AddMilliseconds(300.0), 40));
			DebugScreenshotHelperImageStore imageStore = new DebugScreenshotHelperImageStore(new OneDragonEnvironment(text));
			using ScreenshotHelperService screenshotHelperService = new ScreenshotHelperService(config, captureSource, imageStore, new StaticDodgeDetector(checkFlash: false), new StaticMiniMapAngleDetector(shouldSave: false), () => now.AddSeconds(10.0));
			screenshotHelperService.CaptureAndProcess();
			screenshotHelperService.CaptureAndProcess();
			screenshotHelperService.CaptureAndProcess();
			Assert.Equal(3, screenshotHelperService.CachedFrameCount);
			Assert.True(screenshotHelperService.HandleKeyPress("1"));
			ScreenshotHelperTickResult screenshotHelperTickResult = screenshotHelperService.CaptureAndProcess();
			Assert.True(screenshotHelperTickResult.Captured);
			Assert.False(screenshotHelperTickResult.IsSavePending);
			Assert.Equal(3, screenshotHelperTickResult.SavedImages.Count);
			Assert.All(screenshotHelperTickResult.SavedImages, delegate(ScreenshotHelperSavedImage image)
			{
				Assert.Equal("switch", image.Prefix);
				Assert.True(File.Exists(image.FilePath));
			});
			Assert.Equal(0, screenshotHelperService.CachedFrameCount);
			Assert.Equal(new string[3] { "switch_1783299600100", "switch_1783299600200", "switch_1783299600300" }, screenshotHelperTickResult.SavedImages.Select((ScreenshotHelperSavedImage image) => image.FileName));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Service_KeepsPythonSaveNodePendingWhenBeforeKeyCaptureIsDisabled()
	{
		string text = CreateTempRoot();
		try
		{
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
			ScreenshotHelperConfig screenshotHelperConfig = new ScreenshotHelperConfig
			{
				FrequencySecond = 0.1,
				LengthSecond = 1.0,
				KeySave = "x",
				DodgeDetect = false,
				ScreenshotBeforeKey = false
			};
			SequenceCaptureSource captureSource = new SequenceCaptureSource(CreateFrame(now, 10), CreateFrame(now.AddMilliseconds(100.0), 20), CreateFrame(now.AddMilliseconds(200.0), 30));
			DebugScreenshotHelperImageStore imageStore = new DebugScreenshotHelperImageStore(new OneDragonEnvironment(text));
			using ScreenshotHelperService screenshotHelperService = new ScreenshotHelperService(screenshotHelperConfig, captureSource, imageStore, new StaticDodgeDetector(checkFlash: false), new StaticMiniMapAngleDetector(shouldSave: false), () => now.AddSeconds(10.0));
			screenshotHelperService.CaptureAndProcess();
			Assert.True(screenshotHelperService.HandleKeyPress("x"));
			ScreenshotHelperTickResult screenshotHelperTickResult = screenshotHelperService.CaptureAndProcess();
			ScreenshotHelperTickResult screenshotHelperTickResult2 = screenshotHelperService.CaptureAndProcess();
			Assert.Empty(screenshotHelperTickResult.SavedImages);
			Assert.True(screenshotHelperTickResult.IsSavePending);
			Assert.True(screenshotHelperTickResult.IsSavingAfterKey);
			Assert.Equal(screenshotHelperConfig.Frequency, screenshotHelperTickResult.NextDelay);
			Assert.Empty(screenshotHelperTickResult2.SavedImages);
			Assert.True(screenshotHelperTickResult2.IsSavePending);
			Assert.True(screenshotHelperTickResult2.IsSavingAfterKey);
			Assert.Equal(screenshotHelperConfig.Frequency, screenshotHelperTickResult2.NextDelay);
			Assert.Equal(0, screenshotHelperService.CachedFrameCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Service_SavesDodgeAndMiniMapDebugImagesThroughInjectedDetectors()
	{
		string text = CreateTempRoot();
		try
		{
			DateTimeOffset captureTimeUtc = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
			ScreenshotHelperConfig config = new ScreenshotHelperConfig
			{
				DodgeDetect = true,
				MiniMapAngleDetect = true
			};
			SequenceCaptureSource captureSource = new SequenceCaptureSource(CreateFrame(captureTimeUtc, 80));
			DebugScreenshotHelperImageStore imageStore = new DebugScreenshotHelperImageStore(new OneDragonEnvironment(text));
			using ScreenshotHelperService screenshotHelperService = new ScreenshotHelperService(config, captureSource, imageStore, new StaticDodgeDetector(checkFlash: true), new StaticMiniMapAngleDetector());
			ScreenshotHelperTickResult screenshotHelperTickResult = screenshotHelperService.CaptureAndProcess();
			Assert.Equal(new string[2] { "mini_map_angle", "dodge" }, screenshotHelperTickResult.SavedImages.Select((ScreenshotHelperSavedImage image) => image.Prefix));
			Assert.All(screenshotHelperTickResult.SavedImages, delegate(ScreenshotHelperSavedImage image)
			{
				Assert.True(File.Exists(image.FilePath));
			});
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			ScreenshotHelperAppFactory screenshotHelperAppFactory = zContext.ApplicationFactoryRegistry.CreateScreenshotHelperFactory();
			IApplication application = screenshotHelperAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = screenshotHelperAppFactory.GetConfig(0, "one_dragon");
			Assert.Equal("screenshot_helper", screenshotHelperAppFactory.AppId);
			Assert.Equal("闪避截图", screenshotHelperAppFactory.AppName);
			Assert.Equal("one_dragon", screenshotHelperAppFactory.GroupId);
			Assert.False(screenshotHelperAppFactory.NeedNotify);
			Assert.IsType<ScreenshotHelperApp>(application);
			Assert.IsType<ScreenshotHelperConfig>(config);
			Assert.IsType<ZApplicationRunRecord>(screenshotHelperAppFactory.GetRunRecord(0));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersScreenshotHelperAsNonDefaultDevtool()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterScreenshotHelperApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("screenshot_helper"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("screenshot_helper"));
			Assert.DoesNotContain("screenshot_helper", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void GlobalInputSourceFollowsAppSubscriptionAndCaptureSuspensionLifetime()
	{
		List<string> received = new List<string>();
		using (ScreenshotHelperGlobalInputSource.Subscribe(delegate(string key)
		{
			received.Add(key);
			return true;
		}))
		{
			ScreenshotHelperGlobalInputSource.Publish("1");
			using (ScreenshotHelperGlobalInputSource.Suspend())
			{
				ScreenshotHelperGlobalInputSource.Publish("2");
			}
			ScreenshotHelperGlobalInputSource.Publish("3");
			int num = 2;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<string> span = CollectionsMarshal.AsSpan(list);
			span[0] = "1";
			span[1] = "3";
			Assert.Equal<List<string>>(list, received);
		}
	}

	[Fact]
	public async Task ScreenshotHelperApp_PreCancelledExecutionFailsAndDisposesService()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ScreenshotHelperConfig config = new ScreenshotHelperConfig
			{
				FrequencySecond = 0.01,
				DodgeDetect = false,
				MiniMapAngleDetect = false
			};
			ScreenshotHelperService service = new ScreenshotHelperService(config, new EmptyCaptureSource(), new DebugScreenshotHelperImageStore(context.Environment), new StaticDodgeDetector(checkFlash: false), new StaticMiniMapAngleDetector(shouldSave: false));
			ZApplicationRunRecord runRecord = new ZApplicationRunRecord("screenshot_helper");
			ScreenshotHelperApp app = new ScreenshotHelperApp(context, config, runRecord, service);
			CancellationTokenSource cts = new CancellationTokenSource();
			try
			{
				cts.Cancel();
				await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.ExecuteAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(1L)));
				Assert.Equal(2, runRecord.RunStatus);
				Assert.Equal(TimeSpan.Zero, context.Controller.ScreenshotAliveTime);
				Assert.Equal(0, context.Controller.MaxScreenshotCount);
				Assert.Throws<ObjectDisposedException>(() => service.CaptureAndProcess());
			}
			finally
			{
				if (cts != null)
				{
					((IDisposable)cts).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenshotHelperApp_UsesPythonControllerCachePolicyAndClearsItAfterCancellation()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			BlockingReadyController controller = new BlockingReadyController();
			context.AttachController(controller);
			ScreenshotHelperConfig config = new ScreenshotHelperConfig
			{
				FrequencySecond = 0.4,
				LengthSecond = 2.75,
				DodgeDetect = false,
				MiniMapAngleDetect = false
			};
			ScreenshotHelperService service = new ScreenshotHelperService(config, new ZContextScreenshotHelperCaptureSource(context), new DebugScreenshotHelperImageStore(context.Environment), new StaticDodgeDetector(checkFlash: false), new StaticMiniMapAngleDetector(shouldSave: false));
			ScreenshotHelperApp app = new ScreenshotHelperApp(context, config, new ZApplicationRunRecord("screenshot_helper"), service);
			using CancellationTokenSource cts = new CancellationTokenSource();
			Task<OperationResult> execution = app.ExecuteAsync(cts.Token);
			await controller.ScreenshotCaptured.Task.WaitAsync(TimeSpan.FromSeconds(1L));
			Assert.Equal(TimeSpan.FromSeconds(3.75), controller.ScreenshotAliveTime);
			Assert.Equal(11, controller.MaxScreenshotCount);
			cts.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
			Assert.Equal(TimeSpan.Zero, controller.ScreenshotAliveTime);
			Assert.Equal(0, controller.MaxScreenshotCount);
			Assert.Throws<ObjectDisposedException>(() => service.CaptureAndProcess());
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static ScreenshotHelperFrame CreateFrame(DateTimeOffset captureTimeUtc, byte value)
	{
		Mat image = new Mat(4, 4, MatType.CV_8UC3, new Scalar((int)value, (int)value, (int)value));
		return new ScreenshotHelperFrame(captureTimeUtc, image);
	}
}
