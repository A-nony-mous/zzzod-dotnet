using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试 atomic op factory。
/// </summary>
public interface IOperationDebugAtomicOpFactory
{
	/// <summary>
	/// 创建 atomic op。
	/// </summary>
	AtomicOp Create(OperationDef operationDef);
}
