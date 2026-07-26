using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业传送点。
/// </summary>
public sealed record RandomPlayTransportPoint(string Value, string AreaName, string TransportPointName)
{
	/// <summary>录像店柜台。</summary>
	public static RandomPlayTransportPoint VideoStoreCounter { get; } = new RandomPlayTransportPoint("录像店 - 柜台", "录像店", "柜台");

	/// <summary>澄辉坪录像店营业点。</summary>
	public static RandomPlayTransportPoint FailumeHeightsBusinessPoint { get; } = new RandomPlayTransportPoint("澄辉坪 - 录像店营业点", "澄辉坪", "录像店营业点");

	/// <summary>所有可选传送点。</summary>
	public static IReadOnlyList<RandomPlayTransportPoint> All { get; } = new RandomPlayTransportPoint[2] { VideoStoreCounter, FailumeHeightsBusinessPoint };

	/// <summary>设置选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[2]
	{
		new ConfigItem(VideoStoreCounter.Value),
		new ConfigItem(FailumeHeightsBusinessPoint.Value)
	};

	/// <summary>
	/// 按配置值尝试解析传送点。旧版本曾用仅传送点名的短形式作为值，这里继续兼容解析，
	/// 保证历史配置文件中残留的旧值仍能被识别。
	/// </summary>
	public static bool TryFromValue(string? value, out RandomPlayTransportPoint? point)
	{
		RandomPlayTransportPoint? legacy = value switch
		{
			"柜台" => VideoStoreCounter,
			"录像店营业点" => FailumeHeightsBusinessPoint,
			_ => null,
		};
		point = legacy ?? All.FirstOrDefault((RandomPlayTransportPoint item) => string.Equals(item.Value, value, StringComparison.Ordinal));
		return (object)point != null;
	}

	/// <summary>
	/// 按配置值解析传送点。
	/// </summary>
	public static RandomPlayTransportPoint FromValue(string? value)
	{
		if (!TryFromValue(value, out RandomPlayTransportPoint point))
		{
			throw new ArgumentOutOfRangeException("value", value, "无效录像店营业传送点");
		}
		return point;
	}
}
