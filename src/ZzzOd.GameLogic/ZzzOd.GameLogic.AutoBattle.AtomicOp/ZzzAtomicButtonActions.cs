using System.Collections.Generic;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public static class ZzzAtomicButtonActions
{
	private static readonly HashSet<string> KnownActions = new HashSet<string>
	{
		BattleStateEnum.BtnDodge.GetDescription(),
		BattleStateEnum.BtnSwitchNext.GetDescription(),
		BattleStateEnum.BtnSwitchPrev.GetDescription(),
		BattleStateEnum.BtnSwitchBackup.GetDescription(),
		BattleStateEnum.BtnSwitchNormalAttack.GetDescription(),
		BattleStateEnum.BtnSwitchSpecialAttack.GetDescription(),
		BattleStateEnum.BtnUltimate.GetDescription(),
		BattleStateEnum.BtnChainLeft.GetDescription(),
		BattleStateEnum.BtnChainRight.GetDescription(),
		BattleStateEnum.BtnChainCancel.GetDescription(),
		BattleStateEnum.BtnMoveW.GetDescription(),
		BattleStateEnum.BtnMoveS.GetDescription(),
		BattleStateEnum.BtnMoveA.GetDescription(),
		BattleStateEnum.BtnMoveD.GetDescription(),
		BattleStateEnum.BtnLock.GetDescription()
	};

	public static bool IsKnownAction(string actionName)
	{
		return KnownActions.Contains(actionName);
	}
}
