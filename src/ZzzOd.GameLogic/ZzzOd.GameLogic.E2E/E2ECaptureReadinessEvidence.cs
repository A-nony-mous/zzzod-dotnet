using System;
using OpenCvSharp;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 截图 readiness evidence。
/// </summary>
public sealed class E2ECaptureReadinessEvidence
{
	public long WindowHandle { get; set; }

	public string ScreenshotMethod { get; set; } = string.Empty;

	public int? FirstFrameWidth { get; set; }

	public int? FirstFrameHeight { get; set; }

	public DateTimeOffset? FirstFrameCapturedAtUtc { get; set; }

	public string? FailureReason { get; set; }

	public static E2ECaptureReadinessEvidence Succeeded(nint windowHandle, string screenshotMethod, Mat firstFrame, DateTimeOffset capturedAtUtc)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(screenshotMethod, "screenshotMethod");
		ArgumentNullException.ThrowIfNull(firstFrame, "firstFrame");
		return new E2ECaptureReadinessEvidence
		{
			WindowHandle = ((IntPtr)windowHandle).ToInt64(),
			ScreenshotMethod = screenshotMethod,
			FirstFrameWidth = firstFrame.Width,
			FirstFrameHeight = firstFrame.Height,
			FirstFrameCapturedAtUtc = capturedAtUtc
		};
	}

	public static E2ECaptureReadinessEvidence Failed(nint windowHandle, string screenshotMethod, string failureReason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(screenshotMethod, "screenshotMethod");
		ArgumentException.ThrowIfNullOrWhiteSpace(failureReason, "failureReason");
		return new E2ECaptureReadinessEvidence
		{
			WindowHandle = ((IntPtr)windowHandle).ToInt64(),
			ScreenshotMethod = screenshotMethod,
			FailureReason = failureReason
		};
	}
}
