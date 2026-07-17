using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnLock : AutoBattleButtonAtomicOp
{
	public AtomicBtnLock(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnLock.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
