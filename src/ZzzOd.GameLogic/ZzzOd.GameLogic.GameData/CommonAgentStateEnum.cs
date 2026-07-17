using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

public sealed class CommonAgentStateEnum
{
	public static readonly CommonAgentStateEnum ENERGY_31 = new CommonAgentStateEnum(new AgentStateDef("前台-能量"));

	public static readonly CommonAgentStateEnum ENERGY_32 = new CommonAgentStateEnum(new AgentStateDef("后台-1-能量"));

	public static readonly CommonAgentStateEnum ENERGY_33 = new CommonAgentStateEnum(new AgentStateDef("后台-2-能量"));

	public static readonly CommonAgentStateEnum ENERGY_21 = new CommonAgentStateEnum(new AgentStateDef("前台-能量"));

	public static readonly CommonAgentStateEnum ENERGY_22 = new CommonAgentStateEnum(new AgentStateDef("后台-1-能量"));

	public static readonly CommonAgentStateEnum SPECIAL_31 = new CommonAgentStateEnum(new AgentStateDef("前台-特殊技可用"));

	public static readonly CommonAgentStateEnum SPECIAL_32 = new CommonAgentStateEnum(new AgentStateDef("后台-1-特殊技可用"));

	public static readonly CommonAgentStateEnum SPECIAL_33 = new CommonAgentStateEnum(new AgentStateDef("后台-2-特殊技可用"));

	public static readonly CommonAgentStateEnum SPECIAL_21 = new CommonAgentStateEnum(new AgentStateDef("前台-特殊技可用"));

	public static readonly CommonAgentStateEnum SPECIAL_22 = new CommonAgentStateEnum(new AgentStateDef("后台-1-特殊技可用"));

	public static readonly CommonAgentStateEnum ULTIMATE_31 = new CommonAgentStateEnum(new AgentStateDef("前台-终结技可用"));

	public static readonly CommonAgentStateEnum ULTIMATE_32 = new CommonAgentStateEnum(new AgentStateDef("后台-1-终结技可用"));

	public static readonly CommonAgentStateEnum ULTIMATE_33 = new CommonAgentStateEnum(new AgentStateDef("后台-2-终结技可用"));

	public static readonly CommonAgentStateEnum ULTIMATE_21 = new CommonAgentStateEnum(new AgentStateDef("前台-终结技可用"));

	public static readonly CommonAgentStateEnum ULTIMATE_22 = new CommonAgentStateEnum(new AgentStateDef("后台-1-终结技可用"));

	public static readonly CommonAgentStateEnum LIFE_DEDUCTION_31 = new CommonAgentStateEnum(new AgentStateDef("前台-血量扣减"));

	public static readonly CommonAgentStateEnum LIFE_DEDUCTION_21 = new CommonAgentStateEnum(new AgentStateDef("前台-血量扣减"));

	public static readonly CommonAgentStateEnum GUARD_BREAK = new CommonAgentStateEnum(new AgentStateDef("格挡-破碎"));

	public static readonly CommonAgentStateEnum SWITCH_BAN = new CommonAgentStateEnum(new AgentStateDef("切人-冷却"));

	public AgentStateDef Value { get; }

	public static IReadOnlyList<CommonAgentStateEnum> Values { get; } = new CommonAgentStateEnum[19]
	{
		ENERGY_31, ENERGY_32, ENERGY_33, ENERGY_21, ENERGY_22, SPECIAL_31, SPECIAL_32, SPECIAL_33, SPECIAL_21, SPECIAL_22,
		ULTIMATE_31, ULTIMATE_32, ULTIMATE_33, ULTIMATE_21, ULTIMATE_22, LIFE_DEDUCTION_31, LIFE_DEDUCTION_21, GUARD_BREAK, SWITCH_BAN
	};

	private CommonAgentStateEnum(AgentStateDef value)
	{
		Value = value;
	}
}
