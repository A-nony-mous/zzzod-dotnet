using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnSwitchNext : AutoBattleButtonAtomicOp
{
	public AtomicBtnSwitchNext(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnSwitchNext.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
