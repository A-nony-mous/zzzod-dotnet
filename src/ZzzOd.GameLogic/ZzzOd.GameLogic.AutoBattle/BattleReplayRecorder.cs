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
    private readonly Channel<ReplayItem> _queue;
    private readonly string _packageDirectory;
    private readonly string _configurationName;
    private readonly string _configurationHash;
    private readonly int _maxFrames;
    private readonly long _maxBytes;
    private readonly CancellationTokenSource _shutdown = new();
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

    public BattleReplayRecorder(
        string replayRoot,
        string label,
        string configurationName,
        string configurationHash,
        int maxFrames = 10_000,
        long maxBytes = 512L * 1024 * 1024,
        int queueCapacity = 64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _configurationName = configurationName;
        _configurationHash = configurationHash;
        _maxFrames = maxFrames;
        _maxBytes = maxBytes;
        string folder = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{Sanitize(label)}";
        _packageDirectory = Path.Combine(replayRoot, folder);
        _queue = Channel.CreateBounded<ReplayItem>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public string PackageDirectory => _packageDirectory;

    public long DroppedFrameCount => Interlocked.Read(ref _droppedFrameCount);

    public bool IsRecording => _writerTask != null && !_stopped;

    public void Start()
    {
        lock (_stateLock)
        {
            if (_writerTask != null)
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
        if (!CanAcceptFrame())
        {
            Interlocked.Increment(ref _droppedFrameCount);
            return false;
        }

        if (!_queue.Writer.TryWrite(new FrameItem(frame.Clone(), screenshotTimeUtc)))
        {
            Interlocked.Increment(ref _droppedFrameCount);
            return false;
        }

        return true;
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _endedAtUtc = DateTimeOffset.UtcNow;
            _queue.Writer.TryComplete();
        }

        if (_writerTask != null)
        {
            await _writerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        WriteManifest();
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
        finally
        {
            _shutdown.Dispose();
        }
    }

    private void EnqueueRequired(ReplayItem item)
    {
        if (!IsRecording)
        {
            return;
        }

        try
        {
            _queue.Writer.WriteAsync(item, _shutdown.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StopAfterFailure(ex);
        }
    }

    private bool CanAcceptFrame()
    {
        lock (_stateLock)
        {
            if (!IsRecording || _frameCount >= _maxFrames || _frameBytes >= _maxBytes)
            {
                _truncated = true;
                return false;
            }

            return true;
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await using FileStream states = File.Create(Path.Combine(_packageDirectory, "states.jsonl"));
            await using FileStream decisions = File.Create(Path.Combine(_packageDirectory, "decisions.jsonl"));
            await foreach (ReplayItem item in _queue.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                switch (item)
                {
                    case StateItem state:
                        await WriteJsonLineAsync(states, state).ConfigureAwait(false);
                        break;
                    case DecisionItem decision:
                        await WriteJsonLineAsync(decisions, decision.Value).ConfigureAwait(false);
                        break;
                    case FrameItem frame:
                        WriteFrame(frame);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StopAfterFailure(ex);
        }
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
            _queue.Writer.TryComplete(exception);
        }
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
        await stream.FlushAsync().ConfigureAwait(false);
    }

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
