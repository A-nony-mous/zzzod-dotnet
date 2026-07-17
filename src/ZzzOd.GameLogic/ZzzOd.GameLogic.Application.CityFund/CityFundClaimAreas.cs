using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.CityFund;

internal static class CityFundClaimAreas
{
	public static IReadOnlyList<(string ScreenName, string AreaName)> LevelClaimAreas { get; } = new(string, string)[2]
	{
		("丽都城募", "等级-全部领取"),
		("丽都城募", "按钮-确认")
	};
}
