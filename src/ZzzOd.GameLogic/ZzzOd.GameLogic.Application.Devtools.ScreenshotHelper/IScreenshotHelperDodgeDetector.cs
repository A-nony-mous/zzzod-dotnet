using System;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 截图助手闪避检测器。
/// </summary>
public interface IScreenshotHelperDodgeDetector
{
	/// <summary>
	/// 检查闪避红光或黄光。
	/// </summary>
	bool CheckDodgeFlash(Mat screen, DateTimeOffset captureTimeUtc);

	/// <summary>
	/// 检查闪避音频。
	/// </summary>
	bool CheckDodgeAudio(DateTimeOffset captureTimeUtc);
}
