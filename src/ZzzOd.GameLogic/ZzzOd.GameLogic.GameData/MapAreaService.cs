using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Runtime;
using OneDragon.Core.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.GameLogic.Const;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 地图区域数据服务。
/// </summary>
public sealed class MapAreaService
{
	private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

	private readonly OneDragonEnvironment _environment;

	public string DataFilePath => Path.Combine(GameConst.GetGameDataPath(_environment), "map_area.yml");

	public bool DataFileExists { get; private set; }

	public DateTimeOffset LastReloadedAt { get; private set; }

	public IReadOnlyList<MapArea> AreaList { get; private set; } = Array.Empty<MapArea>();

	public IReadOnlyDictionary<string, MapArea> AreaNameMap { get; private set; } = new Dictionary<string, MapArea>();

	public MapAreaService(OneDragonEnvironment environment)
	{
		_environment = environment;
		Reload();
	}

	public void Reload()
	{
		DataFileExists = File.Exists(DataFilePath);
		LastReloadedAt = DateTimeOffset.UtcNow;
		if (!DataFileExists)
		{
			AreaList = Array.Empty<MapArea>();
			AreaNameMap = new Dictionary<string, MapArea>();
			return;
		}
		using StreamReader input = new StreamReader(DataFilePath);
		List<MapArea> list = Deserializer.Deserialize<List<MapArea>>(input);
		AreaList = list ?? new List<MapArea>();
		AreaNameMap = AreaList.Where((MapArea area) => !string.IsNullOrWhiteSpace(area.AreaName)).ToDictionary<MapArea, string>((MapArea area) => area.AreaName, StringComparer.Ordinal);
	}

	public MapArea? GetBestMatchArea(string ocrResult)
	{
		if (string.IsNullOrWhiteSpace(ocrResult) || AreaList.Count == 0)
		{
			return null;
		}
		List<string> targetTexts = AreaList.Select((MapArea area) => area.AreaName).ToList();
		int? num = StringUtils.FindBestMatchByDifflib(ocrResult, targetTexts);
		return num.HasValue ? AreaList[num.Value] : null;
	}

	public int GetDirectionToTargetArea(MapArea currentArea, MapArea targetArea)
	{
		int num = AreaList.ToList().IndexOf(currentArea);
		int num2 = AreaList.ToList().IndexOf(targetArea);
		if (num < 0 || num2 < 0)
		{
			return 0;
		}
		int num3 = num2 - num;
		int count = AreaList.Count;
		if (num3 > 0 && count - num3 < num3)
		{
			num3 = -(count - num3);
		}
		else if (num3 < 0 && count + num3 < -num3)
		{
			num3 = count + num3;
		}
		return num3;
	}

	public string? GetBestMatchTp(string areaName, string ocrResult)
	{
		if (!AreaNameMap.TryGetValue(areaName, out MapArea value) || string.IsNullOrWhiteSpace(ocrResult))
		{
			return null;
		}
		int? num = StringUtils.FindBestMatchByDifflib(ocrResult, value.TpList);
		return num.HasValue ? value.TpList[num.Value] : null;
	}
}
