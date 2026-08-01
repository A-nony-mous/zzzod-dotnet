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
		Assert.Equal(8, loaded.Package.States.Count);
		Assert.Equal(11, loaded.Package.Decisions.Count);
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
	public async Task SyntheticPackage_DrivesOperatorWindowCooldownAndPrioritySemantics()
	{
		BattleReplayPackage package = BattleReplayPackageReader.Read(GetSyntheticPackageDirectory(), "SYNTHETIC").Package!;
		var runner = new BattleReplayRunner();

		IReadOnlyList<BattleReplayDecision> actual = await runner.RunAsync(package, package.Directory, readFromMerged: false);

		string? mismatch = BattleReplayDecisionComparer.FindFirstMismatch(package.Decisions, actual, TimeSpan.FromMilliseconds(1));
		Assert.True(mismatch is null, mismatch);
	}

    [Fact]
    public void DecisionComparer_ReportsFirstDifferentDecisionWithContext()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-07-27T00:00:00Z");
        BattleReplayDecision[] expected = [new("started", "A", "op", false, null, now, 10d, "[A, 0, 1]")];
        BattleReplayDecision[] actual = [new("started", "B", "op", false, null, now, 10d, "[B, 0, 1]")];

        string? mismatch = BattleReplayDecisionComparer.FindFirstMismatch(expected, actual, TimeSpan.FromMilliseconds(1));

        Assert.Contains("决策 0", mismatch, StringComparison.Ordinal);
        Assert.Contains("期望 (started, A", mismatch, StringComparison.Ordinal);
    }

    [BattleReplayIntegrationFact]
    [Trait("Category", "Integration")]
	public async Task RealPackages_PassSchemaFingerprintAndDecisionReplay()
	{
		string root = Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable)!;
		string expectedHash = Environment.GetEnvironmentVariable("ZZZOD_BATTLE_REPLAY_CONFIG_HASH") ?? string.Empty;
		string configurationRoot = Environment.GetEnvironmentVariable("ZZZOD_BATTLE_REPLAY_CONFIG_ROOT")
			?? Directory.GetParent(Path.GetFullPath(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))?.FullName
			?? throw new InvalidOperationException("无法推断回放配置根目录");
		string[] packages = Directory.GetDirectories(root);
		Assert.NotEmpty(packages);
		var runner = new BattleReplayRunner();
		foreach (string packageDirectory in packages)
		{
			BattleReplayLoadResult result = BattleReplayPackageReader.Read(packageDirectory, expectedHash);
			Assert.False(result.IsSkipped, result.SkipReason);
			BattleReplayPackage package = result.Package!;
			Assert.NotEmpty(package.Decisions);
			IReadOnlyList<BattleReplayDecision> actual = await runner.RunAsync(package, configurationRoot, readFromMerged: true);
			string? mismatch = BattleReplayDecisionComparer.FindFirstMismatch(package.Decisions, actual, TimeSpan.FromMilliseconds(2));
			Assert.True(mismatch is null, mismatch);
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
