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

	public string ConfiguredScreenshotMethod { get; set; } = string.Empty;

	public string? ActiveScreenshotMethod { get; set; }

	public IReadOnlyList<string> AttemptedScreenshotMethods { get; set; } = Array.Empty<string>();

	public int? FirstFrameWidth { get; set; }

	public int? FirstFrameHeight { get; set; }

	public DateTimeOffset? FirstFrameCapturedAtUtc { get; set; }

	public double? FirstFrameCaptureElapsedMilliseconds { get; set; }

	public string? FailureReason { get; set; }

	public static E2ECaptureReadinessEvidence Succeeded(nint windowHandle, string screenshotMethod, Mat firstFrame, DateTimeOffset capturedAtUtc)
	{
		return Succeeded(windowHandle, screenshotMethod, screenshotMethod, Array.Empty<string>(), firstFrame, capturedAtUtc, null);
	}

	public static E2ECaptureReadinessEvidence Succeeded(
		nint windowHandle,
		string configuredScreenshotMethod,
		string activeScreenshotMethod,
		IReadOnlyList<string> attemptedScreenshotMethods,
		Mat firstFrame,
		DateTimeOffset capturedAtUtc,
		double? captureElapsedMilliseconds)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(configuredScreenshotMethod, "configuredScreenshotMethod");
		ArgumentException.ThrowIfNullOrWhiteSpace(activeScreenshotMethod, "activeScreenshotMethod");
		ArgumentNullException.ThrowIfNull(attemptedScreenshotMethods);
		ArgumentNullException.ThrowIfNull(firstFrame, "firstFrame");
		return new E2ECaptureReadinessEvidence
		{
			WindowHandle = ((IntPtr)windowHandle).ToInt64(),
			ScreenshotMethod = activeScreenshotMethod,
			ConfiguredScreenshotMethod = configuredScreenshotMethod,
			ActiveScreenshotMethod = activeScreenshotMethod,
			AttemptedScreenshotMethods = attemptedScreenshotMethods.ToArray(),
			FirstFrameWidth = firstFrame.Width,
			FirstFrameHeight = firstFrame.Height,
			FirstFrameCapturedAtUtc = capturedAtUtc,
			FirstFrameCaptureElapsedMilliseconds = captureElapsedMilliseconds,
		};
	}

	public static E2ECaptureReadinessEvidence Failed(nint windowHandle, string screenshotMethod, string failureReason)
	{
		return Failed(windowHandle, screenshotMethod, null, Array.Empty<string>(), failureReason);
	}

	public static E2ECaptureReadinessEvidence Failed(
		nint windowHandle,
		string configuredScreenshotMethod,
		string? activeScreenshotMethod,
		IReadOnlyList<string> attemptedScreenshotMethods,
		string failureReason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(configuredScreenshotMethod, "configuredScreenshotMethod");
		ArgumentNullException.ThrowIfNull(attemptedScreenshotMethods);
		ArgumentException.ThrowIfNullOrWhiteSpace(failureReason, "failureReason");
		return new E2ECaptureReadinessEvidence
		{
			WindowHandle = ((IntPtr)windowHandle).ToInt64(),
			ScreenshotMethod = activeScreenshotMethod ?? configuredScreenshotMethod,
			ConfiguredScreenshotMethod = configuredScreenshotMethod,
			ActiveScreenshotMethod = activeScreenshotMethod,
			AttemptedScreenshotMethods = attemptedScreenshotMethods.ToArray(),
			FailureReason = failureReason
		};
	}
}
