using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.Tests.HollowZero;

public sealed class HollowContextTests
{
	[Fact]
	public void WitheredDomainMapNavigator_AvoidsRightEntryOptionWhenSelectingMapClick()
	{
		HollowZeroMapNode nextNode = new HollowZeroMapNode(new Rect(100, 200, 200, 300), new HollowZeroEntry("0001-目标"));
		Point actual = WitheredDomainMapNavigator.SelectMapNodeClickPosition(nextNode, new Point(1000, 240));
		Point actual2 = WitheredDomainMapNavigator.SelectMapNodeClickPosition(nextNode, null);
		Assert.Equal(new Point(115, 285), actual);
		Assert.Equal(new Point(150, 250), actual2);
	}

	[Fact]
	public void WitheredDomainContext_TracksMoveVisitedNodesAndLevelState()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config", "hollow_zero_challenge"));
		string sourceFileName = ResolveEntryListAssetPath();
		string text2 = Path.Combine(text, "assets", "game_data", "hollow_zero");
		Directory.CreateDirectory(text2);
		File.Copy(sourceFileName, Path.Combine(text2, "entry_list.yml"));
		File.WriteAllText(Path.Combine(text, "config", "hollow_zero_challenge", "测试.yml"), "auto_battle: 全配队通用\npath_finding: 默认");
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			WitheredDomainContext witheredDomain = zContext.WitheredDomain;
			witheredDomain.InitBeforeRun("测试");
			witheredDomain.InitBeforeHollowStart("旧都列车", "旧都列车-核心");
			HollowZeroMap hollowZeroMap = CreateTwoNodeMap();
			HollowZeroMapNode nextToMove = witheredDomain.GetNextToMove(hollowZeroMap);
			witheredDomain.UpdateContextAfterMove(hollowZeroMap, nextToMove);
			Assert.NotNull(nextToMove);
			Assert.Equal("旧都列车", witheredDomain.LevelInfo.MissionTypeName);
			Assert.Equal(1, witheredDomain.LevelInfo.Level);
			Assert.Equal(1, hollowZeroMap.CurrentIdx);
			Assert.Equal("空白已通行", hollowZeroMap.Nodes[0].Entry.EntryName);
			Assert.Equal("当前", hollowZeroMap.Nodes[1].Entry.EntryName);
			Assert.Single(witheredDomain.VisitedNodes);
			Assert.Equal(1, witheredDomain.VisitedNodes[0].VisitedTimes);
			Assert.True(witheredDomain.HadBeenEntry("目标"));
			witheredDomain.UpdateToNextLevel();
			Assert.Empty(witheredDomain.VisitedNodes);
			Assert.Equal(2, witheredDomain.LevelInfo.Level);
			Assert.Equal(1, witheredDomain.LevelInfo.Phase);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void LostVoidContext_ResetsDynamicStateAndCleansPriorityInput()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		LostVoidContext lostVoid = zContext.LostVoid;
		lostVoid.DynamicPriorityList.Add("强攻");
		lostVoid.DynamicAbandonList.Add("异常");
		lostVoid.PriorityUpdated = true;
		lostVoid.HadInteractedOpheliaOnCurrentLevel = true;
		lostVoid.InitBeforeRun();
		var (actual, actual2) = lostVoid.CheckArtifactPriorityInput(" 强攻\n\n击破 ");
		var (actual3, actual4) = lostVoid.CheckRegionTypePriorityInput("精英\n非法", new HashSet<string> { "精英" });
		Assert.False(lostVoid.PriorityUpdated);
		Assert.False(lostVoid.HadInteractedOpheliaOnCurrentLevel);
		Assert.Empty(lostVoid.DynamicPriorityList);
		Assert.Empty(lostVoid.DynamicAbandonList);
		Assert.Equal("全配队通用", lostVoid.GetAutoOpName());
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "强攻";
		span[1] = "击破";
		Assert.Equal(list, actual);
		Assert.Equal(string.Empty, actual2);
		num = 1;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = "精英";
		Assert.Equal(list2, actual3);
		Assert.Equal("输入非法 非法", actual4);
	}

	private static HollowZeroMap CreateTwoNodeMap()
	{
		HollowZeroMapNode hollowZeroMapNode = new HollowZeroMapNode(new Rect(0, 0, 40, 40), new HollowZeroEntry("0000-当前"));
		HollowZeroMapNode hollowZeroMapNode2 = new HollowZeroMapNode(new Rect(100, 0, 140, 40), new HollowZeroEntry("0001-目标", isBenefit: true, 1, isBase: false, canGo: true, isTp: false, moveAfterwards: false, 1));
		int num = 2;
		List<HollowZeroMapNode> list = new List<HollowZeroMapNode>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<HollowZeroMapNode> span = CollectionsMarshal.AsSpan(list);
		span[0] = hollowZeroMapNode;
		span[1] = hollowZeroMapNode2;
		int? currentIdx = 0;
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		num = 1;
		List<int> list2 = new List<int>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = 1;
		dictionary[0] = list2;
		num = 1;
		List<int> list3 = new List<int>(num);
		CollectionsMarshal.SetCount(list3, num);
		CollectionsMarshal.AsSpan(list3)[0] = 0;
		dictionary[1] = list3;
		return new HollowZeroMap(list, currentIdx, dictionary);
	}

	private static string ResolveEntryListAssetPath()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "assets";
			buffer[2] = "game_data";
			buffer[3] = "hollow_zero";
			buffer[4] = "entry_list.yml";
			string text = Path.Combine(buffer);
			if (File.Exists(text))
			{
				return text;
			}
		}
		throw new FileNotFoundException("未找到枯萎之都入口固定资产。", "assets/game_data/hollow_zero/entry_list.yml");
	}
}
