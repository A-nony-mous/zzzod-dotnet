namespace ZzzOd.GameLogic.GameData;

public static class DmgTypeEnumExtensions
{
	public static string GetStringValue(this DmgTypeEnum type)
	{
		if (1 == 0)
		{
		}
		string result = type switch
		{
			DmgTypeEnum.ELECTRIC => "电属性", 
			DmgTypeEnum.ETHER => "以太属性", 
			DmgTypeEnum.PHYSICAL => "物理属性", 
			DmgTypeEnum.FIRE => "火属性", 
			DmgTypeEnum.ICE => "冰属性", 
			DmgTypeEnum.WIND => "风属性", 
			_ => "未知", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
