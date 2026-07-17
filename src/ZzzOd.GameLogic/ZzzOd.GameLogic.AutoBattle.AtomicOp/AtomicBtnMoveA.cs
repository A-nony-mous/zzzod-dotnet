using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnMoveA : AutoBattleButtonAtomicOp
{
	public AtomicBtnMoveA(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnMoveA.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
