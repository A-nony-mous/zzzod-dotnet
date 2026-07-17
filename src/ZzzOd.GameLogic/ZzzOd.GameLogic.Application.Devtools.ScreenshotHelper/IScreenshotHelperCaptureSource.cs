namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 截图来源。
/// </summary>
public interface IScreenshotHelperCaptureSource
{
	/// <summary>
	/// 获取一张截图。
	/// </summary>
	ScreenshotHelperFrame? Capture();
}
