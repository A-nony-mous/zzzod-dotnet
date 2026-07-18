using System;
using System.IO;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Backend;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Backend;

public sealed class BackendTests
{
	private sealed class FakeBackendController : ControllerBase, IBackendWindowStatusProvider
	{
		private readonly Mat? _screenshot;

		public override bool IsGameWindowReady => true;

		public FakeBackendController(Mat? screenshot = null)
		{
			_screenshot = screenshot;
		}

		public WindowStatus GetWindowStatus()
		{
			return new WindowStatus("Fake Window", IsWinValid: true, IsWinActive: true, IsWinScale: false, 1, 2, 3, 4);
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
			return _screenshot?.Clone();
		}
	}

	private sealed class UnavailableBackendController : ControllerBase
	{
		public override bool IsGameWindowReady => false;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			return false;
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
			return null;
		}
	}

	private sealed class WaitingOperation : Operation
	{
		private readonly TaskCompletionSource _startedSignal;

		private bool _started;

		public WaitingOperation(ZContext context, TaskCompletionSource startedSignal)
			: base(context, "WaitingOperation")
		{
			_startedSignal = startedSignal;
		}

		[OperationNode("wait", IsStartNode = true, ScreenshotBeforeRound = false)]
		private OperationRoundResult Wait()
		{
			if (!_started)
			{
				_started = true;
				_startedSignal.TrySetResult();
			}
			return RoundWait("waiting", null, TimeSpan.FromMilliseconds(20L));
		}
	}

	[Fact]
	public void WindowStatusSchema_MapsAllFields()
	{
		WindowStatus windowStatus = new WindowStatus("绝区零", IsWinValid: true, IsWinActive: false, IsWinScale: true, 10, 20, 1600, 900, IsWinMinimized: true, Dpi: 144);
		Assert.Equal("绝区零", windowStatus.WinTitle);
		Assert.True(windowStatus.IsWinValid);
		Assert.False(windowStatus.IsWinActive);
		Assert.True(windowStatus.IsWinScale);
		Assert.Equal(10, windowStatus.X);
		Assert.Equal(20, windowStatus.Y);
		Assert.Equal(1600, windowStatus.Width);
		Assert.Equal(900, windowStatus.Height);
		Assert.True(windowStatus.IsWinMinimized);
		Assert.Equal(144u, windowStatus.Dpi);
	}

	[Fact]
	public void RunStatusSchema_MapsAllFields()
	{
		RunStatusResult runStatusResult = new RunStatusResult("running", "mcp", "FakeOperation", "2026-07-05T10:00:00", 3.5, "等待节点", 2, "执行中");
		Assert.Equal("running", runStatusResult.State);
		Assert.Equal("mcp", runStatusResult.Source);
		Assert.Equal("FakeOperation", runStatusResult.App);
		Assert.Equal("2026-07-05T10:00:00", runStatusResult.StartedAt);
		Assert.Equal(3.5, runStatusResult.DurationSeconds);
		Assert.Equal("等待节点", runStatusResult.CurrentNode);
		Assert.Equal(2, runStatusResult.RetryCount);
		Assert.Equal("执行中", runStatusResult.LastStatus);
	}

	[Fact]
	public void CheckWindow_UsesControllerWindowStatusProvider()
	{
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using ZContext zContext = new ZContext(environment);
			zContext.AttachController(new FakeBackendController());
			ZzzBackendContext zzzBackendContext = new ZzzBackendContext(zContext);
			WindowStatus windowStatus = zzzBackendContext.CheckWindow();
			Assert.Equal("Fake Window", windowStatus.WinTitle);
			Assert.True(windowStatus.IsWinValid);
			Assert.True(windowStatus.IsWinActive);
			Assert.False(windowStatus.IsWinScale);
			Assert.Equal(1, windowStatus.X);
			Assert.Equal(2, windowStatus.Y);
			Assert.Equal(3, windowStatus.Width);
			Assert.Equal(4, windowStatus.Height);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Capture_ReturnsControllerScreenshotWithoutWindow()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using ZContext zContext = new ZContext(environment);
			using Mat mat = new Mat(2, 3, MatType.CV_8UC3, Scalar.All(7.0));
			zContext.AttachController(new FakeBackendController(mat.Clone()));
			ZzzBackendContext zzzBackendContext = new ZzzBackendContext(zContext);
			using Mat mat2 = zzzBackendContext.Capture();
			Assert.Equal(2, mat2.Rows);
			Assert.Equal(3, mat2.Cols);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task StartRun_QueryStatus_Stop_TracksLifecycle()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			OneDragonEnvironment env = new OneDragonEnvironment(rootDirectory);
			using ZContext context = new ZContext(env);
			ZzzBackendContext backend = new ZzzBackendContext(context);
			TaskCompletionSource startedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			bool started;
			Task<OperationResult> runTask;
			(started, runTask) = backend.StartRun("mcp", (ZContext ctx) => new WaitingOperation(ctx, startedSignal));
			Assert.True(started);
			Assert.NotNull(runTask);
			await startedSignal.Task;
			RunStatusResult runningStatus = backend.QueryStatus();
			Assert.Equal("running", runningStatus.State);
			Assert.Equal("mcp", runningStatus.Source);
			Assert.Equal("WaitingOperation", runningStatus.App);
			StopRunResult stopResult = backend.Stop();
			Assert.True(stopResult.Stopped);
			Assert.Equal("mcp", stopResult.Source);
			OperationResult result = await runTask;
			Assert.False(result.IsSuccess);
			Assert.Equal("人工结束", result.Status);
			RunStatusResult stoppedStatus = backend.QueryStatus();
			Assert.Equal("stopped", stoppedStatus.State);
			Assert.Equal("人工结束", stoppedStatus.LastStatus);
			Assert.Equal("WaitingOperation", stoppedStatus.App);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void BackendMethods_ThrowWhenContextNotReadyOrWindowUnavailable()
	{
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using ZContext zContext = new ZContext(environment);
			ZzzBackendContext backend = new ZzzBackendContext(zContext);
			zContext.SetReadyForApplication(ready: false);
			Assert.Throws<BackendNotReadyException>(() => backend.CheckWindow());
			zContext.SetReadyForApplication(ready: true);
			zContext.AttachController(new UnavailableBackendController());
			Assert.Throws<BackendNotReadyException>(() => backend.Capture());
			Assert.Throws<BackendNotReadyException>(() => backend.CloseGame());
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task Shutdown_StopsRunningOperationAndMarksBackendStopped()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			OneDragonEnvironment env = new OneDragonEnvironment(rootDirectory);
			using ZContext context = new ZContext(env);
			ZzzBackendContext backend = new ZzzBackendContext(context);
			TaskCompletionSource startedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			bool started;
			Task<OperationResult> runTask;
			(started, runTask) = backend.StartRun("shutdown-test", (ZContext ctx) => new WaitingOperation(ctx, startedSignal));
			Assert.True(started);
			Assert.NotNull(runTask);
			await startedSignal.Task;
			backend.Start();
			backend.Shutdown();
			OperationResult result = await runTask;
			RunStatusResult status = backend.QueryStatus();
			Assert.False(backend.IsStarted);
			Assert.False(result.IsSuccess);
			Assert.Equal("人工结束", result.Status);
			Assert.Equal("stopped", status.State);
			Assert.Equal("人工结束", status.LastStatus);
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

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
