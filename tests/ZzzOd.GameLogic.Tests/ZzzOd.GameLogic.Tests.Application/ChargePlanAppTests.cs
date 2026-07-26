using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class ChargePlanAppTests
{
	private sealed class RecordingChargePlanFlow : IChargePlanAppFlow
	{
		public int RunCount { get; private set; }

		public ChargePlanRunRecord? LastRunRecord { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, ChargePlanConfig config, ChargePlanRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			LastRunRecord = runRecord;
			return Task.FromResult(new OperationResult(IsSuccess: true, "已完成一轮计划"));
		}
	}

	private sealed class SizeAwareSingleLineMatcher(Func<int, int, string> textFactory) : IOcrMatcher
	{
		public (int Width, int Height)? LastSize { get; private set; }

		public void UpdateUseGpu(bool useGpu)
		{
		}

		public bool IsUseGpu()
		{
			return false;
		}

		public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
		{
			return true;
		}

		public string RunOcrSingleLine(Mat image, double? threshold = null, bool strictOneLine = true)
		{
			LastSize = (image.Width, image.Height);
			return textFactory(image.Width, image.Height);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in Ocr(image, threshold.GetValueOrDefault(), mergeLineDistance))
			{
				if (!dictionary.TryGetValue(item.Text, out var value))
				{
					value = new MatchResultList(onlyBest: false);
					dictionary[item.Text] = value;
				}
				value.Append(item, autoMerge: false);
			}
			return dictionary;
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			string text = RunOcrSingleLine(image, threshold);
			return new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, image.Width, image.Height, text) };
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesChargePlanApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			ChargePlanAppFactory chargePlanAppFactory = zContext.ApplicationFactoryRegistry.CreateChargePlanFactory();
			IApplication application = chargePlanAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = chargePlanAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = chargePlanAppFactory.GetRunRecord(0);
			Assert.Equal("charge_plan", chargePlanAppFactory.AppId);
			Assert.Equal("体力刷本", chargePlanAppFactory.AppName);
			Assert.Equal("one_dragon", chargePlanAppFactory.GroupId);
			Assert.True(chargePlanAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<ChargePlanApp>(application);
			Assert.IsType<ChargePlanConfig>(config);
			Assert.IsType<ChargePlanRunRecord>(runRecord);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersChargePlanAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterChargePlanApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("charge_plan"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("charge_plan"));
			Assert.Contains("charge_plan", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanConfig_LoadsPythonFieldsAndSelectsNextPlan()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "charge_plan.yml"), "loop: false\ndaily_reset_plan_times: true\nlast_daily_reset_dt: \"20260705\"\nskip_plan: true\ndouble_reward: true\nrestore_charge: \"使用储蓄电量\"\ncombat_simulation_double_reward_config:\n  tab_name: \"训练\"\n  category_name: \"实战模拟室\"\n  mission_type_name: \"基础材料\"\n  mission_name: \"调查专项\"\n  card_num: \"2\"\nplan_list:\n  - tab_name: \"训练\"\n    category_name: \"实战模拟室\"\n    mission_type_name: \"基础材料\"\n    mission_name: \"调查专项\"\n    run_times: 1\n    plan_times: 2\n    card_num: \"2\"\n    plan_id: \"plan-a\"\n  - tab_name: \"训练\"\n    category_name: \"区域巡防\"\n    mission_type_name: \"驱动校验\"\n    mission_name:\n    run_times: 1\n    plan_times: 1\n    plan_id: \"plan-b\"\nhistory_list:\n  - tab_name: \"训练\"\n    category_name: \"实战模拟室\"\n    mission_type_name: \"基础材料\"\n    mission_name: \"调查专项\"\n    card_num: \"2\"");
			ChargePlanConfig chargePlanConfig = ChargePlanConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.False(chargePlanConfig.Loop);
			Assert.True(chargePlanConfig.DailyResetPlanTimes);
			Assert.True(chargePlanConfig.SkipPlan);
			Assert.True(chargePlanConfig.DoubleReward);
			Assert.Equal(RestoreChargeMode.BackupOnly.DisplayName, chargePlanConfig.RestoreCharge);
			Assert.Equal("基础材料", chargePlanConfig.CombatSimulationDoubleRewardConfig.MissionTypeName);
			Assert.Equal("plan-a", chargePlanConfig.GetNextPlan()?.PlanId);
			Assert.Equal(40, chargePlanConfig.GetNextPlan().EstimatedChargePower);
			Assert.Null(chargePlanConfig.GetHistoryByUid(chargePlanConfig.PlanList[0]));
			Assert.True(chargePlanConfig.TryResetPlanTimesByDt("20260706"));
			Assert.All(chargePlanConfig.PlanList, delegate(ChargePlanItem plan)
			{
				Assert.Equal(0, plan.RunTimes);
			});
			Assert.False(chargePlanConfig.TryResetPlanTimesByDt("20260706"));
			Assert.Contains((IEnumerable<ConfigItem>)ChargePlanCardNum.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, "5")));
			ChargePlanConfig chargePlanConfig2 = ChargePlanConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("20260706", chargePlanConfig2.LastDailyResetDt);
			Assert.All(chargePlanConfig2.PlanList, delegate(ChargePlanItem plan)
			{
				Assert.Equal(0, plan.RunTimes);
			});
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanConfig_ResetPlansAndSkipsFinishedPlans()
	{
		ChargePlanConfig chargePlanConfig = new ChargePlanConfig();
		int num = 2;
		List<ChargePlanItem> list = new List<ChargePlanItem>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<ChargePlanItem> span = CollectionsMarshal.AsSpan(list);
		span[0] = new ChargePlanItem
		{
			PlanId = "a",
			RunTimes = 2,
			PlanTimes = 2
		};
		span[1] = new ChargePlanItem
		{
			PlanId = "b",
			RunTimes = 1,
			PlanTimes = 1,
			Skipped = true
		};
		chargePlanConfig.PlanList = list;
		ChargePlanConfig chargePlanConfig2 = chargePlanConfig;
		Assert.True(chargePlanConfig2.AllPlanFinished());
		chargePlanConfig2.ResetPlans();
		Assert.Equal(0, chargePlanConfig2.PlanList[0].RunTimes);
		Assert.Equal("a", chargePlanConfig2.GetNextPlan()?.PlanId);
		Assert.Null(chargePlanConfig2.GetNextPlan(chargePlanConfig2.PlanList[0]));
	}

	[Fact]
	public void ChargePlanConfig_AddPlanRunTimesPersistsToYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "charge_plan.yml"), "plan_list:\n  - tab_name: \"训练\"\n    category_name: \"实战模拟室\"\n    mission_type_name: \"基础材料\"\n    mission_name: \"调查专项\"\n    run_times: 0\n    plan_times: 2\n    plan_id: \"plan-a\"");
			ChargePlanConfig chargePlanConfig = ChargePlanConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			chargePlanConfig.AddPlanRunTimes(chargePlanConfig.PlanList[0]);
			ChargePlanConfig chargePlanConfig2 = ChargePlanConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal(1, chargePlanConfig2.PlanList[0].RunTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanRunRecord_RecordsAndEstimatesRecoveredChargePower()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		ChargePlanRunRecord chargePlanRunRecord = new ChargePlanRunRecord(0, () => now);
		chargePlanRunRecord.RecordCurrentChargePower(100);
		DateTimeOffset later = now.AddMinutes(18.0);
		ChargePlanRunRecord chargePlanRunRecord2 = new ChargePlanRunRecord(0, () => later)
		{
			ChargePowerSnapshot = chargePlanRunRecord.ChargePowerSnapshot.ToList()
		};
		Assert.Equal(103, chargePlanRunRecord2.GetEstimatedChargePower());
		chargePlanRunRecord.ResetRecord();
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = 0;
		span[1] = -1;
		Assert.Equal(list, chargePlanRunRecord.ChargePowerSnapshot);
	}

	[Fact]
	public void ChargePlanRunRecord_PersistsStatusAndSnapshotInTheSameYamlDocument()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "charge_plan.yml"), "dt: \"20260706\"\nrun_time: \"07-06 01:00\"\nrun_time_float: 1783299600\nrun_status: 1\ncurrent_charge_power_snapshot: [100, 1783299600]");
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 2, 0, 0, TimeSpan.Zero);
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			ChargePlanRunRecord chargePlanRunRecord = ChargePlanRunRecord.Load(environment, 0, 0, () => now);
			chargePlanRunRecord.RecordCurrentChargePower(80);
			chargePlanRunRecord.UpdateStatus(2);
			ChargePlanRunRecord chargePlanRunRecord2 = ChargePlanRunRecord.Load(environment, 0, 0, () => now);
			Assert.Equal(2, chargePlanRunRecord2.RunStatus);
			Assert.Equal("20260706", chargePlanRunRecord2.Dt);
			Assert.Equal(80, chargePlanRunRecord2.ChargePowerSnapshot[0]);
			Assert.Equal((int)now.ToUnixTimeSeconds(), chargePlanRunRecord2.ChargePowerSnapshot[1]);
			chargePlanRunRecord2.ResetRecord();
			ChargePlanRunRecord chargePlanRunRecord3 = ChargePlanRunRecord.Load(environment, 0, 0, () => now);
			Assert.Equal(0, chargePlanRunRecord3.RunStatus);
			int num = 2;
			List<int> list = new List<int>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<int> span = CollectionsMarshal.AsSpan(list);
			span[0] = 0;
			span[1] = -1;
			Assert.Equal(list, chargePlanRunRecord3.ChargePowerSnapshot);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanConfig_UsesPythonPlanEqualityWhenPlanIdIsMissing()
	{
		ChargePlanItem chargePlanItem = new ChargePlanItem
		{
			PlanId = null,
			RunTimes = 0,
			PlanTimes = 2
		};
		ChargePlanItem chargePlanItem2 = new ChargePlanItem
		{
			PlanId = null,
			RunTimes = 0,
			PlanTimes = 2
		};
		ChargePlanItem chargePlanItem3 = chargePlanItem.Clone();
		chargePlanItem3.PlanId = null;
		chargePlanItem3.RunTimes = 1;
		ChargePlanConfig chargePlanConfig = new ChargePlanConfig();
		int num = 2;
		List<ChargePlanItem> list = new List<ChargePlanItem>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<ChargePlanItem> span = CollectionsMarshal.AsSpan(list);
		span[0] = chargePlanItem;
		span[1] = chargePlanItem2;
		chargePlanConfig.PlanList = list;
		ChargePlanConfig chargePlanConfig2 = chargePlanConfig;
		Assert.Same(chargePlanItem, chargePlanConfig2.GetNextPlan(chargePlanItem3));
	}

	[Fact]
	public void ChargePlanItem_InvalidCardNumberKeepsPythonFailureSemantics()
	{
		ChargePlanItem item = new ChargePlanItem
		{
			CategoryName = "实战模拟室",
			CardNum = "not-a-card-number"
		};
		Assert.Throws<FormatException>(() => item.EstimatedChargePower);
	}

	[Fact]
	public async Task ChargePlanApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingChargePlanFlow flow = new RecordingChargePlanFlow();
			ChargePlanRunRecord runRecord = new ChargePlanRunRecord();
			ChargePlanApp app = new ChargePlanApp(context, new ChargePlanConfig(), runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("已完成一轮计划", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ChargePlanOperation_RunsInjectedCombatSimulationFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ChargePlanItem plan = new ChargePlanItem
			{
				CategoryName = "实战模拟室",
				MissionTypeName = "基础材料",
				RunTimes = 0,
				PlanTimes = 1,
				CardNum = "1"
			};
			ChargePlanConfig config = new ChargePlanConfig
			{
				PlanList = new List<ChargePlanItem>(1) { plan }
			};
			ChargePlanRunRecord runRecord = new ChargePlanRunRecord();
			int backCount = 0;
			int compendiumCount = 0;
			int transportCount = 0;
			int combatCount = 0;
			ChargePlanOperation operation = new ChargePlanOperation(context, config, runRecord, backToWorldBeforeCompendiumAsync: delegate
			{
				backCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
			}, transportAsync: delegate(ZContext _, ChargePlanItem actualPlan)
			{
				transportCount++;
				Assert.Same(plan, actualPlan);
				return Task.FromResult(new OperationResult(IsSuccess: true, "传送完成"));
			}, combatSimulationAsync: delegate(ZContext _, ChargePlanItem actualPlan)
			{
				combatCount++;
				Assert.Same(plan, actualPlan);
				return Task.FromResult(new OperationResult(IsSuccess: true, "挑战完成"));
			}, resourceReader: (ZContext _) => new ChargePlanResourceReading(100, 0, 0), openCompendiumAsync: delegate
			{
				compendiumCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "快捷手册-训练"));
			});
			Assert.True(operation.StartChargePlan().IsSuccess);
			Assert.True((await operation.BackBeforeOpenCompendium().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.OpenCompendium().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			OperationRoundResult charge = operation.CheckBatteryCharge();
			OperationRoundResult select = operation.FindNextPlan();
			OperationRoundResult check = operation.CheckBeforeTransport();
			OperationRoundResult transport = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult missionType = operation.CheckMissionType();
			OperationRoundResult combat = await operation.CombatSimulation().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(charge.IsSuccess);
			Assert.Equal("查找候选计划", charge.Status);
			Assert.True(select.IsSuccess);
			Assert.True(check.IsSuccess);
			Assert.True(transport.IsSuccess);
			Assert.Equal("传送完成", transport.Status);
			Assert.True(missionType.IsSuccess);
			Assert.Equal("实战模拟室", missionType.Status);
			Assert.True(combat.IsSuccess);
			Assert.Equal("挑战完成", combat.Status);
			Assert.Equal(1, backCount);
			Assert.Equal(1, compendiumCount);
			Assert.Equal(1, transportCount);
			Assert.Equal(1, combatCount);
			Assert.Equal(new List<int>(2)
			{
				100,
				runRecord.ChargePowerSnapshot[1]
			}, runRecord.ChargePowerSnapshot);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanOperation_FinishesWhenPowerIsNotEnough()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ChargePlanConfig config = new ChargePlanConfig
			{
				PlanList = new List<ChargePlanItem>(1)
				{
					new ChargePlanItem
					{
						CategoryName = "区域巡防",
						MissionTypeName = "定期清剿",
						RunTimes = 0,
						PlanTimes = 1
					}
				}
			};
			ChargePlanOperation operation = new ChargePlanOperation(context, config, new ChargePlanRunRecord(), resourceReader: (ZContext _) => new ChargePlanResourceReading(20, 0, 0));
			operation.StartChargePlan();
			operation.CheckBatteryCharge();
			OperationRoundResult select = operation.FindNextPlan();
			Assert.True(select.IsSuccess);
			OperationRoundResult check = operation.CheckBeforeTransport();
			Assert.True(check.IsSuccess);
			Assert.Equal(ChargePlanOperation.StatusRoundFinished, check.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanOperation_ReadResourcesCropsThreeFixedFieldsFromResourceBar()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			WriteChargePlanScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			SizeAwareSingleLineMatcher sizeAwareSingleLineMatcher = new SizeAwareSingleLineMatcher((int width, int height) => (width, height) switch
			{
				(150, 64) => "100",
				(135, 64) => "2400",
				(110, 64) => "300",
				_ => "999",
			});
			zContext.OcrService.Matcher = sizeAwareSingleLineMatcher;
			ChargePlanOperation chargePlanOperation = new ChargePlanOperation(zContext, new ChargePlanConfig(), new ChargePlanRunRecord());
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			ChargePlanResourceReading? actual = chargePlanOperation.ReadResourcesForTesting(screen);
			Assert.NotNull(actual);
			Assert.Equal(100, actual.BatteryCharge);
			Assert.Equal(2400, actual.BackupBatteryCharge);
			Assert.Equal(300, actual.EtherBattery);
			Assert.Equal((110, 64), sizeAwareSingleLineMatcher.LastSize);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanOperation_BatteryCheckRetriesWhenAnyFieldMissingAndChecksDoubleRewardOnce()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ChargePlanConfig config = new ChargePlanConfig
			{
				DoubleReward = true
			};
			ChargePlanResourceReading? reading = null;
			ChargePlanOperation operation = new ChargePlanOperation(context, config, new ChargePlanRunRecord(), resourceReader: (ZContext _) => reading);
			operation.StartChargePlan();
			OperationRoundResult missing = operation.CheckBatteryCharge();
			Assert.Equal(OperationRoundResultKind.Retry, missing.Kind);
			Assert.Equal("未识别到电量", missing.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), missing.Delay);
			reading = new ChargePlanResourceReading(100, 2400, 300);
			OperationRoundResult first = operation.CheckBatteryCharge();
			Assert.True(first.IsSuccess);
			Assert.Equal("查看双倍活动", first.Status);
			OperationRoundResult second = operation.CheckBatteryCharge();
			Assert.True(second.IsSuccess);
			Assert.Equal("查找候选计划", second.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanOperation_RestoreCoverageAllowsPlanWhenBackupOrEtherCoversDeficit()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ChargePlanConfig config = new ChargePlanConfig
			{
				RestoreCharge = RestoreChargeMode.BackupOnly.DisplayName,
				PlanList = new List<ChargePlanItem>(1)
				{
					new ChargePlanItem
					{
						CategoryName = "区域巡防",
						MissionTypeName = "定期清剿",
						RunTimes = 0,
						PlanTimes = 1
					}
				}
			};
			ChargePlanOperation operation = new ChargePlanOperation(context, config, new ChargePlanRunRecord(), resourceReader: (ZContext _) => new ChargePlanResourceReading(20, 100, 0));
			operation.StartChargePlan();
			operation.CheckBatteryCharge();
			Assert.True(operation.FindNextPlan().IsSuccess);
			OperationRoundResult backupCheck = operation.CheckBeforeTransport();
			Assert.True(backupCheck.IsSuccess);
			Assert.True(string.IsNullOrEmpty(backupCheck.Status));
			config.RestoreCharge = RestoreChargeMode.EtherOnly.DisplayName;
			ChargePlanOperation etherOperation = new ChargePlanOperation(context, config, new ChargePlanRunRecord(), resourceReader: (ZContext _) => new ChargePlanResourceReading(20, 0, 1));
			etherOperation.StartChargePlan();
			etherOperation.CheckBatteryCharge();
			Assert.True(etherOperation.FindNextPlan().IsSuccess);
			OperationRoundResult etherCheck = etherOperation.CheckBeforeTransport();
			Assert.True(etherCheck.IsSuccess);
			Assert.True(string.IsNullOrEmpty(etherCheck.Status));
			config.RestoreCharge = RestoreChargeMode.None.DisplayName;
			config.SkipPlan = true;
			ChargePlanOperation skipOperation = new ChargePlanOperation(context, config, new ChargePlanRunRecord(), resourceReader: (ZContext _) => new ChargePlanResourceReading(20, 0, 0));
			skipOperation.StartChargePlan();
			skipOperation.CheckBatteryCharge();
			Assert.True(skipOperation.FindNextPlan().IsSuccess);
			OperationRoundResult skipCheck = skipOperation.CheckBeforeTransport();
			Assert.True(skipCheck.IsSuccess);
			Assert.Equal(ChargePlanOperation.StatusFindNextPlan, skipCheck.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanOperation_ReadDoubleRewardTimesUsesQuickCompendiumCrop()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			WriteChargePlanScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			SizeAwareSingleLineMatcher sizeAwareSingleLineMatcher = new SizeAwareSingleLineMatcher((int width, int height) => (width == 40 && height == 20) ? "25" : "999");
			zContext.OcrService.Matcher = sizeAwareSingleLineMatcher;
			ChargePlanOperation chargePlanOperation = new ChargePlanOperation(zContext, new ChargePlanConfig(), new ChargePlanRunRecord());
			using Mat screen = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
			ChargePlanDoubleRewardOcrResult chargePlanDoubleRewardOcrResult = chargePlanOperation.ReadDoubleRewardTimesLeftForTesting(screen);
			Assert.Equal(ChargePlanDoubleRewardOcrResultKind.Activity, chargePlanDoubleRewardOcrResult.Kind);
			Assert.Equal(2, chargePlanDoubleRewardOcrResult.TimesLeft);
			Assert.Equal((40, 20), sizeAwareSingleLineMatcher.LastSize);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChargePlanOperation_DoubleRewardTimesOcrRetriesWhenParsedTimesIsImpossible()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			WriteChargePlanScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new SizeAwareSingleLineMatcher((int width, int height) => (width == 40 && height == 20) ? "65" : "999");
			ChargePlanOperation chargePlanOperation = new ChargePlanOperation(zContext, new ChargePlanConfig(), new ChargePlanRunRecord());
			using Mat screen = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
			ChargePlanDoubleRewardOcrResult chargePlanDoubleRewardOcrResult = chargePlanOperation.ReadDoubleRewardTimesLeftForTesting(screen);
			Assert.Equal(ChargePlanDoubleRewardOcrResultKind.Retry, chargePlanDoubleRewardOcrResult.Kind);
			Assert.Equal("双倍活动识别出错", chargePlanDoubleRewardOcrResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task ChargePlanOperation_DoubleRewardTransportFailureReturnsFail()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			ChargePlanOperation operation = new ChargePlanOperation(context, new ChargePlanConfig(), new ChargePlanRunRecord(), doubleRewardTransportAsync: (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: false, "传送失败")));
			OperationRoundResult result = await operation.CheckDoubleRewardEvent().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsFail);
			Assert.Equal("传送失败", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ChargePlanOperation_DoubleRewardOcrFailureReturnsRetry()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			ChargePlanOperation operation = new ChargePlanOperation(context, new ChargePlanConfig(), new ChargePlanRunRecord(), doubleRewardPlanAsync: (ZContext _, int _) => Task.FromResult(ChargePlanDoubleRewardResult.Retry("双倍活动识别出错")));
			OperationRoundResult result = await operation.CheckDoubleRewardEvent().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Retry, result.Kind);
			Assert.Equal("双倍活动识别出错", result.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), result.Delay);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteChargePlanScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: compendium_train\n  screen_name: \"快捷手册\"\n  area_list:\n    - area_name: \"怪物卡双倍剩余次数\"\n      pc_rect: [30, 50, 70, 70]\n    - area_name: \"资源栏\"\n      pc_rect: [1220, 35, 1770, 110]\n      color_range: [[208, 208, 208], [255, 255, 255]]");
	}
}
