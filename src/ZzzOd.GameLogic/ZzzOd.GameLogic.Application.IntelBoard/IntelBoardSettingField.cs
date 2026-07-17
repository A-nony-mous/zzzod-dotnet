namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板设置字段元数据。
/// </summary>
public sealed record IntelBoardSettingField(string Key, string DisplayName, IntelBoardSettingType Type, object DefaultValue, string Description);
