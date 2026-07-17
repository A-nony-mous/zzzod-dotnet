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
		string text = controller.ActiveScreenshotMethod ?? controller.ConfiguredScreenshotMethod;
		if (windowHandle == 0)
		{
			return E2ECaptureReadinessEvidence.Failed(windowHandle, text, "游戏窗口句柄无效");
		}
		try
		{
			var (capturedAtUtc, mat) = controller.Screenshot();
			using (mat)
			{
				if (mat == null)
				{
					return E2ECaptureReadinessEvidence.Failed(windowHandle, text, "首帧截图为空");
				}
				string screenshotMethod = controller.ActiveScreenshotMethod ?? text;
				return E2ECaptureReadinessEvidence.Succeeded(windowHandle, screenshotMethod, mat, capturedAtUtc);
			}
		}
		catch (Exception ex)
		{
			return E2ECaptureReadinessEvidence.Failed(windowHandle, text, ex.Message);
		}
	}
}
