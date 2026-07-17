using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class WitheredDomainAppTests
{
	private sealed class RecordingWitheredDomainRunner : IWitheredDomainRunner
	{
		private readonly Action<WitheredDomainRunRecord>? _afterRun;

		public int RunCount { get; private set; }

		public RecordingWitheredDomainRunner(Action<WitheredDomainRunRecord>? afterRun = null)
		{
			_afterRun = afterRun;
		}

		public Task<OperationResult> RunAsync(ZContext context, WitheredDomainConfig config, WitheredDomainRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			_afterRun?.Invoke(runRecord);
			return Task.FromResult(new OperationResult(IsSuccess: true, "枯萎之都完成"));
		}
	}

	private sealed class ScriptedWitheredDomainActions : IWitheredDomainAppActions
	{
		public string FirstScreenStatus { get; init; } = "零号空洞-入口";

		public string ChooseMissionTypeStatus { get; init; } = "下一步";

		public Func<WitheredDomainRunRecord, string>? ChooseMissionTypeStatusResolver { get; init; }

		public Queue<OperationResult> ClickNextResults { get; } = new Queue<OperationResult>();

		public OperationResult FinishResult { get; init; } = new OperationResult(IsSuccess: true, "返回大世界");

		public List<string> Calls { get; } = new List<string>();

		public Task<OperationResult> CheckFirstScreenAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("CheckFirstScreen");
			return Task.FromResult(new OperationResult(IsSuccess: true, FirstScreenStatus));
		}

		public Task<OperationResult> TransportToEntryAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("TransportToEntry");
			return Task.FromResult(new OperationResult(IsSuccess: true, "前往零号空洞-入口"));
		}

		public Task<OperationResult> WaitEntryLoadingAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("WaitEntryLoading");
			return Task.FromResult(new OperationResult(IsSuccess: true, "街区"));
		}

		public Task<OperationResult> ChooseMissionTypeAsync(ZContext context, WitheredDomainRunRecord runRecord, string missionTypeName, CancellationToken cancellationToken)
		{
			Calls.Add("ChooseMissionType:" + missionTypeName);
			string status = ChooseMissionTypeStatusResolver?.Invoke(runRecord) ?? ChooseMissionTypeStatus;
			return Task.FromResult(new OperationResult(IsSuccess: true, status));
		}

		public Task<OperationResult> ChooseMissionAsync(ZContext context, string missionName, CancellationToken cancellationToken)
		{
			Calls.Add("ChooseMission:" + missionName);
			return Task.FromResult(new OperationResult(IsSuccess: true, missionName));
		}

		public Task<OperationResult> ClickNextAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("ClickNext");
			return Task.FromResult((ClickNextResults.Count == 0) ? new OperationResult(IsSuccess: true, "出战") : ClickNextResults.Dequeue());
		}

		public Task<OperationResult> DeployAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("Deploy");
			return Task.FromResult(new OperationResult(IsSuccess: true, "出战"));
		}

		public Task<OperationResult> WaitBackLoadingAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("WaitBackLoading");
			return Task.FromResult(new OperationResult(IsSuccess: true, "街区"));
		}

		public Task<OperationResult> FinishAsync(ZContext context, CancellationToken cancellationToken)
		{
			Calls.Add("Finish");
			return Task.FromResult(FinishResult);
		}
	}

	private sealed class ScriptedHollowEventSource : IHollowEventSource
	{
		private readonly Queue<string> _events;

		private readonly Func<Mat?>? _screenProvider;

		public ScriptedHollowEventSource(params string[] events)
			: this(null, events)
		{
		}

		public ScriptedHollowEventSource(Func<Mat?>? screenProvider, params string[] events)
		{
			_events = new Queue<string>(events);
			_screenProvider = screenProvider;
		}

		public Task<HollowEventDetection?> DetectAsync(CancellationToken cancellationToken)
		{
			if (_events.Count == 0)
			{
				return Task.FromResult<HollowEventDetection>(null);
			}
			return Task.FromResult(new HollowEventDetection(_events.Dequeue(), 0.99, DateTimeOffset.UtcNow, (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0, _screenProvider?.Invoke()));
		}
	}

	private sealed class MissionCompletedDispatcher : IHollowEventDispatcher
	{
		public Task<HollowEventHandleResult> DispatchAsync(string eventName, CancellationToken cancellationToken)
		{
			return Task.FromResult(new HollowEventHandleResult(eventName, HollowEventOutcomeKind.MissionCompleted, Success: true));
		}
	}

	private sealed class ScriptedHollowMapSource : IHollowMapSource
	{
		private readonly HollowZeroMap? _map;

		public int Calls { get; private set; }

		public ScriptedHollowMapSource(HollowZeroMap? map)
		{
			_map = map;
		}

		public Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken)
		{
			Calls++;
			return Task.FromResult(_map);
		}
	}

	private sealed class ScriptedHollowMapNavigator(HollowMapMoveResult result) : IHollowMapNavigator
	{
		public HollowMapMoveResult? MoveNext(HollowZeroMap map, Mat? screen)
		{
			return result;
		}
	}

	private sealed class RecordingHollowMapNavigator : IHollowMapNavigator
	{
		public HollowMapMoveResult? MoveNext(HollowZeroMap map, Mat? screen)
		{
			HollowZeroMapNode hollowZeroMapNode = map.Nodes.First((HollowZeroMapNode node) => !node.Entry.EntryName.Contains("当前", StringComparison.Ordinal));
			return new HollowMapMoveResult(hollowZeroMapNode, hollowZeroMapNode.Pos.Center, Clicked: true);
		}
	}

	[Fact]
	public async Task App_PauseResumeAndStop_GateAutoBattleLifecycle()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			string autoBattleDirectory = Path.Combine(rootDirectory, "config", "auto_battle");
			Directory.CreateDirectory(autoBattleDirectory);
			File.WriteAllText(Path.Combine(autoBattleDirectory, "生命周期.yml"), "scenes: []");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			context.AutoBattleContext.InitAutoOp("生命周期");
			context.AutoBattleContext.StartAutoBattle();
			WitheredDomainConfig config = new WitheredDomainConfig();
			WitheredDomainApp app = new WitheredDomainApp(context, config, new WitheredDomainRunRecord(config));
			await app.OnPauseAsync(CancellationToken.None);
			Assert.False(context.AutoBattleContext.IsRuntimeRunning);
			await app.OnResumeAsync(CancellationToken.None);
			Assert.True(context.AutoBattleContext.IsRuntimeRunning);
			await app.OnStopAsync(CancellationToken.None);
			Assert.False(context.AutoBattleContext.IsRuntimeRunning);
			await app.OnResumeAsync(CancellationToken.None);
			Assert.False(context.AutoBattleContext.IsRuntimeRunning);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void EventDataService_LoadsPythonNormalEventOptionsAndWaits()
	{
		string text = CreateTempRoot();
		try
		{
			string[] buffer = new string[5];
			buffer[0] = text;
			buffer[1] = "assets";
			buffer[2] = "game_data";
			buffer[3] = "hollow_zero";
			buffer[4] = "normal_event";
			string text2 = Path.Combine(buffer);
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "事件.yml"), "- entry_name: \"测试入口\"\n  event_name: \"测试事件\"\n  lcs_percent: 0.6\n  options:\n    - option_name: \"优先选项\"\n      ocr_word: \"OCR选项\"\n      wait: 1.5\n      lcs_percent: 0.8");
			string[] buffer2 = new string[5];
			buffer2[0] = text;
			buffer2[1] = "assets";
			buffer2[2] = "game_data";
			buffer2[3] = "hollow_zero";
			buffer2[4] = "resonium.yml";
			File.WriteAllText(Path.Combine(buffer2), "- category: \"强攻\"\n  name: \"战术交流\"\n  level: \"S\"");
			WitheredDomainEventDataService witheredDomainEventDataService = new WitheredDomainEventDataService(new OneDragonEnvironment(text));
			HollowZeroEvent normalEventByName = witheredDomainEventDataService.GetNormalEventByName("测试事件");
			Assert.NotNull(normalEventByName);
			Assert.Equal("测试入口", normalEventByName.EntryName);
			Assert.Equal(0.6f, normalEventByName.LcsPercent);
			HollowZeroNormalEventOption hollowZeroNormalEventOption = Assert.Single(normalEventByName.Options);
			Assert.Equal("优先选项", hollowZeroNormalEventOption.OptionName);
			Assert.Equal("OCR选项", hollowZeroNormalEventOption.OcrWord);
			Assert.Equal(1.5f, hollowZeroNormalEventOption.Wait);
			Assert.Equal(0.8f, hollowZeroNormalEventOption.LcsPercent);
			WitheredDomainResonium witheredDomainResonium = witheredDomainEventDataService.MatchResoniumByOcrFull("[强攻]战术交流");
			Assert.NotNull(witheredDomainResonium);
			Assert.Equal("强攻", witheredDomainResonium.Category);
			Assert.Equal("S", witheredDomainResonium.Level);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ResoniumPriority_UsesPythonLevelAndCategoryOrder()
	{
		IReadOnlyList<int> actual = WitheredDomainEventOperations.OrderResoniumByPythonPriority(new WitheredDomainResonium[3]
		{
			new WitheredDomainResonium
			{
				Category = "强攻",
				Name = "A级",
				Level = "A"
			},
			new WitheredDomainResonium
			{
				Category = "防护",
				Name = "S级",
				Level = "S"
			},
			new WitheredDomainResonium
			{
				Category = "强攻",
				Name = "S级",
				Level = "S"
			}
		}, new string[] { "强攻" }, onlyPriority: false);
		Assert.Equal(new int[3] { 2, 0, 1 }, actual);
		Assert.Equal(new int[] { 0 }, WitheredDomainEventOperations.OrderResoniumByPythonPriority(new WitheredDomainResonium[2]
		{
			new WitheredDomainResonium
			{
				Category = "强攻",
				Name = "S级",
				Level = "S"
			},
			new WitheredDomainResonium
			{
				Category = "防护",
				Name = "S级",
				Level = "S"
			}
		}, new string[] { "强攻" }, onlyPriority: true));
	}

	[Theory]
	[InlineData(new object[] { "0", 0 })]
	[InlineData(new object[] { "O", 0 })]
	[InlineData(new object[] { "。", 0 })]
	[InlineData(new object[] { "o12", 12 })]
	public void MerchantFallbackPrice_UsesPythonZeroNormalization(string rawText, int expected)
	{
		Assert.Equal(expected, WitheredDomainEventOperations.ParseMerchantFallbackPrice(rawText));
	}

	[Fact]
	public void Context_LoadsSelectedChallengeYamlAndUsesItsRouteAndBattleValues()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "hollow_zero_challenge");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "实战配置.yml"), "auto_battle: 枯萎实战\ntarget_agents:\n  - 安比\n  - 妮可\n  - ''\npath_finding: 自定义\ngo_in_1_step:\n  - 守门人\nwaypoint:\n  - 邦布商人\navoid:\n  - 危机\nbuy_only_priority: false");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.WitheredDomain.InitBeforeRun("实战配置");
			Assert.Equal("实战配置", zContext.WitheredDomain.ChallengeConfigName);
			Assert.Equal("枯萎实战", zContext.WitheredDomain.GetAutoBattleName());
			Assert.Equal(new string[3]
			{
				"安比",
				"妮可",
				string.Empty
			}, zContext.WitheredDomain.GetTargetAgents());
			Assert.Equal(new string[] { "守门人" }, zContext.WitheredDomain.GetGoInOneStep());
			Assert.Equal(new string[] { "邦布商人" }, zContext.WitheredDomain.GetWaypoint());
			Assert.Equal(new string[] { "危机" }, zContext.WitheredDomain.GetAvoid());
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Context_RequiresTheSelectedChallengeYaml()
	{
		string text = CreateTempRoot();
		try
		{
			ZContext context = new ZContext(new OneDragonEnvironment(text));
			try
			{
				InvalidOperationException ex = Assert.Throws<InvalidOperationException>(delegate
				{
					context.WitheredDomain.InitBeforeRun();
				});
				FileNotFoundException ex2 = Assert.Throws<FileNotFoundException>(delegate
				{
					context.WitheredDomain.InitBeforeRun("不存在的挑战");
				});
				Assert.Equal("枯萎之都未选择挑战配置。", ex.Message);
				Assert.Contains("不存在的挑战", ex2.Message, StringComparison.Ordinal);
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_ReportsPythonEquivalentWeekAndDayStatus()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 8, 1, 0, 0, TimeSpan.Zero);
		WitheredDomainConfig config = new WitheredDomainConfig
		{
			WeeklyPlanTimes = 2,
			DailyPlanTimes = 9,
			ExtraTask = "不进行"
		};
		WitheredDomainRunRecord witheredDomainRunRecord = new WitheredDomainRunRecord(config, 0, () => now)
		{
			Dt = "20260707",
			WeeklyRunTimes = 2,
			DailyRunTimes = 1
		};
		Assert.Equal(1, witheredDomainRunRecord.RunStatusUnderNow);
		Assert.True(witheredDomainRunRecord.IsDone);
		now = now.AddDays(7.0);
		Assert.Equal(0, witheredDomainRunRecord.RunStatusUnderNow);
		Assert.False(witheredDomainRunRecord.IsDone);
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesWitheredDomainApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			WitheredDomainAppFactory witheredDomainAppFactory = zContext.ApplicationFactoryRegistry.CreateWitheredDomainFactory();
			IApplication application = witheredDomainAppFactory.CreateApplication(0, "default");
			IApplicationConfig config = witheredDomainAppFactory.GetConfig(0, "default");
			IApplicationRunRecord runRecord = witheredDomainAppFactory.GetRunRecord(0);
			Assert.Equal("withered_domain", witheredDomainAppFactory.AppId);
			Assert.Equal("枯萎之都", witheredDomainAppFactory.AppName);
			Assert.Equal("default", witheredDomainAppFactory.GroupId);
			Assert.True(witheredDomainAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<WitheredDomainApp>(application);
			Assert.IsType<WitheredDomainConfig>(config);
			WitheredDomainRunRecord witheredDomainRunRecord = Assert.IsType<WitheredDomainRunRecord>(runRecord);
			Assert.Equal("withered_domain", witheredDomainRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersWitheredDomainAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterWitheredDomainApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("withered_domain"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("withered_domain"));
			Assert.Contains("withered_domain", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
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
			string text2 = Path.Combine(text, "config", "00", "default");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "withered_domain.yml"), "mission_name: 施工废墟-核心\nchallenge_config: 自定义挑战\nweekly_plan_times: 4\ndaily_plan_times: 3\nextra_task: 刷满业绩点\nextra_exit: 2层业绩后退出");
			WitheredDomainConfig witheredDomainConfig = WitheredDomainConfig.Load(new OneDragonEnvironment(text), 0, "default");
			Assert.Equal("withered_domain", witheredDomainConfig.AppId);
			Assert.Equal(0, witheredDomainConfig.InstanceIndex);
			Assert.Equal("default", witheredDomainConfig.GroupId);
			Assert.Equal("施工废墟-核心", witheredDomainConfig.MissionName);
			Assert.Equal("自定义挑战", witheredDomainConfig.ChallengeConfig);
			Assert.Equal(4, witheredDomainConfig.WeeklyPlanTimes);
			Assert.Equal(3, witheredDomainConfig.DailyPlanTimes);
			Assert.Equal("刷满业绩点", witheredDomainConfig.ExtraTask);
			Assert.Equal("2层业绩后退出", witheredDomainConfig.ExtraExit);
			Assert.Equal("INTERFACE", "INTERFACE");
			Assert.Contains((IEnumerable<WitheredDomainSettingField>)WitheredDomainSettings.Fields, (Predicate<WitheredDomainSettingField>)((WitheredDomainSettingField field) => field.Key == "mission_name" && field.DefaultValue.Equals("旧都列车-内部")));
			Assert.Contains((IEnumerable<WitheredDomainSettingField>)WitheredDomainSettings.Fields, (Predicate<WitheredDomainSettingField>)((WitheredDomainSettingField field) => field.Key == "extra_task" && field.Options.Any((ConfigItem option) => object.Equals(option.Value, "刷满周期奖励"))));
			Assert.Contains((IEnumerable<WitheredDomainSettingField>)WitheredDomainSettings.Fields, (Predicate<WitheredDomainSettingField>)((WitheredDomainSettingField field) => field.Key == "extra_exit" && field.Options.Any((ConfigItem option) => object.Equals(option.Value, "3层业绩后退出"))));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_TracksWeeklyDailyAndExtraTaskCompletion()
	{
		string text = CreateTempRoot();
		try
		{
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
			WitheredDomainConfig config = new WitheredDomainConfig
			{
				WeeklyPlanTimes = 2,
				DailyPlanTimes = 3,
				ExtraTask = "刷满周期奖励"
			};
			WitheredDomainRunRecord witheredDomainRunRecord = WitheredDomainRunRecord.Load(new OneDragonEnvironment(text), config, 0, 4, () => now);
			witheredDomainRunRecord.AddTimes();
			witheredDomainRunRecord.AddTimes();
			witheredDomainRunRecord.AddDailyTimes();
			Assert.True(witheredDomainRunRecord.IsFinishedByWeeklyTimes());
			Assert.False(witheredDomainRunRecord.IsFinishedByWeek());
			Assert.False(witheredDomainRunRecord.IsFinishedByDay());
			witheredDomainRunRecord.PeriodRewardComplete = true;
			Assert.True(witheredDomainRunRecord.IsFinishedByWeek());
			Assert.True(witheredDomainRunRecord.IsFinishedByDay());
			witheredDomainRunRecord.AddDailyTimes();
			witheredDomainRunRecord.AddDailyTimes();
			Assert.True(witheredDomainRunRecord.IsFinishedByDay());
			WitheredDomainRunRecord witheredDomainRunRecord2 = WitheredDomainRunRecord.Load(new OneDragonEnvironment(text), config, 0, 4, () => now);
			Assert.Equal(2, witheredDomainRunRecord2.WeeklyRunTimes);
			Assert.Equal(3, witheredDomainRunRecord2.DailyRunTimes);
			Assert.True(witheredDomainRunRecord2.PeriodRewardComplete);
			now = now.AddDays(7.0);
			witheredDomainRunRecord2.CheckAndUpdateStatus();
			Assert.Equal(0, witheredDomainRunRecord2.WeeklyRunTimes);
			Assert.Equal(0, witheredDomainRunRecord2.DailyRunTimes);
			Assert.False(witheredDomainRunRecord2.PeriodRewardComplete);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ExtraTaskEvaluator_UsesPythonExitRulesAndPersistsEmptyEvaPoint()
	{
		string text = CreateTempRoot();
		try
		{
			WitheredDomainConfig config = new WitheredDomainConfig
			{
				WeeklyPlanTimes = 2,
				DailyPlanTimes = 9,
				ExtraTask = "刷满业绩点",
				ExtraExit = "2层业绩后退出"
			};
			WitheredDomainRunRecord witheredDomainRunRecord = WitheredDomainRunRecord.Load(new OneDragonEnvironment(text), config, 0);
			witheredDomainRunRecord.WeeklyRunTimes = 2;
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.WitheredDomain.InitLevelInfo("旧都列车", "旧都列车-核心", 2);
			HollowZeroMap currentMap = CreateMap("业绩考察点空");
			Assert.True(WitheredDomainExtraTaskEvaluator.ShouldLeave(config, witheredDomainRunRecord, zContext.WitheredDomain, currentMap));
			Assert.True(witheredDomainRunRecord.NoEvalPoint);
			WitheredDomainRunRecord witheredDomainRunRecord2 = WitheredDomainRunRecord.Load(new OneDragonEnvironment(text), config, 0);
			Assert.True(witheredDomainRunRecord2.NoEvalPoint);
			WitheredDomainConfig config2 = new WitheredDomainConfig
			{
				WeeklyPlanTimes = 2,
				DailyPlanTimes = 9,
				ExtraTask = "刷满周期奖励",
				ExtraExit = "通关"
			};
			WitheredDomainRunRecord runRecord = new WitheredDomainRunRecord(config2)
			{
				WeeklyRunTimes = 2
			};
			zContext.WitheredDomain.InitLevelInfo("旧都列车", "旧都列车-核心", 3, 2);
			Assert.False(WitheredDomainExtraTaskEvaluator.ShouldLeave(config2, runRecord, zContext.WitheredDomain, CreateMap("空白未通行")));
			WitheredDomainConfig config3 = new WitheredDomainConfig
			{
				WeeklyPlanTimes = 2,
				DailyPlanTimes = 9,
				ExtraTask = "刷满周期奖励",
				ExtraExit = "3层业绩后退出"
			};
			WitheredDomainRunRecord runRecord2 = new WitheredDomainRunRecord(config3)
			{
				WeeklyRunTimes = 2
			};
			zContext.WitheredDomain.InitLevelInfo("旧都列车", "旧都列车-核心", 3, 2);
			Assert.True(WitheredDomainExtraTaskEvaluator.ShouldLeave(config3, runRecord2, zContext.WitheredDomain, CreateMap("空白未通行")));
			WitheredDomainRunRecord runRecord3 = new WitheredDomainRunRecord(config)
			{
				WeeklyRunTimes = 1
			};
			Assert.False(WitheredDomainExtraTaskEvaluator.ShouldLeave(config, runRecord3, zContext.WitheredDomain, currentMap));
			WitheredDomainConfig config4 = new WitheredDomainConfig
			{
				WeeklyPlanTimes = 9,
				DailyPlanTimes = 1,
				ExtraTask = "不进行"
			};
			WitheredDomainRunRecord runRecord4 = new WitheredDomainRunRecord(config4)
			{
				DailyRunTimes = 1
			};
			Assert.True(WitheredDomainExtraTaskEvaluator.ShouldLeave(config4, runRecord4, zContext.WitheredDomain, CreateMap("空白未通行")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task App_RunsEntryFlowDeploysAndInitializesWitheredContext()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteChallengeConfig(rootDirectory, "自定义挑战");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			WitheredDomainConfig config = new WitheredDomainConfig
			{
				MissionName = "施工废墟-核心",
				ChallengeConfig = "自定义挑战",
				WeeklyPlanTimes = 5,
				DailyPlanTimes = 1,
				ExtraTask = "不进行"
			};
			WitheredDomainRunRecord runRecord = new WitheredDomainRunRecord(config);
			RecordingWitheredDomainRunner runner = new RecordingWitheredDomainRunner(delegate(WitheredDomainRunRecord record)
			{
				record.AddDailyTimes();
			});
			ScriptedWitheredDomainActions actions = new ScriptedWitheredDomainActions
			{
				FirstScreenStatus = "可前往快捷手册",
				ChooseMissionTypeStatus = "施工废墟",
				ChooseMissionTypeStatusResolver = (WitheredDomainRunRecord record) => record.IsFinishedByDay() ? "已完成基本次数" : "施工废墟"
			};
			actions.ClickNextResults.Enqueue(new OperationResult(IsSuccess: true, "出战"));
			WitheredDomainApp app = new WitheredDomainApp(context, config, runRecord, new OperationWitheredDomainAppFlow(runner, actions));
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("返回大世界", result.Status);
			Assert.Equal<List<string>>(new List<string>(11)
			{
				"CheckFirstScreen", "TransportToEntry", "WaitEntryLoading", "ChooseMissionType:施工废墟", "ChooseMission:施工废墟-核心", "ClickNext", "Deploy", "WaitEntryLoading", "ChooseMissionType:施工废墟", "WaitBackLoading",
				"Finish"
			}, actions.Calls);
			Assert.Equal(1, runner.RunCount);
			Assert.Equal("自定义挑战", context.WitheredDomain.ChallengeConfigName);
			Assert.Equal("施工废墟", context.WitheredDomain.LevelInfo.MissionTypeName);
			Assert.Equal("施工废墟-核心", context.WitheredDomain.LevelInfo.MissionName);
			Assert.Equal(0, runRecord.WeeklyRunTimes);
			Assert.Equal(1, runRecord.DailyRunTimes);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task App_ContinuesExistingHollowWithUnknownLevelAndPhase()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteChallengeConfig(rootDirectory, "默认-专属空洞-艾莲");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			WitheredDomainConfig config = new WitheredDomainConfig
			{
				MissionName = "旧都列车-内部",
				DailyPlanTimes = 1,
				ExtraTask = "不进行"
			};
			WitheredDomainRunRecord runRecord = new WitheredDomainRunRecord(config);
			RecordingWitheredDomainRunner runner = new RecordingWitheredDomainRunner(delegate(WitheredDomainRunRecord record)
			{
				record.AddDailyTimes();
			});
			ScriptedWitheredDomainActions actions = new ScriptedWitheredDomainActions
			{
				FirstScreenStatus = "在空洞内",
				ChooseMissionTypeStatusResolver = (WitheredDomainRunRecord record) => record.IsFinishedByDay() ? "已完成基本次数" : "下一步"
			};
			OperationWitheredDomainAppFlow flow = new OperationWitheredDomainAppFlow(runner, actions);
			Assert.True((await flow.RunAsync(context, config, runRecord, CancellationToken.None)).IsSuccess);
			Assert.Equal<List<string>>(new List<string>(5) { "CheckFirstScreen", "WaitEntryLoading", "ChooseMissionType:旧都列车", "WaitBackLoading", "Finish" }, actions.Calls);
			Assert.Equal(1, runner.RunCount);
			Assert.Equal("旧都列车", context.WitheredDomain.LevelInfo.MissionTypeName);
			Assert.Equal("旧都列车-内部", context.WitheredDomain.LevelInfo.MissionName);
			Assert.Equal(-1, context.WitheredDomain.LevelInfo.Level);
			Assert.Equal(-1, context.WitheredDomain.LevelInfo.Phase);
			Assert.Equal(0, runRecord.WeeklyRunTimes);
			Assert.Equal(1, runRecord.DailyRunTimes);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task App_FinishedTimesWaitsEntryAndReturnsToWorldWithoutRunner()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteChallengeConfig(rootDirectory, "默认-专属空洞-艾莲");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			WitheredDomainConfig config = new WitheredDomainConfig
			{
				WeeklyPlanTimes = 0,
				DailyPlanTimes = 99,
				ExtraTask = "不进行"
			};
			WitheredDomainRunRecord runRecord = new WitheredDomainRunRecord(config);
			RecordingWitheredDomainRunner runner = new RecordingWitheredDomainRunner();
			ScriptedWitheredDomainActions actions = new ScriptedWitheredDomainActions
			{
				FirstScreenStatus = "零号空洞-入口",
				ChooseMissionTypeStatus = "已完成基本次数",
				FinishResult = new OperationResult(IsSuccess: true, "大世界-普通")
			};
			OperationWitheredDomainAppFlow flow = new OperationWitheredDomainAppFlow(runner, actions);
			OperationResult result = await flow.RunAsync(context, config, runRecord, CancellationToken.None);
			Assert.True(result.IsSuccess);
			Assert.Equal("大世界-普通", result.Status);
			Assert.Equal<List<string>>(new List<string>(5) { "CheckFirstScreen", "WaitEntryLoading", "ChooseMissionType:旧都列车", "WaitBackLoading", "Finish" }, actions.Calls);
			Assert.Equal(0, runner.RunCount);
			Assert.Equal(0, runRecord.WeeklyRunTimes);
			Assert.Equal(0, runRecord.DailyRunTimes);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task HollowRunnerWitheredDomainRunner_RetriesWhenDefaultScreenshotMapCannotDetect()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.AttachController(new ReadyController());
		using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100L));
		HollowRunnerWitheredDomainRunner runner = new HollowRunnerWitheredDomainRunner(new ScriptedHollowEventSource(HollowZeroSpecialEvent.HollowInside.EventName));
		OperationResult result = await runner.RunAsync(context, new WitheredDomainConfig(), new WitheredDomainRunRecord(new WitheredDomainConfig()), cancellation.Token).WaitAsync(TimeSpan.FromSeconds(2L));
		Assert.False(result.IsSuccess);
		Assert.Equal("枯萎之都已取消", result.Status);
	}

	[Fact]
	public async Task HollowRunnerWitheredDomainRunner_UsesInjectedMapSourceUntilMissionComplete()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.AttachController(new ReadyController());
		ScriptedHollowMapSource mapSource = new ScriptedHollowMapSource(CreateTwoNodeMap());
		HollowRunnerWitheredDomainRunner runner = new HollowRunnerWitheredDomainRunner(new ScriptedHollowEventSource(HollowZeroSpecialEvent.HollowInside.EventName, HollowZeroSpecialEvent.MissionComplete.EventName), mapSource, new ScriptedHollowMapNavigator(new HollowMapMoveResult(CreateTwoNodeMap().Nodes[1], new OneDragon.Core.Abstractions.Geometry.Point(120, 220), Clicked: true)), new MissionCompletedDispatcher());
		OperationResult result = await runner.RunAsync(context, new WitheredDomainConfig(), new WitheredDomainRunRecord(new WitheredDomainConfig()), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(4L));
		Assert.True(result.IsSuccess);
		Assert.Equal("枯萎之都完成", result.Status);
		Assert.Equal(1, mapSource.Calls);
	}

	[Fact]
	public async Task HollowRunnerWitheredDomainRunner_UsesScreenshotMapSourceUntilMissionComplete()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.AttachController(new ReadyController());
		HollowRunnerWitheredDomainRunner runner = new HollowRunnerWitheredDomainRunner(mapSource: new ColorCodedHollowMapSource(delegate
		{
			throw new InvalidOperationException("地图检测必须消费当前事件截图");
		}), eventSource: new ScriptedHollowEventSource(CreateColorCodedMapScreen, HollowZeroSpecialEvent.HollowInside.EventName, HollowZeroSpecialEvent.MissionComplete.EventName), mapNavigator: new RecordingHollowMapNavigator(), eventDispatcher: new MissionCompletedDispatcher());
		OperationResult result = await runner.RunAsync(context, new WitheredDomainConfig(), new WitheredDomainRunRecord(new WitheredDomainConfig()), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(4L));
		Assert.True(result.IsSuccess);
		Assert.Equal("枯萎之都完成", result.Status);
	}

	[Fact]
	public async Task HollowRunnerWitheredDomainRunner_ReturnsCanceledStatusWhenStopped()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.AttachController(new ReadyController());
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50L));
		HollowRunnerWitheredDomainRunner runner = new HollowRunnerWitheredDomainRunner(new ScriptedHollowEventSource(), new ScriptedHollowMapSource(CreateTwoNodeMap()));
		OperationResult result = await runner.RunAsync(context, new WitheredDomainConfig(), new WitheredDomainRunRecord(new WitheredDomainConfig()), cts.Token).WaitAsync(TimeSpan.FromSeconds(2L));
		Assert.False(result.IsSuccess);
		Assert.Equal("枯萎之都已取消", result.Status);
	}

	[Fact]
	public void HollowBattle_ConsumesPreviousBattleResultBeforeSubmittingNewDetection()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(new ReadyController());
		zContext.AutoBattleContext.LastCheckEndResult = "零号空洞-挑战结果";
		WitheredDomainHollowBattle witheredDomainHollowBattle = new WitheredDomainHollowBattle(zContext, new WitheredDomainRunRecord(new WitheredDomainConfig()));
		OperationRoundResult operationRoundResult = witheredDomainHollowBattle.AutoBattle();
		Assert.True(operationRoundResult.IsSuccess);
		Assert.Equal("零号空洞-挑战结果", operationRoundResult.Status);
		Assert.False(zContext.AutoBattleContext.IsRuntimeRunning);
	}

	[Fact]
	public void HollowBattle_UpdatesLevelAfterSettlement()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(new ReadyController());
		zContext.WitheredDomain.InitLevelInfo("旧都列车", "旧都列车-核心");
		WitheredDomainHollowBattle witheredDomainHollowBattle = new WitheredDomainHollowBattle(zContext, new WitheredDomainRunRecord(new WitheredDomainConfig()));
		OperationRoundResult operationRoundResult = witheredDomainHollowBattle.UpdateLevelInfo();
		Assert.True(operationRoundResult.IsSuccess);
		Assert.Equal(2, zContext.WitheredDomain.LevelInfo.Level);
		Assert.Equal(1, zContext.WitheredDomain.LevelInfo.Phase);
	}

	[Fact]
	public void HollowBattle_PauseStopsAutoBattleAndReleasesRuntime()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(new ReadyController());
		zContext.AutoBattleContext.StartContextAsync();
		WitheredDomainHollowBattle witheredDomainHollowBattle = new WitheredDomainHollowBattle(zContext, new WitheredDomainRunRecord(new WitheredDomainConfig()));
		witheredDomainHollowBattle.PauseAutoBattle();
		Assert.False(zContext.AutoBattleContext.IsRuntimeRunning);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteChallengeConfig(string rootDirectory, string moduleName)
	{
		string text = Path.Combine(rootDirectory, "config", "hollow_zero_challenge");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, moduleName + ".yml"), "auto_battle: 全配队通用\npath_finding: 默认");
	}

	private static HollowZeroMap CreateTwoNodeMap()
	{
		HollowZeroMapNode hollowZeroMapNode = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 40, 40), new HollowZeroEntry("0000-当前"));
		HollowZeroMapNode hollowZeroMapNode2 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(100, 200, 140, 240), new HollowZeroEntry("0001-目标"));
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

	private static HollowZeroMap CreateMap(string entryName)
	{
		HollowZeroMapNode hollowZeroMapNode = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 40, 40), new HollowZeroEntry("0000-当前"));
		HollowZeroMapNode hollowZeroMapNode2 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(100, 0, 140, 40), new HollowZeroEntry("0001-" + entryName));
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

	private static Mat CreateColorCodedMapScreen()
	{
		Mat mat = new Mat(300, 300, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(20, 20, 20, 20), new Scalar(0.0, 0.0, 255.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(120, 20, 20, 20), new Scalar(0.0, 255.0, 0.0), -1);
		return mat;
	}
}
