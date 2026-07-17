using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnSpecialAttack : AutoBattleButtonAtomicOp
{
	public AtomicBtnSpecialAttack(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnSwitchSpecialAttack.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
