using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnChainRight : AutoBattleButtonAtomicOp
{
	public AtomicBtnChainRight(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnChainRight.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
