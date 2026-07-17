using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnSwitchBackup : AutoBattleButtonAtomicOp
{
	public AtomicBtnSwitchBackup(AutoBattleContext? context, OperationDef operationDef, bool press = false, double? pressTimeSeconds = null, bool release = false)
		: base(context, BattleStateEnum.BtnSwitchBackup.GetDescription(), operationDef, press, pressTimeSeconds, release)
	{
	}
}
