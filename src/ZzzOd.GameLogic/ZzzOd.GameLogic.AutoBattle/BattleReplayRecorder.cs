using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Security.Cryptography;
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
    bool Truncated,
    string? ConfigurationRelativePath = null);

internal sealed record BattleReplayConfigurationSnapshot(
    string WorkDirectory,
    string PrimaryPath,
    IReadOnlyList<string> LoadedPaths);

/// <summary>
/// 将自动战斗的状态、决策和帧旁路写入可回放场景包。
/// </summary>
public sealed class BattleReplayRecorder : IStateRecordUpdateListener, IShutdownParticipant, IDisposable
{
    private const int CurrentSchemaVersion = 2;
    private static readonly TimeSpan FrameSamplingInterval = TimeSpan.FromMilliseconds(500);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private readonly Channel<ReplayItem> _requiredQueue;
    private readonly Channel<FrameItem> _frameQueue;
    private readonly string _packageDirectory;
    private readonly string _configurationName;
    private readonly string _configurationHash;
    private readonly string? _configurationRelativePath;
    private readonly BattleReplayConfigurationSnapshot? _configurationSnapshot;
    private readonly int _schemaVersion;
    private readonly int _maxFrames;
    private readonly long _maxBytes;
    private readonly Task _requiredWriterStartGate;
    private readonly Task _frameWriterStartGate;
    private readonly object _stateLock = new();
    private Task? _writerTask;
    private DateTimeOffset _startedAtUtc;
    private DateTimeOffset? _endedAtUtc;
    private int _frameCount;
    private int _frameWidth;
    private int _frameHeight;
    private long _frameBytes;
    private long _droppedFrameCount;
    private DateTimeOffset? _nextFrameSampleAtUtc;
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
        BattleReplayConfigurationSnapshot configurationSnapshot)
        : this(
            replayRoot,
            label,
            configurationName,
            ComputeConfigurationHash(configurationSnapshot.PrimaryPath),
            10_000,
            512L * 1024 * 1024,
            64,
            Task.CompletedTask,
            Task.CompletedTask,
            configurationSnapshot)
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
        : this(
            replayRoot,
            label,
            configurationName,
            configurationHash,
            maxFrames,
            maxBytes,
            queueCapacity,
            writerStartGate,
            writerStartGate)
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
        Task requiredWriterStartGate,
        Task frameWriterStartGate,
        BattleReplayConfigurationSnapshot? configurationSnapshot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _configurationName = configurationName;
        _configurationHash = configurationHash;
        _configurationSnapshot = configurationSnapshot;
        _schemaVersion = configurationSnapshot == null ? 1 : CurrentSchemaVersion;
        _configurationRelativePath = configurationSnapshot == null
            ? null
            : NormalizeRelativePath(configurationSnapshot.WorkDirectory, configurationSnapshot.PrimaryPath);
        _maxFrames = maxFrames;
        _maxBytes = maxBytes;
        _requiredWriterStartGate = requiredWriterStartGate ?? throw new ArgumentNullException(nameof(requiredWriterStartGate));
        _frameWriterStartGate = frameWriterStartGate ?? throw new ArgumentNullException(nameof(frameWriterStartGate));
        string folder = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{Sanitize(label)}";
        _packageDirectory = Path.Combine(replayRoot, folder);
        _requiredQueue = Channel.CreateUnbounded<ReplayItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        _frameQueue = Channel.CreateBounded<FrameItem>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
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

            try
            {
                Directory.CreateDirectory(Path.Combine(_packageDirectory, "frames"));
                CopyConfigurationSnapshot();
                _startedAtUtc = DateTimeOffset.UtcNow;
                WriteManifest();
                Task requiredWriter = Task.Run(WriteRequiredLoopAsync);
                Task frameWriter = Task.Run(WriteFrameLoopAsync);
                _writerTask = Task.WhenAll(requiredWriter, frameWriter);
            }
            catch (Exception ex)
            {
                StopAfterFailure(ex);
            }
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

            if (_nextFrameSampleAtUtc.HasValue && screenshotTimeUtc < _nextFrameSampleAtUtc.Value)
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

            _nextFrameSampleAtUtc = screenshotTimeUtc + FrameSamplingInterval;
            while (_frameQueue.Reader.TryRead(out FrameItem? staleFrame))
            {
                staleFrame.Image.Dispose();
                Interlocked.Increment(ref _droppedFrameCount);
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

    private async Task WriteRequiredLoopAsync()
    {
        try
        {
            await _requiredWriterStartGate.ConfigureAwait(false);
            await using FileStream states = OpenRequiredStream(Path.Combine(_packageDirectory, "states.jsonl"));
            await using FileStream decisions = OpenRequiredStream(Path.Combine(_packageDirectory, "decisions.jsonl"));
            while (await _requiredQueue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_requiredQueue.Reader.TryRead(out ReplayItem? item))
                {
                    await WriteRequiredItemAsync(states, decisions, item).ConfigureAwait(false);
                }
            }

            await states.FlushAsync().ConfigureAwait(false);
            await decisions.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StopAfterFailure(ex);
        }
    }

    private async Task WriteFrameLoopAsync()
    {
        try
        {
            await _frameWriterStartGate.ConfigureAwait(false);
            while (await _frameQueue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_frameQueue.Reader.TryRead(out FrameItem? frame))
                {
                    WriteFrame(frame);
                }
            }
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

    private static FileStream OpenRequiredStream(string path)
    {
        return new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
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

    private void CopyConfigurationSnapshot()
    {
        if (_configurationSnapshot == null)
        {
            return;
        }

        string sourceRoot = Path.GetFullPath(_configurationSnapshot.WorkDirectory);
        string packageRoot = Path.GetFullPath(_packageDirectory);
        IEnumerable<string> sourcePaths = _configurationSnapshot.LoadedPaths
            .Append(_configurationSnapshot.PrimaryPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (string sourcePath in sourcePaths)
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);
            string relativePath = NormalizeRelativePath(sourceRoot, fullSourcePath);
            string destinationPath = Path.GetFullPath(Path.Combine(packageRoot, relativePath));
            EnsurePathUnderRoot(packageRoot, destinationPath);
            if (!File.Exists(fullSourcePath))
            {
                throw new FileNotFoundException("战斗回放配置快照源文件不存在", fullSourcePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(fullSourcePath, destinationPath, overwrite: true);
        }
    }

    private static string NormalizeRelativePath(string rootPath, string filePath)
    {
        string fullRootPath = Path.GetFullPath(rootPath);
        string fullFilePath = Path.GetFullPath(filePath);
        EnsurePathUnderRoot(fullRootPath, fullFilePath);
        return Path.GetRelativePath(fullRootPath, fullFilePath).Replace('\\', '/');
    }

    private static void EnsurePathUnderRoot(string rootPath, string filePath)
    {
        string rootWithSeparator = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)) + Path.DirectorySeparatorChar;
        string fullFilePath = Path.GetFullPath(filePath);
        if (!fullFilePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"战斗回放配置路径超出工作目录: {fullFilePath}");
        }
    }

    private static string ComputeConfigurationHash(string configurationPath)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(configurationPath)));
    }

    private void WriteManifest()
    {
        if (_startedAtUtc == default || !Directory.Exists(_packageDirectory))
        {
            return;
        }

        BattleReplayManifest manifest = new(
            _schemaVersion,
            _startedAtUtc,
            _endedAtUtc,
            _configurationName,
            _configurationHash,
            _frameWidth,
            _frameHeight,
            DroppedFrameCount,
            _truncated,
            _configurationRelativePath);
        string yaml = $"schemaVersion: {manifest.SchemaVersion}\nstartedAtUtc: {manifest.StartedAtUtc:O}\nendedAtUtc: {(manifest.EndedAtUtc?.ToString("O") ?? string.Empty)}\nconfigurationName: {EscapeYaml(manifest.ConfigurationName)}\nconfigurationHash: {manifest.ConfigurationHash}\nconfigurationRelativePath: {EscapeYaml(manifest.ConfigurationRelativePath ?? string.Empty)}\nframeWidth: {manifest.FrameWidth}\nframeHeight: {manifest.FrameHeight}\ndroppedFrameCount: {manifest.DroppedFrameCount}\ntruncated: {manifest.Truncated.ToString().ToLowerInvariant()}\n";
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
