using OpenCvSharp;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 恶名狩猎战斗前移动检测器。
/// </summary>
public interface INotoriousHuntMoveDetector
{
	/// <summary>检测距离提示白点。</summary>
	NotoriousHuntDistanceHint? DetectDistanceHint(Mat screen);
}
