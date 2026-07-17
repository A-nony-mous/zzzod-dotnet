using System;
using System.Collections.Generic;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class AutoBattleStateEnumParityTests
{
	[Fact]
	public void BattleStateEnum_DescriptionsMatchPythonValues()
	{
		Dictionary<BattleStateEnum, string> dictionary = new Dictionary<BattleStateEnum, string>
		{
			[BattleStateEnum.BtnDodge] = "按键-闪避",
			[BattleStateEnum.BtnSwitchNext] = "按键-切换角色-下一个",
			[BattleStateEnum.BtnSwitchPrev] = "按键-切换角色-上一个",
			[BattleStateEnum.BtnSwitchBackup] = "按键-切换后援",
			[BattleStateEnum.BtnSwitchNormalAttack] = "按键-普通攻击",
			[BattleStateEnum.BtnSwitchSpecialAttack] = "按键-特殊攻击",
			[BattleStateEnum.BtnUltimate] = "按键-终结技",
			[BattleStateEnum.BtnChainLeft] = "按键-连携技-左",
			[BattleStateEnum.BtnChainRight] = "按键-连携技-右",
			[BattleStateEnum.BtnMoveW] = "按键-移动-前",
			[BattleStateEnum.BtnMoveS] = "按键-移动-后",
			[BattleStateEnum.BtnMoveA] = "按键-移动-左",
			[BattleStateEnum.BtnMoveD] = "按键-移动-右",
			[BattleStateEnum.BtnLock] = "按键-锁定敌人",
			[BattleStateEnum.BtnChainCancel] = "按键-连携技-取消",
			[BattleStateEnum.StatusSpecialReady] = "按键可用-特殊攻击",
			[BattleStateEnum.StatusUltimateReady] = "按键可用-终结技",
			[BattleStateEnum.StatusChainReady] = "按键可用-连携技",
			[BattleStateEnum.StatusQuickAssistReady] = "按键可用-快速支援",
			[BattleStateEnum.StatusSwitchBackupReady] = "按键可用-切换后援"
		};
		Assert.Equal(dictionary.Count, Enum.GetValues<BattleStateEnum>().Length);
		foreach (var (value, expected) in dictionary)
		{
			Assert.Equal(expected, value.GetDescription());
		}
	}

	[Fact]
	public void YoloStateEventEnum_DescriptionsMatchPythonValues()
	{
		Dictionary<YoloStateEventEnum, string> dictionary = new Dictionary<YoloStateEventEnum, string>
		{
			[YoloStateEventEnum.DODGE_YELLOW] = "闪避识别-黄光",
			[YoloStateEventEnum.DODGE_RED] = "闪避识别-红光",
			[YoloStateEventEnum.DODGE_AUDIO] = "闪避识别-声音"
		};
		Assert.Equal(dictionary.Count, Enum.GetValues<YoloStateEventEnum>().Length);
		foreach (var (value, expected) in dictionary)
		{
			Assert.Equal(expected, value.GetDescription());
		}
	}

	[Fact]
	public void StateRecordService_AcceptsPythonCompatibleBattleAndYoloStates()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("按键-移动-前"));
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("按键-移动-前-按下"));
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("按键-切换角色-下一个"));
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("闪避识别-声音"));
	}
}
