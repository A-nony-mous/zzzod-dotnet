using System;
using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡店传送点。
/// </summary>
public sealed record CoffeeTransportPoint(string Value, string AreaName, string TransportPointName)
{
	/// <summary>六分街咖啡店。</summary>
	public static CoffeeTransportPoint SixthStreet { get; } = new CoffeeTransportPoint("六分街 - 咖啡店", "六分街", "咖啡店");

	/// <summary>澄辉坪汀曼咖啡。</summary>
	public static CoffeeTransportPoint FailumeHeights { get; } = new CoffeeTransportPoint("澄辉坪 - 汀曼咖啡", "澄辉坪", "汀曼咖啡");

	/// <summary>布亚斯特城区片刻闲。</summary>
	public static CoffeeTransportPoint Buyaste { get; } = new CoffeeTransportPoint("布亚斯特城区 - 片刻闲", "布亚斯特城区", "片刻闲");

	/// <summary>
	/// 所有传送点。
	/// </summary>
	public static IReadOnlyList<CoffeeTransportPoint> All { get; } = new CoffeeTransportPoint[3] { SixthStreet, FailumeHeights, Buyaste };

	/// <summary>
	/// 按配置值解析。
	/// </summary>
	public static bool TryFromValue(string? value, out CoffeeTransportPoint? point)
	{
		string text = value;
		CoffeeTransportPoint coffeeTransportPoint = text switch
		{
			"咖啡店" => SixthStreet,
			"汀曼咖啡" => FailumeHeights,
			"片刻闲" => Buyaste,
			_ => All.FirstOrDefault((CoffeeTransportPoint item) => string.Equals(item.Value, value, StringComparison.Ordinal))
		};
		point = coffeeTransportPoint;
		return (object)point != null;
	}

	/// <summary>
	/// 按配置值解析。
	/// </summary>
	public static CoffeeTransportPoint FromValue(string? value)
	{
		if (!TryFromValue(value, out CoffeeTransportPoint point))
		{
			throw new ArgumentOutOfRangeException("value", value, "无效咖啡传送点");
		}
		return point;
	}
}
