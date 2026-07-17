namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试控制方式切换器。
/// </summary>
public interface IOperationDebugControllerModeSwitcher
{
	/// <summary>
	/// 按配置检查并切换控制方式。
	/// </summary>
	OperationDebugControllerModeResult CheckAndApply();
}
