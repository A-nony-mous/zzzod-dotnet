using System.ComponentModel;
using System.Reflection;

namespace ZzzOd.GameLogic.AutoBattle;

public static class YoloStateEventEnumExtensions
{
	public static string GetDescription(this YoloStateEventEnum value)
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
