using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

namespace ZzzOd.Gui.Pages.Settings;

internal sealed record ZzzGameSettingOption(string Label, string Value, string? Description = null);

internal sealed class ZzzGameActionKeyRow
{
    public required string Label { get; init; }
    public required string Key { get; init; }
    public required IReadOnlyList<ZzzGameSettingOption> ModifierOptions { get; init; }
    public required IReadOnlyList<ZzzGameSettingOption> ButtonOptions { get; init; }
    public ZzzGameSettingOption? SelectedModifier { get; set; }
    public ZzzGameSettingOption? SelectedButton { get; set; }
}

internal sealed class ZzzGamepadKeyRow
{
    public required string Label { get; init; }
    public required string Key { get; init; }
    public required IReadOnlyList<ZzzGameSettingOption> Options { get; init; }
    public ZzzGameSettingOption? SelectedOption { get; set; }
}

internal sealed class ZzzGameKeyCaptureRow : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private bool _capturing;

    public required string Label { get; init; }
    public required string Key { get; init; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    public bool Capturing
    {
        get => _capturing;
        set
        {
            if (_capturing == value)
            {
                return;
            }

            _capturing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    public string DisplayValue => Capturing ? "请按键" : Value.ToUpperInvariant();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed partial class ZzzGameSettingsAxamlPage : UserControl, IZzzPageLifecycle
{
    private const string HelpUrl = "https://one-dragon.com/zzz/zh/setting_game.html";
    private readonly IZzzAppBackend _backend;
    private readonly IZzzManualAutoHdrService _hdrService;
    private readonly IVirtualGamepadDependencyChecker _virtualGamepadDependencyChecker;
    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly bool _ownsInputMonitor;
    private readonly int? _instanceIndex;
    private readonly string? _instanceUnavailableMessage;
    private readonly DispatcherTimer _warningTimer;
    private readonly InfoBar _dependencyWarningBar;
    private readonly SettingsExpanderItem _inputWayItem;
    private readonly FAComboBox _inputWayCombo;
    private readonly ToggleSwitch _backgroundModeToggle;
    private readonly FAComboBox _backgroundGamepadTypeCombo;
    private readonly NumberBox _mouseFlashDurationBox;
    private readonly ItemsControl _backgroundActionList;
    private readonly Button _disableHdrButton;
    private readonly Button _enableHdrButton;
    private readonly ToggleSwitch _launchArgumentToggle;
    private readonly FAComboBox _screenSizeCombo;
    private readonly FAComboBox _fullScreenCombo;
    private readonly ToggleSwitch _popupWindowToggle;
    private readonly FAComboBox _monitorCombo;
    private readonly TextBox _launchArgumentAdvanceTextBox;
    private readonly FAComboBox _controlMethodCombo;
    private readonly ItemsControl _keyboardKeyList;
    private readonly FAComboBox _gamepadDisplayCombo;
    private readonly NumberBox _gamepadKeyPressTimeBox;
    private readonly ItemsControl _gamepadKeyList;
    private IReadOnlyDictionary<string, object?> _values = new Dictionary<string, object?>();
    private string _backgroundGamepadType = "xbox";
    private string _gamepadDisplayType = "xbox";
    private bool _loading;

    public ZzzGameSettingsAxamlPage(
        IZzzAppBackend backend,
        IZzzManualAutoHdrService? hdrService = null,
        IVirtualGamepadDependencyChecker? virtualGamepadDependencyChecker = null,
        ZzzGlobalInputMonitor? inputMonitor = null,
        ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _hdrService = hdrService ?? new ZzzWindowsManualAutoHdrService();
        _virtualGamepadDependencyChecker = virtualGamepadDependencyChecker ?? new ViGEmVirtualGamepadDependencyChecker();
        _inputMonitor = inputMonitor ?? new ZzzGlobalInputMonitor();
        _operations = operations ?? new ZzzGuiOperationTracker();
        _ownsInputMonitor = inputMonitor is null;
        ZzzBackendResult<ZzzInstanceDto> current = backend.GetCurrentInstance();
        _instanceIndex = current.Success && current.Value is not null ? current.Value.Index : null;
        _instanceUnavailableMessage = current.Error;

        AvaloniaXamlLoader.Load(this);
        _dependencyWarningBar = Required<InfoBar>("DependencyWarningBar");
        _inputWayItem = Required<SettingsExpanderItem>("InputWayItem");
        _inputWayCombo = Required<FAComboBox>("InputWayCombo");
        _backgroundModeToggle = Required<ToggleSwitch>("BackgroundModeToggle");
        _backgroundGamepadTypeCombo = Required<FAComboBox>("BackgroundGamepadTypeCombo");
        _mouseFlashDurationBox = Required<NumberBox>("MouseFlashDurationBox");
        _backgroundActionList = Required<ItemsControl>("BackgroundActionList");
        _disableHdrButton = Required<Button>("DisableHdrButton");
        _enableHdrButton = Required<Button>("EnableHdrButton");
        _launchArgumentToggle = Required<ToggleSwitch>("LaunchArgumentToggle");
        _screenSizeCombo = Required<FAComboBox>("ScreenSizeCombo");
        _fullScreenCombo = Required<FAComboBox>("FullScreenCombo");
        _popupWindowToggle = Required<ToggleSwitch>("PopupWindowToggle");
        _monitorCombo = Required<FAComboBox>("MonitorCombo");
        _launchArgumentAdvanceTextBox = Required<TextBox>("LaunchArgumentAdvanceTextBox");
        _controlMethodCombo = Required<FAComboBox>("ControlMethodCombo");
        _keyboardKeyList = Required<ItemsControl>("KeyboardKeyList");
        _gamepadDisplayCombo = Required<FAComboBox>("GamepadDisplayCombo");
        _gamepadKeyPressTimeBox = Required<NumberBox>("GamepadKeyPressTimeBox");
        _gamepadKeyList = Required<ItemsControl>("GamepadKeyList");
        _warningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _warningTimer.Tick += OnWarningTimerTick;
        InitializeOptions();
    }

    internal IReadOnlyList<ZzzGameActionKeyRow> BackgroundActionRows =>
        _backgroundActionList.ItemsSource?.Cast<ZzzGameActionKeyRow>().ToArray() ?? [];

    internal IReadOnlyList<ZzzGameKeyCaptureRow> KeyboardRows =>
        _keyboardKeyList.ItemsSource?.Cast<ZzzGameKeyCaptureRow>().ToArray() ?? [];

    internal IReadOnlyList<ZzzGamepadKeyRow> GamepadRows =>
        _gamepadKeyList.ItemsSource?.Cast<ZzzGamepadKeyRow>().ToArray() ?? [];

    internal string BackgroundGamepadType => _backgroundGamepadType;
    internal string GamepadDisplayType => _gamepadDisplayType;
    internal bool DependencyWarningIsOpen => _dependencyWarningBar.IsOpen;

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-game", "reload-game-settings");
        try
        {
            _operations.Complete(operationId, Reload() ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ShowWarning("游戏设置读取失败", exception.Message, InfoBarSeverity.Error);
        }
    }
    public void OnPageLeave() => StopCapturing();
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
    }

    private void InitializeOptions()
    {
        _inputWayCombo.ItemsSource = InputWayOptions;
        _backgroundGamepadTypeCombo.ItemsSource = GamepadTypeOptions;
        _screenSizeCombo.ItemsSource = ScreenSizeOptions;
        _fullScreenCombo.ItemsSource = FullScreenOptions;
        _monitorCombo.ItemsSource = MonitorOptions;
        _controlMethodCombo.ItemsSource = ControlMethodOptions;
        _gamepadDisplayCombo.ItemsSource = GamepadTypeOptions;
    }

    private bool Reload()
    {
        if (_instanceIndex is null)
        {
            ShowWarning("游戏设置读取失败", "游戏配置不可用。", InfoBarSeverity.Error);
            return false;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("game", _instanceIndex);
        if (!result.Success || result.Value is null)
        {
		_loading = true;
            return false;
        }

        _loading = true;
        _values = result.Value.Values;
        Select(_inputWayCombo, Read("type_input_way", "clipboard"));
        UpdateInputWayDescription();
        _backgroundModeToggle.IsChecked = Read("background_mode", false);
        _backgroundGamepadType = Read("background_gamepad_type", "xbox");
        Select(_backgroundGamepadTypeCombo, _backgroundGamepadType);
        _mouseFlashDurationBox.Value = Read("mouse_flash_duration", 0.05d);
        _launchArgumentToggle.IsChecked = Read("launch_argument", false);
        Select(_screenSizeCombo, Read("screen_size", "1920x1080"));
        Select(_fullScreenCombo, Read("full_screen", "0"));
        _popupWindowToggle.IsChecked = Read("popup_window", false);
        Select(_monitorCombo, Read("monitor", "1"));
        _launchArgumentAdvanceTextBox.Text = Read("launch_argument_advance", string.Empty);
        Select(_controlMethodCombo, Read("control_method", "keyboard"));
        _gamepadDisplayType = "xbox";
        Select(_gamepadDisplayCombo, _gamepadDisplayType);
        RefreshBackgroundActionRows();
        RefreshKeyboardRows();
        RefreshGamepadRows();
        _loading = false;
        return true;
    }

    private void RefreshBackgroundActionRows()
    {
        IReadOnlyList<ZzzGameSettingOption> buttons = OptionsForGamepad(_backgroundGamepadType);
        IReadOnlyList<ZzzGameSettingOption> modifiers = [new("无", string.Empty)];
        _backgroundActionList.ItemsSource = GamepadActions.Select(action =>
        {
            IReadOnlyList<string> value = ReadStringList($"{_backgroundGamepadType}_action_{action.Key}");
            string modifier = value.Count >= 2 ? value[0] : string.Empty;
            string button = value.Count > 0 ? value[^1] : string.Empty;
            return new ZzzGameActionKeyRow
            {
                Label = action.Label,
                Key = action.Key,
                ModifierOptions = modifiers,
                ButtonOptions = buttons,
                SelectedModifier = modifiers.FirstOrDefault(option => option.Value == modifier) ?? modifiers[0],
                SelectedButton = buttons.FirstOrDefault(option => option.Value == button),
            };
        }).ToArray();
    }

    private void RefreshKeyboardRows()
    {
        _keyboardKeyList.ItemsSource = GameKeyActions.Select(action => new ZzzGameKeyCaptureRow
        {
            Label = action.Label,
            Key = action.Key,
            Value = Read($"key_{action.Key}", KeyboardDefaults[action.Key]),
        }).ToArray();
    }

    private void RefreshGamepadRows()
    {
        IReadOnlyList<ZzzGameSettingOption> options = OptionsForGamepad(_gamepadDisplayType);
        _gamepadKeyPressTimeBox.Value = Read($"{_gamepadDisplayType}_key_press_time", 0.02d);
        IReadOnlyDictionary<string, string> defaults = _gamepadDisplayType == "ds4" ? Ds4Defaults : XboxDefaults;
        _gamepadKeyList.ItemsSource = GameKeyActions.Select(action =>
        {
            string value = Read($"{_gamepadDisplayType}_key_{action.Key}", defaults[action.Key]);
            return new ZzzGamepadKeyRow
            {
                Label = action.Label,
                Key = action.Key,
                Options = options,
                SelectedOption = options.FirstOrDefault(option => option.Value == value),
            };
        }).ToArray();
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true });

    private void OnInputWayChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _inputWayCombo.SelectedItem is not ZzzGameSettingOption option)
        {
            return;
        }

        UpdateInputWayDescription();
        Save("type_input_way", option.Value);
    }

    private void UpdateInputWayDescription() =>
        _inputWayItem.Description = (_inputWayCombo.SelectedItem as ZzzGameSettingOption)?.Description;

    private void OnBackgroundModeChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        bool enabled = _backgroundModeToggle.IsChecked == true;
        if (enabled && !_virtualGamepadDependencyChecker.IsAvailable())
        {
            _loading = true;
            _backgroundModeToggle.IsChecked = false;
            _loading = false;
            Save("background_mode", false);
				ShowWarning("后台模式不可用", "未检测到 vgamepad / ViGEmBus，请先安装虚拟手柄驱动");
            return;
        }

        Save("background_mode", enabled);
    }

    private void OnBackgroundGamepadTypeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _backgroundGamepadTypeCombo.SelectedItem is not ZzzGameSettingOption option)
        {
            return;
        }

        _backgroundGamepadType = option.Value;
        Save("background_gamepad_type", option.Value);
        RefreshBackgroundActionRows();
    }

    private void OnMouseFlashDurationChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_loading && !double.IsNaN(args.NewValue))
        {
            Save("mouse_flash_duration", Math.Round(args.NewValue, 2));
        }
    }

    private void OnActionKeyChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || sender is not Control { DataContext: ZzzGameActionKeyRow row } || row.SelectedButton is null)
        {
            return;
        }

        List<string> keys = [];
        if (!string.IsNullOrEmpty(row.SelectedModifier?.Value))
        {
            keys.Add(row.SelectedModifier.Value);
        }

        keys.Add(row.SelectedButton.Value);
        Save($"{_backgroundGamepadType}_action_{row.Key}", keys);
    }

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
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("instance", _instanceIndex);
        return result.Success && result.Value is not null && result.Value.Values.TryGetValue("game_path", out object? value)
            ? ConvertValue(value, string.Empty)
            : string.Empty;
    }

    private void OnLaunchArgumentChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("launch_argument", _launchArgumentToggle.IsChecked == true);
        }
    }

    private void OnScreenSizeChanged(object? sender, SelectionChangedEventArgs args) => SaveSelected(_screenSizeCombo, "screen_size");
    private void OnFullScreenChanged(object? sender, SelectionChangedEventArgs args) => SaveSelected(_fullScreenCombo, "full_screen");
    private void OnMonitorChanged(object? sender, SelectionChangedEventArgs args) => SaveSelected(_monitorCombo, "monitor");

    private void OnPopupWindowChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("popup_window", _popupWindowToggle.IsChecked == true);
        }
    }

    private void OnLaunchArgumentAdvanceLostFocus(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("launch_argument_advance", _launchArgumentAdvanceTextBox.Text ?? string.Empty);
        }
    }

    private void OnControlMethodChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _controlMethodCombo.SelectedItem is not ZzzGameSettingOption option)
        {
            return;
        }

        if (option.Value != "keyboard" && !_virtualGamepadDependencyChecker.IsAvailable())
        {
            _loading = true;
            Select(_controlMethodCombo, "keyboard");
            _loading = false;
            Save("control_method", "keyboard");
				ShowWarning("手柄操控不可用", "未检测到 vgamepad / ViGEmBus，请先安装虚拟手柄驱动");
            return;
        }

        Save("control_method", option.Value);
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
				ShowWarning("按键监听不可用", _inputMonitor.LastError ?? "全局按键监听启动失败。", InfoBarSeverity.Error);
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
        Save($"key_{row.Key}", value);
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

    private void OnGamepadDisplayChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _gamepadDisplayCombo.SelectedItem is not ZzzGameSettingOption option)
        {
            return;
        }

        _gamepadDisplayType = option.Value;
        _loading = true;
        RefreshGamepadRows();
        _loading = false;
    }

    private void OnGamepadKeyPressTimeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_loading && !double.IsNaN(args.NewValue))
        {
            Save($"{_gamepadDisplayType}_key_press_time", Math.Round(args.NewValue, 2));
        }
    }

    private void OnGamepadKeyChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && sender is Control { DataContext: ZzzGamepadKeyRow { SelectedOption: not null } row })
        {
            Save($"{_gamepadDisplayType}_key_{row.Key}", row.SelectedOption!.Value);
        }
    }

    private void SaveSelected(FAComboBox comboBox, string key)
    {
        if (!_loading && comboBox.SelectedItem is ZzzGameSettingOption option)
        {
            Save(key, option.Value);
        }
    }

    private void Save(string key, object? value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "game",
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex));
        if (!result.Success)
        {
            ShowWarning("游戏设置保存失败", result.Error ?? "游戏配置不可写。", InfoBarSeverity.Error);
        }
    }

    private T Read<T>(string key, T baselineDefault) =>
        _values.TryGetValue(key, out object? value) ? ConvertValue(value, baselineDefault) : baselineDefault;

    private IReadOnlyList<string> ReadStringList(string key)
    {
        if (!_values.TryGetValue(key, out object? value) || value is null)
        {
            return DefaultActionKeys(key);
        }

        return value switch
        {
            IReadOnlyList<string> strings => strings,
            IEnumerable<string> strings => strings.ToArray(),
            IEnumerable<object> objects => objects.Select(item => Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).Where(item => item.Length > 0).ToArray(),
            JsonElement { ValueKind: JsonValueKind.Array } json => json.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray(),
            _ => DefaultActionKeys(key),
        };
    }

    private static IReadOnlyList<string> DefaultActionKeys(string key) => key switch
    {
        "xbox_action_menu" => ["xbox_start"],
        "xbox_action_map" => ["xbox_dpad_right"],
        "xbox_action_minimap" => ["xbox_back"],
        "xbox_action_compendium" => ["xbox_lt", "xbox_a"],
        "xbox_action_function_menu" => ["xbox_lt", "xbox_start"],
        "ds4_action_menu" => ["ds4_options"],
        "ds4_action_map" => ["ds4_dpad_right"],
        "ds4_action_minimap" => ["ds4_touchpad"],
        "ds4_action_compendium" => ["ds4_l2", "ds4_cross"],
        "ds4_action_function_menu" => ["ds4_l2", "ds4_options"],
        _ => [],
    };

    private static T ConvertValue<T>(object? value, T defaultValue)
    {
        if (value is T typed)
        {
            return typed;
        }

        if (value is JsonElement json)
        {
            try
            {
                T? parsed = json.Deserialize<T>();
                return parsed is null ? defaultValue : parsed;
            }
            catch (JsonException)
            {
                return defaultValue;
            }
        }

        try
        {
            object? converted = Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            return converted is T convertedValue ? convertedValue : defaultValue;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException)
        {
            return defaultValue;
        }
    }

    private void ShowWarning(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Warning)
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

    private static void Select(FAComboBox comboBox, string value) =>
        comboBox.SelectedItem = comboBox.ItemsSource?.Cast<ZzzGameSettingOption>().FirstOrDefault(option => option.Value == value);

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

    private static IReadOnlyList<ZzzGameSettingOption> OptionsForGamepad(string type) => type == "ds4" ? Ds4Options : XboxOptions;

    private static readonly IReadOnlyList<ZzzGameSettingOption> InputWayOptions =
    [
        new("键盘输入", "input", "需确保使用时没有启用输入法"),
        new("剪贴板", "clipboard", "出现剪切板失败时切换到输入法"),
    ];

    private static readonly IReadOnlyList<ZzzGameSettingOption> GamepadTypeOptions = [new("Xbox", "xbox"), new("DS4", "ds4")];
    private static readonly IReadOnlyList<ZzzGameSettingOption> ControlMethodOptions = [new("键鼠", "keyboard"), .. GamepadTypeOptions];
    private static readonly IReadOnlyList<ZzzGameSettingOption> ScreenSizeOptions = [new("1920x1080", "1920x1080"), new("2560x1440", "2560x1440"), new("3840x2160", "3840x2160")];
    private static readonly IReadOnlyList<ZzzGameSettingOption> FullScreenOptions = [new("窗口化", "0"), new("全屏", "1")];
    private static readonly IReadOnlyList<ZzzGameSettingOption> MonitorOptions = [new("1", "1"), new("2", "2"), new("3", "3"), new("4", "4")];

    private static readonly IReadOnlyList<ZzzGameSettingOption> XboxOptions =
    [
        new("A", "xbox_a"), new("B", "xbox_b"), new("X", "xbox_x"), new("Y", "xbox_y"),
        new("LT", "xbox_lt"), new("RT", "xbox_rt"), new("LB", "xbox_lb"), new("RB", "xbox_rb"),
        new("左摇杆-上", "xbox_ls_up"), new("左摇杆-下", "xbox_ls_down"), new("左摇杆-左", "xbox_ls_left"), new("左摇杆-右", "xbox_ls_right"),
        new("左摇杆-按下", "xbox_l_thumb"), new("右摇杆-按下", "xbox_r_thumb"),
        new("十字键-上", "xbox_dpad_up"), new("十字键-下", "xbox_dpad_down"), new("十字键-左", "xbox_dpad_left"), new("十字键-右", "xbox_dpad_right"),
        new("START", "xbox_start"), new("BACK", "xbox_back"),
        new("右摇杆-上", "xbox_rs_up"), new("右摇杆-下", "xbox_rs_down"), new("右摇杆-左", "xbox_rs_left"), new("右摇杆-右", "xbox_rs_right"),
        new("GUIDE", "xbox_guide"),
    ];

    private static readonly IReadOnlyList<ZzzGameSettingOption> Ds4Options =
    [
        new("✕", "ds4_cross"), new("○", "ds4_circle"), new("□", "ds4_square"), new("△", "ds4_triangle"),
        new("L2", "ds4_l2"), new("R2", "ds4_r2"), new("L1", "ds4_l1"), new("R1", "ds4_r1"),
        new("左摇杆-上", "ds4_ls_up"), new("左摇杆-下", "ds4_ls_down"), new("左摇杆-左", "ds4_ls_left"), new("左摇杆-右", "ds4_ls_right"),
        new("左摇杆-按下", "ds4_l_thumb"), new("右摇杆-按下", "ds4_r_thumb"),
        new("十字键-上", "ds4_dpad_up"), new("十字键-下", "ds4_dpad_down"), new("十字键-左", "ds4_dpad_left"), new("十字键-右", "ds4_dpad_right"),
        new("OPTIONS", "ds4_options"), new("SHARE", "ds4_share"), new("触控板", "ds4_touchpad"),
        new("右摇杆-上", "ds4_rs_up"), new("右摇杆-下", "ds4_rs_down"), new("右摇杆-左", "ds4_rs_left"), new("右摇杆-右", "ds4_rs_right"),
        new("PS", "ds4_ps"),
    ];

    private static readonly (string Label, string Key)[] GamepadActions =
    [
        ("菜单", "menu"), ("地图", "map"), ("小地图", "minimap"), ("快捷手册", "compendium"), ("功能导览", "function_menu"),
    ];

    private static readonly (string Label, string Key)[] GameKeyActions =
    [
        ("交互", "interact"), ("普通攻击", "normal_attack"), ("闪避", "dodge"),
        ("角色切换-下一个", "switch_next"), ("角色切换-上一个", "switch_prev"), ("切换后援", "switch_backup"),
        ("特殊攻击", "special_attack"), ("终结技", "ultimate"), ("连携技-左", "chain_left"), ("连携技-右", "chain_right"),
        ("移动-前", "move_w"), ("移动-后", "move_s"), ("移动-左", "move_a"), ("移动-右", "move_d"),
        ("锁定敌人", "lock"), ("连携技-取消", "chain_cancel"),
    ];

    private static readonly IReadOnlyDictionary<string, string> KeyboardDefaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["interact"] = "f", ["normal_attack"] = "mouse_left", ["dodge"] = "shift", ["switch_next"] = "space",
        ["switch_prev"] = "c", ["switch_backup"] = "r", ["special_attack"] = "e", ["ultimate"] = "q",
        ["chain_left"] = "q", ["chain_right"] = "e", ["move_w"] = "w", ["move_s"] = "s",
        ["move_a"] = "a", ["move_d"] = "d", ["lock"] = "mouse_middle", ["chain_cancel"] = "mouse_middle",
    };

    private static readonly IReadOnlyDictionary<string, string> XboxDefaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["interact"] = "xbox_a", ["normal_attack"] = "xbox_x", ["dodge"] = "xbox_a", ["switch_next"] = "xbox_rb",
        ["switch_prev"] = "xbox_lb", ["switch_backup"] = "xbox_b", ["special_attack"] = "xbox_y", ["ultimate"] = "xbox_rt",
        ["chain_left"] = "xbox_lb", ["chain_right"] = "xbox_rb", ["move_w"] = "xbox_ls_up", ["move_s"] = "xbox_ls_down",
        ["move_a"] = "xbox_ls_left", ["move_d"] = "xbox_ls_right", ["lock"] = "xbox_r_thumb", ["chain_cancel"] = "xbox_a",
    };

    private static readonly IReadOnlyDictionary<string, string> Ds4Defaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["interact"] = "ds4_cross", ["normal_attack"] = "ds4_square", ["dodge"] = "ds4_cross", ["switch_next"] = "ds4_r1",
        ["switch_prev"] = "ds4_l1", ["switch_backup"] = "ds4_circle", ["special_attack"] = "ds4_triangle", ["ultimate"] = "ds4_r2",
        ["chain_left"] = "ds4_l1", ["chain_right"] = "ds4_r1", ["move_w"] = "ds4_ls_up", ["move_s"] = "ds4_ls_down",
        ["move_a"] = "ds4_ls_left", ["move_d"] = "ds4_ls_right", ["lock"] = "ds4_r_thumb", ["chain_cancel"] = "ds4_cross",
    };
}

