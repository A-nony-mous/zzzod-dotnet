using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.Application;

/// <summary>
/// ZApplication enter-game behavior tests.
/// </summary>
public sealed class ZApplicationEnterGameTests
{
	private sealed class TestApplication : ZApplication
	{
		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		public TestApplication(ZContext context, Func<CancellationToken, Task<OperationResult>> executeAsync, Func<ZContext, CancellationToken, Task<OperationResult>> defaultEnterGameAsync)
			: base(context, "test-app", null, null, 1, null, needCheckGameWindow: true, null, defaultEnterGameAsync)
		{
			_executeAsync = executeAsync;
		}

		protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
		{
			return _executeAsync(cancellationToken);
		}
	}

	private sealed class ReadyController : ControllerBase
	{
		public override bool IsGameWindowReady => true;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			return true;
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

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return null;
		}
	}

	/// <summary>
	/// NeedCheckGameWindow skips opening the game when a game window is already ready.
	/// </summary>
	[Fact]
	public async Task NeedCheckGameWindowSkipsOpenAndEnterWhenWindowReady()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.AttachController(new ReadyController());
		List<string> steps = new List<string>();
		TestApplication app = new TestApplication(context, delegate
		{
			steps.Add("core");
			return Task.FromResult(new OperationResult(IsSuccess: true, "core-ok"));
		}, delegate
		{
			steps.Add("default-enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "entered"));
		});
		OperationResult result = await app.ExecuteAsync(CancellationToken.None);
		Assert.True(result.IsSuccess);
		Assert.Equal("core-ok", result.Status);
		Assert.Equal<List<string>>(new List<string>(1) { "core" }, steps);
	}

	/// <summary>
	/// NeedCheckGameWindow runs the open-and-enter flow when the game window is missing.
	/// </summary>
	[Fact]
	public async Task NeedCheckGameWindowRunsOpenAndEnterWhenWindowIsMissing()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		List<string> steps = new List<string>();
		TestApplication app = new TestApplication(context, delegate
		{
			steps.Add("core");
			return Task.FromResult(new OperationResult(IsSuccess: true, "core-ok"));
		}, delegate
		{
			steps.Add("default-enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "entered"));
		});
		OperationResult result = await app.ExecuteAsync(CancellationToken.None);
		Assert.True(result.IsSuccess);
		Assert.Equal("core-ok", result.Status);
		Assert.Equal<List<string>>(new List<string>(2) { "default-enter", "core" }, steps);
	}

	/// <summary>
	/// NeedCheckGameWindow stops core execution when opening the game fails.
	/// </summary>
	[Fact]
	public async Task NeedCheckGameWindowStopsCoreWhenOpenAndEnterFails()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		bool coreCalled = false;
		TestApplication app = new TestApplication(context, delegate
		{
			coreCalled = true;
			return Task.FromResult(new OperationResult(IsSuccess: true, "core-ok"));
		}, (ZContext _, CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: false, "enter-failed")));
		OperationResult result = await app.ExecuteAsync(CancellationToken.None);
		Assert.False(result.IsSuccess);
		Assert.Equal("enter-failed", result.Status);
		Assert.False(coreCalled);
	}
}
