using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnNormalAttack : AutoBattleButtonAtomicOp
{
	public AtomicBtnNormalAttack(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnSwitchNormalAttack.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
