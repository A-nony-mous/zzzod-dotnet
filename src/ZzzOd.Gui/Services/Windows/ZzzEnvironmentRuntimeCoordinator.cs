using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZzzOd.AppHost.Backend;
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
    private readonly IZzzAppBackend _backend;
    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly ZzzGuiRunIntentService _runIntent;
    private readonly IZzzImageClipboardService _clipboard;
    private readonly ILogger<ZzzEnvironmentRuntimeCoordinator> _logger;
    private readonly Func<ZzzBackendResult<bool>> _reinitializeContext;
    private readonly Func<ZzzBackendResult<byte[]>> _captureDebugScreenshot;
    private readonly string _runRoot;
    private readonly SemaphoreSlim _actionGate = new(1, 1);
    private CancellationTokenSource _shutdown = new();
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private Task? _eventTask;
    private HotkeySettings? _settings;
    private int _suspensionCount;
    private bool _started;
    private bool _disposed;

    public ZzzEnvironmentRuntimeCoordinator(
        IZzzAppBackend backend,
        ZzzGlobalInputMonitor inputMonitor,
        ZzzGuiRunIntentService runIntent,
        IZzzImageClipboardService clipboard,
        ZzzRuntimeManager runtime,
        ILogger<ZzzEnvironmentRuntimeCoordinator> logger)
        : this(
            backend,
            inputMonitor,
            runIntent,
            clipboard,
            runtime.RunRoot,
            runtime.ReinitializeContext,
            runtime.CaptureDebugScreenshot,
            logger)
    {
    }

    internal ZzzEnvironmentRuntimeCoordinator(
        IZzzAppBackend backend,
        ZzzGlobalInputMonitor inputMonitor,
        ZzzGuiRunIntentService runIntent,
        IZzzImageClipboardService clipboard,
        string runRoot,
        Func<ZzzBackendResult<bool>> reinitializeContext,
        Func<ZzzBackendResult<byte[]>> captureDebugScreenshot,
        ILogger<ZzzEnvironmentRuntimeCoordinator> logger)
    {
        _backend = backend;
        _inputMonitor = inputMonitor;
        _runIntent = runIntent;
        _clipboard = clipboard;
        _runRoot = Path.GetFullPath(runRoot);
        _reinitializeContext = reinitializeContext;
        _captureDebugScreenshot = captureDebugScreenshot;
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
                TogglePauseAndResume();
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

    private void TogglePauseAndResume()
    {
        ZzzBackendResult<ZzzRunStatusDto> current = _backend.GetCurrentRun();
        if (!current.Success || current.Value is null)
        {
            _logger.LogWarning("读取当前运行状态失败：{Error}", current.Error);
            return;
        }

        ZzzBackendResult<ZzzRunStatusDto>? result = current.Value.State switch
        {
            ZzzRunState.Running => _backend.PauseRun(),
            ZzzRunState.Paused => _backend.ResumeRun(),
            _ => null,
        };
        if (result is { Success: false })
        {
            _logger.LogWarning("切换暂停和运行失败：{Error}", result.Error);
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

        if (copyScreenshot)
        {
            await _clipboard.CopyPngAsync(result.Value, cancellationToken).ConfigureAwait(false);
        }
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
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
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

