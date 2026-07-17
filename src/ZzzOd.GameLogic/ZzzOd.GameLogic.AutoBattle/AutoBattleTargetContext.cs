using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Operation;
using OneDragon.Core.Utils;
using OpenCvSharp;
using Serilog;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public class AutoBattleTargetContext
{
	private static readonly DedicatedTaskScheduler TargetCheckScheduler = new DedicatedTaskScheduler("zzz-target-check", 8);

	private static readonly TaskFactory TargetCheckExecutor = new TaskFactory(TargetCheckScheduler);

	private readonly ZContext _ctx;

	private readonly IAutoBattleTargetStateChecker _checker;

	private readonly object _checkLock = new object();

	private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

	private readonly Dictionary<string, double> _lastCheckTimes = new Dictionary<string, double>();

	private readonly Dictionary<string, double> _currentIntervals = new Dictionary<string, double>();

	private readonly Dictionary<string, Dictionary<string, object>> _dynamicIntervalConfigs = new Dictionary<string, Dictionary<string, object>>();

	private readonly Dictionary<string, string> _lastTargetDiagnosticResults = new Dictionary<string, string>();

	private readonly Dictionary<string, long> _lastTargetDiagnosticAtMilliseconds = new Dictionary<string, long>();

	public IReadOnlyList<DetectionTask> Tasks { get; }

	public float LastCheckDistance { get; private set; } = -1f;

	public int WithoutDistanceTimes { get; private set; }

	public int WithDistanceTimes { get; private set; }

	public double CheckDistanceInterval { get; private set; } = 5.0;

	public AutoBattleTargetContext(ZContext ctx, IAutoBattleTargetStateChecker? checker = null, IReadOnlyList<DetectionTask>? tasks = null)
	{
		_ctx = ctx;
		_checker = checker ?? new TargetStateChecker(ctx);
		Tasks = (tasks ?? TargetState.DETECTION_TASKS).Where((DetectionTask task) => task.Enabled).ToList();
		foreach (DetectionTask task in Tasks)
		{
			_lastCheckTimes[task.TaskId] = 0.0;
			_currentIntervals[task.TaskId] = task.Interval;
			_dynamicIntervalConfigs[task.TaskId] = new Dictionary<string, object>(task.DynamicIntervalConfig);
		}
	}

	public void ResetBattleDistance()
	{
		LastCheckDistance = -1f;
		WithoutDistanceTimes = 0;
		WithDistanceTimes = 0;
	}

	public void ResetDistanceCheckInterval()
	{
		CheckDistanceInterval = 5.0;
	}

	public void InitAutoOp()
	{
	}

	public void InitAutoOp(AutoBattleOperator autoOp)
	{
		ApplyConfigIntervals(autoOp.TargetLockInterval, autoOp.AbnormalStatusInterval);
	}

	public void ApplyConfigIntervals(double targetLockInterval, double abnormalStatusInterval)
	{
		foreach (DetectionTask task in Tasks)
		{
			if (task.TaskId == "lock_on" && targetLockInterval > 0.0)
			{
				_currentIntervals[task.TaskId] = targetLockInterval;
				_dynamicIntervalConfigs[task.TaskId]["interval_if_not_state"] = targetLockInterval;
			}
			else if (task.TaskId == "abnormal_statuses" && abnormalStatusInterval > 0.0)
			{
				_currentIntervals[task.TaskId] = abnormalStatusInterval;
			}
		}
	}

	public IReadOnlyList<StateRecord> RunAllChecks(object? screen, double screenshotTime, bool updateState = true, string source = "unknown", double queueDelayMilliseconds = 0.0)
	{
		if (!Monitor.TryEnter(_checkLock))
		{
			return Array.Empty<StateRecord>();
		}
		try
		{
			List<StateRecord> list = new List<StateRecord>();
			List<(DetectionTask, Task<IReadOnlyList<TargetStateCheckResult>>)> list2 = new List<(DetectionTask, Task<IReadOnlyList<TargetStateCheckResult>>)>();
			foreach (DetectionTask task in Tasks)
			{
				double num = _currentIntervals[task.TaskId];
				if (num <= 0.0 || screenshotTime - _lastCheckTimes[task.TaskId] < num)
				{
					continue;
				}
				_lastCheckTimes[task.TaskId] = screenshotTime;
				if (task.IsAsync)
				{
					object taskScreen = ((screen is Mat mat) ? mat.Clone() : screen);
					list2.Add((task, TargetCheckExecutor.StartNew(delegate
					{
						try
						{
							return _checker.RunTask(taskScreen, task);
						}
						finally
						{
							(taskScreen as Mat)?.Dispose();
						}
					}, _shutdownCts.Token)));
				}
				else
				{
					IReadOnlyList<TargetStateCheckResult> results = _checker.RunTask(screen, task);
					HandleResults(list, results, screenshotTime, task);
				}
			}
			foreach (var (detectionTask, task2) in list2)
			{
				try
				{
					if (!task2.Wait(TimeSpan.FromSeconds(1L)))
					{
						Log.Error("目标状态异步检测超时: Detector={Detector}, TaskId={TaskId}, Source={Source}, ScreenshotTime={ScreenshotTime:F3}, FrameAgeMilliseconds={FrameAgeMilliseconds}, RunGeneration={RunGeneration}, QueueDelayMilliseconds={QueueDelayMilliseconds}", "Target", detectionTask.TaskId, source, screenshotTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)Math.Round(screenshotTime * 1000.0), "无", queueDelayMilliseconds);
					}
					else
					{
						HandleResults(list, task2.Result, screenshotTime, detectionTask);
					}
				}
				catch (Exception exception)
				{
					ILogger logger = _ctx.Logger;
					string message = "目标状态异步检测失败 [task_id=" + detectionTask.TaskId + "]";
					double? queueDelayMilliseconds2 = queueDelayMilliseconds;
					AutoBattleDiagnosticLogger.LogFailure(logger, exception, message, "Target", source, screenshotTime, null, queueDelayMilliseconds2);
				}
			}
			if (updateState && list.Count > 0)
			{
				_ctx.AutoBattleContext.StateRecordService.BatchUpdateStates(list);
			}
			return list;
		}
		finally
		{
			Monitor.Exit(_checkLock);
		}
	}

	public double GetCurrentInterval(string taskId)
	{
		double value;
		return _currentIntervals.TryGetValue(taskId, out value) ? value : 0.0;
	}

	public double GetLastCheckTime(string taskId)
	{
		double value;
		return _lastCheckTimes.TryGetValue(taskId, out value) ? value : 0.0;
	}

	public void UpdateBattleDistance(float? distance)
	{
		if (distance.HasValue)
		{
			WithoutDistanceTimes = 0;
			WithDistanceTimes++;
			LastCheckDistance = distance.Value;
			CheckDistanceInterval = 1.0;
		}
		else
		{
			WithoutDistanceTimes++;
			WithDistanceTimes = 0;
			LastCheckDistance = -1f;
			CheckDistanceInterval = 5.0;
		}
	}

	public void AfterAppShutdown()
	{
		_shutdownCts.Cancel();
	}

	private void HandleResults(List<StateRecord> records, IReadOnlyList<TargetStateCheckResult> results, double screenshotTime, DetectionTask task)
	{
		List<string> list = new List<string>();
		foreach (TargetStateCheckResult result in results)
		{
			if (result.IsClear)
			{
				records.Add(new StateRecord(result.StateName, 0.0, null, null, null, isClear: true));
				list.Add(result.StateName + "=清除");
			}
			else if (result.IsHit && result.Value.HasValue)
			{
				records.Add(new StateRecord(result.StateName, screenshotTime, result.Value));
				list.Add($"{result.StateName}=命中({result.Value})");
			}
			else if (result.IsHit)
			{
				records.Add(new StateRecord(result.StateName, screenshotTime));
				list.Add(result.StateName + "=命中");
			}
		}
		if (list.Count > 0)
		{
			LogTargetResults(task.TaskId, screenshotTime, string.Join(", ", list));
		}
		if (!_dynamicIntervalConfigs.TryGetValue(task.TaskId, out Dictionary<string, object> value) || value.Count == 0 || !value.TryGetValue("state_to_watch", out var value2) || !(value2 is string text))
		{
			return;
		}
		bool flag = false;
		foreach (TargetStateCheckResult result2 in results)
		{
			if (result2.StateName != text)
			{
				continue;
			}
			if (result2.IsHit)
			{
				_currentIntervals[task.TaskId] = GetConfigDouble(value, "interval_if_state", task.Interval);
				flag = true;
			}
			break;
		}
		if (!flag)
		{
			_currentIntervals[task.TaskId] = GetConfigDouble(value, "interval_if_not_state", task.Interval);
		}
	}

	private void LogTargetResults(string taskId, double screenshotTime, string results)
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		string value;
		bool flag = _lastTargetDiagnosticResults.TryGetValue(taskId, out value) && string.Equals(value, results, StringComparison.Ordinal);
		long valueOrDefault = _lastTargetDiagnosticAtMilliseconds.GetValueOrDefault(taskId);
		if (!flag || num - valueOrDefault >= 1000)
		{
			_lastTargetDiagnosticResults[taskId] = results;
			_lastTargetDiagnosticAtMilliseconds[taskId] = num;
			_ctx.Logger.Information("自动战斗目标状态: TaskId={TaskId}, ScreenshotTime={ScreenshotTime:F3}, Results={Results}", taskId, screenshotTime, results);
		}
	}

	private static double GetConfigDouble(IReadOnlyDictionary<string, object> config, string key, double fallback)
	{
		if (!config.TryGetValue(key, out object value))
		{
			return fallback;
		}
		if (1 == 0)
		{
		}
		double result = ((value is double num) ? num : ((value is float num2) ? ((double)num2) : ((value is int num3) ? ((double)num3) : ((!(value is long num4)) ? fallback : ((double)num4)))));
		if (1 == 0)
		{
		}
		return result;
	}
}
