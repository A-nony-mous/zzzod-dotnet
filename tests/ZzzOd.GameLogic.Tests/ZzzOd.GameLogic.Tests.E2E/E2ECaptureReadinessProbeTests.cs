using System;
using OneDragon.Core.Runtime;
using OneDragon.Core.Windows.Controller;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Tests.E2E;

/// <summary>
/// 测试 E2E 截图 readiness 探测器。
/// </summary>
public sealed class E2ECaptureReadinessProbeTests
{
	[Theory]
	[InlineData(new object[] { "wgc" })]
	[InlineData(new object[] { "print_window" })]
	public void Probe_ShouldRecordFailureWhenWindowHandleIsMissing(string screenshotMethod)
	{
		WindowsGameController controller = new WindowsGameController(null, screenshotMethod);
		E2ECaptureReadinessEvidence e2ECaptureReadinessEvidence = new E2ECaptureReadinessProbe().Probe(controller);
		Assert.Equal(0L, e2ECaptureReadinessEvidence.WindowHandle);
		Assert.Equal(screenshotMethod, e2ECaptureReadinessEvidence.ScreenshotMethod);
		Assert.Null(e2ECaptureReadinessEvidence.FirstFrameWidth);
		Assert.Null(e2ECaptureReadinessEvidence.FirstFrameHeight);
		Assert.Equal("游戏窗口句柄无效", e2ECaptureReadinessEvidence.FailureReason);
	}

	[Theory]
	[InlineData(new object[] { "wgc" })]
	[InlineData(new object[] { "print_window" })]
	public void CaptureReadinessEvidence_ShouldRecordFirstFrameSizeForSupportedMethods(string screenshotMethod)
	{
		using Mat firstFrame = new Mat(new Size(1280, 720), MatType.CV_8UC3, Scalar.All(0.0));
		E2ECaptureReadinessEvidence e2ECaptureReadinessEvidence = E2ECaptureReadinessEvidence.Succeeded(17767, screenshotMethod, firstFrame, new DateTimeOffset(2026, 7, 7, 2, 0, 0, TimeSpan.Zero));
		Assert.Equal(17767L, e2ECaptureReadinessEvidence.WindowHandle);
		Assert.Equal(screenshotMethod, e2ECaptureReadinessEvidence.ScreenshotMethod);
		Assert.Equal(1280, e2ECaptureReadinessEvidence.FirstFrameWidth);
		Assert.Equal(720, e2ECaptureReadinessEvidence.FirstFrameHeight);
		Assert.Null(e2ECaptureReadinessEvidence.FailureReason);
	}

	[Fact(Skip = "Requires a live ZZZ window, configured screenshot method, real desktop permissions and account state.")]
	[Trait("Category", "E2E")]
	public void CaptureReadiness_E2E_RecordsRealWindowFirstFrame()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment(Environment.CurrentDirectory));
		zContext.InitController();
		ZPcController controller = Assert.IsType<ZPcController>(zContext.Controller);
		E2ECaptureReadinessEvidence e2ECaptureReadinessEvidence = new E2ECaptureReadinessProbe().Probe(controller);
		Assert.True(e2ECaptureReadinessEvidence.WindowHandle != 0);
		Assert.Null(e2ECaptureReadinessEvidence.FailureReason);
		Assert.True(e2ECaptureReadinessEvidence.FirstFrameWidth > 0);
		Assert.True(e2ECaptureReadinessEvidence.FirstFrameHeight > 0);
	}
}
