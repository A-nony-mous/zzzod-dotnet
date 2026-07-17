using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnChainLeft : AutoBattleButtonAtomicOp
{
	public AtomicBtnChainLeft(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnChainLeft.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
