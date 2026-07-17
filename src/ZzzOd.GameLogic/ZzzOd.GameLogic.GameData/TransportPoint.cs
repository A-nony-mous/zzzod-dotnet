namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 地图传送点配置项。
/// </summary>
public sealed class TransportPoint
{
	public string AreaName { get; }

	public string TpName { get; }

	public string Label => AreaName + " - " + TpName;

	public TransportPoint(string areaName, string tpName)
	{
		AreaName = areaName;
		TpName = tpName;
	}
}
