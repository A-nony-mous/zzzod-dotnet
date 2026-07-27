using System.Text.Json;
using OneDragon.Core.Operation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.GameLogic.AutoBattle;

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

internal sealed class BattleReplayExecutionStub(BattleReplayClock clock)
{
    private readonly List<BattleReplayDecision> _actual = [];

    public IReadOnlyList<BattleReplayDecision> Actual => _actual;

    public void Execute(BattleReplayDecision decision)
    {
        if (decision.EndedAt.HasValue)
        {
            clock.AdvanceTo(decision.EndedAt.Value.ToUnixTimeMilliseconds() / 1000d);
        }

        _actual.Add(decision);
    }
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
            if (!string.Equals(left.Trigger, right.Trigger, StringComparison.Ordinal) ||
                !string.Equals(left.Expression, right.Expression, StringComparison.Ordinal))
            {
                return $"决策 {i} 不一致，期望 ({left.Trigger}, {left.Expression})，实际 ({right.Trigger}, {right.Expression})";
            }

            if ((left.Timestamp - right.Timestamp).Duration() > timestampTolerance)
            {
                return $"决策 {i} 时间不一致，期望 {left.Timestamp:O}，实际 {right.Timestamp:O}";
            }
        }

        return expected.Count == actual.Count
            ? null
            : $"决策数量不一致，期望 {expected.Count}，实际 {actual.Count}";
    }
}
