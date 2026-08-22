using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.BattleAssistant;

/// <summary>
/// 战斗助手配置元数据。
/// </summary>
public static class BattleAssistantSettings
{
	/// <summary>
		/// 战斗助手配置字段。
	/// </summary>
	public static IReadOnlyList<BattleAssistantSettingField> Fields { get; } = new BattleAssistantSettingField[5]
	{
		new BattleAssistantSettingField("dodge_assistant_config", "闪避配置", BattleAssistantSettingType.Text, "闪避"),
		new BattleAssistantSettingField("screenshot_interval", "截图间隔", BattleAssistantSettingType.Number, 0.02),
		new BattleAssistantSettingField("control_method", "控制方式", BattleAssistantSettingType.Text, "keyboard"),
		new BattleAssistantSettingField("auto_battle_config", "自动战斗配置", BattleAssistantSettingType.Text, "全配队通用"),
		new BattleAssistantSettingField("auto_ultimate_enabled", "自动终结技", BattleAssistantSettingType.Boolean, false)
	};
}
