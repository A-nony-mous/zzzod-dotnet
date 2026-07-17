using System;
using OneDragon.Core.Runtime;
using OneDragon.Core.Utils;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 保存到工作目录下的 .debug/images。
/// </summary>
public sealed class DebugScreenshotHelperImageStore : IScreenshotHelperImageStore
{
	private readonly OneDragonEnvironment _environment;

	/// <summary>
	/// 初始化保存器。
	/// </summary>
	public DebugScreenshotHelperImageStore(OneDragonEnvironment environment)
	{
		_environment = environment;
	}

	/// <inheritdoc />
	public ScreenshotHelperSavedImage Save(Mat image, string prefix, DateTimeOffset captureTimeUtc)
	{
		ArgumentNullException.ThrowIfNull(image, "image");
		string text = (string.IsNullOrWhiteSpace(prefix) ? "screenshot" : prefix.Trim());
		string text2 = $"{text}_{captureTimeUtc.ToUnixTimeMilliseconds()}";
		string pathUnderWorkDir = _environment.GetPathUnderWorkDir(".debug", "images", text2 + ".png");
		CvImageUtils.SaveImage(image, pathUnderWorkDir);
		return new ScreenshotHelperSavedImage(text2, pathUnderWorkDir, text, captureTimeUtc);
	}
}
