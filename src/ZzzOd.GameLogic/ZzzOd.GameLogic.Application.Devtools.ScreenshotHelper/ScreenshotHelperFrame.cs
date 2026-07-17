using System;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 截图结果。
/// </summary>
public sealed record ScreenshotHelperFrame(DateTimeOffset CaptureTimeUtc, Mat Image) : IDisposable
{
	/// <inheritdoc />
	public void Dispose()
	{
		Image.Dispose();
	}
}
