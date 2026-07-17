using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业设置元数据。
/// </summary>
public static class RandomPlaySettings
{
	/// <summary>BaselineParity 侧设置提供器类型。</summary>
	public const string SettingType = "FLYOUT";

	/// <summary>设置字段列表。</summary>
	public static IReadOnlyList<RandomPlaySettingField> Fields { get; } = new RandomPlaySettingField[3]
	{
		new RandomPlaySettingField("transport_point", "传送点", RandomPlaySettingType.Enum, RandomPlayTransportPoint.VideoStoreCounter.Value, "进入录像店营业页面的位置", RandomPlayTransportPoint.Options),
		new RandomPlaySettingField("agent_name_1", "宣传员 1", RandomPlaySettingType.Text, "随机", "随机时按日期选择位置"),
		new RandomPlaySettingField("agent_name_2", "宣传员 2", RandomPlaySettingType.Text, "随机", "随机时按日期选择位置")
	};
}
