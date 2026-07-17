namespace ZzzOd.GameLogic.Application.BattleAssistant;

/// <summary>
/// 战斗助手设置字段。
/// </summary>
public sealed record BattleAssistantSettingField(string Key, string DisplayName, BattleAssistantSettingType Type, object DefaultValue);
