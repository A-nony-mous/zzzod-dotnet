using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading.Channels;
using OpenCvSharp;
using OneDragon.Core.Abstractions.Runtime;
using OneDragon.Core.Operation;
using Serilog;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed record BattleReplayDecision(
    string Event,
    string Trigger,
    string OperationSummary,
    bool Completed,
    string? ErrorMessage,
    DateTimeOffset Timestamp,
    double? TriggerTime = null,
    string? Expression = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? EndedAt = null);

public sealed record BattleReplayManifest(
    int SchemaVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string ConfigurationName,
    string ConfigurationHash,
    int FrameWidth,
    int FrameHeight,
    long DroppedFrameCount,
    bool Truncated);

/// <summary>
/// 将自动战斗的状态、决策和帧旁路写入可回放场景包。
/// </summary>
public sealed class BattleReplayRecorder : IStateRecordUpdateListener, IShutdownParticipant, IDisposable
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private readonly Channel<ReplayItem> _requiredQueue;
    private readonly Channel<FrameItem> _frameQueue;
    private readonly string _packageDirectory;
    private readonly string _configurationName;
    private readonly string _configurationHash;
    private readonly int _maxFrames;
    private readonly long _maxBytes;
    private readonly Task _writerStartGate;
    private readonly object _stateLock = new();
    private Task? _writerTask;
    private DateTimeOffset _startedAtUtc;
    private DateTimeOffset? _endedAtUtc;
    private int _frameCount;
    private int _frameWidth;
    private int _frameHeight;
    private long _frameBytes;
    private long _droppedFrameCount;
    private bool _truncated;
    private bool _stopped;
    private bool _finalized;

    public BattleReplayRecorder(
        string replayRoot,
        string label,
        string configurationName,
        string configurationHash,
        int maxFrames = 10_000,
        long maxBytes = 512L * 1024 * 1024,
        int queueCapacity = 64)
        : this(replayRoot, label, configurationName, configurationHash, maxFrames, maxBytes, queueCapacity, Task.CompletedTask)
    {
    }

    internal BattleReplayRecorder(
        string replayRoot,
        string label,
        string configurationName,
        string configurationHash,
        int maxFrames,
        long maxBytes,
        int queueCapacity,
        Task writerStartGate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _configurationName = configurationName;
        _configurationHash = configurationHash;
        _maxFrames = maxFrames;
        _maxBytes = maxBytes;
        _writerStartGate = writerStartGate ?? throw new ArgumentNullException(nameof(writerStartGate));
        string folder = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{Sanitize(label)}";
        _packageDirectory = Path.Combine(replayRoot, folder);
        _requiredQueue = Channel.CreateUnbounded<ReplayItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        _frameQueue = Channel.CreateBounded<FrameItem>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public string PackageDirectory => _packageDirectory;

    public long DroppedFrameCount => Interlocked.Read(ref _droppedFrameCount);

    public bool IsRecording
    {
        get
        {
            lock (_stateLock)
            {
                return IsRecordingLocked;
            }
        }
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_writerTask != null || _stopped)
            {
                return;
            }

            Directory.CreateDirectory(Path.Combine(_packageDirectory, "frames"));
            _startedAtUtc = DateTimeOffset.UtcNow;
            WriteManifest();
            _writerTask = Task.Run(WriteLoopAsync);
        }
    }

    public void BatchUpdateStates(IReadOnlyList<StateRecord> stateRecords)
    {
        foreach (StateRecord record in stateRecords)
        {
            EnqueueRequired(new StateItem(record, DateTimeOffset.UtcNow));
        }
    }

    public void RecordDecision(BattleReplayDecision decision) => EnqueueRequired(new DecisionItem(decision));

    public bool RecordFrame(Mat frame, DateTimeOffset screenshotTimeUtc)
    {
        lock (_stateLock)
        {
            if (!CanAcceptFrameLocked())
            {
                return false;
            }
        }

        Mat clonedFrame;
        try
        {
            clonedFrame = frame.Clone();
        }
        catch (Exception ex)
        {
            StopAfterFailure(ex);
            return false;
        }

        lock (_stateLock)
        {
            if (!CanAcceptFrameLocked())
            {
                clonedFrame.Dispose();
                return false;
            }

            if (_frameQueue.Writer.TryWrite(new FrameItem(clonedFrame, screenshotTimeUtc)))
            {
                return true;
            }

            clonedFrame.Dispose();
            Interlocked.Increment(ref _droppedFrameCount);
            return false;
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        // 收尾一旦开始就必须排空已接收的状态、决策和帧，避免取消信号截断场景包尾部。
        _ = cancellationToken;
        Task? writerTask;
        lock (_stateLock)
        {
            if (!_stopped)
            {
                _stopped = true;
                _endedAtUtc = DateTimeOffset.UtcNow;
                _requiredQueue.Writer.TryComplete();
                _frameQueue.Writer.TryComplete();
            }

            writerTask = _writerTask;
        }

        if (writerTask != null)
        {
            await writerTask.ConfigureAwait(false);
        }

        FinalizeManifest();
    }

    public void Dispose()
    {
        try
        {
            ShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "战斗回放录制收尾失败");
        }
    }

    private void EnqueueRequired(ReplayItem item)
    {
        Exception? failure = null;
        lock (_stateLock)
        {
            if (!IsRecordingLocked)
            {
                return;
            }

            if (!_requiredQueue.Writer.TryWrite(item))
            {
                failure = new InvalidOperationException("战斗回放必达队列已停止接收记录");
            }
        }

        if (failure != null)
        {
            StopAfterFailure(failure);
        }
    }

    private bool CanAcceptFrameLocked()
    {
        if (!IsRecordingLocked)
        {
            return false;
        }

        if (_frameCount >= _maxFrames || _frameBytes >= _maxBytes)
        {
            _truncated = true;
            Interlocked.Increment(ref _droppedFrameCount);
            return false;
        }

        return true;
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await _writerStartGate.ConfigureAwait(false);
            await using FileStream states = File.Create(Path.Combine(_packageDirectory, "states.jsonl"));
            await using FileStream decisions = File.Create(Path.Combine(_packageDirectory, "decisions.jsonl"));
            while (true)
            {
                bool wroteItem = false;
                int requiredBatchCount = 0;
                while (requiredBatchCount < 256 && _requiredQueue.Reader.TryRead(out ReplayItem? item))
                {
                    await WriteRequiredItemAsync(states, decisions, item).ConfigureAwait(false);
                    requiredBatchCount++;
                    wroteItem = true;
                }

                if (_frameQueue.Reader.TryRead(out FrameItem? frame))
                {
                    WriteFrame(frame);
                    wroteItem = true;
                }

                if (wroteItem)
                {
                    continue;
                }

                bool requiredCompleted = _requiredQueue.Reader.Completion.IsCompleted;
                bool framesCompleted = _frameQueue.Reader.Completion.IsCompleted;
                if (requiredCompleted && framesCompleted)
                {
                    break;
                }

                await WaitForDataAsync(requiredCompleted, framesCompleted).ConfigureAwait(false);
            }

            await states.FlushAsync().ConfigureAwait(false);
            await decisions.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StopAfterFailure(ex);
        }
        finally
        {
            DisposePendingFrames();
        }
    }

    private static async Task WriteRequiredItemAsync(Stream states, Stream decisions, ReplayItem item)
    {
        switch (item)
        {
            case StateItem state:
                await WriteJsonLineAsync(states, state).ConfigureAwait(false);
                break;
            case DecisionItem decision:
                await WriteJsonLineAsync(decisions, decision.Value).ConfigureAwait(false);
                break;
        }
    }

    private async Task WaitForDataAsync(bool requiredCompleted, bool framesCompleted)
    {
        if (requiredCompleted)
        {
            await _frameQueue.Reader.WaitToReadAsync().ConfigureAwait(false);
            return;
        }

        if (framesCompleted)
        {
            await _requiredQueue.Reader.WaitToReadAsync().ConfigureAwait(false);
            return;
        }

        Task<bool> requiredWait = _requiredQueue.Reader.WaitToReadAsync().AsTask();
        Task<bool> frameWait = _frameQueue.Reader.WaitToReadAsync().AsTask();
        await Task.WhenAny(requiredWait, frameWait).ConfigureAwait(false);
    }

    private void WriteFrame(FrameItem frame)
    {
        using (frame.Image)
        {
            if (!Cv2.ImEncode(".webp", frame.Image, out byte[] bytes))
            {
                Interlocked.Increment(ref _droppedFrameCount);
                return;
            }

            lock (_stateLock)
            {
                if (_frameCount >= _maxFrames || _frameBytes + bytes.LongLength > _maxBytes)
                {
                    _truncated = true;
                    Interlocked.Increment(ref _droppedFrameCount);
                    return;
                }

                string path = Path.Combine(_packageDirectory, "frames", $"{_frameCount:D6}_{frame.ScreenshotTimeUtc:yyyyMMdd_HHmmss_fff}.webp");
                File.WriteAllBytes(path, bytes);
                _frameWidth = frame.Image.Width;
                _frameHeight = frame.Image.Height;
                _frameCount++;
                _frameBytes += bytes.LongLength;
            }
        }
    }

    private void StopAfterFailure(Exception exception)
    {
        Log.Error(exception, "战斗回放录制失败，已停止录制");
        lock (_stateLock)
        {
            _stopped = true;
            _endedAtUtc ??= DateTimeOffset.UtcNow;
            _requiredQueue.Writer.TryComplete(exception);
            _frameQueue.Writer.TryComplete(exception);
        }
    }

    private void DisposePendingFrames()
    {
        while (_frameQueue.Reader.TryRead(out FrameItem? frame))
        {
            frame.Image.Dispose();
        }
    }

    private void FinalizeManifest()
    {
        lock (_stateLock)
        {
            if (_finalized)
            {
                return;
            }

            _finalized = true;
        }

        WriteManifest();
    }

    private void WriteManifest()
    {
        if (_startedAtUtc == default || !Directory.Exists(_packageDirectory))
        {
            return;
        }

        BattleReplayManifest manifest = new(
            SchemaVersion,
            _startedAtUtc,
            _endedAtUtc,
            _configurationName,
            _configurationHash,
            _frameWidth,
            _frameHeight,
            DroppedFrameCount,
            _truncated);
        string yaml = $"schemaVersion: {manifest.SchemaVersion}\nstartedAtUtc: {manifest.StartedAtUtc:O}\nendedAtUtc: {(manifest.EndedAtUtc?.ToString("O") ?? string.Empty)}\nconfigurationName: {EscapeYaml(manifest.ConfigurationName)}\nconfigurationHash: {manifest.ConfigurationHash}\nframeWidth: {manifest.FrameWidth}\nframeHeight: {manifest.FrameHeight}\ndroppedFrameCount: {manifest.DroppedFrameCount}\ntruncated: {manifest.Truncated.ToString().ToLowerInvariant()}\n";
        File.WriteAllText(Path.Combine(_packageDirectory, "manifest.yml"), yaml, Encoding.UTF8);
    }

    private static async Task WriteJsonLineAsync<T>(Stream stream, T value)
    {
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions).ConfigureAwait(false);
        await stream.WriteAsync("\n"u8.ToArray()).ConfigureAwait(false);
    }

    private bool IsRecordingLocked => _writerTask != null && !_stopped;

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private static string EscapeYaml(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private abstract record ReplayItem;
    private sealed record StateItem(StateRecord Record, DateTimeOffset SubmittedAtUtc) : ReplayItem;
    private sealed record DecisionItem(BattleReplayDecision Value) : ReplayItem;
    private sealed record FrameItem(Mat Image, DateTimeOffset ScreenshotTimeUtc) : ReplayItem;
}
