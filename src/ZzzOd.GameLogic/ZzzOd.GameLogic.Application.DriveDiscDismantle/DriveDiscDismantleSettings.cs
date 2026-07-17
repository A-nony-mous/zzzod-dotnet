using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解设置元数据。
/// </summary>
public static class DriveDiscDismantleSettings
{
	/// <summary>BaselineParity 设置提供器类型。</summary>
	public const string SettingType = "FLYOUT";

	/// <summary>字段列表。</summary>
	public static IReadOnlyList<DriveDiscDismantleSettingField> Fields { get; } = new DriveDiscDismantleSettingField[2]
	{
		new DriveDiscDismantleSettingField("dismantle_level", "拆解等级", DriveDiscDismantleSettingType.Enum, "A及以下"),
		new DriveDiscDismantleSettingField("dismantle_abandon", "全选已弃置", DriveDiscDismantleSettingType.Boolean, false)
	};
}
