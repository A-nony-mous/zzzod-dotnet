using System;
using System.IO;
using System.Runtime.CompilerServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Controller;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

/// <summary>
/// Tests ZZZ application launcher initialization.
/// </summary>
public sealed class ZApplicationLauncherTests : IDisposable
{
	private sealed class TestLauncher : ZApplicationLauncher
	{
		public TestLauncher(Func<ZContext> contextFactory, bool initializeOcrProfile, bool validateAssets)
			: base(contextFactory, initializeContext: true, initializeOcrProfile, validateAssets)
		{
		}

		protected override void InitializeController(ZContext context)
		{
			context.AttachController(new EmptyController());
		}

		protected override void InitializeForApplication(ZContext context)
		{
		}
	}

	private sealed class EmptyController : ControllerBase
	{
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

	private readonly string _rootDirectory;

	/// <summary>
	/// Creates a launcher test fixture.
	/// </summary>
	public ZApplicationLauncherTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-launcher-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
	}

	/// <summary>
	/// Launcher initialization reloads screen definitions before operations run.
	/// </summary>
	[Fact]
	public void CreateContext_ReloadsScreenDefinitions()
	{
		ScreenSeed.WriteScreens(
			Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"),
			"- screen_id: normal_world\n  screen_name: 大世界\n  area_list:\n  - area_name: 星期\n    pc_rect:\n    - 201\n    - 38\n    - 270\n    - 86\n    text: 星期");
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		TestLauncher testLauncher = new TestLauncher(() => new ZContext(environment), initializeOcrProfile: false, validateAssets: false);
		using ZContext zContext = testLauncher.CreateContext();
		Assert.NotNull(zContext.ScreenContext.GetArea("大世界", "星期"));
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}
