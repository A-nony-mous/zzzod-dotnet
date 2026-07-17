using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnMoveD : AutoBattleButtonAtomicOp
{
	public AtomicBtnMoveD(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnMoveD.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
