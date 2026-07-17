using OpenCvSharp;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 恶名狩猎战斗前移动检测器。
/// </summary>
public interface INotoriousHuntMoveDetector
{
	/// <summary>初始化检测模型或检测资源。</summary>
	void Initialize();

	/// <summary>检测距离提示白点。</summary>
	NotoriousHuntDistanceHint? DetectDistanceHint(Mat screen);
}
