using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板设置字段。
/// </summary>
public static class IntelBoardSettings
{
	/// <summary>
	/// BaselineParity 侧设置提供器类型。
	/// </summary>
	public const string SettingType = "FLYOUT";

	/// <summary>
	/// 设置字段列表。
	/// </summary>
	public static IReadOnlyList<IntelBoardSettingField> Fields { get; } = new IntelBoardSettingField[3]
	{
		new IntelBoardSettingField("predefined_team_idx", "预备编队下标", IntelBoardSettingType.Integer, -1, "-1 代表不选择预备编队"),
		new IntelBoardSettingField("auto_battle_config", "自动战斗配置名称", IntelBoardSettingType.Text, "全配队通用", "未选择预备编队时使用"),
		new IntelBoardSettingField("exp_grind_mode", "刷满经验模式", IntelBoardSettingType.Boolean, false, "开启后按累计经验判断本周完成")
	};
}
