using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnSwitchPrev : AutoBattleButtonAtomicOp
{
	public AtomicBtnSwitchPrev(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnSwitchPrev.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
