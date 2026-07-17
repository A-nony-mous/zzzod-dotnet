using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnMoveS : AutoBattleButtonAtomicOp
{
	public AtomicBtnMoveS(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnMoveS.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
