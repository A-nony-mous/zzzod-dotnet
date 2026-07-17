using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnDodge : AutoBattleButtonAtomicOp
{
	public AtomicBtnDodge(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnDodge.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
