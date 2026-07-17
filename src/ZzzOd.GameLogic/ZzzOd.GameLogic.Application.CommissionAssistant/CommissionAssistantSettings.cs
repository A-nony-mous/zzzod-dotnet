using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手设置元数据。
/// </summary>
public static class CommissionAssistantSettings
{
	/// <summary>BaselineParity 侧设置提供器类型。</summary>
	public const string SettingType = "FLYOUT";

	/// <summary>设置字段列表。</summary>
	public static IReadOnlyList<CommissionAssistantSettingField> Fields { get; } = new CommissionAssistantSettingField[9]
	{
		new CommissionAssistantSettingField("pause_in_background", "后台暂停", CommissionAssistantSettingType.Bool, true, "游戏窗口不在前台时暂停点击"),
		new CommissionAssistantSettingField("dialog_click_interval", "对话点击间隔", CommissionAssistantSettingType.Number, 0.5, "普通对话点击后的等待秒数"),
		new CommissionAssistantSettingField("story_mode", "剧情模式", CommissionAssistantSettingType.Enum, CommissionAssistantStoryMode.Click.Value, "剧情播放处理方式", CommissionAssistantStoryMode.Options),
		new CommissionAssistantSettingField("dialog_option", "对话选项", CommissionAssistantSettingType.Enum, CommissionAssistantDialogOption.Last.Value, "同屏多个选项时选择第一个或最后一个", CommissionAssistantDialogOption.Options),
		new CommissionAssistantSettingField("dodge_config", "闪避配置", CommissionAssistantSettingType.Text, "闪避", "切换到闪避模式时加载的指令"),
		new CommissionAssistantSettingField("dodge_switch", "闪避热键", CommissionAssistantSettingType.Text, "5", "按下该键切换闪避模式"),
		new CommissionAssistantSettingField("auto_battle", "自动战斗配置", CommissionAssistantSettingType.Text, "全配队通用", "切换到自动战斗模式时加载的指令"),
		new CommissionAssistantSettingField("auto_battle_switch", "自动战斗热键", CommissionAssistantSettingType.Text, "6", "按下该键切换自动战斗模式"),
		new CommissionAssistantSettingField("sleep_after_empty_screen", "未知画面等待", CommissionAssistantSettingType.Number, 0.5, "未知画面后再次检测的等待秒数")
	};
}
