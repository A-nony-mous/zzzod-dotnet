using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OpenCvSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public class AutoBattleContextsTests
{
	private sealed class FakeTargetStateChecker : IAutoBattleTargetStateChecker
	{
		private readonly Queue<IReadOnlyList<TargetStateCheckResult>> _results = new Queue<IReadOnlyList<TargetStateCheckResult>>();

		public int RunCount { get; private set; }

		public TimeSpan Delay { get; init; }

		public void Enqueue(IReadOnlyList<TargetStateCheckResult> results)
		{
			_results.Enqueue(results);
		}

		public IReadOnlyList<TargetStateCheckResult> RunTask(object? screen, DetectionTask task)
		{
			RunCount++;
			if (Delay > TimeSpan.Zero)
			{
				Thread.Sleep(Delay);
			}
			IReadOnlyList<TargetStateCheckResult> result;
			if (_results.Count <= 0)
			{
				IReadOnlyList<TargetStateCheckResult> readOnlyList = Array.Empty<TargetStateCheckResult>();
				result = readOnlyList;
			}
			else
			{
				result = _results.Dequeue();
			}
			return result;
		}
	}

	private sealed class RecordingLogSink : ILogEventSink
	{
		public List<LogEvent> Events { get; } = new List<LogEvent>();

		public void Emit(LogEvent logEvent)
		{
			Events.Add(logEvent);
		}
	}

	private sealed class FixedTextOcrMatcher(string text) : IOcrMatcher
	{
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
			return text;
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			MatchResultList matchResultList = new MatchResultList(onlyBest: false);
			matchResultList.Append(new OcrMatchResult(1.0, 0, 0, 40, 20, text), autoMerge: false);
			return new Dictionary<string, MatchResultList>(StringComparer.Ordinal) { [text] = matchResultList };
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return new OcrMatchResult[] { new OcrMatchResult(1.0, 0, 0, 40, 20, text) };
		}
	}

	private sealed class FakeFlashClassifier : IAutoBattleFlashClassifier
	{
		private readonly int _classIndex;

		public int RunCount { get; private set; }

		public FakeFlashClassifier(int classIndex)
		{
			_classIndex = classIndex;
		}

		public AutoBattleFlashClassification Classify(object? screen)
		{
			RunCount++;
			return new AutoBattleFlashClassification(_classIndex, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0);
		}
	}

	private sealed class FakeDodgeAudioDetector : IAutoBattleDodgeAudioDetector
	{
		private readonly bool _result;

		public FakeDodgeAudioDetector(bool result)
		{
			_result = result;
		}

		public bool CheckAudio(double screenshotTime)
		{
			return _result;
		}

		public void ResetBattle()
		{
		}

		public void Start()
		{
		}

		public void Stop()
		{
		}
	}

	[Fact]
	public void Test_AutoBattleContext_FrameLeaseSharesPixelsAndSurvivesOwnerDispose()
	{
		Mat mat = new Mat(2, 3, MatType.CV_8UC1, new Scalar(7.0));
		using Mat mat2 = AutoBattleContext.CreateFrameLease(mat);
		Assert.Equal(mat.Data, mat2.Data);
		mat.Dispose();
		Assert.False(mat2.Empty());
		Assert.Equal((byte)7, mat2.At<byte>(0, 0));
	}

	[Fact]
	public void Test_AutoBattleAgentContext_Instantiation()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		Assert.NotNull(autoBattleAgentContext.Team);
		Assert.Empty(autoBattleAgentContext.Team.Agents);
		List<StateRecord> collection = autoBattleAgentContext.SwitchNextAgent(1.0);
		Assert.Empty(collection);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_MapsRealAgentTypesAndSwitchStates()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		List<StateRecord> collection = autoBattleAgentContext.SwitchNextAgent(10.0, updateState: false);
		Assert.Equal("妮可", autoBattleAgentContext.Team.Agents[0].Agent.AgentName);
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-妮可"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-支援"));
		Assert.DoesNotContain((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-强攻"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-1-比利"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-1-强攻"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-2-安比"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-2-击破"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-比利"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-安比"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "切换角色-妮可"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "切换角色-支援"));
	}

	[Fact]
	public void Test_AutoBattleAgentContext_SwitchPrevRotatesLikePython()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		List<StateRecord> collection = autoBattleAgentContext.SwitchPrevAgent(10.0, updateState: false);
		Assert.Equal("比利", autoBattleAgentContext.Team.Agents[0].Agent.AgentName);
		Assert.Equal("安比", autoBattleAgentContext.Team.Agents[1].Agent.AgentName);
		Assert.Equal("妮可", autoBattleAgentContext.Team.Agents[2].Agent.AgentName);
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-比利"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-1-安比"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-2-妮可"));
	}

	[Fact]
	public void Test_AutoBattleAgentContext_EmitsEnergyAndReadyStates()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value, 100, 60, specialReady: false, ultimateReady: true));
		List<StateRecord> agentStateRecords = autoBattleAgentContext.GetAgentStateRecords(1.0);
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "安比-能量" && record.Value == 60));
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-能量" && record.Value == 60));
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "按键可用-特殊攻击" && record.IsClear));
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "按键可用-终结技" && !record.IsClear));
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "安比-特殊技可用" && record.IsClear));
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "安比-终结技可用" && !record.IsClear));
	}

	[Fact]
	public void Test_AutoBattleAgentContext_SkipsNamedReadyStatesWithinSwitchGuardWindow()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value, 100, 0, specialReady: true, ultimateReady: true));
		List<StateRecord> collection = autoBattleAgentContext.SwitchNextAgent(10.0, updateState: false);
		List<StateRecord> agentStateRecords = autoBattleAgentContext.GetAgentStateRecords(10.199999809265137);
		Assert.DoesNotContain((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "妮可-特殊技可用"));
		Assert.DoesNotContain((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "妮可-终结技可用"));
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "妮可-特殊技可用"));
		Assert.Contains((IEnumerable<StateRecord>)agentStateRecords, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "妮可-终结技可用"));
	}

	[Fact]
	public void Test_AutoBattleAgentContext_UpdateStateWritesToRecordService()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		agentContext.SwitchNextAgent(10.0);
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("前台-妮可");
		StateRecorder stateRecorder2 = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("前台-支援");
		Assert.Equal(10.0, stateRecorder.LastRecordTime);
		Assert.Equal(10.0, stateRecorder2.LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_UpdateStateFalseDoesNotWriteToRecordService()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		agentContext.SwitchNextAgent(10.0, updateState: false);
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("前台-妮可");
		Assert.Equal(-1.0, stateRecorder.LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_NoneAgentsStayAtTailWhenSwitching()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(null));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		autoBattleAgentContext.SwitchNextAgent(10.0, updateState: false);
		Assert.Equal("妮可", autoBattleAgentContext.Team.Agents[0].Agent.AgentName);
		Assert.Equal("安比", autoBattleAgentContext.Team.Agents[1].Agent.AgentName);
		Assert.Null(autoBattleAgentContext.Team.Agents[2].Agent);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_SwitchByAgentNameUsesCurrentPosition()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		autoBattleAgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		(int, List<StateRecord>) tuple = autoBattleAgentContext.SwitchByAgentName("妮可", 10.0, updateState: false);
		(int, List<StateRecord>) tuple2 = autoBattleAgentContext.SwitchByAgentName("不存在", 11.0, updateState: false);
		Assert.Equal(2, tuple.Item1);
		Assert.NotEmpty(tuple.Item2);
		Assert.Equal("妮可", autoBattleAgentContext.Team.Agents[0].Agent.AgentName);
		Assert.Equal(0, tuple2.Item1);
		Assert.Empty(tuple2.Item2);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_SwitchQuickAssistUsesLatestAssistState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("快速支援-比利", 2.0));
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("快速支援-妮可", 5.0));
		(int, List<StateRecord>) tuple = agentContext.SwitchQuickAssist(10.0, updateState: false);
		Assert.Equal(2, tuple.Item1);
		Assert.Equal("妮可", agentContext.Team.Agents[0].Agent.AgentName);
		Assert.Contains((IEnumerable<StateRecord>)tuple.Item2, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-妮可"));
	}

	[Fact]
	public void Test_AutoBattleAgentContext_ChainLeftUsesBangbooPartner()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("连携技-1-邦布", 5.0));
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("连携技-2-比利", 6.0));
		List<StateRecord> collection = agentContext.ChainLeft(10.0, updateState: false);
		Assert.Equal("比利", agentContext.Team.Agents[0].Agent.AgentName);
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-比利"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "切换角色-比利"));
	}

	[Fact]
	public void Test_AutoBattleAgentContext_ChainSlotsWithoutAgentDoNotInventSwitchStates()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		agentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("连携技-1-邦布", 5.0));
		IReadOnlyList<StateRecord> collection = agentContext.ChainLeft(10.0, updateState: false);
		IReadOnlyList<StateRecord> collection2 = agentContext.ChainRight(10.0, updateState: false);
		Assert.Empty(collection);
		Assert.Empty(collection2);
		Assert.Equal("安比", agentContext.Team.Agents[0].Agent.AgentName);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_CheckAgentRelatedUsesBattleAvatarTemplates()
	{
		using ZContext zContext = CreateContextWithAssets();
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.InitBattleAgentContext();
		using Mat screen = CreateBlankScreen();
		PasteBattleTemplate(zContext, screen, "头像-3-1", "avatar_1_anby");
		PasteBattleTemplate(zContext, screen, "头像-3-2", "avatar_2_nicole");
		PasteBattleTemplate(zContext, screen, "头像-3-3", "avatar_2_billy");
		IReadOnlyList<StateRecord> collection = agentContext.CheckAgentRelated(screen, 1.0);
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "前台-安比"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-1-妮可"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "后台-2-比利"));
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("前台-安比").LastRecordTime);
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("后台-1-妮可").LastRecordTime);
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("后台-2-比利").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_ResetCheckAgentTimeForcesFreshFrameCheck()
	{
		using ZContext zContext = CreateContextWithAssets();
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.InitBattleAgentContext();
		using Mat screen = CreateBlankScreen();
		PasteBattleTemplate(zContext, screen, "头像-3-1", "avatar_1_anby");
		PasteBattleTemplate(zContext, screen, "头像-3-2", "avatar_2_nicole");
		PasteBattleTemplate(zContext, screen, "头像-3-3", "avatar_2_billy");
		agentContext.CheckAgentRelated(screen, 1.0);
		Assert.Empty(agentContext.CheckAgentRelated(screen, 1.1));
		agentContext.ResetCheckAgentTime();
		IReadOnlyList<StateRecord> collection = agentContext.CheckAgentRelated(screen, 1.1);
		Assert.NotEmpty(collection);
		Assert.Equal(1.1, agentContext.LastCheckAgentTime, 6);
	}

	[Fact]
	public void Test_AutoBattleAgentContext_LogsRecognizedTeamState()
	{
		RecordingLogSink recordingLogSink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(recordingLogSink).CreateLogger();
		ZContext zContext = new ZContext(new OneDragonEnvironment(FindRepoRoot()), logger);
		zContext.ScreenContext.Reload();
		AutoBattleAgentContext agentContext = zContext.AutoBattleContext.AgentContext;
		agentContext.InitBattleAgentContext();
		using Mat screen = CreateBlankScreen();
		PasteBattleTemplate(zContext, screen, "头像-3-1", "avatar_1_anby");
		PasteBattleTemplate(zContext, screen, "头像-3-2", "avatar_2_nicole");
		PasteBattleTemplate(zContext, screen, "头像-3-3", "avatar_2_billy");
		agentContext.CheckAgentRelated(screen, 12.345);
		Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text == "自动战斗角色状态: ScreenshotTime={ScreenshotTime:F3}, Team={Team}" && entry.Properties["ScreenshotTime"].ToString().Contains("12.345", StringComparison.Ordinal) && entry.Properties["Team"].ToString().Contains("安比", StringComparison.Ordinal) && entry.Properties["Team"].ToString().Contains("妮可", StringComparison.Ordinal) && entry.Properties["Team"].ToString().Contains("比利", StringComparison.Ordinal)));
	}

	[Fact]
	public void Test_AutoBattleContext_CheckQuickAssistUsesBattleAvatarTemplate()
	{
		using ZContext zContext = CreateContextWithAssets();
		using Mat screen = CreateBlankScreen();
		PasteBattleTemplate(zContext, screen, "按键-切换角色", "avatar_quick_anby");
		IReadOnlyList<StateRecord> collection = zContext.AutoBattleContext.CheckQuickAssist(screen, 1.0);
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "快速支援-安比"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "快速支援-击破"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == BattleStateEnum.StatusQuickAssistReady.GetDescription()));
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder(BattleStateEnum.StatusQuickAssistReady.GetDescription()).LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleContext_CheckChainAttackUsesBattleAvatarTemplate()
	{
		using ZContext zContext = CreateContextWithAssets();
		using Mat screen = CreateBlankScreen();
		PasteBattleTemplate(zContext, screen, "连携技-1", "avatar_chain_anby");
		IReadOnlyList<StateRecord> collection = zContext.AutoBattleContext.CheckChainAttack(screen, 1.0);
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "连携技-1-安比"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "连携技-1-击破"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == "连携技-2-邦布"));
		Assert.Contains((IEnumerable<StateRecord>)collection, (Predicate<StateRecord>)((StateRecord record) => record.StateName == BattleStateEnum.StatusChainReady.GetDescription()));
	}

	[Fact]
	public void Test_AutoBattleContext_CheckBattleStateChecksChainAndEndResultOutsideBattle()
	{
		using ZContext zContext = CreateContextWithAssets();
		zContext.OcrService.Matcher = new FixedTextOcrMatcher("完成");
		using Mat screen = CreateBlankScreen();
		PasteBattleTemplate(zContext, screen, "连携技-1", "avatar_chain_anby");
		bool condition = zContext.AutoBattleContext.CheckBattleState(screen, 10.0, checkBattleEndNormalResult: true, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: false, checkDistance: false, sync: true);
		Assert.False(condition);
		Assert.False(zContext.AutoBattleContext.LastCheckInBattle);
		Assert.Equal("普通战斗-完成", zContext.AutoBattleContext.LastCheckEndResult);
		Assert.Equal(10.0, zContext.AutoBattleContext.LastCheckEndResultScreenshotTime);
		Assert.Equal(10.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("连携技-1-安比").LastRecordTime);
		Assert.Equal(10.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder(BattleStateEnum.StatusChainReady.GetDescription()).LastRecordTime);
	}

	[Fact]
	public async Task Test_AutoBattleContext_AsyncEndDetectionPublishesResultForNextCallerRound()
	{
		using ZContext ctx = CreateContextWithAssets();
		ctx.OcrService.Matcher = new FixedTextOcrMatcher("完成");
		Mat screen = CreateBlankScreen();
		Stopwatch stopwatch = Stopwatch.StartNew();
		bool inBattle;
		try
		{
			inBattle = ctx.AutoBattleContext.CheckBattleState(screen, 10.0, checkBattleEndNormalResult: true);
		}
		finally
		{
			screen.Dispose();
		}
		Assert.False(inBattle);
		Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1L));
		for (int i = 0; i < 40; i++)
		{
			if (ctx.AutoBattleContext.LastCheckEndResult != null)
			{
				break;
			}
			await Task.Delay(25);
		}
		Assert.Equal("普通战斗-完成", ctx.AutoBattleContext.LastCheckEndResult);
	}

	[Fact]
	public void Test_AutoBattleContext_LogsBattleStateWithSourceAndFrameMetadata()
	{
		RecordingLogSink recordingLogSink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(recordingLogSink).CreateLogger();
		ZContext zContext = new ZContext(new OneDragonEnvironment(FindRepoRoot()), logger);
		zContext.ScreenContext.Reload();
		using Mat screen = CreateBlankScreen();
		bool condition = zContext.AutoBattleContext.CheckBattleState(screen, (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0, checkBattleEndNormalResult: false, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: false, checkDistance: false, sync: true, "logging_regression");
		Assert.False(condition);
		Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text.StartsWith("自动战斗战斗状态:", StringComparison.Ordinal) && entry.Properties["Source"].ToString().Contains("logging_regression", StringComparison.Ordinal) && entry.Properties["InBattle"].ToString().Contains("False", StringComparison.Ordinal) && entry.Properties.ContainsKey("PreviousInBattle") && entry.Properties.ContainsKey("EndResult") && entry.Properties.ContainsKey("EndResultScreenshotTime") && entry.Properties.ContainsKey("EndResultMatchesCurrentFrame") && entry.Properties.ContainsKey("Sync") && entry.Properties.ContainsKey("SubmittedTaskCount") && entry.Properties.ContainsKey("CaptureAgeMilliseconds")));
	}

	[Fact]
	public void Test_AutoBattleSubmissionGate_DropsOnlyWhileItsOwnDetectionIsBusy()
	{
		AutoBattleSubmissionGate autoBattleSubmissionGate = new AutoBattleSubmissionGate();
		Assert.True(autoBattleSubmissionGate.TryEnter());
		Assert.False(autoBattleSubmissionGate.TryEnter());
		Assert.Equal(1L, autoBattleSubmissionGate.ConsumeDropped());
		Assert.Equal(0L, autoBattleSubmissionGate.ConsumeDropped());
		autoBattleSubmissionGate.Exit();
		Assert.True(autoBattleSubmissionGate.TryEnter());
		autoBattleSubmissionGate.Exit();
	}

	[Fact]
	public void Test_AutoBattleContext_CheckBattleEndUsesPythonStatusString()
	{
		using ZContext zContext = CreateContextWithAssets();
		zContext.OcrService.Matcher = new FixedTextOcrMatcher("完成");
		using Mat screen = CreateBlankScreen();
		string actual = zContext.AutoBattleContext.CheckBattleEnd(screen, 6.0, checkBattleEndNormalResult: true, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: false);
		Assert.Equal("普通战斗-完成", actual);
		Assert.Equal("普通战斗-完成", zContext.AutoBattleContext.LastCheckEndResult);
	}

	[Fact]
	public void Test_AutoBattleContext_CheckBattleEndDoesNotReturnStaleResultDuringInterval()
	{
		using ZContext zContext = CreateContextWithAssets();
		zContext.OcrService.Matcher = new FixedTextOcrMatcher("完成");
		using Mat screen = CreateBlankScreen();
		string actual = zContext.AutoBattleContext.CheckBattleEnd(screen, 6.0, checkBattleEndNormalResult: true, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: false);
		string text = zContext.AutoBattleContext.CheckBattleEnd(screen, 6.099999904632568, checkBattleEndNormalResult: true, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: false);
		Assert.Equal("普通战斗-完成", actual);
		Assert.Null(text);
		Assert.Equal("普通战斗-完成", zContext.AutoBattleContext.LastCheckEndResult);
	}

	[Fact]
	public void Test_AutoBattleTargetContext_Instantiation()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(ctx);
		autoBattleTargetContext.InitAutoOp();
		Assert.NotNull(autoBattleTargetContext);
		Assert.Contains((IEnumerable<DetectionTask>)autoBattleTargetContext.Tasks, (Predicate<DetectionTask>)((DetectionTask task) => task.TaskId == "lock_on"));
	}

	[Fact]
	public void Test_AutoBattleTargetContext_AppliesOperatorIntervals()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(zContext);
		AutoBattleOperator autoOp = new AutoBattleOperator(zContext.AutoBattleContext, "auto_battle", "test")
		{
			TargetLockInterval = 0.5f
		};
		autoBattleTargetContext.InitAutoOp(autoOp);
		Assert.Equal(0.5, autoBattleTargetContext.GetCurrentInterval("lock_on"));
	}

	[Fact]
	public void Test_AutoBattleTargetContext_RunAllChecksWritesHitState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker();
		fakeTargetStateChecker.Enqueue(new TargetStateCheckResult[] { TargetStateCheckResult.Hit("目标-近距离锁定") });
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(zContext, fakeTargetStateChecker);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		IReadOnlyList<StateRecord> readOnlyList = autoBattleTargetContext.RunAllChecks(null, 0.5);
		Assert.Single(readOnlyList);
		Assert.Equal("目标-近距离锁定", readOnlyList[0].StateName);
		Assert.Equal(0.5, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("目标-近距离锁定").LastRecordTime);
		Assert.Equal(1.0, autoBattleTargetContext.GetCurrentInterval("lock_on"));
	}

	[Fact]
	public void Test_AutoBattleTargetContext_RunAllChecksSkipsBeforeInterval()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker();
		fakeTargetStateChecker.Enqueue(new TargetStateCheckResult[] { TargetStateCheckResult.Hit("目标-近距离锁定") });
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(zContext, fakeTargetStateChecker);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		IReadOnlyList<StateRecord> collection = autoBattleTargetContext.RunAllChecks(null, 0.4000000059604645);
		Assert.Empty(collection);
		Assert.Equal(0, fakeTargetStateChecker.RunCount);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("目标-近距离锁定").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleTargetContext_ClearResultClearsState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker();
		fakeTargetStateChecker.Enqueue(new TargetStateCheckResult[] { TargetStateCheckResult.Hit("目标-近距离锁定") });
		fakeTargetStateChecker.Enqueue(new TargetStateCheckResult[] { TargetStateCheckResult.Clear("目标-近距离锁定") });
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(zContext, fakeTargetStateChecker);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		autoBattleTargetContext.RunAllChecks(null, 0.5);
		autoBattleTargetContext.RunAllChecks(null, 2.0);
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("目标-近距离锁定");
		Assert.Equal(0.0, stateRecorder.LastRecordTime);
		Assert.Null(stateRecorder.LastValue);
		Assert.Equal(0.5, autoBattleTargetContext.GetCurrentInterval("lock_on"));
	}

	[Fact]
	public void Test_AutoBattleTargetContext_MissResultDoesNotWriteState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker();
		fakeTargetStateChecker.Enqueue(new TargetStateCheckResult[] { TargetStateCheckResult.Miss("目标-近距离锁定") });
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(zContext, fakeTargetStateChecker);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		IReadOnlyList<StateRecord> collection = autoBattleTargetContext.RunAllChecks(null, 0.5);
		Assert.Empty(collection);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("目标-近距离锁定").LastRecordTime);
		Assert.Equal(0.5, autoBattleTargetContext.GetCurrentInterval("lock_on"));
	}

	[Fact]
	public void Test_AutoBattleTargetContext_ValueResultWritesTimestampAndValue()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker();
		fakeTargetStateChecker.Enqueue(new TargetStateCheckResult[] { TargetStateCheckResult.HitValue("目标-近距离锁定", 75) });
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(zContext, fakeTargetStateChecker);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		IReadOnlyList<StateRecord> readOnlyList = autoBattleTargetContext.RunAllChecks(null, 0.5);
		Assert.Single(readOnlyList);
		Assert.Equal(75, readOnlyList[0].Value);
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("目标-近距离锁定");
		Assert.Equal(0.5, stateRecorder.LastRecordTime);
		Assert.Equal(75, stateRecorder.LastValue);
	}

	[Fact]
	public void Test_AutoBattleTargetContext_LogsHitStateThroughContextLogger()
	{
		RecordingLogSink recordingLogSink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(recordingLogSink).CreateLogger();
		ZContext ctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"), logger);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker();
		fakeTargetStateChecker.Enqueue(new TargetStateCheckResult[] { TargetStateCheckResult.HitValue("目标-近距离锁定", 75) });
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(ctx, fakeTargetStateChecker);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		autoBattleTargetContext.RunAllChecks(null, 0.5);
		Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text == "自动战斗目标状态: TaskId={TaskId}, ScreenshotTime={ScreenshotTime:F3}, Results={Results}" && entry.Properties["ScreenshotTime"].ToString().Contains("0.5", StringComparison.Ordinal) && entry.Properties["Results"].ToString().Contains("目标-近距离锁定=命中(75)", StringComparison.Ordinal)));
	}

	[Fact]
	public void Test_AutoBattleTargetContext_EmptyResultDoesNotChangeDynamicInterval()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker();
		fakeTargetStateChecker.Enqueue(Array.Empty<TargetStateCheckResult>());
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(ctx, fakeTargetStateChecker);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		IReadOnlyList<StateRecord> collection = autoBattleTargetContext.RunAllChecks(null, 0.5);
		Assert.Empty(collection);
		Assert.Equal(0.5, autoBattleTargetContext.GetCurrentInterval("lock_on"));
	}

	[Fact]
	public void Test_AutoBattleTargetContext_AsyncTaskTimesOutAfterOneSecond()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		FakeTargetStateChecker fakeTargetStateChecker = new FakeTargetStateChecker
		{
			Delay = TimeSpan.FromMilliseconds(1200L)
		};
		DetectionTask item = new DetectionTask
		{
			TaskId = "slow",
			PipelineName = "slow",
			Interval = 0.1,
			IsAsync = true
		};
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(ctx, fakeTargetStateChecker, new DetectionTask[] { item });
		Stopwatch stopwatch = Stopwatch.StartNew();
		IReadOnlyList<StateRecord> collection = autoBattleTargetContext.RunAllChecks(null, 1.0);
		Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900L), $"异步检测未等待 Python 等价的 1 秒超时: {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
		Assert.Empty(collection);
		Assert.Equal(1, fakeTargetStateChecker.RunCount);
	}

	[Fact]
	public void Test_AutoBattleTargetContext_UpdateBattleDistanceTracksPythonCounters()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(ctx);
		autoBattleTargetContext.UpdateBattleDistance(12.5f);
		Assert.Equal(12.5f, autoBattleTargetContext.LastCheckDistance);
		Assert.Equal(1, autoBattleTargetContext.WithDistanceTimes);
		Assert.Equal(0, autoBattleTargetContext.WithoutDistanceTimes);
		Assert.Equal(1.0, autoBattleTargetContext.CheckDistanceInterval);
		autoBattleTargetContext.UpdateBattleDistance(null);
		Assert.Equal(-1f, autoBattleTargetContext.LastCheckDistance);
		Assert.Equal(0, autoBattleTargetContext.WithDistanceTimes);
		Assert.Equal(1, autoBattleTargetContext.WithoutDistanceTimes);
		Assert.Equal(5.0, autoBattleTargetContext.CheckDistanceInterval);
	}

	private static ZContext CreateContextWithAssets()
	{
		ZContext zContext = new ZContext(new OneDragonEnvironment(FindRepoRoot()));
		zContext.ScreenContext.Reload();
		return zContext;
	}

	private static Mat CreateBlankScreen()
	{
		return new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
	}

	private static void PasteBattleTemplate(ZContext ctx, Mat screen, string areaName, string templateId)
	{
		OneDragon.Core.Screen.ScreenArea area = ctx.ScreenContext.GetArea("战斗画面", areaName);
		using Mat mat = ctx.TemplateLoader.GetTemplate("battle", templateId).Raw.Clone();
		using Mat m = new Mat(screen, new Rect(area.Rect.X1, area.Rect.Y1, mat.Width, mat.Height));
		mat.CopyTo(m);
	}

	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string fullName = directoryInfo.FullName;
			if (Directory.Exists(Path.Combine(fullName, "assets", "template", "battle")) && Directory.Exists(Path.Combine(fullName, "assets", "game_data", "screen_info")))
			{
				return fullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 仓库根目录。");
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_Instantiation()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(ctx, new FakeFlashClassifier(-1), new FakeDodgeAudioDetector(result: false));
		Assert.NotNull(autoBattleDodgeContext);
		bool condition = autoBattleDodgeContext.CheckDodgeAudio(1.0);
		Assert.False(condition);
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_RedFlashWritesStateAndInterrupts()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, new FakeFlashClassifier(1), new FakeDodgeAudioDetector(result: false));
		bool condition = autoBattleDodgeContext.CheckDodgeFlash(null, 1.0);
		Assert.True(condition);
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-红光").LastRecordTime);
		Assert.True(autoBattleDodgeContext.ShouldInterruptForDodge(1.100000023841858));
		Assert.False(autoBattleDodgeContext.ShouldInterruptForDodge(2.0));
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_YellowFlashWritesState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, new FakeFlashClassifier(2), new FakeDodgeAudioDetector(result: false));
		bool condition = autoBattleDodgeContext.CheckDodgeFlash(null, 1.0);
		Assert.True(condition);
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-黄光").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_AudioFallbackWritesAudioState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, new FakeFlashClassifier(-1), new FakeDodgeAudioDetector(result: false));
		bool condition = autoBattleDodgeContext.CheckDodgeFlash(null, 1.0, Task.FromResult(result: true));
		Assert.True(condition);
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-声音").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_FlashHasPriorityOverAudio()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, new FakeFlashClassifier(2), new FakeDodgeAudioDetector(result: false));
		bool condition = autoBattleDodgeContext.CheckDodgeFlash(null, 1.0, Task.FromResult(result: true));
		Assert.True(condition);
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-黄光").LastRecordTime);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-声音").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_RedFlashDoesNotWaitForPendingAudio()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, new FakeFlashClassifier(1), new FakeDodgeAudioDetector(result: false));
		TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		Stopwatch stopwatch = Stopwatch.StartNew();
		bool condition = autoBattleDodgeContext.CheckDodgeFlash(null, 1.0, taskCompletionSource.Task);
		Assert.True(condition);
		Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100L));
		Assert.Equal(1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-红光").LastRecordTime);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-声音").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_NoFlashAndNoAudioDoesNotWriteState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, new FakeFlashClassifier(-1), new FakeDodgeAudioDetector(result: false));
		bool condition = autoBattleDodgeContext.CheckDodgeFlash(null, 1.0, Task.FromResult(result: false));
		Assert.False(condition);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-红光").LastRecordTime);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-黄光").LastRecordTime);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-声音").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_CheckDodgeIntervalSkipsEarlyFrame()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		FakeFlashClassifier fakeFlashClassifier = new FakeFlashClassifier(1);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, fakeFlashClassifier, new FakeDodgeAudioDetector(result: false));
		autoBattleDodgeContext.InitAutoOp(new AutoBattleOperator(zContext.AutoBattleContext, "auto_battle", "test")
		{
			CheckDodgeInterval = new AutoBattleInterval(0.5f, 0.5f)
		});
		bool condition = autoBattleDodgeContext.CheckDodgeFlash(null, 0.20000000298023224);
		Assert.False(condition);
		Assert.Equal(0, fakeFlashClassifier.RunCount);
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-红光").LastRecordTime);
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_FlashAndAudioGatesAreIndependent()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(ctx, new FakeFlashClassifier(-1), new FakeDodgeAudioDetector(result: false));
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out var runGeneration));
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeAudioCheck(out runGeneration));
		Assert.False(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out runGeneration));
		Assert.False(autoBattleDodgeContext.TryScheduleDodgeAudioCheck(out runGeneration));
		Assert.Equal(1L, autoBattleDodgeContext.DroppedFlashBecauseBusy);
		Assert.Equal(1L, autoBattleDodgeContext.DroppedAudioBecauseBusy);
		autoBattleDodgeContext.CompleteDodgeFlashCheck();
		autoBattleDodgeContext.CompleteDodgeAudioCheck();
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out runGeneration));
		autoBattleDodgeContext.CompleteDodgeFlashCheck();
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_VisualResultDoesNotRequireAudioGate()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(ctx, new FakeFlashClassifier(-1), new FakeDodgeAudioDetector(result: false));
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out var runGeneration));
		AutoBattleFlashCheckResult autoBattleFlashCheckResult = autoBattleDodgeContext.CheckDodgeFlashVisual(null, 1.0, runGeneration);
		autoBattleDodgeContext.CompleteDodgeFlashCheck();
		Assert.True(autoBattleFlashCheckResult.ShouldConsumeAudio);
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out var _));
		autoBattleDodgeContext.CompleteDodgeFlashCheck();
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_ReinitializeDoesNotReleaseRunningGate()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(ctx, new FakeFlashClassifier(-1), new FakeDodgeAudioDetector(result: false));
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out var runGeneration));
		autoBattleDodgeContext.InitBattleDodgeContext();
		Assert.False(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out runGeneration));
		autoBattleDodgeContext.CompleteDodgeFlashCheck();
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out runGeneration));
		autoBattleDodgeContext.CompleteDodgeFlashCheck();
	}

	[Fact]
	public void Test_AutoBattleDodgeContext_StaleGenerationDoesNotPublishState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleDodgeContext autoBattleDodgeContext = new AutoBattleDodgeContext(zContext, new FakeFlashClassifier(1), new FakeDodgeAudioDetector(result: false));
		Assert.True(autoBattleDodgeContext.TryScheduleDodgeFlashCheck(out var runGeneration));
		autoBattleDodgeContext.InitBattleDodgeContext();
		Assert.False(autoBattleDodgeContext.CheckDodgeFlash(null, 1.0, Task.FromResult(result: false), runGeneration));
		Assert.Equal(-1.0, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("闪避识别-红光").LastRecordTime);
		autoBattleDodgeContext.CompleteDodgeFlashCheck();
	}

	[Fact]
	public void Test_LatestValueSlot_ReplacesOnlyPendingValue()
	{
		LatestValueSlot<string> slot = new LatestValueSlot<string>();

		Assert.True(slot.Submit("running", out string? replaced));
		Assert.Null(replaced);
		Assert.False(slot.Submit("pending-1", out replaced));
		Assert.Null(replaced);
		Assert.False(slot.Submit("pending-2", out replaced));
		Assert.Equal("pending-1", replaced);
		Assert.Equal("pending-2", slot.CompleteActive());
		Assert.Null(slot.CompleteActive());
		Assert.True(slot.Submit("next-running", out replaced));
		Assert.Null(replaced);
	}

}
