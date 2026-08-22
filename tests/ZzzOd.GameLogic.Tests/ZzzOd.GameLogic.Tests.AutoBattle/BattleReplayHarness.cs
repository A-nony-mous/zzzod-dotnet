using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using OpenCvSharp;
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
    public string? ConfigurationRelativePath { get; set; }
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
    IReadOnlyList<BattleReplayDecision> Decisions,
    BattleReplayConfiguration Configuration);

internal sealed record BattleReplayConfiguration(
    string WorkDirectory,
    string ConfigurationPath,
    string SubDirectory,
    string TemplateName,
    bool ReadFromMerged);

internal sealed record BattleReplayLoadResult(BattleReplayPackage? Package, string? SkipReason)
{
    public bool IsSkipped => SkipReason != null;
}

internal static class BattleReplayPackageReader
{
    private const int CurrentSchemaVersion = 2;
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static BattleReplayLoadResult Read(string packageDirectory, string resourceDirectory)
    {
        try
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
                dto.Truncated,
                dto.ConfigurationRelativePath);
            if (manifest.SchemaVersion is < 1 or > CurrentSchemaVersion)
            {
                return new(null, $"schemaVersion 不匹配，支持 1-{CurrentSchemaVersion}，实际 {manifest.SchemaVersion}");
            }

            BattleReplayConfiguration configuration = ResolveConfiguration(
                packageDirectory,
                resourceDirectory,
                manifest);
            if (!File.Exists(configuration.ConfigurationPath))
            {
                return new(null, $"缺少回放配置 {configuration.ConfigurationPath}");
            }

            string actualConfigurationHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(configuration.ConfigurationPath)));
            if (!string.Equals(manifest.ConfigurationHash, actualConfigurationHash, StringComparison.OrdinalIgnoreCase))
            {
                return new(null, $"配置指纹不匹配，manifest {manifest.ConfigurationHash}，实际 {actualConfigurationHash}");
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
            return new(new BattleReplayPackage(packageDirectory, manifest, states, decisions, configuration), null);
        }
        catch (Exception ex)
        {
            return new(null, $"场景包读取失败: {ex.Message}");
        }
    }

    private static BattleReplayConfiguration ResolveConfiguration(
        string packageDirectory,
        string resourceDirectory,
        BattleReplayManifest manifest)
    {
        if (manifest.SchemaVersion >= 2)
        {
            if (string.IsNullOrWhiteSpace(manifest.ConfigurationRelativePath))
            {
                throw new InvalidDataException("schemaVersion 2 缺少 configurationRelativePath");
            }

            string snapshotConfigurationPath = ResolvePathUnderRoot(packageDirectory, manifest.ConfigurationRelativePath);
            return DescribeConfiguration(packageDirectory, snapshotConfigurationPath);
        }

        OneDragonEnvironment environment = new(resourceDirectory, resourceDirectory);
        string configurationPath = AutoBattleOperator.ResolveYamlPath(
            environment,
            "auto_battle",
            manifest.ConfigurationName,
            readFromMerged: true);
        if (!File.Exists(configurationPath))
        {
            configurationPath = AutoBattleOperator.ResolveYamlPath(
                environment,
                "auto_battle",
                AutoBattleOperator.FallbackTemplateName,
                readFromMerged: true);
        }
        return DescribeConfiguration(resourceDirectory, configurationPath);
    }

    private static BattleReplayConfiguration DescribeConfiguration(string workDirectory, string configurationPath)
    {
        string relativePath = Path.GetRelativePath(workDirectory, configurationPath).Replace('\\', '/');
        string[] parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !string.Equals(parts[0], "config", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"回放配置路径不在 config 目录下: {relativePath}");
        }

        string fileName = parts[^1];
        (string templateName, bool readFromMerged) = fileName switch
        {
            _ when fileName.EndsWith(".merged.yml", StringComparison.OrdinalIgnoreCase) =>
                (fileName[..^".merged.yml".Length], true),
            _ when fileName.EndsWith(".sample.yml", StringComparison.OrdinalIgnoreCase) =>
                (fileName[..^".sample.yml".Length], false),
            _ when fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) =>
                (fileName[..^".yml".Length], false),
            _ => throw new InvalidDataException($"回放配置扩展名无效: {relativePath}"),
        };
        string subDirectory = string.Join(Path.DirectorySeparatorChar, parts[1..^1]);
        return new BattleReplayConfiguration(
            Path.GetFullPath(workDirectory),
            Path.GetFullPath(configurationPath),
            subDirectory,
            templateName,
            readFromMerged);
    }

    private static string ResolvePathUnderRoot(string rootDirectory, string relativePath)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory)) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"回放配置路径超出场景包: {relativePath}");
        }
        return fullPath;
    }

    private static IReadOnlyList<T> ReadJsonLines<T>(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var result = new List<T>();
        int lineNumber = 0;
        foreach (string line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                result.Add(JsonSerializer.Deserialize<T>(line) ?? throw new InvalidDataException("反序列化结果为空"));
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                throw new InvalidDataException($"无法解析 {path} 第 {lineNumber} 行: {ex.Message}", ex);
            }
        }
        return result;
    }
}

internal static partial class BattleReplayPackageVerifier
{
    private static readonly Regex FrameFileNamePattern = CreateFrameFileNamePattern();

    public static BattleReplayVerificationResult Verify(string packageDirectory, string resourceDirectory)
    {
        var errors = new List<string>();
        BattleReplayLoadResult loadResult = BattleReplayPackageReader.Read(packageDirectory, resourceDirectory);
        if (loadResult.IsSkipped || loadResult.Package == null)
        {
            errors.Add(loadResult.SkipReason ?? "场景包读取失败");
            return new BattleReplayVerificationResult(null, errors, 0);
        }

        BattleReplayPackage package = loadResult.Package;
        BattleReplayManifest manifest = package.Manifest;
        if (manifest.StartedAtUtc == default)
        {
            errors.Add("manifest 缺少有效 startedAtUtc");
        }
        if (!manifest.EndedAtUtc.HasValue)
        {
            errors.Add("manifest 缺少 endedAtUtc，recorder 尚未完整收尾");
        }
        else if (manifest.EndedAtUtc.Value <= manifest.StartedAtUtc)
        {
            errors.Add("manifest endedAtUtc 必须晚于 startedAtUtc");
        }
        if (manifest.Truncated)
        {
            errors.Add("manifest 标记 truncated=true");
        }
        if (manifest.DroppedFrameCount < 0)
        {
            errors.Add("manifest droppedFrameCount 不能小于 0");
        }
        if (package.States.Count == 0)
        {
            errors.Add("states.jsonl 没有状态记录");
        }
        if (package.Decisions.Count == 0)
        {
            errors.Add("decisions.jsonl 没有决策记录");
        }

        if (manifest.EndedAtUtc.HasValue)
        {
            foreach (ReplayStateSubmission state in package.States)
            {
                if (!IsWithinRecording(state.SubmittedAtUtc, manifest))
                {
                    errors.Add($"状态提交时刻超出录制范围: {state.Record.StateName} {state.SubmittedAtUtc:O}");
                    break;
                }
            }
            foreach (BattleReplayDecision decision in package.Decisions)
            {
                if (!IsWithinRecording(decision.Timestamp, manifest))
                {
                    errors.Add($"决策时刻超出录制范围: {decision.Event}/{decision.Trigger} {decision.Timestamp:O}");
                    break;
                }
            }
        }

        string framesDirectory = Path.Combine(packageDirectory, "frames");
        string[] frameFiles = Directory.Exists(framesDirectory)
            ? Directory.GetFiles(framesDirectory, "*.webp", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];
        if (frameFiles.Length == 0)
        {
            errors.Add("frames 目录没有 WebP 帧");
        }
        if (manifest.FrameWidth <= 0 || manifest.FrameHeight <= 0)
        {
            errors.Add("manifest 帧尺寸无效");
        }

        for (int i = 0; i < frameFiles.Length; i++)
        {
            VerifyFrame(frameFiles[i], i, manifest, errors);
        }
        return new BattleReplayVerificationResult(package, errors, frameFiles.Length);
    }

    private static void VerifyFrame(
        string framePath,
        int expectedSequence,
        BattleReplayManifest manifest,
        List<string> errors)
    {
        string fileName = Path.GetFileName(framePath);
        Match match = FrameFileNamePattern.Match(fileName);
        if (!match.Success)
        {
            errors.Add($"帧文件名格式无效: {fileName}");
            return;
        }
        if (!int.TryParse(match.Groups["sequence"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int sequence) ||
            sequence != expectedSequence)
        {
            errors.Add($"帧序号不连续: 期望 {expectedSequence:D6}，实际 {match.Groups["sequence"].Value}");
        }
        if (!DateTime.TryParseExact(
                match.Groups["timestamp"].Value,
                "yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime frameTime))
        {
            errors.Add($"帧时间格式无效: {fileName}");
        }
        else if (manifest.EndedAtUtc.HasValue &&
                 !IsWithinRecording(new DateTimeOffset(frameTime), manifest))
        {
            errors.Add($"帧时间超出录制范围: {fileName}");
        }

        try
        {
            using Mat frame = Cv2.ImRead(framePath, ImreadModes.Unchanged);
            if (frame.Empty())
            {
                errors.Add($"帧无法解码: {fileName}");
            }
            else if (frame.Width != manifest.FrameWidth || frame.Height != manifest.FrameHeight)
            {
                errors.Add($"帧尺寸不一致: {fileName} 为 {frame.Width}x{frame.Height}，manifest 为 {manifest.FrameWidth}x{manifest.FrameHeight}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"帧无法解码: {fileName}，{ex.Message}");
        }
    }

    private static bool IsWithinRecording(DateTimeOffset timestamp, BattleReplayManifest manifest)
    {
        return timestamp >= manifest.StartedAtUtc &&
               (!manifest.EndedAtUtc.HasValue || timestamp <= manifest.EndedAtUtc.Value);
    }

    [GeneratedRegex("^(?<sequence>\\d{6})_(?<timestamp>\\d{8}_\\d{6}_\\d{3})\\.webp$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateFrameFileNamePattern();
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

internal sealed record BattleReplayVerificationResult(
    BattleReplayPackage? Package,
    IReadOnlyList<string> Errors,
    int FrameCount)
{
    public const string CompletionBoundary = "场景包结构完整不等于自动战斗自然结束，仍需日志或人工验收确认结束原因。";

    public bool IsValid => Errors.Count == 0;
}

internal sealed class BattleReplayRunner
{
	private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(2);

	public async Task<IReadOnlyList<BattleReplayDecision>> RunAsync(BattleReplayPackage package)
	{
		BattleReplayConfiguration configuration = package.Configuration;
		using ZContext context = new(new OneDragonEnvironment(configuration.WorkDirectory));
		var clock = new BattleReplayClock([]);
		var actual = new List<BattleReplayDecision>();
		var actualLock = new object();
		var replayOps = new List<BattleReplayAtomicOp>();
		var replayOpsLock = new object();
		var autoBattleOperator = new AutoBattleOperator(
			context.AutoBattleContext,
			configuration.SubDirectory,
			configuration.TemplateName,
			configuration.ReadFromMerged,
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
