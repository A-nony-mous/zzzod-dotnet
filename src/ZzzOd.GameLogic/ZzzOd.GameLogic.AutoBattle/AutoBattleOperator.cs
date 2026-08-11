using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Events;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using OneDragon.Core.Utils;
using Serilog;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.AutoBattle.AtomicOp;

namespace ZzzOd.GameLogic.AutoBattle;

public class AutoBattleOperator : IStateRecordUpdateListener
{
	public event Action<AutoBattleExecutionRecord>? ExecutionRecordAdded;
	internal const string FallbackTemplateName = "全配队通用";

	// 调度器为进程内共享：容量需覆盖并发运行的 operator 数量（主循环 + 周期动作各占一条），
	// 续体现在固定回到这些线程上，容量不足会直接表现为循环节奏被拖慢。
	private static readonly DedicatedTaskScheduler ConditionalOperationScheduler = new DedicatedTaskScheduler("zzz-conditional-operation", 8);

	private static readonly TaskFactory ConditionalOperationExecutor = new TaskFactory(ConditionalOperationScheduler);

	private static readonly DedicatedTaskScheduler PeriodicOperationScheduler = new DedicatedTaskScheduler("zzz-periodic-operation", 8);

	private static readonly TaskFactory PeriodicOperationExecutor = new TaskFactory(PeriodicOperationScheduler);

	private readonly AutoBattleContext _ctx;

	private readonly string _subDir;

	private readonly string _templateName;

	private readonly ICondOpClock _clock;

	private readonly Func<OperationDef, OneDragon.Core.Operation.AtomicOp> _atomicOpFactory;

	private readonly Action<Action>? _sceneDispatcher;

	private readonly bool _runNormalSceneLoop;

	private readonly object _taskLock = new object();

	private readonly List<AutoBattleExecutionRecord> _executionRecords = new List<AutoBattleExecutionRecord>();

	private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

	private readonly Dictionary<string, AutoBattleCondOpScene> _triggerToScene = new Dictionary<string, AutoBattleCondOpScene>(StringComparer.Ordinal);

	private readonly Dictionary<int, double> _lastTriggerTime = new Dictionary<int, double>();

	private long _lastNormalLoopProgressAtMs;

	private long _lastNormalLoopDiagnosticAtMs;

	private readonly object _scenePositionStatesLock = new object();

	private readonly Dictionary<AutoBattleCondOpScene, IReadOnlyList<string>> _scenePositionStates = new Dictionary<AutoBattleCondOpScene, IReadOnlyList<string>>();

	private bool _inited;

	private CancellationTokenSource? _cts;

	private int _periodicGeneration = 0;

	private int _runningExecutorCount;

	private Task<bool>? _runningExecutionTask;

	private ExecutionInfo? _naturalCompletionPriorityProtection;

	private string? _lastLoadedYamlPath;

	private string? _configurationYamlPath;

	private readonly HashSet<string> _loadedYamlPaths = new(StringComparer.OrdinalIgnoreCase);

	internal string? ConfigurationYamlPath => _configurationYamlPath;

	internal IReadOnlyList<string> LoadedYamlPaths => _loadedYamlPaths.ToArray();

	internal string SourceFingerprint { get; private set; } = string.Empty;

	private Task? _normalSceneLoopTask;

	private Task? _periodicLoopTask;

	public string Author { get; set; } = string.Empty;

	public string Homepage { get; set; } = string.Empty;

	public string Thanks { get; set; } = string.Empty;

	public string Version { get; set; } = string.Empty;

	public string Introduction { get; set; } = string.Empty;

	public List<List<string>> TeamList { get; set; } = new List<List<string>>();

	public AutoBattleInterval CheckDodgeInterval { get; set; } = new AutoBattleInterval(0.02f, 0.02f);

	public AutoBattleInterval CheckAgentInterval { get; set; } = new AutoBattleInterval(0.5f, 0.5f);

	public AutoBattleInterval CheckChainInterval { get; set; } = new AutoBattleInterval(1f, 1f);

	public AutoBattleInterval CheckQuickInterval { get; set; } = new AutoBattleInterval(0.5f, 0.5f);

	public AutoBattleInterval CheckEndInterval { get; set; } = new AutoBattleInterval(5f, 5f);

	public float TargetLockInterval { get; set; } = 1f;

	public float AbnormalStatusInterval { get; set; } = 0f;

	public float AutoLockInterval { get; set; } = 1f;

	public float AutoTurnInterval { get; set; } = 2f;

	public string TemplateName => _templateName;

	public double LastLockTime { get; set; }

	public double LastTurnTime { get; set; }

	public bool IsRunning { get; private set; }

	public ExecutionInfo? CurrentExecutionInfo { get; private set; }

	public OperationExecutor? RunningExecutor { get; private set; }

	public IReadOnlyList<AutoBattleCondOpScene> Scenes { get; private set; } = Array.Empty<AutoBattleCondOpScene>();

	public AutoBattleCondOpScene? NormalScene { get; private set; }

	public HashSet<string> UsageStates
	{
		get
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			if (NormalScene != null)
			{
				hashSet.UnionWith(NormalScene.UsageStates);
			}
			foreach (KeyValuePair<string, AutoBattleCondOpScene> item in _triggerToScene)
			{
				hashSet.Add(item.Key);
				hashSet.UnionWith(item.Value.UsageStates);
			}
			return hashSet;
		}
	}

	public IReadOnlyList<AutoBattleExecutionRecord> ExecutionRecords
	{
		get
		{
			lock (_taskLock)
			{
				return _executionRecords.ToArray();
			}
		}
	}

	public AutoBattleOperator(
		AutoBattleContext ctx,
		string subDir,
		string templateName,
		bool readFromMerged = false,
		ICondOpClock? clock = null,
		Func<OperationDef, OneDragon.Core.Operation.AtomicOp>? atomicOpFactory = null,
		Action<Action>? sceneDispatcher = null,
		bool runNormalSceneLoop = true)
	{
		_ctx = ctx;
		_subDir = subDir;
		_templateName = templateName;
		_ = readFromMerged;
		_clock = clock ?? new SystemCondOpClock();
		_atomicOpFactory = atomicOpFactory ?? _ctx.AtomicOpFactory.GetAtomicOp;
		_sceneDispatcher = sceneDispatcher;
		_runNormalSceneLoop = runNormalSceneLoop;
	}

	public (bool Success, string Message) InitBeforeRunning()
	{
		try
		{
			_lastLoadedYamlPath = null;
			_configurationYamlPath = null;
			_loadedYamlPaths.Clear();
			// 与销毁流程保持一致的"先停后建"顺序：重新初始化前先停掉旧的运行状态，
			// 避免旧场景循环、周期动作在新配置构建完成前继续基于旧数据运行。
			StopRunning();
			_ctx.StateRecordService.UnregisterOperator(this);
			Load();
			Build();
			SourceFingerprint = GetSourceFingerprint(_loadedYamlPaths);
			_ctx.StateRecordService.RegisterOperator(this);
			_inited = true;
			return (Success: true, Message: string.Empty);
		}
		catch (Exception ex)
		{
			_inited = false;
			string configPath = _lastLoadedYamlPath ?? ResolveYamlPath(_subDir, _templateName);
			Log.Error(ex, "自动战斗初始化失败: ConfigPath={ConfigPath} 如果是共享配队文件请在群内提醒对应作者修复", configPath);
			return (Success: false, Message: $"自动战斗配置 {configPath} 初始化失败: {ex.Message}");
		}
	}

	public void LoadOtherInfo(Dictionary<string, object> data)
	{
		Author = GetString(data, "author", string.Empty);
		Homepage = GetString(data, "homepage", "https://qm.qq.com/q/wuVRYuZzkA");
		Thanks = GetString(data, "thanks", string.Empty);
		Version = GetString(data, "version", string.Empty);
		Introduction = GetString(data, "introduction", string.Empty);
		TeamList = GetTeamList(data, "team_list");
		CheckDodgeInterval = GetInterval(data, "check_dodge_interval", new AutoBattleInterval(0.02f, 0.02f));
		CheckAgentInterval = GetInterval(data, "check_agent_interval", new AutoBattleInterval(0.5f, 0.5f));
		CheckChainInterval = GetInterval(data, "check_chain_interval", new AutoBattleInterval(1f, 1f));
		CheckQuickInterval = GetInterval(data, "check_quick_interval", new AutoBattleInterval(0.5f, 0.5f));
		CheckEndInterval = GetInterval(data, "check_end_interval", new AutoBattleInterval(5f, 5f));
		TargetLockInterval = GetFloat(data, "target_lock_interval", 1f);
		AbnormalStatusInterval = GetFloat(data, "abnormal_status_interval", 0f);
		AutoLockInterval = GetFloat(data, "auto_lock_interval", 1f);
		AutoTurnInterval = GetFloat(data, "auto_turn_interval", 2f);
	}

	public OneDragon.Core.Operation.AtomicOp GetAtomicOp(OperationDef opDef)
	{
		return _atomicOpFactory(opDef);
	}

	public bool SubmitExecution(ExecutionInfo executionInfo, string? trigger = null, double? triggerTime = null, bool consumeNaturalCompletionProtection = false)
	{
		ArgumentNullException.ThrowIfNull(executionInfo, "executionInfo");
		lock (_taskLock)
		{
			if (consumeNaturalCompletionProtection)
			{
				_naturalCompletionPriorityProtection = null;
			}
			OperationExecutor runningExecutor = RunningExecutor;
			ExecutionInfo? protectedExecution = runningExecutor != null && runningExecutor.Running
				? CurrentExecutionInfo
				: _naturalCompletionPriorityProtection;
			if (protectedExecution != null && !CanInterrupt(protectedExecution, executionInfo))
			{
				AddExecutionRecordLocked("rejected", trigger ?? executionInfo.TriggerDisplay, executionInfo, completed: false, "priority-blocked", triggerTime);
				PublishExecutionDecision(
					executionInfo,
					trigger ?? executionInfo.TriggerDisplay,
					"priority-blocked",
					new Dictionary<string, object?>
					{
						["current_priority"] = protectedExecution.Priority,
						["candidate_priority"] = executionInfo.Priority,
					});
				return false;
			}
			_naturalCompletionPriorityProtection = null;
			_runningExecutorCount++;
			runningExecutor = RunningExecutor;
			if (runningExecutor != null && runningExecutor.Running)
			{
				StopRunningTaskLocked("interrupted");
			}
			executionInfo.Trigger = trigger ?? executionInfo.Trigger;
			CurrentExecutionInfo = executionInfo;
			RunningExecutor = new OperationExecutor(executionInfo.OpList, triggerTime ?? Now());
			_runningExecutionTask = RunningExecutor.RunAsync();
			OperationExecutor executor = RunningExecutor;
			AddExecutionRecordLocked("started", executionInfo.TriggerDisplay, executionInfo, completed: false, triggerTime: triggerTime ?? executor.TriggerTime);
			PublishExecutionDecision(
				executionInfo,
				executionInfo.TriggerDisplay,
				"accepted",
				new Dictionary<string, object?>
				{
					["trigger_time"] = triggerTime ?? executor.TriggerTime,
				});
			_ctx.ZContext.Logger.Information("自动战斗执行开始: Template={Template}, Trigger={Trigger}, Operations={Operations}", _templateName, executionInfo.TriggerDisplay, string.Join(" | ", executionInfo.OpList.Select((OneDragon.Core.Operation.AtomicOp operation) => operation.OpName)));
			// 收尾回调走专用调度器：留在共享 ThreadPool 上会被视觉识别任务挤占，
			// 使执行计数的归还与"执行结束"日志时间都被推迟数百毫秒。
			_runningExecutionTask.ContinueWith(delegate(Task<bool> task)
			{
				OnExecutionDone(executor, executionInfo, task);
			}, CancellationToken.None, TaskContinuationOptions.None, ConditionalOperationScheduler);
			return true;
		}
	}

	public bool TryInterrupt(ExecutionInfo executionInfo, string trigger, double? triggerTime = null)
	{
		return SubmitExecution(executionInfo, trigger, triggerTime);
	}

	public bool InterruptIfCurrentMatches(double now)
	{
		lock (_taskLock)
		{
			OperationExecutor runningExecutor = RunningExecutor;
			if (runningExecutor == null || !runningExecutor.Running || CurrentExecutionInfo?.InterruptCalTree == null || !CurrentExecutionInfo.InterruptCalTree.InTimeRange(now))
			{
				return false;
			}
			StopRunningTaskLocked("interrupt-condition");
			return true;
		}
	}

	public Task<bool>? GetRunningExecutionTask()
	{
		lock (_taskLock)
		{
			return _runningExecutionTask;
		}
	}

	public AutoBattleOperatorRuntimeSnapshot GetRuntimeSnapshot()
	{
		lock (_taskLock)
		{
			ExecutionInfo currentExecutionInfo = CurrentExecutionInfo;
			OperationExecutor runningExecutor = RunningExecutor;
			return new AutoBattleOperatorRuntimeSnapshot(IsRunning, currentExecutionInfo?.TriggerDisplay, currentExecutionInfo?.ExprDisplay, runningExecutor?.StartedAtUtc, UsageStates.OrderBy<string, string>((string state) => state, StringComparer.Ordinal).ToArray());
		}
	}

	public bool StartRunningAsync()
	{
		lock (_taskLock)
		{
			if (!_inited)
			{
				Log.Error("未完成初始化 无法运行");
				return false;
			}
			if (IsRunning)
			{
				return false;
			}
			_cts?.Dispose();
			_cts = new CancellationTokenSource();
			IsRunning = true;
			_runningExecutorCount = 0;
			_naturalCompletionPriorityProtection = null;
			Interlocked.Exchange(ref _lastNormalLoopProgressAtMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			Interlocked.Exchange(ref _lastNormalLoopDiagnosticAtMs, 0L);
			_periodicGeneration++;
			int gen = _periodicGeneration;
			CancellationToken token = _cts.Token;
			if (NormalScene != null && _runNormalSceneLoop)
			{
				_normalSceneLoopTask = PeriodicOperationExecutor.StartNew(() => RunNormalSceneLoopAsync(gen, token), token).Unwrap();
				ObserveBackgroundTask(_normalSceneLoopTask, "自动战斗常驻场景");
			}
			_periodicLoopTask = PeriodicOperationExecutor.StartNew(() => OperatePeriodicallyAsync(gen, token), token).Unwrap();
			ObserveBackgroundTask(_periodicLoopTask, "自动战斗周期动作");
			return true;
		}
	}

	public void BatchUpdateStates(IReadOnlyList<StateRecord> stateRecords)
	{
		if (!IsRunning || !_inited)
		{
			return;
		}
		AutoBattleCondOpScene topPriorityScene = null;
		string topPriorityState = null;
		double topPriorityStateTime = 0.0;
		foreach (StateRecord stateRecord in stateRecords)
		{
			double num = ((stateRecord.TriggerTime > 0.0) ? stateRecord.TriggerTime : Now());
			if (!stateRecord.IsClear && _triggerToScene.TryGetValue(stateRecord.StateName, out AutoBattleCondOpScene value))
			{
				bool flag = topPriorityScene == null;
				if (!flag && !topPriorityScene.Priority.HasValue)
				{
					flag = true;
				}
				else if (!flag && value.Priority.HasValue && topPriorityScene.Priority.HasValue && value.Priority > topPriorityScene.Priority)
				{
					flag = true;
				}
				if (flag)
				{
					topPriorityScene = value;
					topPriorityState = stateRecord.StateName;
					topPriorityStateTime = num;
				}
			}
		}
		if (topPriorityScene != null && topPriorityState != null)
		{
			Action action = delegate
			{
				TriggerScene(topPriorityState, topPriorityScene, topPriorityStateTime);
			};
			if (_sceneDispatcher != null)
			{
				_sceneDispatcher(action);
			}
			else
			{
				Task task = ConditionalOperationExecutor.StartNew(action);
				ObserveBackgroundTask(task, "自动战斗条件场景");
			}
		}
		else
		{
			InterruptIfCurrentMatches(Now());
		}
	}

	private async Task OperatePeriodicallyAsync(int generation, CancellationToken token)
	{
		if (AutoLockInterval <= 0f && AutoTurnInterval <= 0f)
		{
			return;
		}
		while (IsRunning && _periodicGeneration == generation && !token.IsCancellationRequested)
		{
			if (!_ctx.HasZPcController)
			{
				try
				{
					await Task.Delay(200, token);
				}
				catch (TaskCanceledException)
				{
					break;
				}
				continue;
			}
			double now = _clock.NowSeconds();
			if (!_ctx.LastCheckInBattle)
			{
				try
				{
					await Task.Delay(200, token);
				}
				catch (TaskCanceledException)
				{
					break;
				}
				continue;
			}
			bool anyDone = false;
			if (!IsRunning)
			{
				break;
			}
			if (AutoLockInterval > 0f && now - LastLockTime > (double)AutoLockInterval)
			{
				AtomicBtnLock lockOp = new AtomicBtnLock(_ctx, new OperationDef
				{
					OpName = BattleStateEnum.BtnLock.GetDescription()
				});
				lockOp.Execute();
				LastLockTime = now;
				_ctx.ZContext.Logger.Information("自动战斗周期动作: Template={Template}, Action=锁定目标, IntervalSeconds={IntervalSeconds}", _templateName, AutoLockInterval);
				anyDone = true;
			}
			if (!IsRunning)
			{
				break;
			}
			if (AutoTurnInterval > 0f && now - LastTurnTime > (double)AutoTurnInterval)
			{
				AtomicTurn turnOp = new AtomicTurn(_ctx, 100f);
				turnOp.Execute();
				LastTurnTime = now;
				_ctx.ZContext.Logger.Information("自动战斗周期动作: Template={Template}, Action=自动转向, Distance=100, IntervalSeconds={IntervalSeconds}", _templateName, AutoTurnInterval);
				anyDone = true;
			}
			if (!anyDone)
			{
				try
				{
					await Task.Delay(200, token);
				}
				catch (TaskCanceledException)
				{
					break;
				}
			}
		}
	}

	public void StopRunning()
	{
		IsRunning = false;
		_cts?.Cancel();
		lock (_taskLock)
		{
			StopRunningTaskLocked("stopped");
		}
	}

	public void Dispose()
	{
		StopRunning();
		_ctx.StateRecordService.UnregisterOperator(this);
		_cts?.Dispose();
		_cts = null;
		_normalSceneLoopTask = null;
		_periodicLoopTask = null;
	}

	private void Load()
	{
		Dictionary<string, object> dictionary = LoadYamlConfig(_subDir, ResolveTemplateName());
		_configurationYamlPath = _lastLoadedYamlPath;
		LoadOtherInfo(dictionary.ToDictionary<KeyValuePair<string, object>, string, object>((KeyValuePair<string, object> pair) => pair.Key, (KeyValuePair<string, object> pair) => pair.Value ?? new object(), StringComparer.Ordinal));
		Scenes = (from scene in AutoBattleCondOpScene.GetDictionaryList(dictionary, "scenes")
			select new AutoBattleCondOpScene(scene)).ToList();
		ValidateScenes(Scenes, _configurationYamlPath ?? string.Empty);
		ExpandTemplates();
	}

	private void Build()
	{
		_triggerToScene.Clear();
		_lastTriggerTime.Clear();
		NormalScene = null;
		foreach (AutoBattleCondOpScene scene in Scenes)
		{
			scene.Build(_ctx.StateRecordService.GetStateRecorder, GetAtomicOp);
			if (scene.Triggers.Count > 0)
			{
				foreach (string trigger in scene.Triggers)
				{
					_triggerToScene[trigger] = scene;
				}
			}
			else
			{
				NormalScene = scene;
			}
		}
	}

	private void ExpandTemplates()
	{
		for (int sceneIndex = 0; sceneIndex < Scenes.Count; sceneIndex++)
		{
			AutoBattleCondOpScene scene = Scenes[sceneIndex];
			List<AutoBattleCondOpStateHandler> list = new List<AutoBattleCondOpStateHandler>();
			for (int handlerIndex = 0; handlerIndex < scene.Handlers.Count; handlerIndex++)
			{
				list.AddRange(ExpandStateHandler(
					scene.Handlers[handlerIndex],
					[],
					$"{_configurationYamlPath} -> scenes[{sceneIndex}].handlers[{handlerIndex}].state_template"));
			}
			scene.SetHandlers(list);
		}
	}

	private List<AutoBattleCondOpStateHandler> ExpandStateHandler(AutoBattleCondOpStateHandler handler, List<string> stateHandlerTemplates, string referencePath)
	{
		if (!string.IsNullOrWhiteSpace(handler.StateTemplate))
		{
			if (stateHandlerTemplates.Contains(handler.StateTemplate, StringComparer.Ordinal))
			{
				throw new InvalidOperationException($"状态处理器模板循环引用: {string.Join(" -> ", stateHandlerTemplates.Append(handler.StateTemplate))}; {referencePath}");
			}
			stateHandlerTemplates.Add(handler.StateTemplate);
			Dictionary<string, object> data = LoadYamlConfig("auto_battle_state_handler", handler.StateTemplate, referencePath);
			string sourcePath = _lastLoadedYamlPath ?? referencePath;
			List<AutoBattleCondOpStateHandler> list = new List<AutoBattleCondOpStateHandler>();
			List<Dictionary<string, object>> handlers = AutoBattleCondOpScene.GetDictionaryList(data, "handlers");
			for (int handlerIndex = 0; handlerIndex < handlers.Count; handlerIndex++)
			{
				list.AddRange(ExpandStateHandler(new AutoBattleCondOpStateHandler(handlers[handlerIndex]), stateHandlerTemplates, $"{sourcePath} -> handlers[{handlerIndex}].state_template"));
			}
			stateHandlerTemplates.RemoveAt(stateHandlerTemplates.Count - 1);
			return list;
		}
		if (handler.SubHandlers.Count > 0)
		{
			List<AutoBattleCondOpStateHandler> list2 = new List<AutoBattleCondOpStateHandler>();
			for (int handlerIndex = 0; handlerIndex < handler.SubHandlers.Count; handlerIndex++)
			{
				list2.AddRange(ExpandStateHandler(handler.SubHandlers[handlerIndex], stateHandlerTemplates, $"{referencePath} -> sub_handlers[{handlerIndex}].state_template"));
			}
			handler.SetSubHandlers(list2);
		}
		else if (handler.Operations.Count > 0)
		{
			List<OperationDef> list3 = new List<OperationDef>();
			for (int operationIndex = 0; operationIndex < handler.Operations.Count; operationIndex++)
			{
				list3.AddRange(ExpandOperation(handler.Operations[operationIndex], [], $"{referencePath} -> operations[{operationIndex}].operation_template"));
			}
			handler.SetOperations(list3);
		}
		int num = 1;
		List<AutoBattleCondOpStateHandler> list4 = new List<AutoBattleCondOpStateHandler>(num);
		CollectionsMarshal.SetCount(list4, num);
		CollectionsMarshal.AsSpan(list4)[0] = handler;
		return list4;
	}

	private List<OperationDef> ExpandOperation(OperationDef operation, List<string> operationTemplates, string referencePath)
	{
		if (string.IsNullOrWhiteSpace(operation.OperationTemplate))
		{
			int num = 1;
			List<OperationDef> list = new List<OperationDef>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = operation;
			return list;
		}
		if (operationTemplates.Contains(operation.OperationTemplate, StringComparer.Ordinal))
		{
			throw new InvalidOperationException($"指令模板循环引用: {string.Join(" -> ", operationTemplates.Append(operation.OperationTemplate))}; {referencePath}");
		}
		operationTemplates.Add(operation.OperationTemplate);
		Dictionary<string, object> data = LoadYamlConfig("auto_battle_operation", operation.OperationTemplate, referencePath);
		string sourcePath = _lastLoadedYamlPath ?? referencePath;
		List<OperationDef> list2 = new List<OperationDef>();
		List<Dictionary<string, object>> operations = AutoBattleCondOpScene.GetDictionaryList(data, "operations");
		for (int operationIndex = 0; operationIndex < operations.Count; operationIndex++)
		{
			list2.AddRange(ExpandOperation(new OperationDef(operations[operationIndex]), operationTemplates, $"{sourcePath} -> operations[{operationIndex}].operation_template"));
		}
		operationTemplates.RemoveAt(operationTemplates.Count - 1);
		return list2;
	}

	private async Task RunNormalSceneLoopAsync(int generation, CancellationToken token)
	{
		while (IsRunning && _periodicGeneration == generation && !token.IsCancellationRequested)
		{
			AutoBattleCondOpScene scene = NormalScene;
			if (scene == null)
			{
				break;
			}
			if (Volatile.Read(in _runningExecutorCount) > 0)
			{
				LogNormalLoopIdle("WaitingExecutor", scene);
				await DelayNoThrow(TimeSpan.FromMilliseconds(20L), token);
				continue;
			}
			NormalSceneEvaluationResult evaluation = EvaluateNormalScene(scene);
			// 等待时间不能写在锁里，需尽快释放锁。
			if (evaluation.ToSleepSeconds.HasValue)
			{
				await DelayNoThrow(TimeSpan.FromSeconds(evaluation.ToSleepSeconds.Value), token);
				continue;
			}
			await DelayNoThrow(TimeSpan.FromMilliseconds(20L), token);
		}
	}

	internal bool RunNormalSceneReplayTick()
	{
		AutoBattleCondOpScene? scene = NormalScene;
		if (!IsRunning || !_inited || scene == null || Volatile.Read(in _runningExecutorCount) > 0)
		{
			return false;
		}

		return EvaluateNormalScene(scene).Submitted;
	}

	private NormalSceneEvaluationResult EvaluateNormalScene(AutoBattleCondOpScene scene)
	{
		// 读取/判断冷却时间、匹配执行、写回触发时间、提交执行整段作为一个原子操作上锁，
		// 避免与状态触发路径（TriggerScene）在 _lastTriggerTime 上出现竞态。
		// SubmitExecution 内部同样会对 _taskLock 加锁，但同一线程重入不会死锁。
		lock (_taskLock)
		{
			double triggerTime = Now();
			int sceneId = scene.GetHashCode();
			double lastTriggerTime = _lastTriggerTime.GetValueOrDefault(sceneId);
			double pastTime = triggerTime - lastTriggerTime;
			if (pastTime < scene.IntervalSeconds)
			{
				return new NormalSceneEvaluationResult(false, scene.IntervalSeconds - pastTime);
			}

			ExecutionInfo? executionInfo = scene.MatchExecution(triggerTime);
			if (executionInfo == null)
			{
				LogNormalLoopIdle("NoMatch", scene);
				return new NormalSceneEvaluationResult(false, null);
			}

			executionInfo.Priority = scene.Priority;
			PublishExecutionDecision(
				executionInfo,
				executionInfo.TriggerDisplay,
				"matched",
				CreateSceneDecisionMetadata(scene, triggerTime));
			_lastTriggerTime[sceneId] = triggerTime;
			SubmitExecution(executionInfo, null, triggerTime, consumeNaturalCompletionProtection: true);
			Interlocked.Exchange(ref _lastNormalLoopProgressAtMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			return new NormalSceneEvaluationResult(true, null);
		}
	}

	private readonly record struct NormalSceneEvaluationResult(bool Submitted, double? ToSleepSeconds);

	/// <summary>
	/// 主循环连续空转（既没提交执行、也没被打断）超过 1 秒时按秒记录一次。
	/// 战斗中"发呆"在日志上此前是完全静默的：主循环匹配不到 handler 时不打任何日志，
	/// 因而无法区分"引擎在转但没有 handler 的守卫成立"与"引擎被未归还的执行计数卡住"。
	/// </summary>
	private void LogNormalLoopIdle(string reason, AutoBattleCondOpScene scene)
	{
		long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long idleMilliseconds = nowMs - Interlocked.Read(ref _lastNormalLoopProgressAtMs);
		if (idleMilliseconds < 1000L)
		{
			return;
		}
		long lastDiagnostic = Interlocked.Read(ref _lastNormalLoopDiagnosticAtMs);
		if (nowMs - lastDiagnostic < 1000L || Interlocked.CompareExchange(ref _lastNormalLoopDiagnosticAtMs, nowMs, lastDiagnostic) != lastDiagnostic)
		{
			return;
		}
		_ctx.ZContext.Logger.Information("[.NET诊断] 自动战斗主循环空转: Reason={Reason}, IdleMilliseconds={IdleMilliseconds}, RunningExecutorCount={RunningExecutorCount}, PositionStateAges={PositionStateAges}", reason, idleMilliseconds, Volatile.Read(in _runningExecutorCount), DescribePositionStateAges(scene, Now()));
	}

	private void TriggerScene(string triggerState, AutoBattleCondOpScene scene, double stateMatchTime)
	{
		lock (_taskLock)
		{
			if (!IsRunning)
			{
				return;
			}
			double num = Now();
			int hashCode = scene.GetHashCode();
			if (num - _lastTriggerTime.GetValueOrDefault(hashCode) < scene.IntervalSeconds)
			{
				return;
			}
			double num2 = Math.Max(0.0, num - stateMatchTime) * 1000.0;
			string positionStateAges = DescribePositionStateAges(scene, num);
			ExecutionInfo executionInfo = scene.MatchExecution(num);
			if (executionInfo == null)
			{
				PublishSceneDecision(
					triggerState,
					scene,
					"not-matched",
					stateMatchTime,
					num2);
				_ctx.ZContext.Logger.Information("自动战斗条件未匹配: TriggerState={TriggerState}, StateAgeMilliseconds={StateAgeMilliseconds:F0}, StateMatchTime={StateMatchTime}, PositionStateAges={PositionStateAges}, Expression={Expression}", triggerState, num2, stateMatchTime, positionStateAges, GetSceneExpressionDisplay(scene));
				return;
			}
			_ctx.ZContext.Logger.Information("自动战斗条件命中: TriggerState={TriggerState}, StateAgeMilliseconds={StateAgeMilliseconds:F0}, StateMatchTime={StateMatchTime}, PositionStateAges={PositionStateAges}, Expression={Expression}", triggerState, num2, stateMatchTime, positionStateAges, executionInfo.ExprDisplay);
			executionInfo.Priority = scene.Priority;
			PublishExecutionDecision(
				executionInfo,
				triggerState,
				"matched",
				CreateSceneDecisionMetadata(scene, stateMatchTime, num2));
			if (SubmitExecution(executionInfo, triggerState, num))
			{
				_lastTriggerTime[hashCode] = num;
			}
		}
	}

	private static string GetSceneExpressionDisplay(AutoBattleCondOpScene scene)
	{
		return string.Join(" || ", scene.Handlers.Select((AutoBattleCondOpStateHandler handler) => handler.DisplayName ?? handler.States));
	}

	/// <summary>
	/// 输出本场景分支表达式所依赖的前台/后台位置状态在评估时刻的状态龄（毫秒）。
	/// 条件引擎对裸状态默认只认最近 1 秒，这些状态一旦过期就会让分支落到兜底路径，
	/// 因此验收和排查都需要能从日志直接读出它们的新鲜度。
	/// </summary>
	private string DescribePositionStateAges(AutoBattleCondOpScene scene, double now)
	{
		IReadOnlyList<string> positionStates = GetScenePositionStates(scene);
		if (positionStates.Count == 0)
		{
			return "无";
		}
		List<string> parts = new List<string>(positionStates.Count);
		foreach (string stateName in positionStates)
		{
			StateRecorder recorder = _ctx.StateRecordService.GetStateRecorder(stateName);
			if (recorder == null)
			{
				continue;
			}
			double lastRecordTime = recorder.LastRecordTime;
			if (lastRecordTime <= 0.0)
			{
				continue;
			}
			parts.Add($"{stateName}={Math.Max(0.0, now - lastRecordTime) * 1000.0:F0}");
		}
		return (parts.Count == 0) ? "无" : string.Join(",", parts);
	}

	private IReadOnlyList<string> GetScenePositionStates(AutoBattleCondOpScene scene)
	{
		lock (_scenePositionStatesLock)
		{
			if (_scenePositionStates.TryGetValue(scene, out IReadOnlyList<string> cached))
			{
				return cached;
			}
			string[] positionStates = (from state in scene.UsageStates
				where state.StartsWith("前台-", StringComparison.Ordinal) || state.StartsWith("后台-", StringComparison.Ordinal)
				orderby state, StringComparer.Ordinal
				select state).ToArray();
			_scenePositionStates[scene] = positionStates;
			return positionStates;
		}
	}

	private string ResolveTemplateName()
	{
		string path = ResolveYamlPath(_subDir, _templateName);
		if (File.Exists(path))
		{
			return _templateName;
		}
		// 找不到对应配队的自定义配置时会回退到全配队通用配置，记录一次告警方便定位是否为预期行为
		_ctx.ZContext.Logger.Warning("自动战斗配置回退: OriginalTemplate={OriginalTemplate}, FallbackTemplate={FallbackTemplate}, SubDir={SubDir}", _templateName, FallbackTemplateName, _subDir);
		return FallbackTemplateName;
	}

	private Dictionary<string, object?> LoadYamlConfig(string subDir, string templateName, string? referencePath = null)
	{
		string text = ResolveYamlPath(subDir, templateName);
		_lastLoadedYamlPath = text;
		_loadedYamlPaths.Add(text);
		if (!File.Exists(text))
		{
			string reference = string.IsNullOrWhiteSpace(referencePath) ? string.Empty : $"; 引用: {referencePath}";
			throw new FileNotFoundException("未找到配置文件 " + subDir + "/" + templateName + reference, text);
		}
		object value = _yamlDeserializer.Deserialize<object>(File.ReadAllText(text));
		return NormalizeDictionary(value);
	}

	private string ResolveYamlPath(string subDir, string templateName)
	{
		return ResolveYamlPath(_ctx.ZContext.Environment, subDir, templateName);
	}

	internal static string ResolveYamlPath(
		OneDragonEnvironment environment,
		string subDir,
		string templateName,
		bool readFromMerged = false)
	{
		ArgumentNullException.ThrowIfNull(environment);
		_ = readFromMerged;
		string pathUnderWorkDir = environment.GetPathUnderWorkDir("config", subDir);
		string yamlPath = Path.Combine(pathUnderWorkDir, templateName + ".yml");
		return File.Exists(yamlPath) ? yamlPath : Path.Combine(pathUnderWorkDir, templateName + ".sample.yml");
	}

	internal static string GetSourceFingerprint(IEnumerable<string> paths)
	{
		return string.Join(
			"|",
			paths
				.Select(Path.GetFullPath)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path =>
				{
					FileInfo fileInfo = new FileInfo(path);
					return fileInfo.Exists
						? $"{path}:{fileInfo.LastWriteTimeUtc.Ticks}:{fileInfo.Length}"
						: $"{path}:missing";
				}));
	}

	private static Dictionary<string, object?> NormalizeDictionary(object? value)
	{
		if (value is IDictionary dictionary)
		{
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (DictionaryEntry item in dictionary)
			{
				string key = Convert.ToString(item.Key, CultureInfo.InvariantCulture) ?? string.Empty;
				dictionary2[key] = NormalizeValue(item.Value);
			}
			return dictionary2;
		}
		return new Dictionary<string, object>();
	}

	private static object? NormalizeValue(object? value)
	{
		if (value is IDictionary)
		{
			return NormalizeDictionary(value);
		}
		if (value is IEnumerable enumerable && !(enumerable is string))
		{
			List<object> list = new List<object>();
			foreach (object item in enumerable)
			{
				list.Add(NormalizeValue(item));
			}
			return list;
		}
		return value;
	}

	private static void ValidateScenes(IReadOnlyList<AutoBattleCondOpScene> scenes, string sourcePath)
	{
		Dictionary<string, List<int>> triggerLocations = new Dictionary<string, List<int>>(StringComparer.Ordinal);
		List<int> normalScenes = new List<int>();
		for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
		{
			AutoBattleCondOpScene scene = scenes[sceneIndex];
			if (scene.Triggers.Count > 0)
			{
				foreach (string trigger in scene.Triggers.Where(trigger => !string.IsNullOrWhiteSpace(trigger)))
				{
					if (!triggerLocations.TryGetValue(trigger, out List<int>? locations))
					{
						locations = [];
						triggerLocations.Add(trigger, locations);
					}

					locations.Add(sceneIndex);
				}
			}
			else
			{
				normalScenes.Add(sceneIndex);
			}
		}

		List<string> errors = triggerLocations
			.Where(pair => pair.Value.Count > 1)
			.Select(pair => $"重复 trigger '{pair.Key}': {string.Join(", ", pair.Value.Select(index => $"{sourcePath} -> scenes[{index}].triggers"))}")
			.ToList();
		if (normalScenes.Count > 1)
		{
			errors.Add($"多个无 trigger 场景: {string.Join(", ", normalScenes.Select(index => $"{sourcePath} -> scenes[{index}].triggers"))}");
		}

		if (errors.Count > 0)
		{
			throw new InvalidOperationException(string.Join(System.Environment.NewLine, errors));
		}
	}

	private static async Task DelayNoThrow(TimeSpan delay, CancellationToken token)
	{
		try
		{
			await Task.Delay(delay, token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (TaskCanceledException)
		{
		}
	}

	private static bool CanInterrupt(ExecutionInfo? current, ExecutionInfo next)
	{
		if (current == null)
		{
			return true;
		}
		if (!current.Priority.HasValue)
		{
			return true;
		}
		return next.Priority.HasValue && next.Priority > current.Priority;
	}

	private void StopRunningTaskLocked(string reason)
	{
		_naturalCompletionPriorityProtection = null;
		if (RunningExecutor != null)
		{
			ExecutionInfo currentExecutionInfo = CurrentExecutionInfo;
			bool flag = RunningExecutor.Stop();
			if (!flag)
			{
				_runningExecutorCount--;
			}
			if (currentExecutionInfo != null)
			{
				AddExecutionRecordLocked(reason, currentExecutionInfo.TriggerDisplay, currentExecutionInfo, flag);
				PublishExecutionDecision(
					currentExecutionInfo,
					currentExecutionInfo.TriggerDisplay,
					reason,
					new Dictionary<string, object?>
					{
						["completed"] = flag,
					});
			}
			RunningExecutor = null;
			CurrentExecutionInfo = null;
		}
	}

	private void OnExecutionDone(OperationExecutor executor, ExecutionInfo executionInfo, Task<bool> task)
	{
		lock (_taskLock)
		{
			bool flag = false;
			string text = null;
			if (task.IsFaulted)
			{
				text = task.Exception?.GetBaseException().Message;
			}
			else
			{
				flag = task.Result;
				text = executor.LastException?.Message;
				if (flag)
				{
					_runningExecutorCount--;
				}
			}
			if (RunningExecutor == executor)
			{
				AddExecutionRecordLocked((text == null) ? "finished" : "error", executionInfo.TriggerDisplay, executionInfo, flag, text);
				PublishExecutionDecision(
					executionInfo,
					executionInfo.TriggerDisplay,
					text is null ? (flag ? "completed" : "stopped") : "error",
					new Dictionary<string, object?>
					{
						["completed"] = flag,
						["error"] = text,
					});
				_ctx.ZContext.Logger.Information("自动战斗执行结束: Template={Template}, Trigger={Trigger}, Completed={Completed}, Error={Error}", _templateName, executionInfo.TriggerDisplay, flag, text ?? "无");
				RunningExecutor = null;
				CurrentExecutionInfo = null;
				_naturalCompletionPriorityProtection = flag ? executionInfo : null;
			}
		}
	}

	private void AddExecutionRecordLocked(string @event, string trigger, ExecutionInfo executionInfo, bool completed, string? errorMessage = null, double? triggerTime = null)
	{
		string text = ((executionInfo.OpList.Count == 0) ? "-" : string.Join(" | ", from op in executionInfo.OpList.Take(3)
			select op.OpName));
		if (executionInfo.OpList.Count > 3)
		{
			text += " | ...";
		}
		AutoBattleExecutionRecord record = new(@event, trigger, text, completed, errorMessage, DateTimeOffset.UtcNow, executionInfo.ExprDisplay, triggerTime);
		_executionRecords.Add(record);
		try
		{
			ExecutionRecordAdded?.Invoke(record);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "自动战斗回放决策记录失败");
		}
	}

	private void PublishExecutionDecision(
		ExecutionInfo executionInfo,
		string trigger,
		string status,
		IReadOnlyDictionary<string, object?>? additionalMetadata = null)
	{
		Dictionary<string, object?> metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["template"] = _templateName,
			["priority"] = executionInfo.Priority,
			["operation_count"] = executionInfo.OpList.Count,
		};
		if (additionalMetadata != null)
		{
			foreach (KeyValuePair<string, object?> pair in additionalMetadata)
			{
				metadata[pair.Key] = pair.Value;
			}
		}

		_ctx.ZContext.OverlayDebugBus.PublishDecision(new DecisionTraceItem(
			"auto_battle",
			trigger,
			executionInfo.ExprDisplay,
			string.Join(" | ", executionInfo.OpList.Select(operation => operation.OpName)),
			status,
			DateTimeOffset.UtcNow,
			Metadata: metadata));
	}

	private void PublishSceneDecision(
		string trigger,
		AutoBattleCondOpScene scene,
		string status,
		double stateMatchTime,
		double stateAgeMilliseconds)
	{
		_ctx.ZContext.OverlayDebugBus.PublishDecision(new DecisionTraceItem(
			"auto_battle",
			trigger,
			GetSceneExpressionDisplay(scene),
			string.Empty,
			status,
			DateTimeOffset.UtcNow,
			Metadata: CreateSceneDecisionMetadata(scene, stateMatchTime, stateAgeMilliseconds)));
	}

	private Dictionary<string, object?> CreateSceneDecisionMetadata(
		AutoBattleCondOpScene scene,
		double stateMatchTime,
		double? stateAgeMilliseconds = null)
	{
		Dictionary<string, object?> metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["template"] = _templateName,
			["scene_priority"] = scene.Priority,
			["scene_interval_seconds"] = scene.IntervalSeconds,
			["state_match_time"] = stateMatchTime,
		};
		if (stateAgeMilliseconds.HasValue)
		{
			metadata["state_age_ms"] = stateAgeMilliseconds.Value;
		}

		return metadata;
	}

	private double Now()
	{
		return _clock.NowSeconds();
	}

	private static void ObserveBackgroundTask(Task task, string taskName)
	{
		task.ContinueWith(delegate(Task completed)
		{
			Log.Error(completed.Exception, "{TaskName}运行失败", taskName);
		}, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
	}

	private static string GetString(IReadOnlyDictionary<string, object> data, string key, string fallback)
	{
		object value;
		return (data.TryGetValue(key, out value) && value != null) ? (Convert.ToString(value) ?? fallback) : fallback;
	}

	private static float GetFloat(IReadOnlyDictionary<string, object> data, string key, float fallback)
	{
		if (!data.TryGetValue(key, out object value) || value == null)
		{
			return fallback;
		}
		if (1 == 0)
		{
		}
		float result2;
		float result = ((value is float num) ? num : ((value is double num2) ? ((float)num2) : ((value is int num3) ? ((float)num3) : ((value is long num4) ? ((float)num4) : ((!(value is decimal num5)) ? (float.TryParse(Convert.ToString(value), out result2) ? result2 : fallback) : ((float)num5))))));
		if (1 == 0)
		{
		}
		return result;
	}

	private static AutoBattleInterval GetInterval(IReadOnlyDictionary<string, object> data, string key, AutoBattleInterval fallback)
	{
		if (!data.TryGetValue(key, out object value) || value == null)
		{
			return fallback;
		}
		if (value is IEnumerable<object> source && !(value is string))
		{
			float[] array = source.Select((object item) => Convert.ToSingle(item, CultureInfo.InvariantCulture)).ToArray();
			int num = array.Length;
			if (1 == 0)
			{
			}
			AutoBattleInterval result = num switch
			{
				0 => new AutoBattleInterval(0f, 0f), 
				1 => new AutoBattleInterval(array[0], array[0]), 
				_ => new AutoBattleInterval(array[0], array[1]), 
			};
			if (1 == 0)
			{
			}
			return result;
		}
		float num2 = GetFloat(data, key, fallback.Start);
		return new AutoBattleInterval(num2, num2);
	}

	private static List<List<string>> GetTeamList(IReadOnlyDictionary<string, object> data, string key)
	{
		if (!data.TryGetValue(key, out object value) || !(value is IEnumerable<object> enumerable))
		{
			return new List<List<string>>();
		}
		List<List<string>> list = new List<List<string>>();
		foreach (object item in enumerable)
		{
			if (item is string text)
			{
				int num = 1;
				List<string> list2 = new List<string>(num);
				CollectionsMarshal.SetCount(list2, num);
				CollectionsMarshal.AsSpan(list2)[0] = text;
				list.Add(list2);
			}
			else if (item is IEnumerable<object> source)
			{
				list.Add(source.Select((object item) => Convert.ToString(item) ?? string.Empty).ToList());
			}
		}
		return list;
	}
}
