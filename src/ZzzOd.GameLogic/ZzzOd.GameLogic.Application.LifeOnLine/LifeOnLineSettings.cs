using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线设置字段。
/// </summary>
public static class LifeOnLineSettings
{
	/// <summary>
	/// BaselineParity 侧设置提供器类型。
	/// </summary>
	public const string SettingType = "FLYOUT";

	/// <summary>
	/// 设置字段列表。
	/// </summary>
	public static IReadOnlyList<LifeOnLineSettingField> Fields { get; } = new LifeOnLineSettingField[2]
	{
		new LifeOnLineSettingField("daily_plan_times", "每日计划次数", LifeOnLineSettingType.Integer, 20, "达到次数后结束"),
		new LifeOnLineSettingField("predefined_team_idx", "预备编队", LifeOnLineSettingType.Integer, -1, "-1 代表使用游戏内配队")
	};
}
