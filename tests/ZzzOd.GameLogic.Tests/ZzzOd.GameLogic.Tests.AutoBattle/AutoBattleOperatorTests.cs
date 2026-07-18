using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
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

		private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(initialState: false);

		public int ExecuteCount { get; private set; }

		public int StopCount { get; private set; }

		public ManualResetEventSlim Started { get; } = new ManualResetEventSlim(initialState: false);

		public RecordingAtomicOp(string opName, bool asyncOp = false, bool blockUntilStopped = false, bool throwOnExecute = false)
			: base(opName, asyncOp)
		{
			_blockUntilStopped = blockUntilStopped;
			_throwOnExecute = throwOnExecute;
		}

		public override void Execute()
		{
			ExecuteCount++;
			Started.Set();
			if (_throwOnExecute)
			{
				throw new InvalidOperationException("boom");
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
	public async Task OperationExecutor_RecordsErrorAndStopsSequence()
	{
		RecordingAtomicOp failing = new RecordingAtomicOp("bad", asyncOp: false, blockUntilStopped: false, throwOnExecute: true);
		RecordingAtomicOp skipped = new RecordingAtomicOp("skipped");
		OperationExecutor executor = new OperationExecutor(new AtomicOp[2] { failing, skipped }, 1.0);
		Assert.False(await executor.RunAsync());
		Assert.NotNull(executor.LastException);
		Assert.Equal(0, skipped.ExecuteCount);
		Assert.Contains((IEnumerable<OperationExecutionStepRecord>)executor.Records, (Predicate<OperationExecutionStepRecord>)((OperationExecutionStepRecord record) => record.OpName == "bad" && record.Event == "error"));
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
