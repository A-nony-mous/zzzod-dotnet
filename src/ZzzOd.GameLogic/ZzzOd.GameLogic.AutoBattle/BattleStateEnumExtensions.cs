using System.ComponentModel;
using System.Reflection;

namespace ZzzOd.GameLogic.AutoBattle;

/// <summary>
/// 自动战斗状态枚举扩展
/// </summary>
public static class BattleStateEnumExtensions
{
	/// <summary>
	/// 获取枚举的描述值 (等同于 BaselineParity 的 .value)
	/// </summary>
	public static string GetDescription(this BattleStateEnum value)
	{
		FieldInfo field = value.GetType().GetField(value.ToString());
		if (field == null)
		{
			return value.ToString();
		}
		DescriptionAttribute customAttribute = field.GetCustomAttribute<DescriptionAttribute>();
		return (customAttribute == null) ? value.ToString() : customAttribute.Description;
	}
}
