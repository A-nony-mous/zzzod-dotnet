using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地设置元数据。
/// </summary>
public static class LostVoidSettings
{
	/// <summary>BaselineParity 设置提供器类型。</summary>
	public const string SettingType = "INTERFACE";

	/// <summary>字段列表。</summary>
	public static IReadOnlyList<LostVoidSettingField> Fields { get; } = new LostVoidSettingField[5]
	{
		new LostVoidSettingField("daily_plan_times", "每日计划次数", LostVoidSettingType.Integer, 5),
		new LostVoidSettingField("weekly_plan_times", "每周计划次数", LostVoidSettingType.Integer, 2),
		new LostVoidSettingField("extra_task", "额外任务", LostVoidSettingType.Enum, "完成悬赏委托", LostVoidTask.Options),
		new LostVoidSettingField("mission_name", "副本", LostVoidSettingType.String, "战线肃清"),
		new LostVoidSettingField("challenge_config", "挑战配置", LostVoidSettingType.String, "默认-成就模式")
	};
}
