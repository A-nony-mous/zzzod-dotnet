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

	/// <summary>
	/// 所有传送点。
	/// </summary>
	public static IReadOnlyList<CoffeeTransportPoint> All { get; } = new CoffeeTransportPoint[2] { SixthStreet, FailumeHeights };

	/// <summary>
	/// 按配置值解析。
	/// </summary>
	public static bool TryFromValue(string? value, out CoffeeTransportPoint? point)
	{
		if (1 == 0)
		{
		}
		string text = value;
		CoffeeTransportPoint coffeeTransportPoint = ((text == "咖啡店") ? SixthStreet : ((!(text == "汀曼咖啡")) ? All.FirstOrDefault((CoffeeTransportPoint item) => string.Equals(item.Value, value, StringComparison.Ordinal)) : FailumeHeights));
		if (1 == 0)
		{
		}
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
