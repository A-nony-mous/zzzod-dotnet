using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnChainCancel : AutoBattleButtonAtomicOp
{
	public AtomicBtnChainCancel(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnChainCancel.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
