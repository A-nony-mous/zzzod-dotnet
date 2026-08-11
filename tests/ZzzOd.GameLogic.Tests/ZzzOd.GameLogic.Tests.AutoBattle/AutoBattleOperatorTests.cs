using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class AutoBattleOperatorTests
{
	private sealed class RecordingAtomicOp : AtomicOp
	{
		private readonly bool _blockUntilStopped;

		private readonly bool _throwOnExecute;

		private readonly int _executeMilliseconds;

		private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(initialState: false);

		public int ExecuteCount { get; private set; }

		public int StopCount { get; private set; }

		public ManualResetEventSlim Started { get; } = new ManualResetEventSlim(initialState: false);

		public RecordingAtomicOp(string opName, bool asyncOp = false, bool blockUntilStopped = false, bool throwOnExecute = false, int executeMilliseconds = 0)
			: base(opName, asyncOp)
		{
			_blockUntilStopped = blockUntilStopped;
			_throwOnExecute = throwOnExecute;
			_executeMilliseconds = executeMilliseconds;
		}

		public override void Execute()
		{
			ExecuteCount++;
			Started.Set();
			if (_throwOnExecute)
			{
				throw new InvalidOperationException("boom");
			}
			if (_executeMilliseconds > 0)
			{
				Thread.Sleep(_executeMilliseconds);
			}
			if (_blockUntilStopped)
			{
				_stopEvent.Wait();
			}
		}

		public override void Stop()
		{
			StopCount++;
			_stopEvent.Set();
		}
	}

	[Fact]
	public void LoadOtherInfo_LoadsPythonCompatibleDefaultsAndOverrides()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator autoBattleOperator = new AutoBattleOperator(zContext.AutoBattleContext, "auto_battle", "test");
		autoBattleOperator.LoadOtherInfo(new Dictionary<string, object>
		{
			["author"] = "tester",
			["team_list"] = new object[1] { new object[2] { "安比", "妮可" } },
			["check_dodge_interval"] = 0.1,
			["auto_lock_interval"] = "3.5",
			["auto_turn_interval"] = 0
		});
		Assert.Equal("tester", autoBattleOperator.Author);
		Assert.Equal("https://qm.qq.com/q/wuVRYuZzkA", autoBattleOperator.Homepage);
		int num = 1;
		List<List<string>> list = new List<List<string>>(num);
		CollectionsMarshal.SetCount(list, num);
		ref List<string> reference = ref CollectionsMarshal.AsSpan(list)[0];
		int num2 = 2;
		List<string> list2 = new List<string>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<string> span = CollectionsMarshal.AsSpan(list2);
		span[0] = "安比";
		span[1] = "妮可";
		reference = list2;
		Assert.Equal<List<List<string>>>(list, autoBattleOperator.TeamList);
		Assert.Equal(new AutoBattleInterval(0.1f, 0.1f), autoBattleOperator.CheckDodgeInterval);
		Assert.Equal(3.5f, autoBattleOperator.AutoLockInterval);
		Assert.Equal(0f, autoBattleOperator.AutoTurnInterval);
	}

	[Fact]
	public void LoadOtherInfo_PreservesPythonIntervalArraysForEveryBattleCheck()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator autoBattleOperator = new AutoBattleOperator(zContext.AutoBattleContext, "auto_battle", "test");
		autoBattleOperator.LoadOtherInfo(new Dictionary<string, object>
		{
			["check_dodge_interval"] = new object[2] { 0.1, 0.2 },
			["check_agent_interval"] = new object[2] { 0.3, 0.4 },
			["check_chain_interval"] = new object[2] { 0.5, 0.6 },
			["check_quick_interval"] = new object[2] { 0.7, 0.8 },
			["check_end_interval"] = new object[2] { 0.9, 1.0 }
		});
		Assert.Equal(new AutoBattleInterval(0.1f, 0.2f), autoBattleOperator.CheckDodgeInterval);
		Assert.Equal(new AutoBattleInterval(0.3f, 0.4f), autoBattleOperator.CheckAgentInterval);
		Assert.Equal(new AutoBattleInterval(0.5f, 0.6f), autoBattleOperator.CheckChainInterval);
		Assert.Equal(new AutoBattleInterval(0.7f, 0.8f), autoBattleOperator.CheckQuickInterval);
		Assert.Equal(new AutoBattleInterval(0.9f, 1f), autoBattleOperator.CheckEndInterval);
	}

	[Fact]
	public void StartRunningAsync_RejectsUninitializedAndRepeatedStarts()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCondOpConfig(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			AutoBattleOperator autoBattleOperator = new AutoBattleOperator(zContext.AutoBattleContext, "auto_battle", "测试配置", readFromMerged: false);
			Assert.False(autoBattleOperator.StartRunningAsync());
			Assert.True(autoBattleOperator.InitBeforeRunning().Success);
			Assert.True(autoBattleOperator.StartRunningAsync());
			Assert.False(autoBattleOperator.StartRunningAsync());
			autoBattleOperator.StopRunning();
			Assert.False(autoBattleOperator.IsRunning);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task OperationExecutor_RunsAtomicOpsInOrderAndRecordsSteps()
	{
		RecordingAtomicOp first = new RecordingAtomicOp("first");
		RecordingAtomicOp second = new RecordingAtomicOp("second");
		OperationExecutor executor = new OperationExecutor(new AtomicOp[2] { first, second }, 1.0);
		Assert.True(await executor.RunAsync());
		Assert.False(executor.Running);
		Assert.Equal(1, first.ExecuteCount);
		Assert.Equal(1, second.ExecuteCount);
		Assert.Contains((IEnumerable<OperationExecutionStepRecord>)executor.Records, (Predicate<OperationExecutionStepRecord>)((OperationExecutionStepRecord record) => record.OpName == "first" && record.Event == "started"));
		Assert.Contains((IEnumerable<OperationExecutionStepRecord>)executor.Records, (Predicate<OperationExecutionStepRecord>)((OperationExecutionStepRecord record) => record.OpName == "second" && record.Event == "completed"));
	}

	[Fact]
	public async Task OperationExecutor_StopStopsCurrentAndAsyncOps()
	{
		RecordingAtomicOp asyncPress = new RecordingAtomicOp("press", asyncOp: true);
		RecordingAtomicOp blocking = new RecordingAtomicOp("wait", asyncOp: false, blockUntilStopped: true);
		OperationExecutor executor = new OperationExecutor(new AtomicOp[2] { asyncPress, blocking }, 1.0);
		Task<bool> task = executor.RunAsync();
		Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(1L)));
		bool finishedBeforeStop = executor.Stop();
		bool completed = await task.WaitAsync(TimeSpan.FromSeconds(1L));
		Assert.False(finishedBeforeStop);
		Assert.False(completed);
		Assert.Equal(1, asyncPress.StopCount);
		Assert.Equal(1, blocking.StopCount);
		Assert.Contains((IEnumerable<OperationExecutionStepRecord>)executor.Records, (Predicate<OperationExecutionStepRecord>)((OperationExecutionStepRecord record) => record.OpName == "press" && record.Event == "stopping-async"));
	}

	[Fact]
	public async Task OperationExecutor_KeepsStepsOnDedicatedSchedulerNotThreadPool()
	{
		// 指令之间的续体必须留在专用执行线程上；落回共享 ThreadPool 会与视觉识别任务抢线程，
		// 把名义 0.1 秒的指令串拖到秒级（见 restore-auto-battle-state-freshness 基线）。
		// 指令必须慢到触发轮询等待，否则整串会同步跑完、测不出续体落在哪个线程
		RecordingAtomicOp first = new RecordingAtomicOp("first", asyncOp: false, blockUntilStopped: false, throwOnExecute: false, executeMilliseconds: 120);
		RecordingAtomicOp second = new RecordingAtomicOp("second", asyncOp: false, blockUntilStopped: false, throwOnExecute: false, executeMilliseconds: 120);
		OperationExecutor executor = new OperationExecutor(new AtomicOp[2] { first, second }, 1.0);
		Assert.True(await executor.RunAsync());
		OperationExecutionStepRecord lastCompleted = executor.Records.Last(record => record.OpName == "second" && record.Event == "completed");
		Assert.NotNull(lastCompleted.ThreadName);
		Assert.StartsWith("od-operation-executor", lastCompleted.ThreadName, StringComparison.Ordinal);
		foreach (OperationExecutionStepRecord record in executor.Records)
		{
			Assert.StartsWith("od-operation-executor", record.ThreadName ?? string.Empty, StringComparison.Ordinal);
		}
	}

	[Fact]
	public async Task OperationExecutor_RecordsErrorAndContinuesRemainingOps()
	{
		RecordingAtomicOp failing = new RecordingAtomicOp("bad", asyncOp: false, blockUntilStopped: false, throwOnExecute: true);
		RecordingAtomicOp following = new RecordingAtomicOp("following");
		OperationExecutor executor = new OperationExecutor(new AtomicOp[2] { failing, following }, 1.0);
		Assert.True(await executor.RunAsync());
		Assert.NotNull(executor.LastException);
		Assert.Equal(1, following.ExecuteCount);
		Assert.Contains((IEnumerable<OperationExecutionStepRecord>)executor.Records, (Predicate<OperationExecutionStepRecord>)((OperationExecutionStepRecord record) => record.OpName == "bad" && record.Event == "error"));
		Assert.Contains((IEnumerable<OperationExecutionStepRecord>)executor.Records, (Predicate<OperationExecutionStepRecord>)((OperationExecutionStepRecord record) => record.OpName == "following" && record.Event == "completed"));
	}

	[Fact]
	public async Task OperationExecutor_ErrorOnLastOpStillCompletes()
	{
		RecordingAtomicOp first = new RecordingAtomicOp("first");
		RecordingAtomicOp failingLast = new RecordingAtomicOp("bad-last", asyncOp: false, blockUntilStopped: false, throwOnExecute: true);
		OperationExecutor executor = new OperationExecutor(new AtomicOp[2] { first, failingLast }, 1.0);
		Assert.True(await executor.RunAsync());
		Assert.NotNull(executor.LastException);
		Assert.Equal(1, first.ExecuteCount);
	}

	[Fact]
	public async Task SubmitExecution_OpErrorDoesNotStarveFollowingExecutions()
	{
		using ZContext zctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "test");
		ExecutionInfo faulting = new ExecutionInfo(new List<AtomicOp>(1)
		{
			new RecordingAtomicOp("bad", asyncOp: false, blockUntilStopped: false, throwOnExecute: true)
		});
		Assert.True(op.SubmitExecution(faulting, "主循环", 1.0));
		bool completed = await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L));
		await WaitUntilIdle(op);
		Assert.True(completed);
		Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "error" && record.Completed && record.ErrorMessage == "boom"));
		RecordingAtomicOp next = new RecordingAtomicOp("next");
		Assert.True(op.SubmitExecution(new ExecutionInfo(new List<AtomicOp>(1) { next }), "主循环", 2.0));
		bool nextCompleted = await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L));
		await WaitUntilIdle(op);
		Assert.True(nextCompleted);
		Assert.Equal(1, next.ExecuteCount);
	}

	[Fact]
	public async Task SubmitExecution_StartsExecutionAndRecordsCompletion()
	{
		using ZContext zctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "test");
		ExecutionInfo executionInfo = new ExecutionInfo(new List<AtomicOp>(1)
		{
			new RecordingAtomicOp("first")
		});
		bool submitted = op.SubmitExecution(executionInfo, "主循环", 1.0);
		Task<bool> runningTask = op.GetRunningExecutionTask();
		bool completed = await runningTask.WaitAsync(TimeSpan.FromSeconds(1L));
		await WaitUntilIdle(op);
		Assert.True(submitted);
		Assert.True(completed);
		Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "started" && record.Trigger == "主循环"));
		Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "finished" && record.Completed));
		Assert.Contains(zctx.OverlayDebugBus.Snapshot().DecisionItems, item => item.Trigger == "主循环" && item.Operation == "first" && item.Status == "accepted");
		Assert.Contains(zctx.OverlayDebugBus.Snapshot().DecisionItems, item => item.Trigger == "主循环" && item.Operation == "first" && item.Status == "completed");
	}

	[Fact]
	public async Task NaturalCompletionPriorityProtectionRejectsSameAndLowerButAcceptsHigher()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(context.AutoBattleContext, "auto_battle", "test");
		ExecutionInfo current = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("current") }) { Priority = 5 };
		Assert.True(op.SubmitExecution(current, "current"));
		Assert.True(await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L)));
		await WaitUntilIdle(op);

		ExecutionInfo same = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("same") }) { Priority = 5 };
		ExecutionInfo lower = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("lower") }) { Priority = 4 };
		ExecutionInfo higher = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("higher") }) { Priority = 6 };
		Assert.False(op.TryInterrupt(same, "same"));
		Assert.False(op.TryInterrupt(lower, "lower"));
		Assert.True(op.TryInterrupt(higher, "higher"));
		Assert.True(await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L)));
	}

	[Fact]
	public async Task MainLoopSubmissionConsumesNaturalCompletionPriorityProtection()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(context.AutoBattleContext, "auto_battle", "test");
		ExecutionInfo current = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("current") }) { Priority = 5 };
		Assert.True(op.SubmitExecution(current, "current"));
		Assert.True(await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L)));
		await WaitUntilIdle(op);

		ExecutionInfo nextMainLoop = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("next") }) { Priority = 5 };
		Assert.True(op.SubmitExecution(nextMainLoop, trigger: null, consumeNaturalCompletionProtection: true));
		Assert.True(await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L)));
	}

	[Fact]
	public async Task StopRunningClearsNaturalCompletionPriorityProtection()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(context.AutoBattleContext, "auto_battle", "test");
		ExecutionInfo current = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("current") }) { Priority = 5 };
		Assert.True(op.SubmitExecution(current, "current"));
		Assert.True(await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L)));
		await WaitUntilIdle(op);

		op.StopRunning();
		ExecutionInfo same = new ExecutionInfo(new List<AtomicOp> { new RecordingAtomicOp("same") }) { Priority = 5 };
		Assert.True(op.SubmitExecution(same, "same"));
		Assert.True(await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L)));
	}

	[Fact]
	public async Task SubmitExecution_RejectsLowerPriorityInterrupt()
	{
		using ZContext zctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "test");
		RecordingAtomicOp blocking = new RecordingAtomicOp("blocking", asyncOp: false, blockUntilStopped: true);
		ExecutionInfo current = new ExecutionInfo(new List<AtomicOp>(1) { blocking })
		{
			Priority = 5
		};
		ExecutionInfo lower = new ExecutionInfo(new List<AtomicOp>(1)
		{
			new RecordingAtomicOp("lower")
		})
		{
			Priority = 1
		};
		Assert.True(op.SubmitExecution(current, "current", 1.0));
		Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(1L)));
		bool accepted = op.TryInterrupt(lower, "lower", 2.0);
		op.StopRunning();
		await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L));
		Assert.False(accepted);
		Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "rejected" && record.ErrorMessage == "priority-blocked"));
		Assert.Contains(zctx.OverlayDebugBus.Snapshot().DecisionItems, item =>
			item.Trigger == "lower" &&
			item.Operation == "lower" &&
			item.Status == "priority-blocked" &&
			Equals(item.Metadata!["current_priority"], 5) &&
			Equals(item.Metadata["candidate_priority"], 1));
	}

	[Fact]
	public async Task SubmitExecution_HigherPriorityInterruptStopsCurrentAndRunsNext()
	{
		using ZContext zctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "test");
		RecordingAtomicOp blocking = new RecordingAtomicOp("blocking", asyncOp: false, blockUntilStopped: true);
		ExecutionInfo current = new ExecutionInfo(new List<AtomicOp>(1) { blocking })
		{
			Priority = 1
		};
		RecordingAtomicOp nextOp = new RecordingAtomicOp("next");
		ExecutionInfo higher = new ExecutionInfo(new List<AtomicOp>(1) { nextOp })
		{
			Priority = 5
		};
		Assert.True(op.SubmitExecution(current, "current", 1.0));
		Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(1L)));
		bool accepted = op.TryInterrupt(higher, "higher", 2.0);
		Task<bool> runningTask = op.GetRunningExecutionTask();
		bool completed = await runningTask.WaitAsync(TimeSpan.FromSeconds(1L));
		await WaitUntilIdle(op);
		Assert.True(accepted);
		Assert.True(completed);
		Assert.Equal(1, blocking.StopCount);
		Assert.Equal(1, nextOp.ExecuteCount);
		Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "interrupted" && record.Trigger == "current"));
		Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "finished" && record.Trigger == "higher"));
	}

	[Fact]
	public async Task InterruptIfCurrentMatchesStopsRunningExecution()
	{
		using ZContext zctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "test");
		RecordingAtomicOp blocking = new RecordingAtomicOp("blocking", asyncOp: false, blockUntilStopped: true);
		ExecutionInfo execution = new ExecutionInfo(new List<AtomicOp>(1) { blocking }, new StateCalNode(StateCalNodeType.TRUE));
		Assert.True(op.SubmitExecution(execution, "current", 1.0));
		Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(1L)));
		bool interrupted = op.InterruptIfCurrentMatches(2.0);
		await op.GetRunningExecutionTask().WaitAsync(TimeSpan.FromSeconds(1L));
		Assert.True(interrupted);
		Assert.Equal(1, blocking.StopCount);
		Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "interrupt-condition"));
	}

	[Fact]
	public void InitBeforeRunning_LoadsYamlAndExpandsTemplates()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCondOpConfig(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			AutoBattleOperator autoBattleOperator = new AutoBattleOperator(zContext.AutoBattleContext, "auto_battle", "测试配置", readFromMerged: false);
			var (condition, userMessage) = autoBattleOperator.InitBeforeRunning();
			zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-触发", 1.0));
			ExecutionInfo executionInfo = autoBattleOperator.Scenes[0].MatchExecution(1.100000023841858);
			Assert.True(condition, userMessage);
			Assert.Equal("tester", autoBattleOperator.Author);
			Assert.Single(autoBattleOperator.Scenes);
			Assert.Contains("自定义-触发", autoBattleOperator.UsageStates);
			Assert.Contains("自定义-中断", autoBattleOperator.UsageStates);
			Assert.NotNull(executionInfo);
			Assert.Equal("模板命中", executionInfo.ExprDisplay);
			Assert.Single(executionInfo.OpList);
			Assert.Equal("设置状态 自定义-命中", executionInfo.OpList[0].OpName);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ReferenceGraphLoader_LoadsExpandedClosureIntoStableSnapshot()
	{
		string root = CreateTempRoot();
		try
		{
			WriteCondOpConfig(root);
			AutoBattleReferenceGraphSnapshot snapshot = new AutoBattleReferenceGraphLoader(new OneDragonEnvironment(root))
				.Load("auto_battle", "测试配置");

			Assert.Equal("tester", snapshot.Configuration["author"]);
			Assert.Single(snapshot.Scenes);
			Assert.Single(snapshot.Scenes[0].Handlers);
			Assert.Single(snapshot.Scenes[0].Handlers[0].Operations);
			Assert.Equal("设置状态", snapshot.Scenes[0].Handlers[0].Operations[0].OpName);
			Assert.Equal(
				snapshot.LoadedYamlPaths.OrderBy(path => path, StringComparer.Ordinal),
				snapshot.LoadedYamlPaths);
			Assert.All(snapshot.LoadedYamlPaths, path => Assert.True(Path.IsPathFullyQualified(path)));
			Assert.Equal(AutoBattleOperator.GetSourceFingerprint(snapshot.LoadedYamlPaths), snapshot.SourceFingerprint);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void ReferenceGraphSnapshot_DetectsDependencyChangesAndKeepsLastVerifiedSnapshotOnFailure()
	{
		string root = CreateTempRoot();
		try
		{
			WriteCondOpConfig(root);
			AutoBattleReferenceGraphLoader loader = new(new OneDragonEnvironment(root));
			AutoBattleReferenceGraphSnapshot first = loader.Load("auto_battle", "测试配置");
			Dictionary<string, AutoBattleReferenceGraphSnapshot> cache = new(StringComparer.Ordinal)
			{
				["auto_battle-测试配置"] = first,
			};

			Assert.True(AutoBattleOperator.IsSourceFingerprintCurrent(first.SourceFingerprint, first.LoadedYamlPaths));

			File.WriteAllText(
				Path.Combine(root, "config", "auto_battle_state_handler", "测试状态模板.yml"),
				"handlers:\n  - states: \"[自定义-触发, 0, 2]\"\n    debug_name: \"模板命中\"\n    interrupt_states: \"[自定义-中断, 0, 1]\"\n    operations:\n      - operation_template: \"设置命中\"");
			Assert.False(AutoBattleOperator.IsSourceFingerprintCurrent(first.SourceFingerprint, first.LoadedYamlPaths));

			File.WriteAllText(Path.Combine(root, "config", "auto_battle", "测试配置.yml"), "scenes:\n  - triggers: []\n    handlers:\n      - state_template: 不存在");
			Assert.Throws<FileNotFoundException>(() => loader.Load("auto_battle", "测试配置"));
			Assert.Same(first, cache["auto_battle-测试配置"]);

			WriteCondOpConfig(root);
			AutoBattleReferenceGraphSnapshot reloaded = loader.Load("auto_battle", "测试配置");
			cache["auto_battle-测试配置"] = reloaded;
			Assert.NotSame(first, cache["auto_battle-测试配置"]);
			Assert.True(AutoBattleOperator.IsSourceFingerprintCurrent(reloaded.SourceFingerprint, reloaded.LoadedYamlPaths));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void InitBeforeRunning_WhitespaceExpressionReturnsReadableFailure()
	{
		string root = CreateTempRoot();
		try
		{
			string directory = Path.Combine(root, "config", "auto_battle");
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, "空白表达式.yml"), "scenes:\n  - triggers: []\n    priority: 1\n    handlers:\n      - states: \"   \"\n        operations:\n          - op_name: \"等待秒数\"\n            seconds: 0.1");
			using ZContext context = new ZContext(new OneDragonEnvironment(root));
			AutoBattleOperator op = new AutoBattleOperator(context.AutoBattleContext, "auto_battle", "空白表达式", readFromMerged: false);
			var result = op.InitBeforeRunning();
			Assert.False(result.Success);
			Assert.Contains("空白", result.Message, StringComparison.Ordinal);
			Assert.Contains("空白表达式.yml", result.Message, StringComparison.Ordinal);
			Assert.False(op.IsRunning);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void InitBeforeRunning_ReportsAllSceneTriggerConflictsWithYamlPaths()
	{
		string root = CreateTempRoot();
		try
		{
			string directory = Path.Combine(root, "config", "auto_battle");
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, "冲突.yml"), "scenes:\n  - triggers: [\"重复\"]\n    handlers: []\n  - triggers: [\"重复\"]\n    handlers: []\n  - triggers: []\n    handlers: []\n  - triggers: []\n    handlers: []");
			using ZContext context = new ZContext(new OneDragonEnvironment(root));
			AutoBattleOperator op = new AutoBattleOperator(context.AutoBattleContext, "auto_battle", "冲突");

			var result = op.InitBeforeRunning();

			Assert.False(result.Success);
			Assert.Contains("冲突.yml", result.Message, StringComparison.Ordinal);
			Assert.Contains("scenes[0].triggers", result.Message, StringComparison.Ordinal);
			Assert.Contains("scenes[1].triggers", result.Message, StringComparison.Ordinal);
			Assert.Contains("scenes[2].triggers", result.Message, StringComparison.Ordinal);
			Assert.Contains("scenes[3].triggers", result.Message, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void InitBeforeRunning_ReportsTemplateReferenceChainForMissingAndCycle()
	{
		string root = CreateTempRoot();
		try
		{
			string config = Path.Combine(root, "config");
			Directory.CreateDirectory(Path.Combine(config, "auto_battle"));
			Directory.CreateDirectory(Path.Combine(config, "auto_battle_state_handler"));
			Directory.CreateDirectory(Path.Combine(config, "auto_battle_operation"));
			File.WriteAllText(Path.Combine(config, "auto_battle", "缺失.yml"), "scenes:\n  - triggers: []\n    handlers:\n      - state_template: 不存在");
			using ZContext missingContext = new ZContext(new OneDragonEnvironment(root));
			var missing = new AutoBattleOperator(missingContext.AutoBattleContext, "auto_battle", "缺失").InitBeforeRunning();
			Assert.False(missing.Success);
			Assert.Contains("缺失.yml", missing.Message, StringComparison.Ordinal);
			Assert.Contains("state_template", missing.Message, StringComparison.Ordinal);

			File.WriteAllText(Path.Combine(config, "auto_battle", "循环.yml"), "scenes:\n  - triggers: []\n    handlers:\n      - state_template: A");
			File.WriteAllText(Path.Combine(config, "auto_battle_state_handler", "A.yml"), "handlers:\n  - state_template: B");
			File.WriteAllText(Path.Combine(config, "auto_battle_state_handler", "B.yml"), "handlers:\n  - state_template: A");
			using ZContext cycleContext = new ZContext(new OneDragonEnvironment(root));
			var cycle = new AutoBattleOperator(cycleContext.AutoBattleContext, "auto_battle", "循环").InitBeforeRunning();
			Assert.False(cycle.Success);
			Assert.Contains("A -> B -> A", cycle.Message, StringComparison.Ordinal);
			Assert.Contains("state_template", cycle.Message, StringComparison.Ordinal);

			File.WriteAllText(Path.Combine(config, "auto_battle", "操作循环.yml"), "scenes:\n  - triggers: []\n    handlers:\n      - states: \"\"\n        operations:\n          - operation_template: 操作A");
			File.WriteAllText(Path.Combine(config, "auto_battle_operation", "操作A.yml"), "operations:\n  - operation_template: 操作B");
			File.WriteAllText(Path.Combine(config, "auto_battle_operation", "操作B.yml"), "operations:\n  - operation_template: 操作A");
			using ZContext operationContext = new ZContext(new OneDragonEnvironment(root));
			var operationCycle = new AutoBattleOperator(operationContext.AutoBattleContext, "auto_battle", "操作循环").InitBeforeRunning();
			Assert.False(operationCycle.Success);
			Assert.Contains("操作A -> 操作B -> 操作A", operationCycle.Message, StringComparison.Ordinal);
			Assert.Contains("operation_template", operationCycle.Message, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task BatchUpdateStates_TriggersDslSceneAndExecutesOperation()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteCondOpConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory));
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "测试配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			op.StartRunningAsync();
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-触发", Now()));
			await WaitUntilStateAsync(zctx, "自定义-命中");
			await WaitUntilIdle(op);
			op.StopRunning();
			Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "started" && record.Trigger == "自定义-触发"));
			Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "finished" && record.Completed));
			Assert.Contains(zctx.OverlayDebugBus.Snapshot().DecisionItems, item =>
				item.Trigger == "自定义-触发" &&
				item.Expression == "模板命中" &&
				item.Operation == "设置状态 自定义-命中" &&
				item.Status == "matched");
			Assert.Contains(zctx.OverlayDebugBus.Snapshot().DecisionItems, item =>
				item.Trigger == "自定义-触发" &&
				item.Operation == "设置状态 自定义-命中" &&
				item.Status == "accepted");
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task BatchUpdateStates_UsesInjectedClockForWindowEvaluation()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteCondOpConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory));
			var clock = new FixedCondOpClock(1000d);
			AutoBattleOperator op = new AutoBattleOperator(
				zctx.AutoBattleContext,
				"auto_battle",
				"测试配置",
				readFromMerged: false,
				clock: clock);
			Assert.True(op.InitBeforeRunning().Success);
			Assert.True(op.StartRunningAsync());

			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-触发", clock.NowSeconds()));
			await WaitUntilStateAsync(zctx, "自定义-命中");
			op.StopRunning();

			Assert.Contains(op.ExecutionRecords, record => record.Event == "started" && record.Trigger == "自定义-触发");
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task TriggerScene_LogsPositionStateAgesForFreshnessDiagnosis()
	{
		string rootDirectory = CreateTempRoot();
		RecordingLogSink sink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		try
		{
			WritePositionStateConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory), logger);
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "位置状态配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			op.StartRunningAsync();
			// 位置状态记录在 1.2 秒前：超出裸状态默认 1 秒窗口，分支不该命中，但状态龄必须可从日志读出
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("前台-耀嘉音", Now() - 1.2));
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-位置触发", Now()));
			LogEvent entry = null;
			for (int i = 0; i < 100 && entry == null; i++)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(20L));
				lock (sink.Events)
				{
					entry = sink.Events.LastOrDefault(e => e.MessageTemplate.Text.StartsWith("自动战斗条件未匹配:", StringComparison.Ordinal));
				}
			}
			await WaitUntilIdle(op);
			op.StopRunning();
			Assert.True(entry != null, "未找到条件未匹配日志");
			Assert.True(entry.Properties.TryGetValue("PositionStateAges", out LogEventPropertyValue ages));
			string agesText = ages.ToString();
			Assert.Contains("前台-耀嘉音=", agesText, StringComparison.Ordinal);
			int recordedAge = int.Parse(agesText.Split("前台-耀嘉音=")[1].TrimEnd('"').Split(',')[0], CultureInfo.InvariantCulture);
			Assert.InRange(recordedAge, 1000, 5000);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NormalSceneLoop_LogsIdleWhenNoHandlerMatches()
	{
		// 战斗中"发呆"此前在日志上完全静默：主循环匹配不到 handler 时不打任何日志。
		// 该诊断必须真的会触发，否则实机复现时仍然无从区分空转与卡死。
		string rootDirectory = CreateTempRoot();
		RecordingLogSink sink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		try
		{
			WriteNeverMatchingNormalSceneConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory), logger);
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "空转配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			op.StartRunningAsync();
			LogEvent entry = null;
			for (int i = 0; i < 150 && entry == null; i++)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(20L));
				lock (sink.Events)
				{
					entry = sink.Events.LastOrDefault(e => e.MessageTemplate.Text.StartsWith("[.NET诊断] 自动战斗主循环空转:", StringComparison.Ordinal));
				}
			}
			op.StopRunning();
			Assert.True(entry != null, "主循环空转超过 1 秒却没有诊断日志");
			Assert.True(entry.Properties.TryGetValue("Reason", out LogEventPropertyValue reason));
			Assert.Contains("NoMatch", reason.ToString(), StringComparison.Ordinal);
			Assert.True(entry.Properties.TryGetValue("IdleMilliseconds", out LogEventPropertyValue idle));
			Assert.True(int.Parse(idle.ToString(), CultureInfo.InvariantCulture) >= 1000);
			Assert.True(entry.Properties.ContainsKey("RunningExecutorCount"));
			Assert.True(entry.Properties.ContainsKey("PositionStateAges"));
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task BatchUpdateStates_DoesNotMatchExpiredDelayedCapture()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteDelayedCaptureConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory));
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "延迟截图配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			op.StartRunningAsync();
			double captureTime = Now() - 0.5;
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-延迟截图触发", captureTime));
			await Task.Delay(250);
			op.StopRunning();
			Assert.DoesNotContain((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Trigger == "自定义-延迟截图触发"));
			Assert.Equal(-1.0, zctx.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-延迟截图命中").LastRecordTime);
			Assert.Equal(captureTime, zctx.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-延迟截图触发").LastRecordTime);
			Assert.Contains(zctx.OverlayDebugBus.Snapshot().DecisionItems, item =>
				item.Trigger == "自定义-延迟截图触发" &&
				item.Expression == "[自定义-延迟截图触发, 0, 0.3]" &&
				item.Operation == string.Empty &&
				item.Status == "not-matched");
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task BatchUpdateStates_HigherPrioritySceneInterruptsLowerPriorityExecution()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WritePriorityConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory));
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "优先级配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			op.StartRunningAsync();
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-低优先级", Now()));
			await WaitUntilRunningAsync(op);
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-高优先级", Now()));
			await WaitUntilStateAsync(zctx, "自定义-高优先级已执行");
			await WaitUntilIdle(op);
			op.StopRunning();
			Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "interrupted" && record.Trigger == "自定义-低优先级"));
			Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "finished" && record.Trigger == "自定义-高优先级"));
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NormalScene_DoesNotRestartWhileHigherPriorityExecutionIsRunning()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteNormalScenePriorityConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory));
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "主循环优先级配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			Assert.True(op.StartRunningAsync());
			await WaitUntilExecutionRecordAsync(op, "started", "主循环");
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-高优先级", Now()));
			await WaitUntilExecutionRecordAsync(op, "started", "自定义-高优先级");
			await Task.Delay(200);
			Assert.Single(op.ExecutionRecords, (AutoBattleExecutionRecord record) => record.Event == "started" && record.Trigger == "主循环");
			Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "started" && record.Trigger == "自定义-高优先级"));
			op.StopRunning();
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task BatchUpdateStates_InterruptStatesStopCurrentExecution()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteInterruptConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory));
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "中断配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			op.StartRunningAsync();
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-开始", Now()));
			await WaitUntilRunningAsync(op);
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-中断", Now()));
			await WaitUntilIdle(op);
			op.StopRunning();
			Assert.Contains((IEnumerable<AutoBattleExecutionRecord>)op.ExecutionRecords, (Predicate<AutoBattleExecutionRecord>)((AutoBattleExecutionRecord record) => record.Event == "interrupt-condition"));
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task BatchUpdateStates_RespectsSceneIntervalForRepeatedTrigger()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntervalConfig(rootDirectory);
			using ZContext zctx = new ZContext(new OneDragonEnvironment(rootDirectory));
			AutoBattleOperator op = new AutoBattleOperator(zctx.AutoBattleContext, "auto_battle", "间隔配置", readFromMerged: false);
			Assert.True(op.InitBeforeRunning().Success);
			op.StartRunningAsync();
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-间隔触发", Now()));
			await WaitUntilStateAsync(zctx, "自定义-间隔命中");
			zctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-间隔触发", Now()));
			await Task.Delay(120);
			op.StopRunning();
			Assert.Single(op.ExecutionRecords, (AutoBattleExecutionRecord record) => record.Event == "started" && record.Trigger == "自定义-间隔触发");
			Assert.Single(op.ExecutionRecords, (AutoBattleExecutionRecord record) => record.Event == "finished" && record.Trigger == "自定义-间隔触发");
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static async Task WaitUntilIdle(AutoBattleOperator op)
	{
		for (int i = 0; i < 20; i++)
		{
			if (op.RunningExecutor == null)
			{
				break;
			}
			await Task.Delay(50);
		}
	}

	private static async Task WaitUntilRunningAsync(AutoBattleOperator op)
	{
		for (int i = 0; i < 40; i++)
		{
			if (op.RunningExecutor?.Running ?? false)
			{
				return;
			}
			await Task.Delay(50);
		}
		Assert.Fail("operator did not start execution");
	}

	private static async Task WaitUntilStateAsync(ZContext context, string stateName)
	{
		for (int i = 0; i < 40; i++)
		{
			if (context.AutoBattleContext.StateRecordService.GetStateRecorder(stateName).LastRecordTime > 0.0)
			{
				return;
			}
			await Task.Delay(50);
		}
		Assert.Fail("state " + stateName + " was not written");
	}

	private static async Task WaitUntilExecutionRecordAsync(AutoBattleOperator op, string @event, string trigger)
	{
		for (int i = 0; i < 40; i++)
		{
			if (op.ExecutionRecords.Any((AutoBattleExecutionRecord record) => record.Event == @event && record.Trigger == trigger))
			{
				return;
			}
			await Task.Delay(50);
		}
		Assert.Fail("未找到执行记录 " + @event + "/" + trigger);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteCondOpConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		string text2 = Path.Combine(rootDirectory, "config", "auto_battle_state_handler");
		string text3 = Path.Combine(rootDirectory, "config", "auto_battle_operation");
		Directory.CreateDirectory(text);
		Directory.CreateDirectory(text2);
		Directory.CreateDirectory(text3);
		File.WriteAllText(Path.Combine(text, "测试配置.yml"), "author: tester\nscenes:\n  - triggers: [\"自定义-触发\"]\n    priority: 5\n    interval: 0\n    handlers:\n      - state_template: \"测试状态模板\"");
		File.WriteAllText(Path.Combine(text2, "测试状态模板.yml"), "handlers:\n  - states: \"[自定义-触发, 0, 1]\"\n    debug_name: \"模板命中\"\n    interrupt_states: \"[自定义-中断, 0, 1]\"\n    operations:\n      - operation_template: \"设置命中\"");
		File.WriteAllText(Path.Combine(text3, "设置命中.yml"), "operations:\n  - op_name: \"设置状态\"\n    state: \"自定义-命中\"");
	}

	private static void WritePriorityConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "优先级配置.yml"), "scenes:\n  - triggers: [\"自定义-低优先级\"]\n    priority: 1\n    interval: 0\n    handlers:\n      - states: \"[自定义-低优先级, 0, 1]\"\n        operations:\n          - op_name: \"等待秒数\"\n            seconds: 5\n  - triggers: [\"自定义-高优先级\"]\n    priority: 5\n    interval: 0\n    handlers:\n      - states: \"[自定义-高优先级, 0, 1]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-高优先级已执行\"");
	}

	private static void WriteNormalScenePriorityConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "主循环优先级配置.yml"), "scenes:\n  - triggers: []\n    priority: 9\n    interval: 0\n    handlers:\n      - states: \"\"\n        operations:\n          - op_name: \"等待秒数\"\n            seconds: 5\n  - triggers: [\"自定义-高优先级\"]\n    priority: 97\n    interval: 0\n    handlers:\n      - states: \"[自定义-高优先级, 0, 1]\"\n        operations:\n          - op_name: \"等待秒数\"\n            seconds: 1");
	}

	private static void WriteNeverMatchingNormalSceneConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		// 主循环场景的守卫依赖一个永远不会被设置的状态：模拟"引擎在转但没有 handler 成立"
		File.WriteAllText(Path.Combine(text, "空转配置.yml"), "scenes:\n  - triggers: []\n    priority: 9\n    interval: 0.02\n    handlers:\n      - states: \"[自定义-永不设置, 0, 1]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-不该命中\"");
	}

	private static void WritePositionStateConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "位置状态配置.yml"), "scenes:\n  - triggers: [\"自定义-位置触发\"]\n    priority: 5\n    interval: 0\n    handlers:\n      - states: \"[前台-耀嘉音]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-位置命中\"");
	}

	private sealed class RecordingLogSink : ILogEventSink
	{
		public List<LogEvent> Events { get; } = new List<LogEvent>();

		public void Emit(LogEvent logEvent)
		{
			lock (Events)
			{
				Events.Add(logEvent);
			}
		}
	}

	private sealed class FixedCondOpClock(double nowSeconds) : ICondOpClock
	{
		public double NowSeconds() => nowSeconds;

		public DateTimeOffset UtcNow => DateTimeOffset.FromUnixTimeMilliseconds((long)(nowSeconds * 1000d));
	}

	private static void WriteDelayedCaptureConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "延迟截图配置.yml"), "scenes:\n  - triggers: [\"自定义-延迟截图触发\"]\n    priority: 5\n    interval: 0\n    handlers:\n      - states: \"[自定义-延迟截图触发, 0, 0.3]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-延迟截图命中\"");
	}

	private static void WriteInterruptConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "中断配置.yml"), "scenes:\n  - triggers: [\"自定义-开始\"]\n    priority: 5\n    interval: 0\n    handlers:\n      - states: \"[自定义-开始, 0, 1]\"\n        interrupt_states: \"[自定义-中断, 0, 1]\"\n        operations:\n          - op_name: \"等待秒数\"\n            seconds: 5");
	}

	private static void WriteIntervalConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "间隔配置.yml"), "scenes:\n  - triggers: [\"自定义-间隔触发\"]\n    priority: 5\n    interval: 5\n    handlers:\n      - states: \"[自定义-间隔触发, 0, 1]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-间隔命中\"");
	}

	private static double Now()
	{
		return (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
	}
}
