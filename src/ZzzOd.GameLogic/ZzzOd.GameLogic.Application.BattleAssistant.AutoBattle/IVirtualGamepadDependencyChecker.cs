namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 虚拟手柄依赖检查器。
/// </summary>
public interface IVirtualGamepadDependencyChecker
{
	/// <summary>
	/// 虚拟手柄依赖是否可用。
	/// </summary>
	bool IsAvailable();
}
