using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed record ZzzEnvironmentOption(string Label, string Value);

internal sealed partial class FrontierEnvironmentSettingsPage : UserControl, IZzzPageLifecycle
{
    private static readonly ZzzEnvironmentOption[] ScreenshotMethods =
    [
        new("自动", "auto"),
        new("Windows Graphics Capture", "wgc"),
        new("BitBlt", "bitblt"),
        new("Print Window", "print_window"),
    ];

    private static readonly ZzzEnvironmentOption[] ProxyTypes =
    [
        new("无", "None"),
        new("个人代理", "personal"),
    ];

    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly IZzzEnvironmentRuntimeCoordinator? _runtimeCoordinator;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly ZzzEnvironmentSettingsViewModel _viewModel;
    private readonly bool _ownsInputMonitor;
    private readonly FAInfoBar _actionBar;
    private readonly IReadOnlyDictionary<string, Button> _hotkeyButtons;
    private Button? _captureButton;
    private IDisposable? _hotkeyActionSuspension;

    public FrontierEnvironmentSettingsPage(
        IZzzAppBackend backend,
        ZzzGlobalInputMonitor? inputMonitor = null,
        IZzzEnvironmentRuntimeCoordinator? runtimeCoordinator = null,
        ZzzGuiOperationTracker? operations = null)
    {
        _inputMonitor = inputMonitor ?? new ZzzGlobalInputMonitor();
        _runtimeCoordinator = runtimeCoordinator;
        _operations = operations ?? new ZzzGuiOperationTracker();
        _ownsInputMonitor = inputMonitor is null;
        AvaloniaXamlLoader.Load(this);

        _actionBar = Required<FAInfoBar>("ActionBar");
        _viewModel = new ZzzEnvironmentSettingsViewModel(
            backend,
            ScreenshotMethods,
            ProxyTypes,
            runtimeCoordinator,
            ShowError,
            ReinitializeContextAsync);
        DataContext = _viewModel;
        _hotkeyButtons = new Dictionary<string, Button>(StringComparer.Ordinal)
        {
            ["key_start_running"] = Required<Button>("StartRunningKeyButton"),
            ["key_stop_running"] = Required<Button>("StopRunningKeyButton"),
            ["key_screenshot"] = Required<Button>("ScreenshotKeyButton"),
            ["key_debug"] = Required<Button>("DebugKeyButton"),
        };

    }

    internal bool PersonalProxyVisible => _viewModel.PersonalProxyVisible;

    internal string? SelectedProxyType => _viewModel.SelectedProxyType?.Value;

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-environment", "reload-environment-settings");
        try
        {
            _viewModel.OnPageShown();
            RefreshHotkeyButtons();
            _operations.Complete(
                operationId,
                _viewModel.LastError is null ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ShowError(exception.Message);
        }
    }

    public void OnPageHidden() => CancelHotkeyCapture();

    public void DisposePage()
    {
        CancelHotkeyCapture();
        _viewModel.DisposePage();
        if (_ownsInputMonitor)
        {
            _inputMonitor.Dispose();
        }
    }

    internal void SaveStringForTest(string key, string value)
    {
        if (!_viewModel.SaveString(key, value))
        {
            return;
        }

        if (_hotkeyButtons.TryGetValue(key, out Button? button))
        {
            button.Content = value.ToUpperInvariant();
        }
    }

    private void OnHotkeyCaptureClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button button)
        {
            return;
        }

        CancelHotkeyCapture();
        _captureButton = button;
        _hotkeyActionSuspension = _runtimeCoordinator?.SuspendHotkeyActions();
			button.Content = "请按键";
        button.Focus();
        _inputMonitor.InputPressed += OnGlobalInputPressed;
        if (!_inputMonitor.EnsureStarted())
        {
				ShowError(_inputMonitor.LastError ?? "全局按键监听启动失败。");
            CancelHotkeyCapture();
        }
    }

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs args)
    {
        if (!ReferenceEquals(sender, _captureButton)
            || sender is not Button button
            || button.Tag is not string key)
        {
            return;
        }

        CompleteHotkeyCapture(button, key, args.Key.ToString().ToLowerInvariant());
        args.Handled = true;
    }

    private void OnGlobalInputPressed(object? sender, string value)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_captureButton is Button { Tag: string key } button)
            {
                CompleteHotkeyCapture(button, key, value);
            }
        });
    }

    private void CompleteHotkeyCapture(Button button, string key, string value)
    {
        if (_viewModel.SaveString(key, value))
        {
            button.Content = value.ToUpperInvariant();
        }

        _inputMonitor.InputPressed -= OnGlobalInputPressed;
        _hotkeyActionSuspension?.Dispose();
        _hotkeyActionSuspension = null;
        _captureButton = null;
    }

    private void CancelHotkeyCapture()
    {
        _inputMonitor.InputPressed -= OnGlobalInputPressed;
        _hotkeyActionSuspension?.Dispose();
        _hotkeyActionSuspension = null;
        if (_captureButton?.Tag is string key)
        {
            _captureButton.Content = _viewModel.GetHotkey(key).ToUpperInvariant();
        }

        _captureButton = null;
    }

    private void RefreshHotkeyButtons()
    {
        foreach ((string key, Button button) in _hotkeyButtons)
        {
            button.Content = _viewModel.GetHotkey(key).ToUpperInvariant();
        }
    }

    private async Task ReinitializeContextAsync()
    {
        if (_runtimeCoordinator is null)
        {
            return;
        }

        try
        {
            ZzzBackendResult<bool> result = await _runtimeCoordinator.ReinitializeContextAsync();
            if (!result.Success)
            {
                ShowError(result.Error ?? "脚本环境重新初始化失败。");
            }
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowError(string? message)
    {
        if (message is null)
        {
            _actionBar.IsOpen = false;
            return;
        }

        _actionBar.Title = "脚本环境读取失败";
        _actionBar.Message = message;
        _actionBar.Severity = FAInfoBarSeverity.Error;
        _actionBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"脚本环境页面缺少 {name}。");
}
