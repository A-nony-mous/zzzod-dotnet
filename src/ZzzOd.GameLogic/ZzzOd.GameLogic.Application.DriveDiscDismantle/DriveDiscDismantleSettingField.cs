namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解设置字段。
/// </summary>
public sealed record DriveDiscDismantleSettingField(string Key, string DisplayName, DriveDiscDismantleSettingType Type, object DefaultValue);
