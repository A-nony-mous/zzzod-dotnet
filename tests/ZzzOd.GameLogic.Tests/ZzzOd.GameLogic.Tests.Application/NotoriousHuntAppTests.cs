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
using Xunit;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.NotoriousHunt;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class NotoriousHuntAppTests
{
	private sealed class RecordingNotoriousHuntFlow : INotoriousHuntAppFlow
	{
		public int RunCount { get; private set; }

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, NotoriousHuntConfig config, NotoriousHuntRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "本轮计划已完成"));
		}

		public void Pause(ZContext context)
		{
			PauseCount++;
		}

		public void Resume(ZContext context)
		{
			ResumeCount++;
		}

		public void Stop(ZContext context)
		{
			StopCount++;
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesNotoriousHuntApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			NotoriousHuntAppFactory notoriousHuntAppFactory = zContext.ApplicationFactoryRegistry.CreateNotoriousHuntFactory();
			IApplication application = notoriousHuntAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = notoriousHuntAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = notoriousHuntAppFactory.GetRunRecord(0);
			Assert.Equal("notorious_hunt", notoriousHuntAppFactory.AppId);
			Assert.Equal("恶名狩猎", notoriousHuntAppFactory.AppName);
			Assert.Equal("one_dragon", notoriousHuntAppFactory.GroupId);
			Assert.True(notoriousHuntAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<NotoriousHuntApp>(application);
			Assert.IsType<NotoriousHuntConfig>(config);
			NotoriousHuntRunRecord notoriousHuntRunRecord = Assert.IsType<NotoriousHuntRunRecord>(runRecord);
			Assert.Equal("notorious_hunt", notoriousHuntRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersNotoriousHuntAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterNotoriousHuntApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("notorious_hunt"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("notorious_hunt"));
			Assert.Contains("notorious_hunt", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void NotoriousHuntConfig_LoadsPythonFieldsAndMigratesLegacyTabs()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "notorious_hunt.yml"), "weekly_challenge_start_weekday: 4\nloop: false\nplan_list:\n  - tab_name: \"挑战\"\n    category_name: \"恶名狩猎\"\n    mission_type_name: \"凶念之菲尼克斯\"\n    mission_name:\n    level: \"等级Lv.65\"\n    auto_battle_config: \"强攻通用\"\n    run_times: 0\n    plan_times: 1\n    predefined_team_idx: 2\n    notorious_hunt_buff_num: 3\n    plan_id: \"old-a\"\n  - tab_name: \"作战\"\n    category_name: \"恶名狩猎\"\n    mission_type_name: \"未知复合侵蚀体\"\n    mission_name:\n    run_times: 1\n    plan_times: 2\n    plan_id: \"old-b\"");
			NotoriousHuntConfig notoriousHuntConfig = NotoriousHuntConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("notorious_hunt", notoriousHuntConfig.AppId);
			Assert.Equal(0, notoriousHuntConfig.InstanceIndex);
			Assert.Equal("one_dragon", notoriousHuntConfig.GroupId);
			Assert.Equal(4, notoriousHuntConfig.WeeklyChallengeStartWeekday);
			Assert.False(notoriousHuntConfig.Loop);
			Assert.Equal("训练", notoriousHuntConfig.PlanList[0].TabName);
			Assert.Equal("训练", notoriousHuntConfig.PlanList[1].TabName);
			Assert.Equal("凶念之菲尼克斯", notoriousHuntConfig.PlanList[0].MissionTypeName);
			Assert.Equal("等级Lv.65", notoriousHuntConfig.PlanList[0].Level);
			Assert.Equal(3, notoriousHuntConfig.PlanList[0].NotoriousHuntBuffNum);
			Assert.Equal("old-a", notoriousHuntConfig.GetNextPlan()?.PlanId);
			Assert.Contains((IEnumerable<ConfigItem>)NotoriousHuntLevel.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, "等级Lv.65")));
			Assert.Contains((IEnumerable<ConfigItem>)NotoriousHuntBuff.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, 3)));
			Assert.Contains((IEnumerable<ConfigItem>)NotoriousHuntWeekday.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, 7)));
			NotoriousHuntConfig notoriousHuntConfig2 = NotoriousHuntConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("训练", notoriousHuntConfig2.PlanList[0].TabName);
			Assert.Equal("训练", notoriousHuntConfig2.PlanList[1].TabName);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void NotoriousHuntConfig_ResetPlansAndSelectsNextAfterLastTried()
	{
		NotoriousHuntConfig notoriousHuntConfig = new NotoriousHuntConfig();
		int num = 3;
		List<ChargePlanItem> list = new List<ChargePlanItem>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<ChargePlanItem> span = CollectionsMarshal.AsSpan(list);
		span[0] = new ChargePlanItem
		{
			PlanId = "a",
			RunTimes = 1,
			PlanTimes = 1
		};
		span[1] = new ChargePlanItem
		{
			PlanId = "b",
			RunTimes = 2,
			PlanTimes = 2
		};
		span[2] = new ChargePlanItem
		{
			PlanId = "c",
			RunTimes = 0,
			PlanTimes = 1,
			Skipped = true
		};
		notoriousHuntConfig.PlanList = list;
		NotoriousHuntConfig notoriousHuntConfig2 = notoriousHuntConfig;
		Assert.True(notoriousHuntConfig2.AllPlanFinished());
		notoriousHuntConfig2.ResetPlans();
		Assert.Equal(0, notoriousHuntConfig2.PlanList[0].RunTimes);
		Assert.Equal(0, notoriousHuntConfig2.PlanList[1].RunTimes);
		Assert.Equal("a", notoriousHuntConfig2.GetNextPlan()?.PlanId);
		Assert.Equal("b", notoriousHuntConfig2.GetNextPlan(notoriousHuntConfig2.PlanList[0])?.PlanId);
		Assert.Null(notoriousHuntConfig2.GetNextPlan(notoriousHuntConfig2.PlanList[1]));
	}

	[Fact]
	public void NotoriousHuntConfig_AddPlanRunTimesAndResetPlansPersistToYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "notorious_hunt.yml"), "loop: true\nplan_list:\n  - tab_name: \"训练\"\n    category_name: \"恶名狩猎\"\n    mission_type_name: \"猎血清道夫\"\n    mission_name:\n    run_times: 0\n    plan_times: 1\n    plan_id: \"hunt-a\"");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			NotoriousHuntConfig notoriousHuntConfig = NotoriousHuntConfig.Load(environment, 0, "one_dragon");
			notoriousHuntConfig.AddPlanRunTimes(notoriousHuntConfig.PlanList[0]);
			NotoriousHuntConfig notoriousHuntConfig2 = NotoriousHuntConfig.Load(environment, 0, "one_dragon");
			Assert.Equal(1, notoriousHuntConfig2.PlanList[0].RunTimes);
			notoriousHuntConfig2.ResetPlans();
			NotoriousHuntConfig notoriousHuntConfig3 = NotoriousHuntConfig.Load(environment, 0, "one_dragon");
			Assert.Equal(0, notoriousHuntConfig3.PlanList[0].RunTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void NotoriousHuntRunRecord_UsesWeeklyLeftTimesAndWeekdayGate()
	{
		DateTimeOffset monday = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		NotoriousHuntConfig config = new NotoriousHuntConfig
		{
			WeeklyChallengeStartWeekday = 4
		};
		NotoriousHuntRunRecord notoriousHuntRunRecord = new NotoriousHuntRunRecord(config, 0, () => monday)
		{
			Dt = "20260706",
			LeftTimes = 0,
			RunStatus = 0
		};
		Assert.Equal(1, notoriousHuntRunRecord.CurrentWeekday);
		Assert.False(notoriousHuntRunRecord.IsAutoRunAllowedToday);
		Assert.True(notoriousHuntRunRecord.IsFinishedByWeek);
		Assert.True(notoriousHuntRunRecord.IsDone);
		Assert.Equal(1, notoriousHuntRunRecord.RunStatusUnderNow);
		notoriousHuntRunRecord.ResetRecord();
		Assert.Equal(3, notoriousHuntRunRecord.LeftTimes);
	}

	[Fact]
	public void NotoriousHuntRunRecord_LoadsAndPersistsLeftTimes()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "notorious_hunt.yml"), "dt: \"20260706\"\nrun_status: 0\nleft_times: 2");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			NotoriousHuntConfig config = new NotoriousHuntConfig();
			NotoriousHuntRunRecord notoriousHuntRunRecord = NotoriousHuntRunRecord.Load(environment, 0, config);
			Assert.Equal(2, notoriousHuntRunRecord.LeftTimes);
			notoriousHuntRunRecord.UpdateLeftTimes(1);
			NotoriousHuntRunRecord notoriousHuntRunRecord2 = NotoriousHuntRunRecord.Load(environment, 0, config);
			Assert.Equal(1, notoriousHuntRunRecord2.LeftTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task NotoriousHuntApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingNotoriousHuntFlow flow = new RecordingNotoriousHuntFlow();
			NotoriousHuntRunRecord runRecord = new NotoriousHuntRunRecord(new NotoriousHuntConfig());
			NotoriousHuntApp app = new NotoriousHuntApp(context, new NotoriousHuntConfig(), runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("本轮计划已完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NotoriousHuntApp_DelegatesPauseResumeAndStopToFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			RecordingNotoriousHuntFlow flow = new RecordingNotoriousHuntFlow();
			NotoriousHuntConfig config = new NotoriousHuntConfig();
			NotoriousHuntApp app = new NotoriousHuntApp(context, config, new NotoriousHuntRunRecord(config), flow);
			await app.OnPauseAsync(CancellationToken.None);
			await app.OnResumeAsync(CancellationToken.None);
			await app.OnStopAsync(CancellationToken.None);
			Assert.Equal(1, flow.PauseCount);
			Assert.Equal(1, flow.ResumeCount);
			Assert.Equal(1, flow.StopCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NotoriousHuntOperation_UsesInjectedTransportHuntAndBackWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ChargePlanItem plan = new ChargePlanItem
			{
				PlanId = "hunt-a",
				CategoryName = "恶名狩猎",
				MissionTypeName = "凶念之菲尼克斯",
				RunTimes = 0,
				PlanTimes = 1,
				NotoriousHuntBuffNum = 2
			};
			NotoriousHuntConfig config = new NotoriousHuntConfig
			{
				Loop = false,
				PlanList = new List<ChargePlanItem>(1) { plan }
			};
			NotoriousHuntRunRecord runRecord = new NotoriousHuntRunRecord(config)
			{
				LeftTimes = 1
			};
			int transportCount = 0;
			int huntCount = 0;
			int backCount = 0;
			NotoriousHuntOperation operation = new NotoriousHuntOperation(context, config, runRecord, delegate(ZContext _, ChargePlanItem actualPlan)
			{
				transportCount++;
				Assert.Same(plan, actualPlan);
				return Task.FromResult(new OperationResult(IsSuccess: true, "传送完成"));
			}, delegate(ZContext _, ChargePlanItem actualPlan)
			{
				huntCount++;
				Assert.Same(plan, actualPlan);
				config.AddPlanRunTimes(actualPlan);
				runRecord.UpdateLeftTimes(0);
				return Task.FromResult(new OperationResult(IsSuccess: true, "挑战完成"));
			}, delegate
			{
				backCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
			});
			OperationRoundResult start = operation.StartHunt();
			OperationRoundResult find = operation.FindNextPlan();
			OperationRoundResult transport = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult hunt = await operation.Hunt().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult leftTimes = operation.CheckLeftTimes();
			OperationRoundResult back = await operation.BackToWorld().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(start.IsSuccess);
			Assert.True(find.IsSuccess);
			Assert.True(transport.IsSuccess);
			Assert.Equal("传送完成", transport.Status);
			Assert.True(hunt.IsSuccess);
			Assert.Equal("挑战完成", hunt.Status);
			Assert.True(leftTimes.IsSuccess);
			Assert.Equal("周期挑战无剩余次数", leftTimes.Status);
			Assert.True(back.IsSuccess);
			Assert.Equal("返回大世界", back.Status);
			Assert.Equal(1, plan.RunTimes);
			Assert.Equal(0, runRecord.LeftTimes);
			Assert.Equal(1, transportCount);
			Assert.Equal(1, huntCount);
			Assert.Equal(1, backCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NotoriousHuntOperation_SkipsFailedPlanAndContinuesWithNext()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ChargePlanItem firstPlan = new ChargePlanItem
			{
				PlanId = "hunt-a",
				CategoryName = "恶名狩猎",
				MissionTypeName = "凶念之菲尼克斯"
			};
			ChargePlanItem secondPlan = new ChargePlanItem
			{
				PlanId = "hunt-b",
				CategoryName = "恶名狩猎",
				MissionTypeName = "未知复合侵蚀体"
			};
			NotoriousHuntConfig config = new NotoriousHuntConfig
			{
				PlanList = new List<ChargePlanItem>(2) { firstPlan, secondPlan }
			};
			NotoriousHuntOperation operation = new NotoriousHuntOperation(context, config, new NotoriousHuntRunRecord(config)
			{
				LeftTimes = 2
			}, (ZContext _, ChargePlanItem actualPlan) => Task.FromResult(new OperationResult(IsSuccess: true, actualPlan.PlanId)), (ZContext _, ChargePlanItem _) => Task.FromResult(new OperationResult(IsSuccess: false, "战斗失败")));
			operation.StartHunt();
			operation.FindNextPlan();
			OperationRoundResult firstHunt = await operation.Hunt().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult check = operation.CheckLeftTimes();
			OperationRoundResult nextFind = operation.FindNextPlan();
			OperationRoundResult nextTransport = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(firstHunt.IsSuccess);
			Assert.True(check.IsSuccess);
			Assert.True(firstPlan.Skipped);
			Assert.True(nextFind.IsSuccess);
			Assert.True(nextTransport.IsSuccess);
			Assert.Equal("hunt-b", nextTransport.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void NotoriousHuntOperation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(NotoriousHuntOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[10] { "开始恶名狩猎", "查找下一条计划", "前往大世界", "传送", "跳过或结束计划", "恶名狩猎", "判断剩余次数", "点击奖励入口", "全部领取", "返回大世界" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["开始恶名狩猎"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["查找下一条计划"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "判断剩余次数");
		Assert.Contains(readOnlyDictionary["前往大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "查找下一条计划");
		Assert.Contains(readOnlyDictionary["传送"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "前往大世界");
		Assert.Contains(readOnlyDictionary["跳过或结束计划"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "传送" && !edge.Success && edge.Status == "找不到 代理人方案培养");
		Assert.Contains(readOnlyDictionary["点击奖励入口"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "判断剩余次数" && edge.Status == "周期挑战无剩余次数");
		Assert.Contains(readOnlyDictionary["返回大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "全部领取" && !edge.Success);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
