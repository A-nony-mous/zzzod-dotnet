using OneDragon.Core.Operation;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class BattleReplayHarnessTests
{
    private const string IntegrationEnvironmentVariable = "ZZZOD_BATTLE_REPLAY_DIR";

    [Fact]
    public void Reader_LoadsSyntheticPackageAndRejectsMismatchedFingerprint()
    {
        string packageDirectory = GetSyntheticPackageDirectory();

        BattleReplayLoadResult loaded = BattleReplayPackageReader.Read(packageDirectory, "SYNTHETIC");
        BattleReplayLoadResult skipped = BattleReplayPackageReader.Read(packageDirectory, "OTHER");

        Assert.False(loaded.IsSkipped, loaded.SkipReason);
        Assert.NotNull(loaded.Package);
        Assert.Equal(3, loaded.Package.States.Count);
        Assert.Equal(3, loaded.Package.Decisions.Count);
        Assert.True(skipped.IsSkipped);
        Assert.Contains("配置指纹不匹配", skipped.SkipReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayClock_SuppliesRecordedSequenceAndCanAdvanceAtExecutionBoundary()
    {
        var clock = new BattleReplayClock([10d, 11d]);
        Assert.Equal(10d, clock.NowSeconds());
        Assert.Equal(11d, clock.NowSeconds());

        clock.AdvanceTo(12.5d);
        Assert.Equal(12.5d, clock.NowSeconds());
    }

    [Fact]
    public void SyntheticPackage_ExercisesRealStateWindowAndStubbedExecution()
    {
        BattleReplayPackage package = BattleReplayPackageReader.Read(GetSyntheticPackageDirectory(), "SYNTHETIC").Package!;
        var recorders = new Dictionary<string, StateRecorder>(StringComparer.Ordinal);
        foreach (ReplayStateSubmission state in package.States)
        {
            StateRecorder recorder = recorders.GetValueOrDefault(state.Record.StateName) ?? new StateRecorder(state.Record.StateName);
            recorders[state.Record.StateName] = recorder;
            recorder.UpdateStateRecord(state.Record);
        }

        var clock = new BattleReplayClock(package.Decisions.Select(decision => decision.TriggerTime ?? 0d));
        var executor = new BattleReplayExecutionStub(clock);
        foreach (BattleReplayDecision decision in package.Decisions)
        {
            StateCalNode expression = StateCalExpressionParser.Construct(decision.Expression, name => recorders.GetValueOrDefault(name));
            if (expression.InTimeRange(clock.NowSeconds()))
            {
                executor.Execute(decision);
            }
        }

        Assert.Null(BattleReplayDecisionComparer.FindFirstMismatch(package.Decisions, executor.Actual, TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void DecisionComparer_ReportsFirstDifferentDecisionWithContext()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-07-27T00:00:00Z");
        BattleReplayDecision[] expected = [new("started", "A", "op", false, null, now, 10d, "[A, 0, 1]")];
        BattleReplayDecision[] actual = [new("started", "B", "op", false, null, now, 10d, "[B, 0, 1]")];

        string? mismatch = BattleReplayDecisionComparer.FindFirstMismatch(expected, actual, TimeSpan.FromMilliseconds(1));

        Assert.Contains("决策 0", mismatch, StringComparison.Ordinal);
        Assert.Contains("期望 (A", mismatch, StringComparison.Ordinal);
    }

    [BattleReplayIntegrationFact]
    [Trait("Category", "Integration")]
    public void RealPackages_PassSchemaFingerprintAndDecisionReplay()
    {
        string root = Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable)!;
        string expectedHash = Environment.GetEnvironmentVariable("ZZZOD_BATTLE_REPLAY_CONFIG_HASH") ?? string.Empty;
        string[] packages = Directory.GetDirectories(root);
        Assert.NotEmpty(packages);
        foreach (string packageDirectory in packages)
        {
            BattleReplayLoadResult result = BattleReplayPackageReader.Read(packageDirectory, expectedHash);
            Assert.False(result.IsSkipped, result.SkipReason);
            Assert.NotEmpty(result.Package!.Decisions);
        }
    }

    private static string GetSyntheticPackageDirectory() => Path.Combine(AppContext.BaseDirectory, "TestData", "BattleReplay", "synthetic");

    private sealed class BattleReplayIntegrationFactAttribute : FactAttribute
    {
        public BattleReplayIntegrationFactAttribute()
        {
            string? directory = Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                Skip = $"需要 {IntegrationEnvironmentVariable} 指向真实 .replay 目录";
            }
        }
    }
}
