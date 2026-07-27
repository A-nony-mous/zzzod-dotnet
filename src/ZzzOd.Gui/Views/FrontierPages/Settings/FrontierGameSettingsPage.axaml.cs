using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.Settings;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed partial class FrontierGameSettingsPage : UserControl, IZzzPageLifecycle
{
    private const string HelpUrl = "https://one-dragon.com/zzz/zh/setting_game.html";
    private readonly IZzzManualAutoHdrService _hdrService;
    private readonly IVirtualGamepadDependencyChecker _virtualGamepadDependencyChecker;
    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly bool _ownsInputMonitor;
    private readonly DispatcherTimer _warningTimer;
    private readonly FAInfoBar _dependencyWarningBar;
    private readonly Button _disableHdrButton;
    private readonly Button _enableHdrButton;

    public FrontierGameSettingsPage(
        IZzzAppBackend backend,
        IZzzManualAutoHdrService? hdrService = null,
        IVirtualGamepadDependencyChecker? virtualGamepadDependencyChecker = null,
        ZzzGlobalInputMonitor? inputMonitor = null,
        ZzzGuiOperationTracker? operations = null)
    {
        _hdrService = hdrService ?? new ZzzWindowsManualAutoHdrService();
        _virtualGamepadDependencyChecker = virtualGamepadDependencyChecker ?? new ViGEmVirtualGamepadDependencyChecker();
        _inputMonitor = inputMonitor ?? new ZzzGlobalInputMonitor();
        _operations = operations ?? new ZzzGuiOperationTracker();
        _ownsInputMonitor = inputMonitor is null;
        AvaloniaXamlLoader.Load(this);
        ZzzGameSettingsViewModel viewModel = new(backend, _virtualGamepadDependencyChecker, _operations);
        viewModel.WarningRequested += (_, warning) => ShowWarning(warning.Title, warning.Message);
        DataContext = viewModel;
        _dependencyWarningBar = Required<FAInfoBar>("DependencyWarningBar");
        _disableHdrButton = Required<Button>("DisableHdrButton");
        _enableHdrButton = Required<Button>("EnableHdrButton");
        _warningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _warningTimer.Tick += OnWarningTimerTick;
    }

    internal IReadOnlyList<ZzzGameActionKeyRow> BackgroundActionRows =>
        (DataContext as ZzzGameSettingsViewModel)?.BackgroundActionRows ?? [];

    internal IReadOnlyList<ZzzGameKeyCaptureRow> KeyboardRows =>
        (DataContext as ZzzGameSettingsViewModel)?.KeyboardRows ?? [];

    internal IReadOnlyList<ZzzGamepadKeyRow> GamepadRows =>
        (DataContext as ZzzGameSettingsViewModel)?.GamepadRows ?? [];

    internal string BackgroundGamepadType => (DataContext as ZzzGameSettingsViewModel)?.SelectedBackgroundGamepadType?.Value ?? "xbox";
    internal string GamepadDisplayType => (DataContext as ZzzGameSettingsViewModel)?.SelectedGamepadDisplay?.Value ?? "xbox";
    internal bool DependencyWarningIsOpen => _dependencyWarningBar.IsOpen;

    public void OnPageShown()
    {
        (DataContext as IZzzPageLifecycle)?.OnPageShown();
    }
    public void OnPageLeave()
    {
        StopCapturing();
        (DataContext as IZzzPageLifecycle)?.OnPageLeave();
    }
    public void OnPageHidden() => StopCapturing();

    public void DisposePage()
    {
        _warningTimer.Stop();
        _warningTimer.Tick -= OnWarningTimerTick;
        StopCapturing();
        if (_ownsInputMonitor)
        {
            _inputMonitor.Dispose();
        }
        (DataContext as IZzzPageLifecycle)?.DisposePage();
    }



    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true });



    private void OnDisableHdrClicked(object? sender, RoutedEventArgs args)
    {
        _disableHdrButton.IsEnabled = false;
        _enableHdrButton.IsEnabled = true;
        _hdrService.SetEnabled(ReadGamePath(), false);
    }

    private void OnEnableHdrClicked(object? sender, RoutedEventArgs args)
    {
        _enableHdrButton.IsEnabled = false;
        _disableHdrButton.IsEnabled = true;
        _hdrService.SetEnabled(ReadGamePath(), true);
    }

    private string ReadGamePath()
    {
        return (DataContext as ZzzGameSettingsViewModel)?.GetGamePath() ?? string.Empty;
    }


    private void OnKeyCaptureClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: ZzzGameKeyCaptureRow row } button)
        {
            return;
        }

        bool start = !row.Capturing;
        StopCapturing();
        row.Capturing = start;
        if (start)
        {
            button.Focus();
            _inputMonitor.InputPressed += OnGlobalInputPressed;
            if (!_inputMonitor.EnsureStarted())
            {
				ShowWarning("按键监听不可用", _inputMonitor.LastError ?? "全局按键监听启动失败。", FAInfoBarSeverity.Error);
                StopCapturing();
            }
        }
    }

    private void OnKeyCaptureKeyDown(object? sender, KeyEventArgs args)
    {
        if (sender is Button { DataContext: ZzzGameKeyCaptureRow { Capturing: true } row })
        {
            CompleteCapture(row, NormalizeKey(args.Key));
            args.Handled = true;
        }
    }

    private void OnKeyCapturePointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Button { DataContext: ZzzGameKeyCaptureRow { Capturing: true } row })
        {
            return;
        }

        PointerPointProperties properties = args.GetCurrentPoint((Control)sender).Properties;
        string? key = properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => "mouse_left",
            PointerUpdateKind.RightButtonPressed => "mouse_right",
            PointerUpdateKind.MiddleButtonPressed => "mouse_middle",
            PointerUpdateKind.XButton1Pressed => "mouse_x1",
            PointerUpdateKind.XButton2Pressed => "mouse_x2",
            _ => null,
        };
        if (key is not null)
        {
            CompleteCapture(row, key);
            args.Handled = true;
        }
    }

    private void CompleteCapture(ZzzGameKeyCaptureRow row, string value)
    {
        row.Value = value;
        row.Capturing = false;
        (DataContext as ZzzGameSettingsViewModel)?.SaveKeyboardKey(row.Key, value);
        _inputMonitor.InputPressed -= OnGlobalInputPressed;
    }

    private void OnGlobalInputPressed(object? sender, string value)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            ZzzGameKeyCaptureRow? row = KeyboardRows.FirstOrDefault(item => item.Capturing);
            if (row is not null)
            {
                CompleteCapture(row, value);
            }
        });
    }

    private void StopCapturing()
    {
        _inputMonitor.InputPressed -= OnGlobalInputPressed;
        foreach (ZzzGameKeyCaptureRow row in KeyboardRows)
        {
            row.Capturing = false;
        }
    }

    private void ShowWarning(string title, string message, FAInfoBarSeverity severity = FAInfoBarSeverity.Warning)
    {
        _dependencyWarningBar.Title = title;
        _dependencyWarningBar.Message = message;
        _dependencyWarningBar.Severity = severity;
        _dependencyWarningBar.IsOpen = true;
        _warningTimer.Stop();
        _warningTimer.Start();
    }

    private void OnWarningTimerTick(object? sender, EventArgs args)
    {
        _warningTimer.Stop();
        _dependencyWarningBar.IsOpen = false;
    }


    private static string NormalizeKey(Key key) => key switch
    {
        Key.LeftShift or Key.RightShift => "shift",
        Key.LeftCtrl => "ctrl_l",
        Key.RightCtrl => "ctrl_r",
        Key.LeftAlt => "alt_l",
        Key.RightAlt => "alt_r",
        Key.Space => "space",
        Key.Return => "enter",
        Key.Escape => "esc",
        Key.Back => "backspace",
        Key.Delete => "delete",
        Key.Tab => "tab",
        Key.Up => "up",
        Key.Down => "down",
        Key.Left => "left",
        Key.Right => "right",
        _ => key.ToString().ToLowerInvariant(),
    };

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"游戏设置页缺少控件: {name}");


}
