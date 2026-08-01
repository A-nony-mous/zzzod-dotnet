using System.Text.Json;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

internal sealed record ReplayStateSubmission(StateRecord Record, DateTimeOffset SubmittedAtUtc);

internal sealed class ReplayManifestDto
{
    public int SchemaVersion { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public string ConfigurationName { get; set; } = string.Empty;
    public string ConfigurationHash { get; set; } = string.Empty;
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }
    public long DroppedFrameCount { get; set; }
    public bool Truncated { get; set; }
}

internal sealed class ReplayStateLineDto
{
    public ReplayStateRecordDto Record { get; set; } = new();
    public DateTimeOffset SubmittedAtUtc { get; set; }
}

internal sealed class ReplayStateRecordDto
{
    public string StateName { get; set; } = string.Empty;
    public bool IsClear { get; set; }
    public double TriggerTime { get; set; }
    public double? TriggerTimeAdd { get; set; }
    public int? Value { get; set; }
    public int? ValueAdd { get; set; }
}

internal sealed record BattleReplayPackage(
    string Directory,
    BattleReplayManifest Manifest,
    IReadOnlyList<ReplayStateSubmission> States,
    IReadOnlyList<BattleReplayDecision> Decisions);

internal sealed record BattleReplayLoadResult(BattleReplayPackage? Package, string? SkipReason)
{
    public bool IsSkipped => SkipReason != null;
}

internal static class BattleReplayPackageReader
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static BattleReplayLoadResult Read(string packageDirectory, string expectedConfigurationHash)
    {
        string manifestPath = Path.Combine(packageDirectory, "manifest.yml");
        if (!File.Exists(manifestPath))
        {
            return new(null, "缺少 manifest.yml");
        }

        ReplayManifestDto dto = Yaml.Deserialize<ReplayManifestDto>(File.ReadAllText(manifestPath));
        BattleReplayManifest manifest = new(
            dto.SchemaVersion,
            dto.StartedAtUtc,
            dto.EndedAtUtc,
            dto.ConfigurationName,
            dto.ConfigurationHash,
            dto.FrameWidth,
            dto.FrameHeight,
            dto.DroppedFrameCount,
            dto.Truncated);
        if (manifest.SchemaVersion != 1)
        {
            return new(null, $"schemaVersion 不匹配，期望 1，实际 {manifest.SchemaVersion}");
        }

        if (!string.Equals(manifest.ConfigurationHash, expectedConfigurationHash, StringComparison.OrdinalIgnoreCase))
        {
            return new(null, $"配置指纹不匹配，期望 {expectedConfigurationHash}，实际 {manifest.ConfigurationHash}");
        }

        IReadOnlyList<ReplayStateSubmission> states = ReadJsonLines<ReplayStateLineDto>(Path.Combine(packageDirectory, "states.jsonl"))
            .Select(line => new ReplayStateSubmission(
                new StateRecord(
                    line.Record.StateName,
                    line.Record.TriggerTime,
                    line.Record.Value,
                    line.Record.ValueAdd,
                    line.Record.TriggerTimeAdd,
                    line.Record.IsClear),
                line.SubmittedAtUtc))
            .ToArray();
        IReadOnlyList<BattleReplayDecision> decisions = ReadJsonLines<BattleReplayDecision>(Path.Combine(packageDirectory, "decisions.jsonl"));
        return new(new BattleReplayPackage(packageDirectory, manifest, states, decisions), null);
    }

    private static IReadOnlyList<T> ReadJsonLines<T>(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<T>(line) ?? throw new InvalidDataException($"无法解析 {path}"))
            .ToArray();
    }
}

internal sealed class BattleReplayClock(IEnumerable<double> nowSequence) : ICondOpClock
{
    private readonly Queue<double> _sequence = new(nowSequence);
    private double _current;

    public double NowSeconds()
    {
        if (_sequence.Count > 0)
        {
            _current = _sequence.Dequeue();
        }

        return _current;
    }

    public DateTimeOffset UtcNow => DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(_current * 1000d));

    public void AdvanceTo(double nowSeconds) => _current = nowSeconds;
}

internal sealed class BattleReplayAtomicOp(string opName) : AtomicOp(opName)
{
	private readonly AutoResetEvent _release = new(false);
	private readonly object _failureLock = new();
	private Exception? _failure;
	private int _executeCount;
	private int _stopCount;

	public int ExecuteCount => Volatile.Read(ref _executeCount);

	public int StopCount => Volatile.Read(ref _stopCount);

	public override void Execute()
	{
		Interlocked.Increment(ref _executeCount);
		_release.WaitOne();
		Exception? failure;
		lock (_failureLock)
		{
			failure = _failure;
			_failure = null;
		}

		if (failure != null)
		{
			throw failure;
		}
	}

	public override void Stop()
	{
		Interlocked.Increment(ref _stopCount);
		_release.Set();
	}

	public void Complete() => _release.Set();

	public void Fail(Exception failure)
	{
		lock (_failureLock)
		{
			_failure = failure;
		}
		_release.Set();
	}

	public override void Dispose() => _release.Dispose();
}

internal sealed class BattleReplayRunner
{
	private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(2);

	public async Task<IReadOnlyList<BattleReplayDecision>> RunAsync(
		BattleReplayPackage package,
		string configurationRoot,
		bool readFromMerged)
	{
		using ZContext context = new(new OneDragonEnvironment(configurationRoot));
		var clock = new BattleReplayClock([]);
		var actual = new List<BattleReplayDecision>();
		var actualLock = new object();
		var replayOps = new List<BattleReplayAtomicOp>();
		var replayOpsLock = new object();
		var autoBattleOperator = new AutoBattleOperator(
			context.AutoBattleContext,
			"auto_battle",
			package.Manifest.ConfigurationName,
			readFromMerged,
			clock,
			operationDef =>
			{
				var replayOp = new BattleReplayAtomicOp(operationDef.OpName ?? "回放操作");
				lock (replayOpsLock)
				{
					replayOps.Add(replayOp);
				}
				return replayOp;
			},
			action => action(),
			runNormalSceneLoop: false);
		autoBattleOperator.ExecutionRecordAdded += record =>
		{
			lock (actualLock)
			{
				actual.Add(new BattleReplayDecision(
					record.Event,
					record.Trigger,
					record.OperationSummary,
					record.Completed,
					record.ErrorMessage,
					record.Timestamp,
					record.TriggerTime,
					record.Expression));
			}
		};

		var initResult = autoBattleOperator.InitBeforeRunning();
		if (!initResult.Success)
		{
			throw new InvalidOperationException(initResult.Message);
		}
		if (!autoBattleOperator.StartRunningAsync())
		{
			throw new InvalidOperationException("回放 operator 启动失败");
		}

		try
		{
			foreach (ReplayAction replayAction in BuildTimeline(package))
			{
				clock.AdvanceTo(replayAction.NowSeconds);
				switch (replayAction.Kind)
				{
					case ReplayActionKind.State:
						SubmitState(context, autoBattleOperator, replayAction.State!);
						break;
					case ReplayActionKind.NormalSceneTick:
						autoBattleOperator.RunNormalSceneReplayTick();
						break;
					case ReplayActionKind.Complete:
						await CompleteCurrentExecutionAsync(autoBattleOperator, null).ConfigureAwait(false);
						break;
					case ReplayActionKind.Fail:
						await CompleteCurrentExecutionAsync(
							autoBattleOperator,
							new InvalidOperationException(replayAction.Decision?.ErrorMessage ?? "录制执行失败")).ConfigureAwait(false);
						break;
					case ReplayActionKind.Stop:
						Task<bool>? runningTask = autoBattleOperator.GetRunningExecutionTask();
						autoBattleOperator.StopRunning();
						if (runningTask != null)
						{
							await runningTask.WaitAsync(ExecutionTimeout).ConfigureAwait(false);
						}
						break;
				}
			}

			lock (actualLock)
			{
				return actual.ToArray();
			}
		}
		finally
		{
			Task<bool>? remainingTask = autoBattleOperator.GetRunningExecutionTask();
			autoBattleOperator.StopRunning();
			if (remainingTask != null && !remainingTask.IsCompleted)
			{
				await remainingTask.WaitAsync(ExecutionTimeout).ConfigureAwait(false);
			}
			autoBattleOperator.Dispose();
			lock (replayOpsLock)
			{
				foreach (BattleReplayAtomicOp replayOp in replayOps)
				{
					replayOp.Dispose();
				}
			}
		}
	}

	private static IReadOnlyList<ReplayAction> BuildTimeline(BattleReplayPackage package)
	{
		var timeline = new List<ReplayAction>();
		for (int i = 0; i < package.States.Count; i++)
		{
			ReplayStateSubmission state = package.States[i];
			timeline.Add(new ReplayAction(
				state.SubmittedAtUtc,
				ReplayActionKind.State,
				ToUnixSeconds(state.SubmittedAtUtc),
				i,
				state,
				null));
		}

		for (int i = 0; i < package.Decisions.Count; i++)
		{
			BattleReplayDecision decision = package.Decisions[i];
			ReplayActionKind? kind = decision.Event switch
			{
				"started" when string.Equals(decision.Trigger, "主循环", StringComparison.Ordinal) => ReplayActionKind.NormalSceneTick,
				"finished" => ReplayActionKind.Complete,
				"error" => ReplayActionKind.Fail,
				"stopped" => ReplayActionKind.Stop,
				_ => null,
			};
			if (!kind.HasValue)
			{
				continue;
			}

			double nowSeconds = kind == ReplayActionKind.NormalSceneTick && decision.TriggerTime.HasValue
				? decision.TriggerTime.Value
				: ToUnixSeconds(decision.EndedAt ?? decision.Timestamp);
			timeline.Add(new ReplayAction(
				decision.Timestamp,
				kind.Value,
				nowSeconds,
				i,
				null,
				decision));
		}

		return timeline
			.OrderBy(action => action.AtUtc)
			.ThenBy(action => action.Kind)
			.ThenBy(action => action.SourceIndex)
			.ToArray();
	}

	private static void SubmitState(
		ZContext context,
		AutoBattleOperator autoBattleOperator,
		ReplayStateSubmission submission)
	{
		StateRecorder? recorder = context.AutoBattleContext.StateRecordService.GetStateRecorder(submission.Record.StateName);
		if (recorder == null)
		{
			throw new InvalidOperationException($"回放状态不存在 {submission.Record.StateName}");
		}

		if (submission.Record.IsClear)
		{
			recorder.ClearStateRecord();
		}
		else
		{
			recorder.UpdateStateRecord(submission.Record);
		}
		autoBattleOperator.BatchUpdateStates([submission.Record]);
	}

	private static async Task CompleteCurrentExecutionAsync(
		AutoBattleOperator autoBattleOperator,
		Exception? failure)
	{
		OperationExecutor? executor = autoBattleOperator.RunningExecutor;
		Task<bool>? runningTask = autoBattleOperator.GetRunningExecutionTask();
		if (executor == null || runningTask == null)
		{
			return;
		}

		foreach (BattleReplayAtomicOp replayOp in executor.OpList.OfType<BattleReplayAtomicOp>())
		{
			if (failure == null)
			{
				replayOp.Complete();
			}
			else
			{
				replayOp.Fail(failure);
			}
		}
		await runningTask.WaitAsync(ExecutionTimeout).ConfigureAwait(false);
		for (int attempt = 0; attempt < 100 && ReferenceEquals(autoBattleOperator.RunningExecutor, executor); attempt++)
		{
			await Task.Delay(1).ConfigureAwait(false);
		}
		if (ReferenceEquals(autoBattleOperator.RunningExecutor, executor))
		{
			throw new TimeoutException("回放执行完成后 operator 未在限定时间内写入收尾决策");
		}
	}

	private static double ToUnixSeconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds() / 1000d;

	private enum ReplayActionKind
	{
		State,
		NormalSceneTick,
		Complete,
		Fail,
		Stop,
	}

	private sealed record ReplayAction(
		DateTimeOffset AtUtc,
		ReplayActionKind Kind,
		double NowSeconds,
		int SourceIndex,
		ReplayStateSubmission? State,
		BattleReplayDecision? Decision);
}

internal static class BattleReplayDecisionComparer
{
    public static string? FindFirstMismatch(
        IReadOnlyList<BattleReplayDecision> expected,
        IReadOnlyList<BattleReplayDecision> actual,
        TimeSpan timestampTolerance)
    {
        int commonCount = Math.Min(expected.Count, actual.Count);
		for (int i = 0; i < commonCount; i++)
		{
			BattleReplayDecision left = expected[i];
			BattleReplayDecision right = actual[i];
			if (!string.Equals(left.Event, right.Event, StringComparison.Ordinal) ||
				!string.Equals(left.Trigger, right.Trigger, StringComparison.Ordinal) ||
				!string.Equals(left.Expression, right.Expression, StringComparison.Ordinal) ||
				left.Completed != right.Completed ||
				!string.Equals(left.ErrorMessage, right.ErrorMessage, StringComparison.Ordinal))
			{
				return $"决策 {i} 不一致，期望 ({left.Event}, {left.Trigger}, {left.Expression}, {left.Completed}, {left.ErrorMessage})，实际 ({right.Event}, {right.Trigger}, {right.Expression}, {right.Completed}, {right.ErrorMessage})";
			}

			if (left.TriggerTime.HasValue != right.TriggerTime.HasValue ||
				(left.TriggerTime.HasValue && Math.Abs(left.TriggerTime.Value - right.TriggerTime!.Value) > timestampTolerance.TotalSeconds))
			{
				return $"决策 {i} 触发时刻不一致，期望 {left.TriggerTime}，实际 {right.TriggerTime}";
			}
        }

        return expected.Count == actual.Count
            ? null
            : $"决策数量不一致，期望 {expected.Count}，实际 {actual.Count}";
    }
}
