using System.Globalization;
using System.Threading.Channels;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneDragon.Core.Screening;
using OpenCvSharp;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.RunIntent;

namespace ZzzOd.Gui.Services.Windows;

internal interface IZzzEnvironmentRuntimeCoordinator
{
    Task<ZzzBackendResult<bool>> ReinitializeContextAsync(CancellationToken cancellationToken = default);

    IDisposable SuspendHotkeyActions();

    void UpdateEnvironmentConfiguration(ZzzConfigScopeValuesDto values);
}

internal sealed class ZzzEnvironmentRuntimeCoordinator : IHostedService, IZzzEnvironmentRuntimeCoordinator, IDisposable
{
    private static readonly TimeSpan DefaultRunStateObservationTimeout = TimeSpan.FromSeconds(3);

    private readonly IZzzAppBackend _backend;
    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly ZzzGuiRunIntentService _runIntent;
    private readonly IZzzDialogService _dialogService;
    private readonly IZzzImageClipboardService _clipboard;
    private readonly ILogger<ZzzEnvironmentRuntimeCoordinator> _logger;
    private readonly Func<ZzzBackendResult<bool>> _reinitializeContext;
    private readonly Func<ZzzBackendResult<byte[]>> _captureDebugScreenshot;
    private readonly TimeSpan _runStateObservationTimeout;
    private readonly IOverlayCapturer? _overlayCapturer;
    private readonly string _runRoot;
    private readonly SemaphoreSlim _actionGate = new(1, 1);
    private CancellationTokenSource _shutdown = new();
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private Task? _eventTask;
    private HotkeySettings? _settings;
    private PendingRunAction? _pendingRunAction;
    private long _runActionSequence;
    private int _suspensionCount;
    private bool _started;
    private bool _disposed;

    public ZzzEnvironmentRuntimeCoordinator(
        IZzzAppBackend backend,
        ZzzGlobalInputMonitor inputMonitor,
        ZzzGuiRunIntentService runIntent,
        IZzzDialogService dialogService,
        IZzzImageClipboardService clipboard,
        ZzzRuntimeManager runtime,
        IOverlayCapturer overlayCapturer,
        ILogger<ZzzEnvironmentRuntimeCoordinator> logger)
        : this(
            backend,
            inputMonitor,
            runIntent,
            dialogService,
            clipboard,
            runtime.RunRoot,
            runtime.ReinitializeContext,
            runtime.CaptureDebugScreenshot,
            logger,
            overlayCapturer)
    {
    }

    internal ZzzEnvironmentRuntimeCoordinator(
        IZzzAppBackend backend,
        ZzzGlobalInputMonitor inputMonitor,
        ZzzGuiRunIntentService runIntent,
        IZzzDialogService dialogService,
        IZzzImageClipboardService clipboard,
        string runRoot,
        Func<ZzzBackendResult<bool>> reinitializeContext,
        Func<ZzzBackendResult<byte[]>> captureDebugScreenshot,
        ILogger<ZzzEnvironmentRuntimeCoordinator> logger,
        IOverlayCapturer? overlayCapturer = null,
        TimeSpan? runStateObservationTimeout = null)
    {
        _backend = backend;
        _inputMonitor = inputMonitor;
        _runIntent = runIntent;
        _dialogService = dialogService;
        _clipboard = clipboard;
        _runRoot = Path.GetFullPath(runRoot);
        _reinitializeContext = reinitializeContext;
        _captureDebugScreenshot = captureDebugScreenshot;
        _runStateObservationTimeout = runStateObservationTimeout ?? DefaultRunStateObservationTimeout;
        _overlayCapturer = overlayCapturer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return Task.CompletedTask;
        }

        LoadConfiguration();
        if (!_inputMonitor.EnsureStarted())
        {
			throw new InvalidOperationException(_inputMonitor.LastError ?? "全局按键监听启动失败。");
        }

        _eventReader = _backend.SubscribeEvents();
        _eventTask = ObserveBackendEventsAsync(_eventReader, _shutdown.Token);
        _inputMonitor.InputPressed += OnInputPressed;
        _started = true;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            _inputMonitor.InputPressed -= OnInputPressed;
            _started = false;
        }

        _shutdown.Cancel();
        if (_eventReader is not null)
        {
            _backend.UnsubscribeEvents(_eventReader);
            _eventReader = null;
        }

        if (_eventTask is not null)
        {
            await _eventTask.ConfigureAwait(false);
            _eventTask = null;
        }
    }

    public Task<ZzzBackendResult<bool>> ReinitializeContextAsync(CancellationToken cancellationToken = default) =>
        Task.Run(_reinitializeContext, cancellationToken);

    public IDisposable SuspendHotkeyActions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Interlocked.Increment(ref _suspensionCount);
        return new Suspension(this);
    }

    public void UpdateEnvironmentConfiguration(ZzzConfigScopeValuesDto values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!string.Equals(values.Descriptor.Scope, "env", StringComparison.Ordinal))
        {
            return;
        }

        Volatile.Write(ref _settings, HotkeySettings.From(values.Values));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_started)
        {
            _inputMonitor.InputPressed -= OnInputPressed;
            _started = false;
        }

        _shutdown.Cancel();
        if (_eventReader is not null)
        {
            _backend.UnsubscribeEvents(_eventReader);
            _eventReader = null;
        }

        _shutdown.Dispose();
        _actionGate.Dispose();
    }

    internal Task HandleInputPressedForTestAsync(string key, CancellationToken cancellationToken = default) =>
        HandleInputPressedAsync(key, cancellationToken);

    private async void OnInputPressed(object? sender, string key)
    {
        try
        {
            await HandleInputPressedAsync(key, _shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "处理全局按键 {Key} 失败", key);
        }
    }

    private async Task HandleInputPressedAsync(string key, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _suspensionCount) > 0 || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        await _actionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            HotkeySettings? settings = Volatile.Read(ref _settings);
            if (settings is null)
            {
                settings = LoadConfiguration();
                if (settings is null)
                {
                    return;
                }
            }

            if (string.Equals(settings.StartRunning, key, StringComparison.OrdinalIgnoreCase))
            {
                await TogglePauseAndResumeAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(settings.StopRunning, key, StringComparison.OrdinalIgnoreCase))
            {
                await StopCurrentRunAsync().ConfigureAwait(false);
            }
            else if (string.Equals(settings.Screenshot, key, StringComparison.OrdinalIgnoreCase))
            {
                await CaptureDebugScreenshotAsync(settings.CopyScreenshot, cancellationToken).ConfigureAwait(false);
            }

            _runIntent.PublishGlobalInputPressed(key);
        }
        finally
        {
            _actionGate.Release();
        }
    }

    private async Task TogglePauseAndResumeAsync(CancellationToken cancellationToken)
    {
        ZzzBackendResult<ZzzRunStatusDto> current = _backend.GetCurrentRun();
        if (!current.Success || current.Value is null)
        {
            ReportRunActionError("读取运行状态失败", current.Error ?? "运行状态读取失败。");
            return;
        }

        if (current.Value.State is ZzzRunState.Idle or ZzzRunState.Succeeded or ZzzRunState.Cancelled or ZzzRunState.Failed)
        {
            await StartIdleRunAsync(current.Value, cancellationToken).ConfigureAwait(false);
            return;
        }

        string? action = current.Value.State switch
        {
            ZzzRunState.Running => "pause",
            ZzzRunState.Paused => "resume",
            _ => null,
        };
        if (action is null)
        {
            return;
        }

        long sequence = Interlocked.Increment(ref _runActionSequence);
        PendingRunAction pending = BeginRunAction(sequence, action, current.Value, null);
        LogRunActionRequested(sequence, action, current.Value, null);
        try
        {
            ZzzBackendResult<ZzzRunStatusDto> result = action == "pause"
                ? _backend.PauseRun()
                : _backend.ResumeRun();
            if (!result.Success)
            {
                CancelRunAction(pending);
                LogRunActionResult(sequence, action, result);
                ReportRunActionError(action == "pause" ? "暂停失败" : "恢复失败", result.Error ?? "运行请求失败。");
                return;
            }

            LogRunActionResult(sequence, action, result);
            ObserveAcceptedRunAction(pending, cancellationToken);
        }
        catch (Exception exception)
        {
            CancelRunAction(pending);
            _logger.LogError(exception, "F9 请求 #{Sequence} {Action} 发生异常", sequence, action);
            ReportRunActionError(action == "pause" ? "暂停失败" : "恢复失败", exception.Message);
        }
    }

    private async Task StopCurrentRunAsync()
    {
        ZzzBackendResult<ZzzRunStatusDto> current = _backend.GetCurrentRun();
        if (!current.Success || current.Value is null)
        {
            _logger.LogWarning("读取当前运行状态失败：{Error}", current.Error);
            return;
        }

        if (current.Value.State is not (ZzzRunState.Starting or ZzzRunState.Running or ZzzRunState.Paused))
        {
            return;
        }

        ZzzBackendResult<ZzzRunStatusDto> result = await _backend.StopRunAsync().ConfigureAwait(false);
        if (!result.Success)
        {
            _logger.LogWarning("停止当前运行失败：{Error}", result.Error);
        }
    }

    private async Task CaptureDebugScreenshotAsync(bool copyScreenshot, CancellationToken cancellationToken)
    {
        ZzzBackendResult<byte[]> result = _captureDebugScreenshot();
        if (!result.Success || result.Value is null)
        {
            _logger.LogWarning("游戏截图失败：{Error}", result.Error);
            return;
        }

        string directory = Path.Combine(_runRoot, ".debug", "images");
        Directory.CreateDirectory(directory);
        string path = CreateUniqueScreenshotPath(directory);
        await File.WriteAllBytesAsync(path, result.Value, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("游戏截图已保存 {Path}", path);

        byte[] clipboardBytes = result.Value;
        if (TryGetPatchedCaptureSettings(out string suffix))
        {
            try
            {
                byte[]? patchedBytes = TryComposePatchedScreenshot(result.Value);
                if (patchedBytes is not null)
                {
                    string patchedPath = CreatePatchedScreenshotPath(path, suffix);
                    await File.WriteAllBytesAsync(patchedPath, patchedBytes, cancellationToken).ConfigureAwait(false);
                    clipboardBytes = patchedBytes;
                    _logger.LogInformation("Overlay 合成截图已保存 {Path}", patchedPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Overlay 合成截图失败，已保留原始游戏截图 {Path}", path);
            }
        }

        if (copyScreenshot)
        {
            await _clipboard.CopyPngAsync(clipboardBytes, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StartIdleRunAsync(ZzzRunStatusDto current, CancellationToken cancellationToken)
    {
        ZzzGuiRunTarget? target = _runIntent.CurrentRunTarget;
        if (target is null)
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> config = _backend.GetConfigScope("standalone-app");
            string? activeAppId = config.Success && config.Value is not null
                ? ReadString(config.Value.Values, "active_app_id")
                : null;
            if (string.IsNullOrWhiteSpace(activeAppId))
            {
                ReportRunActionError("启动失败", "未选择运行应用。");
                return;
            }

            target = new ZzzGuiRunTarget(activeAppId, ZOneDragonAppConstants.DefaultGroupId, null);
        }

        long sequence = Interlocked.Increment(ref _runActionSequence);
        PendingRunAction pending = BeginRunAction(sequence, "start", current, target);
        LogRunActionRequested(sequence, "start", current, target);
        try
        {
            ZzzBackendResult<ZzzRunStatusDto> result = await _backend.StartRunAsync(
                new ZzzStartRunRequest(target.AppId, target.InstanceIndex, target.GroupId)).ConfigureAwait(false);
            if (!result.Success)
            {
                CancelRunAction(pending);
                LogRunActionResult(sequence, "start", result);
                ReportRunActionError("启动失败", result.Error ?? "应用启动失败。");
                return;
            }

            LogRunActionResult(sequence, "start", result);
            ObserveAcceptedRunAction(pending, cancellationToken);
        }
        catch (Exception exception)
        {
            CancelRunAction(pending);
            _logger.LogError(
                exception,
                "F9 请求 #{Sequence} start 发生异常 app={AppId} group={GroupId} instance={InstanceIndex}",
                sequence,
                target.AppId,
                target.GroupId,
                target.InstanceIndex);
            ReportRunActionError("启动失败", exception.Message);
        }
    }

    private PendingRunAction BeginRunAction(
        long sequence,
        string action,
        ZzzRunStatusDto current,
        ZzzGuiRunTarget? target)
    {
        string? appId = target?.AppId ?? current.AppId;
        string? groupId = target?.GroupId ?? current.GroupId;
        int? instanceIndex = target?.InstanceIndex ?? current.InstanceIndex;
        PendingRunAction pending = new(
            sequence,
            action,
            current.State,
            appId,
            groupId,
            instanceIndex,
            action == "start" ? current.AppId : null,
            action == "start" ? current.StartedAt : null,
            ExpectedStates(action));
        Interlocked.Exchange(ref _pendingRunAction, pending)?.StateObserved.TrySetCanceled();
        return pending;
    }

    private void CancelRunAction(PendingRunAction pending)
    {
        if (ReferenceEquals(Interlocked.CompareExchange(ref _pendingRunAction, null, pending), pending))
        {
            pending.StateObserved.TrySetCanceled();
        }
    }

    private void ObserveAcceptedRunAction(PendingRunAction pending, CancellationToken cancellationToken) =>
        _ = ObserveAcceptedRunActionAsync(pending, cancellationToken);

    private async Task ObserveAcceptedRunActionAsync(PendingRunAction pending, CancellationToken cancellationToken)
    {
        try
        {
            await pending.StateObserved.Task.WaitAsync(_runStateObservationTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _pendingRunAction, null, pending), pending))
            {
                return;
            }

            _logger.LogWarning(
                "F9 请求 #{Sequence} 状态事件超时 action={Action} previous={PreviousState} app={AppId} group={GroupId} instance={InstanceIndex} timeoutMs={TimeoutMs}",
                pending.Sequence,
                pending.Action,
                pending.PreviousState,
                pending.AppId,
                pending.GroupId,
                pending.InstanceIndex,
                _runStateObservationTimeout.TotalMilliseconds);
            ReportRunActionError("运行状态同步失败", "请求已提交，但未收到对应的运行状态事件。");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static IReadOnlySet<ZzzRunState> ExpectedStates(string action) => action switch
    {
        "start" => new HashSet<ZzzRunState>
        {
            ZzzRunState.Starting,
            ZzzRunState.Running,
            ZzzRunState.Paused,
            ZzzRunState.Succeeded,
            ZzzRunState.Cancelled,
            ZzzRunState.Failed,
        },
        "pause" => new HashSet<ZzzRunState> { ZzzRunState.Paused },
        "resume" => new HashSet<ZzzRunState> { ZzzRunState.Running },
        _ => new HashSet<ZzzRunState>(),
    };

    private void ReportRunActionError(string title, string message)
    {
        _logger.LogWarning("{Title}：{Message}", title, message);
        _dialogService.ShowToast(title, message, TimeSpan.FromSeconds(6), FAInfoBarSeverity.Error);
    }

    private void LogRunActionRequested(
        long sequence,
        string action,
        ZzzRunStatusDto current,
        ZzzGuiRunTarget? target) =>
        _logger.LogInformation(
            "F9 请求 #{Sequence} action={Action} state={State} app={AppId} group={GroupId} instance={InstanceIndex}",
            sequence,
            action,
            current.State,
            target?.AppId ?? current.AppId,
            target?.GroupId ?? current.GroupId,
            target?.InstanceIndex ?? current.InstanceIndex);

    private void LogRunActionResult(
        long sequence,
        string action,
        ZzzBackendResult<ZzzRunStatusDto> result) =>
        _logger.LogInformation(
            "F9 请求 #{Sequence} backend result action={Action} success={Success} code={ErrorCode} error={Error}",
            sequence,
            action,
            result.Success,
            result.ErrorCode,
            result.Error);

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private bool TryGetPatchedCaptureSettings(out string suffix)
    {
        suffix = "_patched";
        if (_overlayCapturer is null)
        {
            return false;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("overlay");
        if (!result.Success || result.Value is null)
        {
            _logger.LogWarning("读取 Overlay 截图设置失败：{Error}", result.Error);
            return false;
        }

		IReadOnlyDictionary<string, object?> values = result.Value.Values;
		if (!values.TryGetValue("patched_capture_enabled", out object? enabledValue))
		{
			return false;
		}

		try
		{
			if (!Convert.ToBoolean(enabledValue, CultureInfo.InvariantCulture))
			{
				return false;
			}
		}
		catch (FormatException)
		{
			_logger.LogWarning("Overlay 截图开关格式无效。");
			return false;
		}
		catch (InvalidCastException)
		{
			_logger.LogWarning("Overlay 截图开关格式无效。");
			return false;
		}

		if (values.TryGetValue("patched_capture_suffix", out object? suffixValue))
		{
			string? configuredSuffix = Convert.ToString(suffixValue, CultureInfo.InvariantCulture);
			if (!string.IsNullOrWhiteSpace(configuredSuffix))
			{
				suffix = configuredSuffix;
			}
		}

		return true;
    }

    private byte[]? TryComposePatchedScreenshot(byte[] gameScreenshotBytes)
    {
        if (_overlayCapturer is null)
        {
            return null;
        }

        IReadOnlyList<OverlayCaptureFrame> frames = _overlayCapturer.CaptureFrames();
        try
        {
            if (frames.Count == 0)
            {
                return null;
            }

            using Mat gameScreenshot = Cv2.ImDecode(gameScreenshotBytes, ImreadModes.Unchanged);
            if (gameScreenshot.Empty())
            {
                throw new InvalidDataException("游戏截图不是有效 PNG 图像。");
            }

            using Mat patchedScreenshot = OverlayImageComposer.Compose(gameScreenshot, frames);
            Cv2.ImEncode(".png", patchedScreenshot, out byte[] patchedBytes);
            return patchedBytes;
        }
        finally
        {
            foreach (OverlayCaptureFrame frame in frames)
            {
                frame.Dispose();
            }
        }
    }

    private static string CreatePatchedScreenshotPath(string originalPath, string suffix)
    {
        string directory = Path.GetDirectoryName(originalPath) ?? throw new InvalidOperationException("截图目录不存在。");
        return Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(originalPath) + suffix + Path.GetExtension(originalPath));
    }

    private static string CreateUniqueScreenshotPath(string directory)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string path;
        do
        {
            path = Path.Combine(directory, $"{timestamp.ToString(CultureInfo.InvariantCulture)}.png");
            timestamp++;
        }
        while (File.Exists(path));

        return path;
    }

    private HotkeySettings? LoadConfiguration()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("env");
        if (!result.Success || result.Value is null)
        {
            _logger.LogWarning("读取脚本环境热键失败：{Error}", result.Error);
            return null;
        }

        UpdateEnvironmentConfiguration(result.Value);
        return Volatile.Read(ref _settings);
    }

    private async Task ObserveBackendEventsAsync(
        ChannelReader<ZzzBackendEvent> reader,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (ZzzBackendEvent item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(item.Type, "config.changed", StringComparison.Ordinal)
                    && item.Data is ZzzConfigScopeValuesDto values)
                {
                    UpdateEnvironmentConfiguration(values);
                    continue;
                }

                if (string.Equals(item.Type, "run.stateChanged", StringComparison.Ordinal)
                    && item.Data is ZzzRunStatusDto run)
                {
                    ObserveRunStateChanged(run);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ObserveRunStateChanged(ZzzRunStatusDto run)
    {
        PendingRunAction? pending = Volatile.Read(ref _pendingRunAction);
        if (pending is null)
        {
            return;
        }

        _logger.LogInformation(
            "F9 请求 #{Sequence} 收到状态事件 action={Action} state={State} app={AppId} group={GroupId} instance={InstanceIndex}",
            pending.Sequence,
            pending.Action,
            run.State,
            run.AppId,
            run.GroupId,
            run.InstanceIndex);
        if (!pending.ExpectedStates.Contains(run.State) || !MatchesPendingRun(pending, run))
        {
            return;
        }

        if (ReferenceEquals(Interlocked.CompareExchange(ref _pendingRunAction, null, pending), pending))
        {
            pending.StateObserved.TrySetResult(run);
        }
    }

    private static bool MatchesPendingRun(PendingRunAction pending, ZzzRunStatusDto run)
    {
        if (pending.Action != "start")
        {
            return true;
        }

        bool stillPreviousRun = pending.PreviousState != ZzzRunState.Idle
            && string.Equals(run.AppId, pending.PreviousAppId, StringComparison.Ordinal)
            && string.Equals(run.StartedAt, pending.PreviousStartedAt, StringComparison.Ordinal);
        if (stillPreviousRun)
        {
            return false;
        }

        return (!string.IsNullOrWhiteSpace(run.AppId) && string.Equals(run.AppId, pending.AppId, StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(run.GroupId) || string.Equals(run.GroupId, pending.GroupId, StringComparison.Ordinal))
            && (!run.InstanceIndex.HasValue || !pending.InstanceIndex.HasValue || run.InstanceIndex == pending.InstanceIndex);
    }

    private static string ReadRequiredString(IReadOnlyDictionary<string, object?> values, string name)
    {
        if (!values.TryGetValue(name, out object? value))
        {
			throw new InvalidOperationException("脚本环境缺少配置项 " + name + "。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool ReadRequiredBool(IReadOnlyDictionary<string, object?> values, string name)
    {
        if (!values.TryGetValue(name, out object? value))
        {
			throw new InvalidOperationException("脚本环境缺少配置项 " + name + "。");
        }

        return value is bool flag ? flag : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private void ReleaseSuspension() => Interlocked.Decrement(ref _suspensionCount);

    private sealed class Suspension(ZzzEnvironmentRuntimeCoordinator owner) : IDisposable
    {
        private ZzzEnvironmentRuntimeCoordinator? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseSuspension();
    }

    private sealed record PendingRunAction(
        long Sequence,
        string Action,
        ZzzRunState PreviousState,
        string? AppId,
        string? GroupId,
        int? InstanceIndex,
        string? PreviousAppId,
        string? PreviousStartedAt,
        IReadOnlySet<ZzzRunState> ExpectedStates)
    {
        public TaskCompletionSource<ZzzRunStatusDto> StateObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record HotkeySettings(
        string StartRunning,
        string StopRunning,
        string Screenshot,
        bool CopyScreenshot)
    {
        public static HotkeySettings From(IReadOnlyDictionary<string, object?> values) => new(
            ReadRequiredString(values, "key_start_running"),
            ReadRequiredString(values, "key_stop_running"),
            ReadRequiredString(values, "key_screenshot"),
            ReadRequiredBool(values, "copy_screenshot"));
    }
}

