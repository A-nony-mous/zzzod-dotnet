using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.SuibianTemple;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class SuibianTempleAppTests
{
	private sealed class RecordingSuibianTempleFlow : ISuibianTempleAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, SuibianTempleConfig config, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "随便观完成"));
		}
	}

	private sealed class RecordingSuibianTempleServices : ISuibianTempleOperationServices
	{
		public bool InTempleEntry { get; set; }

		public OperationResult GoToTempleResult { get; set; } = new OperationResult(IsSuccess: true, "随便观-入口");

		public int TransportCount { get; private set; }

		public int GoToTempleCount { get; private set; }

		public int AutoManageCount { get; private set; }

		public List<(bool Claim, bool Dispatch)> AdventureCalls { get; } = new List<(bool, bool)>();

		public int YumChaSinCount { get; private set; }

		public int CraftCount { get; private set; }

		public int SalesStallCount { get; private set; }

		public int GoodGoodsCount { get; private set; }

		public int BooBoxCount { get; private set; }

		public int PawnshopCount { get; private set; }

		public int BackToNormalWorldCount { get; private set; }

		public bool IsInTempleEntry(ZContext context, Mat? screen)
		{
			return InTempleEntry;
		}

		public Task<OperationResult> TransportAsync(ZContext context)
		{
			TransportCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "传送"));
		}

		public OperationResult GoToTempleEntry(ZContext context, Mat? screen, SuibianTempleConfig config)
		{
			GoToTempleCount++;
			return GoToTempleResult;
		}

		public Task<OperationResult> HandleAutoManageAsync(ZContext context, SuibianTempleConfig config)
		{
			AutoManageCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "自动托管"));
		}

		public Task<OperationResult> HandleAdventureSquadAsync(ZContext context, SuibianTempleConfig config, bool claim, bool dispatch)
		{
			AdventureCalls.Add((claim, dispatch));
			return Task.FromResult(new OperationResult(IsSuccess: true, "游历"));
		}

		public Task<OperationResult> HandleYumChaSinAsync(ZContext context, SuibianTempleConfig config)
		{
			YumChaSinCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "饮茶仙"));
		}

		public Task<OperationResult> HandleCraftAsync(ZContext context, SuibianTempleConfig config)
		{
			CraftCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "制造坊"));
		}

		public Task<OperationResult> HandleSalesStallAsync(ZContext context, SuibianTempleConfig config)
		{
			SalesStallCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "售卖铺"));
		}

		public Task<OperationResult> HandleGoodGoodsAsync(ZContext context, SuibianTempleConfig config)
		{
			GoodGoodsCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "好物铺"));
		}

		public Task<OperationResult> HandleBooBoxAsync(ZContext context, SuibianTempleConfig config)
		{
			BooBoxCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "邦巢"));
		}

		public Task<OperationResult> HandlePawnshopAsync(ZContext context, SuibianTempleConfig config)
		{
			PawnshopCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "德丰大押"));
		}

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
		{
			BackToNormalWorldCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesSuibianTempleApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			SuibianTempleFactory suibianTempleFactory = zContext.ApplicationFactoryRegistry.CreateSuibianTempleFactory();
			IApplication application = suibianTempleFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = suibianTempleFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = suibianTempleFactory.GetRunRecord(0);
			Assert.Equal("suibian_temple", suibianTempleFactory.AppId);
			Assert.Equal("随便观", suibianTempleFactory.AppName);
			Assert.Equal("one_dragon", suibianTempleFactory.GroupId);
			Assert.True(suibianTempleFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<SuibianTempleApp>(application);
			SuibianTempleConfig suibianTempleConfig = Assert.IsType<SuibianTempleConfig>(config);
			Assert.True(suibianTempleConfig.YumChaSin);
			Assert.Equal(SuibianTempleAdventureDispatchDuration.Hour20.Name, suibianTempleConfig.AdventureDuration);
			SuibianTempleRunRecord suibianTempleRunRecord = Assert.IsType<SuibianTempleRunRecord>(runRecord);
			Assert.Equal("suibian_temple", suibianTempleRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersSuibianTempleAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ApplicationFactoryRegistry.RegisterSuibianTempleApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("suibian_temple"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("suibian_temple"));
			Assert.Contains("suibian_temple", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Config_LoadsPythonFieldsAndSettingMetadata()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "suibian_temple.yml"), "yum_cha_sin: false\nyum_cha_sin_period_refresh: false\nadventure_duration: HOUR_12\nadventure_mission_1: CRAFT_1_1\nadventure_mission_2: COMMUNITY_2_4\nadventure_mission_3: RESEARCH_2_2\nadventure_mission_4: CRAFT_3_4\ncraft_drag_times: 7\ngood_goods_purchase_enabled: true\nboo_box_purchase_enabled: true\nboo_box_adventure_price: S1\nboo_box_craft_price: S2\nboo_box_sell_price: NONE\npawnshop_omnicoin_enabled: false\npawnshop_omnicoin_priority:\n  - PREPAID_POWER_CARD\n  - ETHER_PLATING_AGENT\npawnshop_crest_enabled: false\npawnshop_crest_priority:\n  - DENNY\npawnshop_crest_unlimited_denny_enabled: true\nauto_manage_enabled: false");
			SuibianTempleConfig suibianTempleConfig = SuibianTempleConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("suibian_temple", suibianTempleConfig.AppId);
			Assert.Equal(0, suibianTempleConfig.InstanceIndex);
			Assert.Equal("one_dragon", suibianTempleConfig.GroupId);
			Assert.False(suibianTempleConfig.YumChaSin);
			Assert.False(suibianTempleConfig.YumChaSinPeriodRefresh);
			Assert.Equal("HOUR_12", suibianTempleConfig.AdventureDuration);
			Assert.Equal("CRAFT_1_1", suibianTempleConfig.AdventureMission1);
			Assert.Equal("COMMUNITY_2_4", suibianTempleConfig.AdventureMission2);
			Assert.Equal("RESEARCH_2_2", suibianTempleConfig.AdventureMission3);
			Assert.Equal("CRAFT_3_4", suibianTempleConfig.AdventureMission4);
			Assert.Equal(7, suibianTempleConfig.CraftDragTimes);
			Assert.True(suibianTempleConfig.GoodGoodsPurchaseEnabled);
			Assert.True(suibianTempleConfig.BooBoxPurchaseEnabled);
			Assert.Equal("S1", suibianTempleConfig.BooBoxAdventurePrice);
			Assert.Equal("S2", suibianTempleConfig.BooBoxCraftPrice);
			Assert.Equal("NONE", suibianTempleConfig.BooBoxSellPrice);
			Assert.False(suibianTempleConfig.PawnshopOmnicoinEnabled);
			int num = 2;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<string> span = CollectionsMarshal.AsSpan(list);
			span[0] = "PREPAID_POWER_CARD";
			span[1] = "ETHER_PLATING_AGENT";
			Assert.Equal<List<string>>(list, suibianTempleConfig.PawnshopOmnicoinPriority);
			Assert.False(suibianTempleConfig.PawnshopCrestEnabled);
			num = 1;
			List<string> list2 = new List<string>(num);
			CollectionsMarshal.SetCount(list2, num);
			CollectionsMarshal.AsSpan(list2)[0] = "DENNY";
			Assert.Equal<List<string>>(list2, suibianTempleConfig.PawnshopCrestPriority);
			Assert.True(suibianTempleConfig.PawnshopCrestUnlimitedDennyEnabled);
			Assert.False(suibianTempleConfig.AutoManageEnabled);
			Assert.Equal("INTERFACE", "INTERFACE");
			Assert.Contains((IEnumerable<SuibianTempleSettingField>)SuibianTempleSettings.Fields, (Predicate<SuibianTempleSettingField>)((SuibianTempleSettingField field) => field.Key == "auto_manage_enabled" && field.DefaultValue.Equals(true)));
			Assert.Contains((IEnumerable<SuibianTempleSettingField>)SuibianTempleSettings.Fields, (Predicate<SuibianTempleSettingField>)((SuibianTempleSettingField field) => field.Key == "pawnshop_omnicoin_priority" && field.Type == SuibianTempleSettingType.MultiEnum));
			Assert.Contains((IEnumerable<ConfigItem>)SuibianTempleAdventureMission.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, SuibianTempleAdventureMission.Research34.Name)));
			Assert.Contains((IEnumerable<ConfigItem>)SuibianTempleBangbooPrice.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, SuibianTempleBangbooPrice.S4.Name)));
			Assert.Contains((IEnumerable<ConfigItem>)SuibianTemplePawnshopOmnicoinGoods.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, "PREPAID_POWER_CARD")));
			Assert.Contains((IEnumerable<ConfigItem>)SuibianTemplePawnshopCrestGoods.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, "DENNY")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_UsesAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		SuibianTempleRunRecord suibianTempleRunRecord = new SuibianTempleRunRecord(4, () => now);
		suibianTempleRunRecord.UpdateStatus(1);
		Assert.Equal("suibian_temple", suibianTempleRunRecord.AppId);
		Assert.Equal("20260706", suibianTempleRunRecord.Dt);
		Assert.True(suibianTempleRunRecord.IsDone);
	}

	[Fact]
	public async Task Operation_RunsInjectedEntryFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			SuibianTempleConfig config = new SuibianTempleConfig
			{
				AutoManageEnabled = false,
				YumChaSin = true,
				GoodGoodsPurchaseEnabled = true,
				BooBoxPurchaseEnabled = true
			};
			RecordingSuibianTempleServices services = new RecordingSuibianTempleServices
			{
				InTempleEntry = false,
				GoToTempleResult = new OperationResult(IsSuccess: true, "随便观-入口")
			};
			SuibianTempleOperation operation = new SuibianTempleOperation(context, config, services);
			Assert.Equal("不在随便观", operation.CheckInitialScreen().Status);
			Assert.True((await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(operation.GoToSuibianTemple().IsSuccess);
			Assert.Equal("未开启自动托管", (await operation.HandleAutoManage().WaitAsync(TimeSpan.FromSeconds(2L))).Status);
			Assert.True((await operation.HandleAdventureSquad().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.HandleYumChaSin().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.HandleAdventureSquadAfterYumChaSin().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.HandleCraft().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.HandleSalesStall().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.HandleGoodGoods().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.HandleBooBox().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal("未开启", (await operation.HandlePawnshop().WaitAsync(TimeSpan.FromSeconds(2L))).Status);
			Assert.True((await operation.BackAtLast().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal(1, services.TransportCount);
			Assert.Equal(1, services.GoToTempleCount);
			Assert.Equal(0, services.AutoManageCount);
			Assert.Equal<List<(bool, bool)>>(new List<(bool, bool)>(2)
			{
				(true, false),
				(false, true)
			}, services.AdventureCalls);
			Assert.Equal(1, services.YumChaSinCount);
			Assert.Equal(1, services.CraftCount);
			Assert.Equal(1, services.SalesStallCount);
			Assert.Equal(1, services.GoodGoodsCount);
			Assert.Equal(1, services.BooBoxCount);
			Assert.Equal(0, services.PawnshopCount);
			Assert.Equal(1, services.BackToNormalWorldCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_SkipsDisabledOptionalFlows()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			SuibianTempleConfig config = new SuibianTempleConfig
			{
				YumChaSin = false,
				GoodGoodsPurchaseEnabled = false,
				BooBoxPurchaseEnabled = false
			};
			RecordingSuibianTempleServices services = new RecordingSuibianTempleServices
			{
				InTempleEntry = true
			};
			SuibianTempleOperation operation = new SuibianTempleOperation(context, config, services);
			Assert.Equal("随便观-入口", operation.CheckInitialScreen().Status);
			Assert.True((await operation.HandleAutoManage().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal("未开启", (await operation.HandleYumChaSin().WaitAsync(TimeSpan.FromSeconds(2L))).Status);
			Assert.Equal("未开启", (await operation.HandleGoodGoods().WaitAsync(TimeSpan.FromSeconds(2L))).Status);
			Assert.Equal("未开启", (await operation.HandleBooBox().WaitAsync(TimeSpan.FromSeconds(2L))).Status);
			Assert.Equal(1, services.AutoManageCount);
			Assert.Equal(0, services.YumChaSinCount);
			Assert.Equal(0, services.GoodGoodsCount);
			Assert.Equal(0, services.BooBoxCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_SkipsPawnshopLikePythonEvenWhenConfigEnabled()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			SuibianTempleConfig config = new SuibianTempleConfig
			{
				PawnshopOmnicoinEnabled = true,
				PawnshopCrestEnabled = true
			};
			RecordingSuibianTempleServices services = new RecordingSuibianTempleServices();
			SuibianTempleOperation operation = new SuibianTempleOperation(context, config, services);
			OperationRoundResult result = await operation.HandlePawnshop().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("未开启", result.Status);
			Assert.Equal(0, services.PawnshopCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(SuibianTempleOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[13]
		{
			"识别初始画面", "传送", "前往随便观", "处理自动托管", "处理游历", "处理饮茶仙", "饮茶仙后处理游历", "处理制造坊", "处理售卖铺", "处理好物铺",
			"处理邦巢", "处理德丰大押", "完成后返回"
		}, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["识别初始画面"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(60.0, readOnlyDictionary["前往随便观"].GetCustomAttribute<OperationNodeAttribute>().TimeoutSeconds);
		Assert.Equal(999, readOnlyDictionary["前往随便观"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Contains(readOnlyDictionary["传送"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "识别初始画面" && edge.Status == "不在随便观");
		Assert.Contains(readOnlyDictionary["处理自动托管"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "识别初始画面" && edge.Status == "随便观-入口");
		Assert.Contains(readOnlyDictionary["处理游历"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "处理自动托管" && edge.Status == "未开启自动托管");
		Assert.Contains(readOnlyDictionary["处理制造坊"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "处理饮茶仙" && edge.Status == "未开启");
		Assert.Contains(readOnlyDictionary["处理好物铺"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "处理自动托管");
		Assert.Contains(readOnlyDictionary["完成后返回"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "处理德丰大押");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
