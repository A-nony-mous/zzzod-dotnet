using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.GameData;

public sealed class AgentEnum
{
	public static readonly AgentEnum ANBY = new AgentEnum(Create("anby", "安比", RareTypeEnum.A, AgentTypeEnum.STUN, DmgTypeEnum.ELECTRIC, new string[] { "anby" }));

	public static readonly AgentEnum ANTON = new AgentEnum(Create("anton", "安东", RareTypeEnum.A, AgentTypeEnum.ATTACK, DmgTypeEnum.ELECTRIC, new string[] { "anton" }));

	public static readonly AgentEnum BEN = new AgentEnum(Create("ben", "本", RareTypeEnum.A, AgentTypeEnum.DEFENSE, DmgTypeEnum.FIRE, new string[] { "ben" }));

	public static readonly AgentEnum BILLY = new AgentEnum(Create("billy", "比利", RareTypeEnum.A, AgentTypeEnum.ATTACK, DmgTypeEnum.ELECTRIC, new string[] { "billy" }));

	public static readonly AgentEnum CORIN = new AgentEnum(Create("corin", "可琳", RareTypeEnum.A, AgentTypeEnum.ATTACK, DmgTypeEnum.PHYSICAL, new string[] { "corin" }));

	public static readonly AgentEnum ELLEN = new AgentEnum(Create("ellen", "艾莲", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ICE, new string[2] { "ellen", "ellen_on_campus" }, new string[] { "艾莲-急冻充能" }));

	public static readonly AgentEnum GRACE = new AgentEnum(Create("grace", "格莉丝", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.ELECTRIC, new string[] { "grace" }, new string[] { "格莉丝-电能" }));

	public static readonly AgentEnum KOLEDA = new AgentEnum(Create("koleda", "珂蕾妲", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.FIRE, new string[] { "koleda" }));

	public static readonly AgentEnum LUCY = new AgentEnum(Create("lucy", "露西", RareTypeEnum.A, AgentTypeEnum.SUPPORT, DmgTypeEnum.FIRE, new string[] { "lucy" }));

	public static readonly AgentEnum LYCAON = new AgentEnum(Create("lycaon", "莱卡恩", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.ICE, new string[] { "lycaon" }));

	public static readonly AgentEnum NEKOMATA = new AgentEnum(Create("nekomata", "猫又", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.PHYSICAL, new string[] { "nekomata" }));

	public static readonly AgentEnum NICOLE = new AgentEnum(Create("nicole", "妮可", RareTypeEnum.A, AgentTypeEnum.SUPPORT, DmgTypeEnum.ETHER, new string[2] { "nicole", "nicole_cunning_cutie" }));

	public static readonly AgentEnum PIPER = new AgentEnum(Create("piper", "派派", RareTypeEnum.A, AgentTypeEnum.ANOMALY, DmgTypeEnum.PHYSICAL, new string[] { "piper" }));

	public static readonly AgentEnum RINA = new AgentEnum(Create("rina", "丽娜", RareTypeEnum.S, AgentTypeEnum.SUPPORT, DmgTypeEnum.ELECTRIC, new string[] { "rina" }));

	public static readonly AgentEnum SOLDIER_11 = new AgentEnum(Create("soldier_11", "11号", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.FIRE, new string[] { "soldier_11" }));

	public static readonly AgentEnum SOUKAKU = new AgentEnum(Create("soukaku", "苍角", RareTypeEnum.A, AgentTypeEnum.SUPPORT, DmgTypeEnum.ICE, new string[] { "soukaku" }, new string[] { "苍角-涡流" }));

	public static readonly AgentEnum ZHU_YUAN = new AgentEnum(Create("zhu_yuan", "朱鸢", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ETHER, new string[] { "zhu_yuan" }, new string[] { "朱鸢-子弹数" }));

	public static readonly AgentEnum QINGYI = new AgentEnum(Create("qingyi", "青衣", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.ELECTRIC, new string[] { "qingyi" }, new string[] { "青衣-电压" }));

	public static readonly AgentEnum JANE_DOE = new AgentEnum(Create("jane_doe", "简", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.PHYSICAL, new string[2] { "jane_doe", "jane_doe_nocturne_of_light" }, new string[2] { "简-萨霍夫跳", "简-狂热心流" }));

	public static readonly AgentEnum SETH_LOWELL = new AgentEnum(Create("seth_lowell", "赛斯", RareTypeEnum.A, AgentTypeEnum.DEFENSE, DmgTypeEnum.ELECTRIC, new string[] { "seth_lowell" }, new string[] { "赛斯-意气" }));

	public static readonly AgentEnum CAESAR_KING = new AgentEnum(Create("caesar_king", "凯撒", RareTypeEnum.S, AgentTypeEnum.DEFENSE, DmgTypeEnum.PHYSICAL, new string[] { "caesar_king" }));

	public static readonly AgentEnum BURNICE_WHITE = new AgentEnum(Create("burnice_white", "柏妮思", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.FIRE, new string[] { "burnice_white" }, new string[] { "柏妮思-燃点" }));

	public static readonly AgentEnum YANAGI = new AgentEnum(Create("yanagi", "柳", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.ELECTRIC, new string[] { "yanagi" }));

	public static readonly AgentEnum LIGHTER = new AgentEnum(Create("lighter", "莱特", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.FIRE, new string[] { "lighter" }, new string[] { "莱特-士气" }));

	public static readonly AgentEnum ASABA_HARUMASA = new AgentEnum(Create("asaba_harumasa", "悠真", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ELECTRIC, new string[] { "asaba_harumasa" }));

	public static readonly AgentEnum HOSHIMI_MIYABI = new AgentEnum(Create("hoshimi_miyabi", "雅", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.ICE, new string[2] { "hoshimi_miyabi", "hoshimi_miyabi_dignified_blossom" }, new string[] { "雅-落霜" }));

	public static readonly AgentEnum ASTRA_YAO = new AgentEnum(Create("astra_yao", "耀嘉音", RareTypeEnum.S, AgentTypeEnum.SUPPORT, DmgTypeEnum.ETHER, new string[2] { "astra_yao", "astra_yao_chandelier" }));

	public static readonly AgentEnum EVELYN_CHEVALIER = new AgentEnum(Create("evelyn_chevalier", "伊芙琳", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.FIRE, new string[] { "evelyn_chevalier" }, new string[2] { "伊芙琳-燎火", "伊芙琳-燎索点" }));

	public static readonly AgentEnum SOLDIER_0_ANBY = new AgentEnum(Create("soldier_0_anby", "零号安比", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ELECTRIC, new string[] { "soldier_0_anby" }));

	public static readonly AgentEnum PULCHRA = new AgentEnum(Create("pulchra", "波可娜", RareTypeEnum.A, AgentTypeEnum.STUN, DmgTypeEnum.PHYSICAL, new string[] { "pulchra" }, new string[] { "波可娜-猎步" }));

	public static readonly AgentEnum TRIGGER = new AgentEnum(Create("trigger", "扳机", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.ELECTRIC, new string[] { "trigger" }, new string[] { "扳机-绝意" }));

	public static readonly AgentEnum VIVIAN = new AgentEnum(Create("vivian", "薇薇安", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.ETHER, new string[2] { "vivian", "vivian_iris_of_the_shore" }, new string[2] { "薇薇安-飞羽", "薇薇安-护羽" }));

	public static readonly AgentEnum HUGO_VLAD = new AgentEnum(Create("hugo_vlad", "雨果", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ICE, new string[] { "hugo_vlad" }));

	public static readonly AgentEnum YIXUAN = new AgentEnum(Create("yixuan", "仪玄", RareTypeEnum.S, AgentTypeEnum.RUPTURE, DmgTypeEnum.ETHER, new string[2] { "yixuan", "yixuan_trails_of_ink" }, new string[3] { "仪玄-玄墨值", "仪玄-术法值全满", "仪玄-术法值" }));

	public static readonly AgentEnum PANYINHU = new AgentEnum(Create("panyinhu", "潘引壶", RareTypeEnum.A, AgentTypeEnum.DEFENSE, DmgTypeEnum.PHYSICAL, new string[2] { "panyinhu", "panyinhu_culinary_jewel" }));

	public static readonly AgentEnum JU_FUFU = new AgentEnum(Create("ju_fufu", "橘福福", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.FIRE, new string[] { "ju_fufu" }, new string[] { "威风" }));

	public static readonly AgentEnum YUZUHA = new AgentEnum(Create("yuzuha", "浮波柚叶", RareTypeEnum.S, AgentTypeEnum.SUPPORT, DmgTypeEnum.PHYSICAL, new string[2] { "yuzuha", "yuzuha_tanuki_in_broad_daylight" }, new string[] { "柚叶-甜度点" }));

	public static readonly AgentEnum ALICE = new AgentEnum(Create("alice", "爱丽丝", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.PHYSICAL, new string[2] { "alice", "alice_sea_of_thyme" }, new string[] { "爱丽丝-剑仪" }));

	public static readonly AgentEnum SEED = new AgentEnum(Create("seed", "席德", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ELECTRIC, new string[] { "seed" }, new string[] { "席德-钢能" }));

	public static readonly AgentEnum ORPHIE = new AgentEnum(Create("orphie", "奥菲丝", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.FIRE, new string[] { "orphie" }, new string[] { "奥菲丝-蓄炎" }));

	public static readonly AgentEnum LUCIA = new AgentEnum(Create("lucia", "卢西娅", RareTypeEnum.S, AgentTypeEnum.SUPPORT, DmgTypeEnum.ETHER, new string[] { "lucia" }, new string[] { "卢西娅-梦境值" }));

	public static readonly AgentEnum MANATO = new AgentEnum(Create("manato", "真斗", RareTypeEnum.A, AgentTypeEnum.RUPTURE, DmgTypeEnum.FIRE, new string[2] { "manato", "manato_white_heart_silhouette" }, new string[] { "真斗-炽心" }));

	public static readonly AgentEnum YIDHARI = new AgentEnum(Create("yidhari", "伊德海莉", RareTypeEnum.S, AgentTypeEnum.RUPTURE, DmgTypeEnum.ICE, new string[] { "yidhari" }, new string[] { "伊德海莉-蓄力段数" }));

	public static readonly AgentEnum DIALYN = new AgentEnum(Create("dialyn", "琉音", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.PHYSICAL, new string[] { "dialyn" }, new string[2] { "琉音-客诉", "琉音-好评" }));

	public static readonly AgentEnum BANYUE = new AgentEnum(Create("banyue", "般岳", RareTypeEnum.S, AgentTypeEnum.RUPTURE, DmgTypeEnum.FIRE, new string[] { "banyue" }, new string[2] { "般岳-嗔火", "般岳-山威" }));

	public static readonly AgentEnum ZHAO = new AgentEnum(Create("zhao", "照", RareTypeEnum.S, AgentTypeEnum.DEFENSE, DmgTypeEnum.ICE, new string[] { "zhao" }, new string[] { "照-霜寒值" }));

	public static readonly AgentEnum SUNNA = new AgentEnum(Create("sunna", "千夏", RareTypeEnum.S, AgentTypeEnum.SUPPORT, DmgTypeEnum.PHYSICAL, new string[2] { "sunna", "sunna_afternoon_tea_break" }));

	public static readonly AgentEnum YESHUNGUANG = new AgentEnum(Create("yeshunguang", "叶瞬光", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.PHYSICAL, new string[2] { "yeshunguang", "yeshunguang_touch_of_dawnlight" }, new string[4] { "叶瞬光-明心境", "叶瞬光-常态", "叶瞬光-青溟剑势-红", "叶瞬光-青溟剑势-白" }));

	public static readonly AgentEnum ARIA = new AgentEnum(Create("aria", "爱芮", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.ETHER, new string[2] { "aria", "aria_discordant_note" }, new string[] { "爱芮-应援能量" }));

	public static readonly AgentEnum NANGONGYU = new AgentEnum(Create("nangongyu", "南宫羽", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.ETHER, new string[2] { "nangongyu", "nangongyu_muse" }, new string[] { "南宫羽-重拍" }));

	public static readonly AgentEnum CISSIA = new AgentEnum(Create("cissia", "希希芙", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ELECTRIC, new string[] { "cissia" }));

	public static readonly AgentEnum PROMEIA = new AgentEnum(Create("promeia", "普罗米娅", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.ICE, new string[] { "promeia" }, new string[] { "普罗米娅-霜刑" }));

	public static readonly AgentEnum VELINA = new AgentEnum(Create("velina", "维琳娜", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.WIND, new string[2] { "velina", "velina_shade_of_leisure" }, new string[] { "维琳娜-风华" }));

	public static readonly AgentEnum PYROIS_WISE = new AgentEnum(Create("pyrois_wise", "佩洛伊斯", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.ETHER, new string[] { "pyrois_wise" }, new string[] { "佩洛伊斯-日珥" }));

	public static readonly AgentEnum STARLIGHT_BILLY_KID = new AgentEnum(Create("starlight_billy_kid", "星辉比利", RareTypeEnum.S, AgentTypeEnum.ATTACK, DmgTypeEnum.PHYSICAL, new string[] { "starlight_billy_kid" }, new string[] { "星辉比利-决心" }));

	public static readonly AgentEnum NORMA = new AgentEnum(Create("norma", "诺姆", RareTypeEnum.S, AgentTypeEnum.STUN, DmgTypeEnum.FIRE, new string[] { "norma" }, new string[] { "诺姆-预热" }));

	public static readonly AgentEnum REMIELLE = new AgentEnum(Create("remielle", "蕾米埃尔", RareTypeEnum.S, AgentTypeEnum.ANOMALY, DmgTypeEnum.LUMIFLUX, new string[2] { "remielle", "remielle_dark" }, new string[2] { "蕾米埃尔-浮晖", "蕾米埃尔-虚曜" }));

	public Agent Value { get; }

	public static IReadOnlyList<AgentEnum> Values { get; } = new AgentEnum[57]
	{
		ANBY, ANTON, BEN, BILLY, CORIN, ELLEN, GRACE, KOLEDA, LUCY, LYCAON,
		NEKOMATA, NICOLE, PIPER, RINA, SOLDIER_11, SOUKAKU, ZHU_YUAN, QINGYI, JANE_DOE, SETH_LOWELL,
		CAESAR_KING, BURNICE_WHITE, YANAGI, LIGHTER, ASABA_HARUMASA, HOSHIMI_MIYABI, ASTRA_YAO, EVELYN_CHEVALIER, SOLDIER_0_ANBY, PULCHRA,
		TRIGGER, VIVIAN, HUGO_VLAD, YIXUAN, PANYINHU, JU_FUFU, YUZUHA, ALICE, SEED, ORPHIE,
		LUCIA, MANATO, YIDHARI, DIALYN, BANYUE, ZHAO, SUNNA, YESHUNGUANG, ARIA, NANGONGYU,
		CISSIA, PROMEIA, VELINA, PYROIS_WISE, STARLIGHT_BILLY_KID, NORMA, REMIELLE
	};

	private AgentEnum(Agent value)
	{
		Value = value;
	}

	private static Agent Create(string agentId, string agentName, RareTypeEnum rareType, AgentTypeEnum agentType, DmgTypeEnum dmgType, IReadOnlyList<string> templateIdList, IReadOnlyList<string>? stateNames = null)
	{
		return new Agent(agentId, agentName, rareType, agentType, dmgType, templateIdList, stateNames?.Select((string name) => new AgentStateDef(name)).ToList());
	}
}
