using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnUltimate : AutoBattleButtonAtomicOp
{
	public AtomicBtnUltimate(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnUltimate.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
