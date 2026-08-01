using System.ComponentModel;

namespace ZzzOd.GameLogic.AutoBattle;

/// <summary>
/// 自动战斗状态枚举
/// </summary>
public enum BattleStateEnum
{
	[Description("按键-闪避")]
	BtnDodge,
	[Description("按键-切换角色-下一个")]
	BtnSwitchNext,
	[Description("按键-切换角色-上一个")]
	BtnSwitchPrev,
	[Description("按键-切换后援")]
	BtnSwitchBackup,
	[Description("按键-普通攻击")]
	BtnSwitchNormalAttack,
	[Description("按键-特殊攻击")]
	BtnSwitchSpecialAttack,
	[Description("按键-终结技")]
	BtnUltimate,
	[Description("按键-连携技-左")]
	BtnChainLeft,
	[Description("按键-连携技-右")]
	BtnChainRight,
	[Description("按键-移动-前")]
	BtnMoveW,
	[Description("按键-移动-后")]
	BtnMoveS,
	[Description("按键-移动-左")]
	BtnMoveA,
	[Description("按键-移动-右")]
	BtnMoveD,
	[Description("按键-锁定敌人")]
	BtnLock,
	[Description("按键-连携技-取消")]
	BtnChainCancel,
	[Description("按键可用-特殊攻击")]
	StatusSpecialReady,
	[Description("按键可用-终结技")]
	StatusUltimateReady,
	[Description("按键可用-连携技")]
	StatusChainReady,
	[Description("按键可用-快速支援")]
	StatusQuickAssistReady,
	[Description("按键可用-切换后援")]
	StatusSwitchBackupReady,
	[Description("按键可用-普通攻击")]
	StatusNormalAttackReady
}
