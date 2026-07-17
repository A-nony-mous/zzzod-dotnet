namespace ZzzOd.GameLogic.HollowZero.GameData;

public static class HollowZeroSpecialEvent
{
	public static readonly HollowZeroEvent HollowInside = new HollowZeroEvent("空洞内部");

	public static readonly HollowZeroEvent ResoniumChoose = new HollowZeroEvent("选择");

	public static readonly HollowZeroEvent ResoniumConfirm1 = new HollowZeroEvent("确认");

	public static readonly HollowZeroEvent ResoniumConfirm2 = new HollowZeroEvent("确定");

	public static readonly HollowZeroEvent ResoniumUpgrade = new HollowZeroEvent("催化");

	public static readonly HollowZeroEvent ResoniumDrop = new HollowZeroEvent("丢弃");

	public static readonly HollowZeroEvent ResoniumDrop2 = new HollowZeroEvent("抵押欠款");

	public static readonly HollowZeroEvent ResoniumSwitch = new HollowZeroEvent("交换");

	public static readonly HollowZeroEvent SwiftSupplyLife = new HollowZeroEvent("回复生命值");

	public static readonly HollowZeroEvent SwiftSupplyCoin = new HollowZeroEvent("获取齿轮硬币");

	public static readonly HollowZeroEvent SwiftSupplyPress = new HollowZeroEvent("降低压力值");

	public static readonly HollowZeroEvent CorruptionRemove = new HollowZeroEvent("清除");

	public static readonly HollowZeroEvent CallForSupport = new HollowZeroEvent("呼叫增援！", null, null, 1f, onTheRight: true);

	public static readonly HollowZeroEvent ResoniumStore0 = new HollowZeroEvent("欢迎光临！本店只收齿轮硬币～", null, null, 1f, onTheRight: true);

	public static readonly HollowZeroEvent ResoniumStore1 = new HollowZeroEvent("欢迎本店欢迎", null, null, 1f, onTheRight: true);

	public static readonly HollowZeroEvent ResoniumStore2 = new HollowZeroEvent("鸣徽交易", null, null, 1f, onTheRight: true);

	public static readonly HollowZeroEvent ResoniumStore3 = new HollowZeroEvent("特价折扣", null, null, 1f, onTheRight: true);

	public static readonly HollowZeroEvent ResoniumStore4 = new HollowZeroEvent("鸣徽催化", null, null, 1f, onTheRight: true);

	public static readonly HollowZeroEvent ResoniumStore5 = new HollowZeroEvent("进入商店", null, null, 1f, onTheRight: true, isEntryOpt: true);

	public static readonly HollowZeroEvent CriticalStageEntry = new HollowZeroEvent("进入守门人决斗", null, null, 1f, onTheRight: true, isEntryOpt: true);

	public static readonly HollowZeroEvent CriticalStageEntry2 = new HollowZeroEvent("进入危险目标决斗", null, null, 1f, onTheRight: true, isEntryOpt: true);

	public static readonly HollowZeroEvent InBattle = new HollowZeroEvent("战斗画面");

	public static readonly HollowZeroEvent MissionComplete = new HollowZeroEvent("副本通关");

	public static readonly HollowZeroEvent FullInBag = new HollowZeroEvent("背包已满");

	public static readonly HollowZeroEvent OldCapital = new HollowZeroEvent("旧都失物");

	public static readonly HollowZeroEvent DoorBattleEntry = new HollowZeroEvent("开门", null, null, 1f, onTheRight: true, isEntryOpt: true);

	public static readonly HollowZeroEvent NeedInteract = new HollowZeroEvent("需要交互");
}
