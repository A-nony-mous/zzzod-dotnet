using System;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 截图保存器。
/// </summary>
public interface IScreenshotHelperImageStore
{
	/// <summary>
	/// 保存截图。
	/// </summary>
	ScreenshotHelperSavedImage Save(Mat image, string prefix, DateTimeOffset captureTimeUtc);
}
