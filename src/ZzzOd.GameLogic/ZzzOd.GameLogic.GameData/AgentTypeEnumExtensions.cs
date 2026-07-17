namespace ZzzOd.GameLogic.GameData;

public static class AgentTypeEnumExtensions
{
	public static string GetStringValue(this AgentTypeEnum type)
	{
		if (1 == 0)
		{
		}
		string result = type switch
		{
			AgentTypeEnum.ATTACK => "强攻", 
			AgentTypeEnum.STUN => "击破", 
			AgentTypeEnum.SUPPORT => "支援", 
			AgentTypeEnum.DEFENSE => "防护", 
			AgentTypeEnum.ANOMALY => "异常", 
			AgentTypeEnum.RUPTURE => "命破", 
			_ => "未知", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
