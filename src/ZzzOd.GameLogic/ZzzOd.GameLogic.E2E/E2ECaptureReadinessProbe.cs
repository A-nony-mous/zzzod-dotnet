using System;
using OneDragon.Core.Windows.Controller;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 截图 readiness 探测器。
/// </summary>
public sealed class E2ECaptureReadinessProbe
{
	/// <summary>
	/// 执行一次首帧截图并生成 readiness evidence。
	/// </summary>
	/// <param name="controller">Windows 游戏控制器。</param>
	/// <returns>截图 readiness evidence。</returns>
	public E2ECaptureReadinessEvidence Probe(WindowsGameController controller)
	{
		ArgumentNullException.ThrowIfNull(controller, "controller");
		nint windowHandle = controller.WindowHandle;
		string configuredMethod = controller.ConfiguredScreenshotMethod;
		string? activeMethod = controller.ActiveScreenshotMethod;
		if (windowHandle == 0)
		{
			return E2ECaptureReadinessEvidence.Failed(windowHandle, configuredMethod, activeMethod, controller.LastScreenshotAttemptedMethods, "游戏窗口句柄无效");
		}
		try
		{
			var (capturedAtUtc, mat) = controller.Screenshot();
			using (mat)
			{
				if (mat == null)
				{
					return E2ECaptureReadinessEvidence.Failed(
						windowHandle,
						configuredMethod,
						controller.ActiveScreenshotMethod,
						controller.LastScreenshotAttemptedMethods,
						controller.LastScreenshotFailureReason ?? "首帧截图为空");
				}
				string screenshotMethod = controller.ActiveScreenshotMethod ?? configuredMethod;
				return E2ECaptureReadinessEvidence.Succeeded(
					windowHandle,
					configuredMethod,
					screenshotMethod,
					controller.LastScreenshotAttemptedMethods,
					mat,
					capturedAtUtc,
					controller.LastCaptureElapsedMilliseconds);
			}
		}
		catch (Exception ex)
		{
			return E2ECaptureReadinessEvidence.Failed(windowHandle, configuredMethod, controller.ActiveScreenshotMethod, controller.LastScreenshotAttemptedMethods, ex.Message);
		}
	}
}
