using System.Threading.Channels;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Windows;

namespace ZzzOd.Gui.Shell;

public interface IZzzShellWindowRuntime : IDisposable
{
    void Attach(Window window, FAInfoBar toastBar);

    void ShowToast(string title, string message, TimeSpan duration, FAInfoBarSeverity severity);
}

public sealed class ZzzShellWindowRuntime : IZzzShellWindowRuntime
{
    private readonly IZzzDialogService _dialogService;
    private readonly IZzzAppBackend _backend;
    private readonly ZzzGlobalInputMonitor _globalInputMonitor;
    private readonly ZzzOverlayController _overlayController;
    private readonly DispatcherTimer _toastTimer;
    private Window? _window;
    private FAInfoBar? _toastBar;
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private CancellationTokenSource? _eventCancellation;
    private bool _disposed;

    public ZzzShellWindowRuntime(IServiceProvider services)
    {
        _dialogService = services.GetRequiredService<IZzzDialogService>();
        _backend = services.GetRequiredService<IZzzAppBackend>();
        _globalInputMonitor = services.GetRequiredService<ZzzGlobalInputMonitor>();
        _overlayController = services.GetRequiredService<ZzzOverlayController>();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += OnToastTimerTick;
    }

    public void Attach(Window window, FAInfoBar toastBar)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_window is not null)
        {
            throw new InvalidOperationException("Shell 窗口运行时已绑定窗口。");
        }

        _window = window;
        _toastBar = toastBar;
        _overlayController.AttachOwner(window);
        _dialogService.ToastRequested += OnToastRequested;
        _globalInputMonitor.InputPressed += OnGlobalInputPressed;
        window.Opened += OnWindowOpened;
        window.Closed += OnWindowClosed;
        StartRunStateEvents();
        _ = _globalInputMonitor.EnsureStarted();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _toastTimer.Stop();
        _dialogService.ToastRequested -= OnToastRequested;
        _globalInputMonitor.InputPressed -= OnGlobalInputPressed;
        StopRunStateEvents();
        if (_window is not null)
        {
            _window.Opened -= OnWindowOpened;
            _window.Closed -= OnWindowClosed;
        }

        _toastBar = null;
        _window = null;
    }

    private void OnWindowOpened(object? sender, EventArgs args)
    {
        if (_window is null)
        {
            return;
        }

        _window.Activate();
        _overlayController.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs args)
    {
        _overlayController.Dispose();
        Dispose();
    }

    private void OnToastRequested(object? sender, ZzzToastRequest request)
    {
        ZzzRunToast toast = CreateRequestToast(request);
        Dispatcher.UIThread.Post(() => ShowToast(toast.Title, toast.Message, toast.Duration, toast.Severity));
    }

    private void OnGlobalInputPressed(object? sender, string key) =>
        _overlayController.TryToggleFromHotkey(key);

    private void StartRunStateEvents()
    {
        _eventReader = _backend.SubscribeEvents();
        _eventCancellation = new CancellationTokenSource();
        ChannelReader<ZzzBackendEvent> reader = _eventReader;
        CancellationToken token = _eventCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (ZzzBackendEvent item in reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    if (item.Type != "run.stateChanged" || item.Data is not ZzzRunStatusDto run)
                    {
                        continue;
                    }

                    ZzzRunToast? toast = CreateRunToast(run);
                    if (toast is not null)
                    {
                        Dispatcher.UIThread.Post(() => ShowToast(toast.Title, toast.Message, toast.Duration, toast.Severity));
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ChannelClosedException)
            {
            }
        });
    }

    private void StopRunStateEvents()
    {
        _eventCancellation?.Cancel();
        if (_eventReader is not null)
        {
            _backend.UnsubscribeEvents(_eventReader);
        }

        _eventCancellation?.Dispose();
        _eventCancellation = null;
        _eventReader = null;
    }

    private void OnToastTimerTick(object? sender, EventArgs args)
    {
        _toastTimer.Stop();
        if (_toastBar is not null)
        {
            _toastBar.IsOpen = false;
        }
    }

    public void ShowToast(string title, string message, TimeSpan duration, FAInfoBarSeverity severity)
    {
        if (_toastBar is null)
        {
            return;
        }

        _toastBar.Title = title;
        _toastBar.Message = message;
        _toastBar.Severity = severity;
        _toastBar.IsOpen = true;
        _toastTimer.Stop();
        _toastTimer.Interval = duration;
        _toastTimer.Start();
    }

    internal static ZzzRunToast CreateRequestToast(ZzzToastRequest request) =>
        new(request.Title, request.Message, request.Duration, request.Severity);

    internal static ZzzRunToast? CreateRunToast(ZzzRunStatusDto run)
    {
        if (run.State is ZzzRunState.Idle)
        {
            return null;
        }

        string state = run.State switch
        {
            ZzzRunState.Starting => "启动中",
            ZzzRunState.Running => "运行中",
            ZzzRunState.Paused => "已暂停",
            ZzzRunState.Stopping => "停止中",
            ZzzRunState.Succeeded => "已完成",
            ZzzRunState.Cancelled => "已停止",
            ZzzRunState.Failed => "运行异常",
            _ => string.Empty,
        };
        string? app = run.AppName ?? run.AppId;
        string title = string.IsNullOrWhiteSpace(app) ? state : $"{state} {app}";
        string message = run.State is ZzzRunState.Failed
            ? run.Error ?? run.LastStatus ?? string.Empty
            : run.State is ZzzRunState.Succeeded or ZzzRunState.Cancelled
                ? run.LastStatus ?? string.Empty
                : string.Empty;
        FAInfoBarSeverity severity = run.State switch
        {
            ZzzRunState.Running or ZzzRunState.Succeeded => FAInfoBarSeverity.Success,
            ZzzRunState.Paused or ZzzRunState.Stopping => FAInfoBarSeverity.Warning,
            ZzzRunState.Failed => FAInfoBarSeverity.Error,
            _ => FAInfoBarSeverity.Informational,
        };
        return new ZzzRunToast(title, message, TimeSpan.FromSeconds(3), severity);
    }
}

internal sealed record ZzzRunToast(
    string Title,
    string Message,
    TimeSpan Duration,
    FAInfoBarSeverity Severity);
