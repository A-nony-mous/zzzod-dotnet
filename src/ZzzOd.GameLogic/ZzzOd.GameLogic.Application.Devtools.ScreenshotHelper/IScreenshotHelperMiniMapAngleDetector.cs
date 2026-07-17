using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 小地图角度检测器。
/// </summary>
public interface IScreenshotHelperMiniMapAngleDetector
{
	/// <summary>
	/// 小地图角度缺失时返回 true。
	/// </summary>
	bool ShouldSaveForMissingAngle(Mat screen);
}
