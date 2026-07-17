using OneDragon.Core.Operation;
using ZzzOd.GameLogic.AutoBattle;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 复用 AutoBattle atomic op factory。
/// </summary>
public sealed class AutoBattleOperationDebugAtomicOpFactory : IOperationDebugAtomicOpFactory
{
	private readonly AutoBattleContext _autoBattleContext;

	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public AutoBattleOperationDebugAtomicOpFactory(AutoBattleContext autoBattleContext)
	{
		_autoBattleContext = autoBattleContext;
	}

	/// <inheritdoc />
	public AtomicOp Create(OperationDef operationDef)
	{
		return _autoBattleContext.AtomicOpFactory.GetAtomicOp(operationDef);
	}
}
