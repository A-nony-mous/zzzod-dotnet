using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class IntelBoardFixedAssetTests
{
	private sealed class FixedAssetController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.Black);

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public override bool IsGameWindowReady => true;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			if (position.HasValue)
			{
				Clicks.Add(position.Value);
			}
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

		public void Reset()
		{
			Clicks.Clear();
		}

		public void Dispose()
		{
			_screenshot.Dispose();
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return _screenshot.Clone();
		}
	}

	[Trait("Category", "Integration")]
	[Fact]
	public async Task RealGameFixtures_RunProductionOcrTemplatesAndIntelBoardServices()
	{
		OpenCvTestRuntime.RequireAvailable();
		string workspaceRoot = FindWorkspaceRoot();
		string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-intel-board-fixed-assets", Guid.NewGuid().ToString("N"));
		CopyDirectory(Path.Combine(workspaceRoot, "config"), Path.Combine(runRoot, "config"));
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(runRoot, workspaceRoot));
			using FixedAssetController controller = new FixedAssetController();
			context.AttachController(controller);
			context.ScreenContext.Reload();
			context.ScreenContext.EnterScope("intel_board");
			double? detLimitSideLen = 1920.0;
			Assert.True(context.UseOcrProfile("v6-small", useGpu: false, null, detLimitSideLen));
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			TemplateInfo starTemplate = Assert.IsType<TemplateInfo>(context.TemplateMatcher.TemplateLoader.GetTemplate("intel_board", "Star"));
			Mat starRaw = Assert.IsType<Mat>(starTemplate.Raw);
			MatchResultList templateMatches = context.TemplateMatcher.MatchTemplate(starRaw, "intel_board", "Star", "raw", 0.8, null, ignoreTemplateMask: false, onlyBest: false);
			Assert.NotEmpty(templateMatches);
			using Mat list = LoadFixture("intel-board-list.png");
			MatchResultList stars = context.TemplateMatcher.MatchTemplate(list, "intel_board", "Star", "raw", 0.8, null, ignoreTemplateMask: false, onlyBest: false);
			Assert.Empty(stars);
			Assert.Equal(actual: await services.FindCommissionAsync(context, list).WaitAsync(TimeSpan.FromSeconds(30L)), expected: IntelBoardCommissionType.NotoriousHunt);
			Assert.InRange(Assert.Single(controller.Clicks).Y, 120, 900);
			controller.Reset();
			using Mat accept = LoadFixture("intel-board-accept.png");
			OperationResult acceptResult = await services.AcceptCommissionAsync(context, accept).WaitAsync(TimeSpan.FromSeconds(30L));
			Assert.True(acceptResult.IsSuccess, acceptResult.Status);
			Assert.Equal("接取委托", acceptResult.Status);
			OneDragon.Core.Abstractions.Geometry.Point acceptClick = Assert.Single(controller.Clicks);
			Assert.InRange(acceptClick.X, 950, 1070);
			Assert.InRange(acceptClick.Y, 820, 880);
			controller.Reset();
			using Mat running = LoadFixture("intel-board-running.png");
			OperationResult runningResult = await services.AcceptCommissionAsync(context, running).WaitAsync(TimeSpan.FromSeconds(30L));
			Assert.True(runningResult.IsSuccess, runningResult.Status);
			Assert.Equal("前往", runningResult.Status);
			OneDragon.Core.Abstractions.Geometry.Point runningClick = Assert.Single(controller.Clicks);
			Assert.InRange(runningClick.X, 1100, 1220);
			Assert.InRange(runningClick.Y, 820, 890);
			controller.Reset();
			using Mat acceptFailed = LoadFixture("intel-board-accept-failed.png");
			OperationResult nextStepResult = await services.NextStepAsync(context, acceptFailed).WaitAsync(TimeSpan.FromSeconds(30L));
			Assert.True(nextStepResult.IsSuccess, nextStepResult.Status);
			Assert.Equal("接取失败", nextStepResult.Status);
			Assert.Empty(controller.Clicks);
			OperationResult confirmResult = await services.ConfirmAcceptFailedAsync(context, acceptFailed).WaitAsync(TimeSpan.FromSeconds(30L));
			Assert.True(confirmResult.IsSuccess, confirmResult.Status);
			Assert.Equal("确认", confirmResult.Status);
			Assert.Single(controller.Clicks);
			context.ScreenContext.ExitScope();
		}
		finally
		{
			if (Directory.Exists(runRoot))
			{
				Directory.Delete(runRoot, recursive: true);
			}
		}
	}

	private static Mat LoadFixture(string fileName)
	{
		string text = Path.Combine(AppContext.BaseDirectory, "TestData", "IntelBoard", fileName);
		Mat mat = Cv2.ImRead(text);
		Assert.False(mat.Empty(), "无法读取固定资产 " + text);
		return mat;
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "assets")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 工作区根目录。");
	}

	private static void CopyDirectory(string sourceDirectory, string targetDirectory)
	{
		foreach (string item in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(sourceDirectory, item);
			string text = Path.Combine(targetDirectory, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item, text, overwrite: true);
		}
	}
}
