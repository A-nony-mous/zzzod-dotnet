using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;

/// <summary>
/// 鼠标灵敏度检测服务。
/// </summary>
public interface IMouseSensitivityCheckerOperationServices
{
	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToNormalWorldAsync(ZContext context);

	/// <summary>传送到录像店房间。</summary>
	Task<OperationResult> TransportToVideoStoreAsync(ZContext context);

	/// <summary>是否手柄后台模式。</summary>
	bool IsGamepadMode(ZContext context);

	/// <summary>读取小地图朝向。</summary>
	double? ReadViewAngle(ZContext context);

	/// <summary>鼠标距离转向。</summary>
	void TurnByDistance(ZContext context, int distance);

	/// <summary>手柄转向测试。</summary>
	void TurnGamepad(ZContext context, double durationSeconds);

	/// <summary>更新鼠标转向系数。</summary>
	void UpdateTurnDx(ZContext context, double turnDx);

	/// <summary>更新手柄转向速度。</summary>
	void UpdateGamepadTurnSpeed(ZContext context, double speed);
}
