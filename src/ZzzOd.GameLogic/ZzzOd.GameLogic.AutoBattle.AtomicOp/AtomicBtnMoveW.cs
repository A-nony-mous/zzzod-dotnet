using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnMoveW : AutoBattleButtonAtomicOp
{
	public AtomicBtnMoveW(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnMoveW.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
