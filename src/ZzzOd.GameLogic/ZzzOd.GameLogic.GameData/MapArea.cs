using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 地图区域定义。
/// </summary>
public sealed class MapArea
{
	public string AreaName { get; init; } = string.Empty;

	public List<string> TpList { get; init; } = new List<string>();
}
