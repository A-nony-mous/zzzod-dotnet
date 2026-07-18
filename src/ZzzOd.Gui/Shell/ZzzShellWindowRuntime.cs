using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Windows;

namespace ZzzOd.Gui.Shell;

public interface IZzzShellWindowRuntime : IDisposable
{
    void Attach(Window window, InfoBar toastBar);

    void ShowToast(string title, string message, TimeSpan duration, InfoBarSeverity severity);
}

public sealed class ZzzShellWindowRuntime : IZzzShellWindowRuntime
{
    private readonly IZzzDialogService _dialogService;
    private readonly ZzzGlobalInputMonitor _globalInputMonitor;
    private readonly ZzzOverlayController _overlayController;
    private readonly ZzzWindowBackdropService _backdropService;
    private readonly DispatcherTimer _toastTimer;
    private Window? _window;
    private InfoBar? _toastBar;
    private bool _disposed;

    public ZzzShellWindowRuntime(IServiceProvider services)
    {
        _dialogService = services.GetRequiredService<IZzzDialogService>();
        _globalInputMonitor = services.GetRequiredService<ZzzGlobalInputMonitor>();
        _overlayController = services.GetRequiredService<ZzzOverlayController>();
        _backdropService = services.GetRequiredService<ZzzWindowBackdropService>();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += OnToastTimerTick;
    }

    public void Attach(Window window, InfoBar toastBar)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_window is not null)
        {
            throw new InvalidOperationException("Shell 窗口运行时已绑定窗口。");
        }

        _window = window;
        _toastBar = toastBar;
        _dialogService.ToastRequested += OnToastRequested;
        _globalInputMonitor.InputPressed += OnGlobalInputPressed;
        window.Opened += OnWindowOpened;
        window.Closed += OnWindowClosed;
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

        _backdropService.Apply(_window);
        _window.Activate();
        _overlayController.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs args)
    {
        _overlayController.Dispose();
        Dispose();
    }

    private void OnToastRequested(object? sender, ZzzToastRequest request) =>
        Dispatcher.UIThread.Post(() => ShowToast(request.Title, request.Message, TimeSpan.FromSeconds(4), InfoBarSeverity.Informational));

    private void OnGlobalInputPressed(object? sender, string key) =>
        _overlayController.TryToggleFromHotkey(key);

    private void OnToastTimerTick(object? sender, EventArgs args)
    {
        _toastTimer.Stop();
        if (_toastBar is not null)
        {
            _toastBar.IsOpen = false;
        }
    }

    public void ShowToast(string title, string message, TimeSpan duration, InfoBarSeverity severity)
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
}
