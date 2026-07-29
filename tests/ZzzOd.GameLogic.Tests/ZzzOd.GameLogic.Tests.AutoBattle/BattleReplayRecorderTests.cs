using OneDragon.Core.Operation;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class BattleReplayRecorderTests
{
    [Fact]
    public void BattleAssistantConfig_DisablesReplayByDefault()
    {
        Assert.False(new BattleAssistantConfig().BattleReplayEnabled);
    }

    [Fact]
    public async Task Recorder_WritesStateDecisionAndManifest()
    {
        string root = CreateTempRoot();
        try
        {
            using var recorder = new BattleReplayRecorder(root, "test", "测试配置", "ABC");
            recorder.Start();
            recorder.BatchUpdateStates([new StateRecord("自定义-A", 10d, value: 3)]);
            recorder.RecordDecision(new BattleReplayDecision("started", "主循环", "按键", false, null, DateTimeOffset.UtcNow, 10d, "[自定义-A, 0, 1]"));

            await recorder.ShutdownAsync(CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(recorder.PackageDirectory, "manifest.yml")));
            Assert.Contains("自定义-A", await File.ReadAllTextAsync(Path.Combine(recorder.PackageDirectory, "states.jsonl")), StringComparison.Ordinal);
            Assert.Contains("主循环", await File.ReadAllTextAsync(Path.Combine(recorder.PackageDirectory, "decisions.jsonl")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Recorder_DropsFramesAtLimitAndPersistsCount()
    {
        string root = CreateTempRoot();
        try
        {
            using var recorder = new BattleReplayRecorder(root, "limit", "测试配置", "ABC", maxFrames: 0);
            recorder.Start();
            using var frame = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);

            Assert.False(recorder.RecordFrame(frame, DateTimeOffset.UtcNow));
            await recorder.ShutdownAsync(CancellationToken.None);

            string manifest = await File.ReadAllTextAsync(Path.Combine(recorder.PackageDirectory, "manifest.yml"));
            Assert.Contains("droppedFrameCount: 1", manifest, StringComparison.Ordinal);
            Assert.Contains("truncated: true", manifest, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Recorder_DoesNotBlockRequiredRecordsWhenFrameQueueIsFullAndDrainsOnShutdown()
    {
        string root = CreateTempRoot();
        var writerGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recorder = new BattleReplayRecorder(
            root,
            "backpressure",
            "测试配置",
            "ABC",
            maxFrames: 10,
            maxBytes: 16 * 1024 * 1024,
            queueCapacity: 1,
            writerStartGate: writerGate.Task);

        try
        {
            recorder.Start();
            using var frame = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
            Assert.True(recorder.RecordFrame(frame, DateTimeOffset.UtcNow));
            Assert.False(recorder.RecordFrame(frame, DateTimeOffset.UtcNow));

            Task producer = Task.Run(() =>
            {
                for (int i = 0; i < 1_000; i++)
                {
                    recorder.RecordDecision(new BattleReplayDecision(
                        "started",
                        "主循环",
                        $"操作-{i}",
                        false,
                        null,
                        DateTimeOffset.UtcNow));
                }
            });
            await producer.WaitAsync(TimeSpan.FromSeconds(2));

            Task shutdown = recorder.ShutdownAsync(CancellationToken.None);
            Assert.False(shutdown.IsCompleted);
            writerGate.SetResult();
            await shutdown;

            string[] decisions = await File.ReadAllLinesAsync(Path.Combine(recorder.PackageDirectory, "decisions.jsonl"));
            string manifest = await File.ReadAllTextAsync(Path.Combine(recorder.PackageDirectory, "manifest.yml"));
            Assert.Equal(1_000, decisions.Length);
            Assert.Contains("操作-999", decisions[^1], StringComparison.Ordinal);
            Assert.Contains("droppedFrameCount: 1", manifest, StringComparison.Ordinal);
            Assert.DoesNotContain("endedAtUtc: \n", manifest, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(Path.Combine(recorder.PackageDirectory, "frames"), "*.webp"));
        }
        finally
        {
            writerGate.TrySetResult();
            await recorder.ShutdownAsync(CancellationToken.None);
            recorder.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Recorder_StopsAfterFrameWriteFailureWithoutThrowingToCaller()
    {
        string root = CreateTempRoot();
        try
        {
            using var recorder = new BattleReplayRecorder(root, "failure", "测试配置", "ABC");
            recorder.Start();
            await WaitForFileAsync(Path.Combine(recorder.PackageDirectory, "states.jsonl"));
            Directory.Delete(Path.Combine(recorder.PackageDirectory, "frames"));
            using var frame = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);

            Exception? exception = Record.Exception(() => recorder.RecordFrame(frame, DateTimeOffset.UtcNow));
            await WaitUntilStoppedAsync(recorder);

            Assert.Null(exception);
            Assert.False(recorder.IsRecording);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task WaitForFileAsync(string path)
    {
        for (int i = 0; i < 50 && !File.Exists(path); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(File.Exists(path));
    }

    private static async Task WaitUntilStoppedAsync(BattleReplayRecorder recorder)
    {
        for (int i = 0; i < 50 && recorder.IsRecording; i++)
        {
            await Task.Delay(20);
        }
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "zzzod-battle-replay-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
