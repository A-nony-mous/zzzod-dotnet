using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.GameLogic.Const;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 手册数据服务。
/// </summary>
public sealed class CompendiumService
{
	private sealed class CoffeeRootDocument
	{
		public List<CoffeeDocument> CoffeeList { get; init; } = new List<CoffeeDocument>();

		public List<CoffeeScheduleDocument> Schedule { get; init; } = new List<CoffeeScheduleDocument>();
	}

	private sealed class CoffeeDocument
	{
		public string CoffeeName { get; init; } = string.Empty;

		public string? TabName { get; init; }

		public string? CategoryName { get; init; }

		public string? MissionTypeName { get; init; }

		public string? MissionName { get; init; }

		public bool Extra { get; init; }
	}

	private sealed class CoffeeScheduleDocument
	{
		public List<int> Days { get; init; } = new List<int>();

		public List<string> CoffeeList { get; init; } = new List<string>();
	}

	private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

	private readonly OneDragonEnvironment _environment;

	public string CompendiumDataFilePath => Path.Combine(GameConst.GetGameDataPath(_environment), "compendium_data.yml");

	public string CoffeeDataFilePath => Path.Combine(GameConst.GetGameDataPath(_environment), "coffee_data.yml");

	public bool CompendiumDataFileExists { get; private set; }

	public bool CoffeeDataFileExists { get; private set; }

	public DateTimeOffset LastReloadedAt { get; private set; }

	public CompendiumData Data { get; private set; } = new CompendiumData();

	public IReadOnlyList<Coffee> CoffeeList { get; private set; } = Array.Empty<Coffee>();

	public IReadOnlyDictionary<string, Coffee> NameToCoffee { get; private set; } = new Dictionary<string, Coffee>();

	public IReadOnlyDictionary<int, IReadOnlyList<Coffee>> CoffeeSchedule { get; private set; } = new Dictionary<int, IReadOnlyList<Coffee>>();

	public CompendiumService(OneDragonEnvironment environment)
	{
		_environment = environment;
		Reload();
	}

	public void Reload()
	{
		CompendiumDataFileExists = File.Exists(CompendiumDataFilePath);
		CoffeeDataFileExists = File.Exists(CoffeeDataFilePath);
		LoadCompendium();
		LoadCoffee();
		LastReloadedAt = DateTimeOffset.UtcNow;
	}

	public CompendiumTab? GetTabData(string? tabName)
	{
		return Data.TabList.FirstOrDefault((CompendiumTab tab) => string.Equals(tab.TabName, tabName, StringComparison.Ordinal));
	}

	public List<CompendiumCategory> GetCategoryListData(string tabName)
	{
		return GetTabData(tabName)?.CategoryList ?? new List<CompendiumCategory>();
	}

	public CompendiumCategory? GetCategoryData(string tabName, string categoryName)
	{
		return GetCategoryListData(tabName).FirstOrDefault((CompendiumCategory category) => string.Equals(category.CategoryName, categoryName, StringComparison.Ordinal));
	}

	public List<CompendiumMissionType> GetMissionTypeListData(string tabName, string categoryName)
	{
		return GetCategoryData(tabName, categoryName)?.MissionTypeList ?? new List<CompendiumMissionType>();
	}

	public CompendiumMissionType? GetMissionTypeData(string tabName, string categoryName, string missionTypeName)
	{
		return GetMissionTypeListData(tabName, categoryName).FirstOrDefault((CompendiumMissionType missionType) => string.Equals(missionType.MissionTypeName, missionTypeName, StringComparison.Ordinal));
	}

	public List<CompendiumMission> GetMissionListData(string tabName, string categoryName, string missionTypeName)
	{
		return GetMissionTypeData(tabName, categoryName, missionTypeName)?.MissionList ?? new List<CompendiumMission>();
	}

	public CompendiumMission? GetMissionData(string tabName, string categoryName, string missionTypeName, string missionName)
	{
		return GetMissionListData(tabName, categoryName, missionTypeName).FirstOrDefault((CompendiumMission mission) => string.Equals(mission.MissionName, missionName, StringComparison.Ordinal));
	}

	public List<ConfigItem> GetChargePlanCategoryList()
	{
		return GetCategoryListData("训练").Select(delegate(CompendiumCategory category)
		{
			string label = ((category.CategoryName == "恶名狩猎") ? (category.CategoryName + " 深度追猎") : category.CategoryName);
			return new ConfigItem(label, category.CategoryName);
		}).ToList();
	}

	public List<ConfigItem> GetChargePlanMissionTypeList(string categoryName)
	{
		return (from missionType in GetMissionTypeListData("训练", categoryName)
			select new ConfigItem(missionType.DisplayName, missionType.MissionTypeName)).ToList();
	}

	public List<ConfigItem> GetChargePlanMissionList(string categoryName, string missionType)
	{
		return (from mission in GetMissionListData("训练", categoryName, missionType)
			select new ConfigItem(mission.DisplayName, mission.MissionName)).ToList();
	}

	public List<CompendiumMissionType>? GetSameCategoryMissionTypeList(string missionTypeName)
	{
		foreach (CompendiumTab tab in Data.TabList)
		{
			foreach (CompendiumCategory category in tab.CategoryList)
			{
				CompendiumMissionType compendiumMissionType = category.MissionTypeList.FirstOrDefault((CompendiumMissionType item) => item.MissionTypeName == missionTypeName);
				if (compendiumMissionType != null)
				{
					return category.MissionTypeList;
				}
			}
		}
		return null;
	}

	public List<ConfigItem> GetNotoriousHuntPlanMissionTypeList(string categoryName)
	{
		return (from missionType in GetMissionTypeListData("训练", categoryName)
			select new ConfigItem(missionType.DisplayName, missionType.MissionTypeName)).ToList();
	}

	public List<string> GetHollowZeroMissionNameList()
	{
		return (from mission in (from missionType in GetMissionTypeListData("作战", "零号空洞")
				where missionType.MissionTypeName != "迷失之地"
				select missionType).SelectMany((CompendiumMissionType missionType) => missionType.MissionList)
			select mission.MissionName).ToList();
	}

	public List<ConfigItem> GetCoffeeConfigListByDay(int day)
	{
		IReadOnlyList<Coffee> value;
		return CoffeeSchedule.TryGetValue(day, out value) ? value.Select((Coffee coffee) => new ConfigItem(coffee.DisplayName, coffee.CoffeeName)).ToList() : new List<ConfigItem>();
	}

	public List<Coffee> GetExtraCoffeeList()
	{
		return CoffeeList.Where((Coffee coffee) => coffee.Extra).ToList();
	}

	public List<string> GetLostVoidMissionNameList()
	{
		return (from mission in GetMissionListData("作战", "零号空洞", "迷失之地")
			select mission.DisplayName).ToList();
	}

	private void LoadCompendium()
	{
		if (!CompendiumDataFileExists)
		{
			Data = new CompendiumData();
			return;
		}
		using StreamReader input = new StreamReader(CompendiumDataFilePath);
		List<CompendiumTab> list = Deserializer.Deserialize<List<CompendiumTab>>(input);
		Data = new CompendiumData
		{
			TabList = (list ?? new List<CompendiumTab>())
		};
		Data.AttachGraph();
	}

	private void LoadCoffee()
	{
		if (!CoffeeDataFileExists)
		{
			CoffeeList = Array.Empty<Coffee>();
			NameToCoffee = new Dictionary<string, Coffee>();
			CoffeeSchedule = new Dictionary<int, IReadOnlyList<Coffee>>();
			return;
		}
		using StreamReader input = new StreamReader(CoffeeDataFilePath);
		CoffeeRootDocument coffeeRootDocument = Deserializer.Deserialize<CoffeeRootDocument>(input);
		List<Coffee> list = new List<Coffee>();
		Dictionary<string, Coffee> nameToCoffee = new Dictionary<string, Coffee>(StringComparer.Ordinal);
		foreach (CoffeeDocument item in coffeeRootDocument?.CoffeeList ?? new List<CoffeeDocument>())
		{
			Coffee coffee = ConstructCoffee(item);
			list.Add(coffee);
			nameToCoffee[coffee.CoffeeName] = coffee;
		}
		Dictionary<int, IReadOnlyList<Coffee>> dictionary = new Dictionary<int, IReadOnlyList<Coffee>>();
		foreach (CoffeeScheduleDocument item2 in coffeeRootDocument?.Schedule ?? new List<CoffeeScheduleDocument>())
		{
			List<Coffee> value = (from name in item2.CoffeeList
				select nameToCoffee.GetValueOrDefault(name) into coffee2
				where coffee2 != null
				select coffee2).Cast<Coffee>().ToList();
			foreach (int day in item2.Days)
			{
				dictionary[day] = value;
			}
		}
		CoffeeList = list;
		NameToCoffee = nameToCoffee;
		CoffeeSchedule = dictionary;
	}

	private Coffee ConstructCoffee(CoffeeDocument document)
	{
		CompendiumTab tab = ((document.TabName == null) ? null : GetTabData(document.TabName));
		CompendiumCategory category = ((document.TabName == null || document.CategoryName == null) ? null : GetCategoryData(document.TabName, document.CategoryName));
		CompendiumMissionType missionType = ((document.TabName == null || document.CategoryName == null || document.MissionTypeName == null) ? null : GetMissionTypeData(document.TabName, document.CategoryName, document.MissionTypeName));
		CompendiumMission mission = ((document.TabName == null || document.CategoryName == null || document.MissionTypeName == null || document.MissionName == null) ? null : GetMissionData(document.TabName, document.CategoryName, document.MissionTypeName, document.MissionName));
		return new Coffee
		{
			CoffeeName = document.CoffeeName,
			Tab = tab,
			Category = category,
			MissionType = missionType,
			Mission = mission,
			Extra = document.Extra
		};
	}
}
