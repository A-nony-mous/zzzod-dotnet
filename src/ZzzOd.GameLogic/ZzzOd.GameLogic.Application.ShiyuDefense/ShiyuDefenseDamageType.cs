using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战属性名称转换。
/// </summary>
public static class ShiyuDefenseDamageType
{
	/// <summary>
	/// 解析 BaselineParity 配置中的属性名称。
	/// </summary>
	public static DmgTypeEnum Parse(string? value)
	{
		if (1 == 0)
		{
		}
		DmgTypeEnum result;
		switch (value)
		{
		case "ELECTRIC":
		case "电属性":
			result = DmgTypeEnum.ELECTRIC;
			break;
		case "ETHER":
		case "以太属性":
			result = DmgTypeEnum.ETHER;
			break;
		case "PHYSICAL":
		case "物理属性":
			result = DmgTypeEnum.PHYSICAL;
			break;
		case "FIRE":
		case "火属性":
			result = DmgTypeEnum.FIRE;
			break;
		case "ICE":
		case "冰属性":
			result = DmgTypeEnum.ICE;
			break;
		case "WIND":
		case "风属性":
			result = DmgTypeEnum.WIND;
			break;
		default:
			result = DmgTypeEnum.UNKNOWN;
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	/// <summary>
	/// 转为 BaselineParity 枚举名称。
	/// </summary>
	public static string ToBaselineName(DmgTypeEnum value)
	{
		if (1 == 0)
		{
		}
		string result = value switch
		{
			DmgTypeEnum.ELECTRIC => "ELECTRIC", 
			DmgTypeEnum.ETHER => "ETHER", 
			DmgTypeEnum.PHYSICAL => "PHYSICAL", 
			DmgTypeEnum.FIRE => "FIRE", 
			DmgTypeEnum.ICE => "ICE", 
			DmgTypeEnum.WIND => "WIND", 
			_ => "UNKNOWN", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
