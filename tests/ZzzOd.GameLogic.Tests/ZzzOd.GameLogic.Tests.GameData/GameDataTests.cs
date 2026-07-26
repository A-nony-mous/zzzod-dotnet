using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Tests.GameData;

public sealed class GameDataTests : IDisposable
{
	private readonly string _rootDirectory;

	public GameDataTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data"));
	}

	[Fact]
	public void MapAreaService_Reload_LoadsAreasAndSupportsMatching()
	{
		File.WriteAllText(Path.Combine(_rootDirectory, "assets", "game_data", "map_area.yml"), "- area_name: \"六分街\"\n  tp_list:\n    - \"录像店\"\n    - \"咖啡店\"\n- area_name: \"澄辉坪\"\n  tp_list:\n    - \"随便观\"\n    - \"汀曼咖啡\"\n- area_name: \"芭莱大厦前\"\n  tp_list:\n    - \"喵吉长官\"");
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		MapAreaService mapAreaService = new MapAreaService(environment);
		Assert.True(mapAreaService.DataFileExists);
		Assert.EndsWith(Path.Combine("assets", "game_data", "map_area.yml"), mapAreaService.DataFilePath);
		Assert.Equal(3, mapAreaService.AreaList.Count);
		Assert.Equal("澄辉坪", mapAreaService.GetBestMatchArea("澄辉平")?.AreaName);
		Assert.Equal(-1, mapAreaService.GetDirectionToTargetArea(mapAreaService.AreaList[0], mapAreaService.AreaList[2]));
		Assert.Equal("汀曼咖啡", mapAreaService.GetBestMatchTp("澄辉坪", "汀曼咖啡"));
		// 字序颠倒仍属同一名称：按基准的相似度语义可命中，按编辑距离则会漏
		Assert.Equal("六分街", mapAreaService.GetBestMatchArea("街六分")?.AreaName);
		Assert.Equal("随便观", mapAreaService.GetBestMatchTp("澄辉坪", "随观便"));
		// 与目标名称无共同字时不得兜底命中
		Assert.Null(mapAreaService.GetBestMatchArea("录像"));
		Assert.Null(mapAreaService.GetBestMatchTp("澄辉坪", "咖啡汀曼"));
	}

	[Fact]
	public void CompendiumService_Reload_LoadsHierarchyAndCoffeeSchedule()
	{
		File.WriteAllText(Path.Combine(_rootDirectory, "assets", "game_data", "compendium_data.yml"), "- tab_name: \"训练\"\n  category_list:\n    - category_name: \"实战模拟室\"\n      mission_type_list:\n        - mission_type_name: \"基础材料\"\n          mission_list:\n            - mission_name: \"调查专项\"\n              mission_name_display: \"代理人经验\"\n        - mission_type_name: \"代理人技能\"\n          mission_list:\n            - mission_name: \"升温测试\"\n              mission_name_display: \"火属性\"\n- tab_name: \"作战\"\n  category_list:\n    - category_name: \"零号空洞\"\n      mission_type_list:\n        - mission_type_name: \"迷失之地\"\n          mission_list:\n            - mission_name: \"旧都列车\"\n              mission_name_display: \"旧都列车\"\n        - mission_type_name: \"剧变节点\"\n          mission_list:\n            - mission_name: \"内部\"\n              mission_name_display: \"内部\"");
		File.WriteAllText(Path.Combine(_rootDirectory, "assets", "game_data", "coffee_data.yml"), "coffee_list:\n  - coffee_name: \"新艾利都特调\"\n  - coffee_name: \"麦草拿提\"\n    tab_name: \"训练\"\n    category_name: \"实战模拟室\"\n    mission_type_name: \"代理人技能\"\n    mission_name: \"升温测试\"\n  - coffee_name: \"沙罗特调\"\n    extra: true\nschedule:\n  - days: [1, 3]\n    coffee_list: [\"麦草拿提\"]");
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		CompendiumService compendiumService = new CompendiumService(environment);
		Assert.True(compendiumService.CompendiumDataFileExists);
		Assert.True(compendiumService.CoffeeDataFileExists);
		Assert.EndsWith(Path.Combine("assets", "game_data", "compendium_data.yml"), compendiumService.CompendiumDataFilePath);
		Assert.EndsWith(Path.Combine("assets", "game_data", "coffee_data.yml"), compendiumService.CoffeeDataFilePath);
		Assert.Equal("实战模拟室", compendiumService.GetCategoryData("训练", "实战模拟室")?.CategoryName);
		Assert.Equal("代理人技能", compendiumService.GetMissionTypeData("训练", "实战模拟室", "代理人技能")?.MissionTypeName);
		Assert.Equal("火属性", compendiumService.GetMissionData("训练", "实战模拟室", "代理人技能", "升温测试")?.MissionNameDisplay);
		Assert.Equal("麦草拿提", Assert.Single(compendiumService.GetCoffeeConfigListByDay(1)).Value);
		Assert.Equal("代理人技能 - 火属性", compendiumService.NameToCoffee["麦草拿提"].DisplayName);
		Assert.Single(compendiumService.GetExtraCoffeeList());
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = "内部";
		Assert.Equal<List<string>>(list, compendiumService.GetHollowZeroMissionNameList());
		num = 1;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = "旧都列车";
		Assert.Equal<List<string>>(list2, compendiumService.GetLostVoidMissionNameList());
	}

	[Fact]
	public void TargetState_ExposesPythonDetectionTaskDefinitions()
	{
		Assert.Equal(3, TargetState.DetectionTasks.Count);
		DetectionTask detectionTask = TargetState.DetectionTasks[0];
		Assert.Equal("lock_on", detectionTask.TaskId);
		Assert.Equal("lock-far", detectionTask.PipelineName);
		Assert.Equal("目标-近距离锁定", Assert.Single(detectionTask.StateDefinitions).StateName);
		Assert.Equal(TargetCheckWay.ContourCountInRange, Assert.Single(detectionTask.StateDefinitions).CheckWay);
		DetectionTask detectionTask2 = TargetState.DetectionTasks[1];
		Assert.Equal("abnormal_statuses", detectionTask2.TaskId);
		Assert.False(detectionTask2.Enabled);
		Assert.True(detectionTask2.IsAsync);
		Assert.Contains((IEnumerable<TargetStateDef>)detectionTask2.StateDefinitions, (Predicate<TargetStateDef>)((TargetStateDef def) => def.StateName == "目标-异常-灼烧"));
		DetectionTask detectionTask3 = TargetState.DetectionTasks[2];
		Assert.Equal("boss_stun_by_length", detectionTask3.TaskId);
		Assert.False(detectionTask3.Enabled);
		Assert.True(detectionTask3.IsAsync);
		Assert.Equal(TargetCheckWay.MapContourLengthToPercent, Assert.Single(detectionTask3.StateDefinitions).CheckWay);
		Assert.True(Assert.Single(detectionTask3.StateDefinitions).ClearOnMiss);
	}

	[Fact]
	public void AgentRegistry_ContainsExpandedPythonAgentCatalog()
	{
		Assert.True(AgentEnum.Values.Count >= 50);
		Assert.Equal("以太属性", DmgTypeEnum.ETHER.GetStringValue());
		Assert.Equal("支援", AgentEnum.ASTRA_YAO.Value.AgentTypeStr);
		Assert.Contains("astra_yao_chandelier", (IEnumerable<string>)AgentEnum.ASTRA_YAO.Value.TemplateIdList);
		Assert.Contains((IEnumerable<AgentStateDef>)AgentEnum.YESHUNGUANG.Value.StateList, (Predicate<AgentStateDef>)((AgentStateDef state) => state.StateName == "叶瞬光-明心境"));
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}
