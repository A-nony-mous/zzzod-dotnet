namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线设置字段元数据。
/// </summary>
public sealed record LifeOnLineSettingField(string Key, string DisplayName, LifeOnLineSettingType Type, object DefaultValue, string Description);
