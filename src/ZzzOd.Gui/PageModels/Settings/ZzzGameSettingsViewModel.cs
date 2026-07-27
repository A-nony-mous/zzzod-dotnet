using CommunityToolkit.Mvvm.ComponentModel;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.PageModels.Settings;

internal sealed record ZzzGameSettingOption(string Label, string Value, string? Description = null)
{
    public override string ToString() => Label;
}

internal sealed record ZzzSettingsWarning(string Title, string Message);

internal sealed partial class ZzzGameActionKeyRow : ObservableObject
{
    [ObservableProperty]
    private ZzzGameSettingOption? _selectedModifier;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedButton;

    public required string Label { get; init; }

    public required string Key { get; init; }

    public required IReadOnlyList<ZzzGameSettingOption> ModifierOptions { get; init; }

    public required IReadOnlyList<ZzzGameSettingOption> ButtonOptions { get; init; }

    public Action<ZzzGameActionKeyRow>? Changed { get; init; }

    partial void OnSelectedModifierChanged(ZzzGameSettingOption? value) => Changed?.Invoke(this);

    partial void OnSelectedButtonChanged(ZzzGameSettingOption? value) => Changed?.Invoke(this);
}

internal sealed partial class ZzzGamepadKeyRow : ObservableObject
{
    [ObservableProperty]
    private ZzzGameSettingOption? _selectedOption;

    public required string Label { get; init; }

    public required string Key { get; init; }

    public required IReadOnlyList<ZzzGameSettingOption> Options { get; init; }

    public Action<ZzzGamepadKeyRow>? Changed { get; init; }

    partial void OnSelectedOptionChanged(ZzzGameSettingOption? value) => Changed?.Invoke(this);
}

internal sealed partial class ZzzGameKeyCaptureRow : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    private string _value = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    private bool _capturing;

    public required string Label { get; init; }

    public required string Key { get; init; }

    public string DisplayValue => Capturing ? "请按键" : Value.ToUpperInvariant();
}

internal sealed partial class ZzzGameSettingsViewModel : ZzzPageViewModel
{
    private readonly IZzzAppBackend _backend;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly IVirtualGamepadDependencyChecker _virtualGamepadDependencyChecker;
    private readonly int? _instanceIndex;
    private IReadOnlyDictionary<string, object?> _values = new Dictionary<string, object?>();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedInputWay;

    [ObservableProperty]
    private bool _backgroundMode;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedBackgroundGamepadType;

    [ObservableProperty]
    private double _mouseFlashDuration;

    [ObservableProperty]
    private bool _launchArgument;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedScreenSize;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedFullScreen;

    [ObservableProperty]
    private bool _popupWindow;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedMonitor;

    [ObservableProperty]
    private string _launchArgumentAdvance = string.Empty;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedControlMethod;

    [ObservableProperty]
    private ZzzGameSettingOption? _selectedGamepadDisplay;

    [ObservableProperty]
    private double _gamepadKeyPressTime;

    [ObservableProperty]
    private IReadOnlyList<ZzzGameActionKeyRow> _backgroundActionRows = [];

    [ObservableProperty]
    private IReadOnlyList<ZzzGameKeyCaptureRow> _keyboardRows = [];

    [ObservableProperty]
    private IReadOnlyList<ZzzGamepadKeyRow> _gamepadRows = [];

    public ZzzGameSettingsViewModel(
        IZzzAppBackend backend,
        IVirtualGamepadDependencyChecker virtualGamepadDependencyChecker,
        ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _virtualGamepadDependencyChecker = virtualGamepadDependencyChecker;
        _operations = operations ?? new ZzzGuiOperationTracker();

        ZzzBackendResult<ZzzInstanceDto> current = backend.GetCurrentInstance();
        _instanceIndex = current.Success && current.Value is not null ? current.Value.Index : null;

        InputWayOptions =
        [
            new ZzzGameSettingOption("键盘输入", "input", "需确保使用时没有启用输入法"),
            new ZzzGameSettingOption("剪贴板", "clipboard", "出现剪切板失败时切换到输入法"),
        ];
        BackgroundGamepadTypeOptions = [new ZzzGameSettingOption("Xbox", "xbox"), new ZzzGameSettingOption("DS4", "ds4")];
        ScreenSizeOptions =
        [
            new ZzzGameSettingOption("1920x1080", "1920x1080"),
            new ZzzGameSettingOption("2560x1440", "2560x1440"),
            new ZzzGameSettingOption("3840x2160", "3840x2160"),
        ];
        FullScreenOptions = [new ZzzGameSettingOption("窗口化", "0"), new ZzzGameSettingOption("全屏", "1")];
        MonitorOptions = [new ZzzGameSettingOption("1", "1"), new ZzzGameSettingOption("2", "2"), new ZzzGameSettingOption("3", "3"), new ZzzGameSettingOption("4", "4")];
        ControlMethodOptions = [new ZzzGameSettingOption("键鼠", "keyboard"), .. BackgroundGamepadTypeOptions];
        GamepadDisplayOptions = BackgroundGamepadTypeOptions;
    }

    public IReadOnlyList<ZzzGameSettingOption> InputWayOptions { get; }

    public IReadOnlyList<ZzzGameSettingOption> BackgroundGamepadTypeOptions { get; }

    public IReadOnlyList<ZzzGameSettingOption> ScreenSizeOptions { get; }

    public IReadOnlyList<ZzzGameSettingOption> FullScreenOptions { get; }

    public IReadOnlyList<ZzzGameSettingOption> MonitorOptions { get; }

    public IReadOnlyList<ZzzGameSettingOption> ControlMethodOptions { get; }

    public IReadOnlyList<ZzzGameSettingOption> GamepadDisplayOptions { get; }

    public event EventHandler<ZzzSettingsWarning>? WarningRequested;

    public override void OnPageShown()
    {
        base.OnPageShown();
        Guid operationId = _operations.Start("settings-game", "reload-game-settings");
        try
        {
            _operations.Complete(operationId, Reload() ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ErrorMessage = exception.Message;
        }
    }

    public bool Reload()
    {
        if (_instanceIndex is null)
        {
            ErrorMessage = "游戏配置不可用。";
            return false;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("game", _instanceIndex);
            if (!result.Success || result.Value is null)
            {
                ErrorMessage = result.Error ?? "游戏设置读取失败。";
                return false;
            }

            _values = result.Value.Values;
            SelectedInputWay = SelectOption(InputWayOptions, ReadString(_values, "type_input_way", "clipboard"));
            BackgroundMode = ReadBool(_values, "background_mode", false);
            SelectedBackgroundGamepadType = SelectOption(BackgroundGamepadTypeOptions, ReadString(_values, "background_gamepad_type", "xbox"));
            MouseFlashDuration = ReadDouble(_values, "mouse_flash_duration", 0.05d);
            LaunchArgument = ReadBool(_values, "launch_argument", false);
            SelectedScreenSize = SelectOption(ScreenSizeOptions, ReadString(_values, "screen_size", "1920x1080"));
            SelectedFullScreen = SelectOption(FullScreenOptions, ReadString(_values, "full_screen", "0"));
            PopupWindow = ReadBool(_values, "popup_window", false);
            SelectedMonitor = SelectOption(MonitorOptions, ReadString(_values, "monitor", "1"));
            LaunchArgumentAdvance = ReadString(_values, "launch_argument_advance", string.Empty);
            SelectedControlMethod = SelectOption(ControlMethodOptions, ReadString(_values, "control_method", "keyboard"));
            SelectedGamepadDisplay = GamepadDisplayOptions[0];
            RefreshAllRows();
            return true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal void SaveKeyboardKey(string key, string value) => Save($"key_{key}", value);

    internal string GetGamePath()
    {
        if (_instanceIndex is null)
        {
            return string.Empty;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("instance", _instanceIndex);
        if (result.Success
            && result.Value is not null
            && result.Value.Values.TryGetValue("game_path", out object? value))
        {
            if (value is System.Text.Json.JsonElement json && json.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return json.GetString() ?? string.Empty;
            }

            return value?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    internal void ReportWarning(string title, string message) => WarningRequested?.Invoke(this, new ZzzSettingsWarning(title, message));

    partial void OnSelectedInputWayChanged(ZzzGameSettingOption? value)
    {
        if (!IsLoading && value is not null)
        {
            Save("type_input_way", value.Value);
        }
    }

    partial void OnBackgroundModeChanged(bool value)
    {
        if (IsLoading)
        {
            return;
        }

        if (value && !_virtualGamepadDependencyChecker.IsAvailable())
        {
            IsLoading = true;
            BackgroundMode = false;
            IsLoading = false;
            Save("background_mode", false);
            ReportWarning("后台模式不可用", "未检测到 vgamepad / ViGEmBus，请先安装虚拟手柄驱动");
            return;
        }

        Save("background_mode", value);
    }

    partial void OnSelectedBackgroundGamepadTypeChanged(ZzzGameSettingOption? value)
    {
        if (IsLoading || value is null)
        {
            return;
        }

        Save("background_gamepad_type", value.Value);
        RefreshBackgroundActionRows();
    }

    partial void OnMouseFlashDurationChanged(double value)
    {
        if (!IsLoading && !double.IsNaN(value))
        {
            Save("mouse_flash_duration", Math.Round(value, 2));
        }
    }

    partial void OnLaunchArgumentChanged(bool value)
    {
        if (!IsLoading)
        {
            Save("launch_argument", value);
        }
    }

    partial void OnSelectedScreenSizeChanged(ZzzGameSettingOption? value)
    {
        if (!IsLoading && value is not null)
        {
            Save("screen_size", value.Value);
        }
    }

    partial void OnSelectedFullScreenChanged(ZzzGameSettingOption? value)
    {
        if (!IsLoading && value is not null)
        {
            Save("full_screen", value.Value);
        }
    }

    partial void OnPopupWindowChanged(bool value)
    {
        if (!IsLoading)
        {
            Save("popup_window", value);
        }
    }

    partial void OnSelectedMonitorChanged(ZzzGameSettingOption? value)
    {
        if (!IsLoading && value is not null)
        {
            Save("monitor", value.Value);
        }
    }

    partial void OnLaunchArgumentAdvanceChanged(string value)
    {
        if (!IsLoading)
        {
            Save("launch_argument_advance", value);
        }
    }

    partial void OnSelectedControlMethodChanged(ZzzGameSettingOption? value)
    {
        if (!IsLoading && value is not null)
        {
            Save("control_method", value.Value);
        }
    }

    partial void OnSelectedGamepadDisplayChanged(ZzzGameSettingOption? value)
    {
        if (!IsLoading && value is not null)
        {
            RefreshGamepadRows();
        }
    }

    partial void OnGamepadKeyPressTimeChanged(double value)
    {
        if (!IsLoading && !double.IsNaN(value) && SelectedGamepadDisplay is not null)
        {
            Save($"{SelectedGamepadDisplay.Value}_key_press_time", Math.Round(value, 2));
        }
    }

    private void RefreshAllRows()
    {
        RefreshBackgroundActionRows();
        RefreshKeyboardRows();
        RefreshGamepadRows();
    }

    private void RefreshBackgroundActionRows()
    {
        string gamepadType = SelectedBackgroundGamepadType?.Value ?? "xbox";
        IReadOnlyList<ZzzGameSettingOption> buttons = gamepadType == "ds4" ? Ds4Options : XboxOptions;
        IReadOnlyList<ZzzGameSettingOption> modifiers = [new ZzzGameSettingOption("无", string.Empty)];
        BackgroundActionRows = GamepadActions.Select(action =>
        {
            IReadOnlyList<string> value = ReadStringList(_values, $"{gamepadType}_action_{action.Key}");
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
                Changed = SaveBackgroundAction,
            };
        }).ToArray();
    }

    private void RefreshKeyboardRows()
    {
        KeyboardRows = GameKeyActions.Select(action => new ZzzGameKeyCaptureRow
        {
            Label = action.Label,
            Key = action.Key,
            Value = ReadString(_values, $"key_{action.Key}", KeyboardDefaults[action.Key]),
        }).ToArray();
    }

    private void RefreshGamepadRows()
    {
        string gamepadType = SelectedGamepadDisplay?.Value ?? "xbox";
        IReadOnlyList<ZzzGameSettingOption> options = gamepadType == "ds4" ? Ds4Options : XboxOptions;
        IReadOnlyDictionary<string, string> defaults = gamepadType == "ds4" ? Ds4Defaults : XboxDefaults;
        bool wasLoading = IsLoading;
        IsLoading = true;
        GamepadKeyPressTime = ReadDouble(_values, $"{gamepadType}_key_press_time", 0.02d);
        IsLoading = wasLoading;
        GamepadRows = GameKeyActions.Select(action =>
        {
            string value = ReadString(_values, $"{gamepadType}_key_{action.Key}", defaults[action.Key]);
            return new ZzzGamepadKeyRow
            {
                Label = action.Label,
                Key = action.Key,
                Options = options,
                SelectedOption = options.FirstOrDefault(option => option.Value == value),
                Changed = SaveGamepadKey,
            };
        }).ToArray();
    }

    private void SaveBackgroundAction(ZzzGameActionKeyRow row)
    {
        if (row.SelectedButton is null || SelectedBackgroundGamepadType is null)
        {
            return;
        }

        List<string> keys = [];
        if (!string.IsNullOrWhiteSpace(row.SelectedModifier?.Value))
        {
            keys.Add(row.SelectedModifier.Value);
        }

        keys.Add(row.SelectedButton.Value);
        Save($"{SelectedBackgroundGamepadType.Value}_action_{row.Key}", keys);
    }

    private void SaveGamepadKey(ZzzGamepadKeyRow row)
    {
        if (row.SelectedOption is not null && SelectedGamepadDisplay is not null)
        {
            Save($"{SelectedGamepadDisplay.Value}_key_{row.Key}", row.SelectedOption.Value);
        }
    }

    private void Save(string key, object? value)
    {
        if (_instanceIndex is null)
        {
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "game",
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex));
        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "游戏设置保存失败。";
        }
    }

    private static ZzzGameSettingOption SelectOption(IReadOnlyList<ZzzGameSettingOption> options, string value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)) ?? options[0];

    private static string ReadString(IReadOnlyDictionary<string, object?> values, string key, string defaultValue)
    {
        if (values.TryGetValue(key, out object? value) && value is string typed)
        {
            return typed;
        }

        if (value is System.Text.Json.JsonElement json && json.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return json.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key, bool defaultValue)
    {
        if (values.TryGetValue(key, out object? value) && value is bool typed)
        {
            return typed;
        }

        if (value is System.Text.Json.JsonElement json && (json.ValueKind == System.Text.Json.JsonValueKind.True || json.ValueKind == System.Text.Json.JsonValueKind.False))
        {
            return json.GetBoolean();
        }

        return defaultValue;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, object?> values, string key, double defaultValue)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            return defaultValue;
        }

        return value switch
        {
            double typed => typed,
            float typed => typed,
            int typed => typed,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } json => json.GetDouble(),
            _ => defaultValue,
        };
    }

    private static IReadOnlyList<string> ReadStringList(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value) || value is null)
        {
            return DefaultActionKeys(key);
        }

        return value switch
        {
            IReadOnlyList<string> strings => strings,
            IEnumerable<string> strings => strings.ToArray(),
            IEnumerable<object> objects => objects.Select(item => Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).Where(item => item.Length > 0).ToArray(),
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } json => json.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray(),
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

    internal static readonly IReadOnlyList<ZzzGameSettingOption> XboxOptions =
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

    internal static readonly IReadOnlyList<ZzzGameSettingOption> Ds4Options =
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

    internal static readonly (string Label, string Key)[] GamepadActions =
    [
        ("菜单", "menu"), ("地图", "map"), ("小地图", "minimap"), ("快捷手册", "compendium"), ("功能导览", "function_menu"),
    ];

    internal static readonly (string Label, string Key)[] GameKeyActions =
    [
        ("交互", "interact"), ("普通攻击", "normal_attack"), ("闪避", "dodge"),
        ("角色切换-下一个", "switch_next"), ("角色切换-上一个", "switch_prev"), ("切换后援", "switch_backup"),
        ("特殊攻击", "special_attack"), ("终结技", "ultimate"), ("连携技-左", "chain_left"), ("连携技-右", "chain_right"),
        ("移动-前", "move_w"), ("移动-后", "move_s"), ("移动-左", "move_a"), ("移动-右", "move_d"),
        ("锁定敌人", "lock"), ("连携技-取消", "chain_cancel"),
    ];

    internal static readonly IReadOnlyDictionary<string, string> KeyboardDefaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["interact"] = "f", ["normal_attack"] = "mouse_left", ["dodge"] = "shift", ["switch_next"] = "space",
        ["switch_prev"] = "c", ["switch_backup"] = "r", ["special_attack"] = "e", ["ultimate"] = "q",
        ["chain_left"] = "q", ["chain_right"] = "e", ["move_w"] = "w", ["move_s"] = "s",
        ["move_a"] = "a", ["move_d"] = "d", ["lock"] = "mouse_middle", ["chain_cancel"] = "mouse_middle",
    };

    internal static readonly IReadOnlyDictionary<string, string> XboxDefaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["interact"] = "xbox_a", ["normal_attack"] = "xbox_x", ["dodge"] = "xbox_a", ["switch_next"] = "xbox_rb",
        ["switch_prev"] = "xbox_lb", ["switch_backup"] = "xbox_b", ["special_attack"] = "xbox_y", ["ultimate"] = "xbox_rt",
        ["chain_left"] = "xbox_lb", ["chain_right"] = "xbox_rb", ["move_w"] = "xbox_ls_up", ["move_s"] = "xbox_ls_down",
        ["move_a"] = "xbox_ls_left", ["move_d"] = "xbox_ls_right", ["lock"] = "xbox_r_thumb", ["chain_cancel"] = "xbox_a",
    };

    internal static readonly IReadOnlyDictionary<string, string> Ds4Defaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["interact"] = "ds4_cross", ["normal_attack"] = "ds4_square", ["dodge"] = "ds4_cross", ["switch_next"] = "ds4_r1",
        ["switch_prev"] = "ds4_l1", ["switch_backup"] = "ds4_circle", ["special_attack"] = "ds4_triangle", ["ultimate"] = "ds4_r2",
        ["chain_left"] = "ds4_l1", ["chain_right"] = "ds4_r1", ["move_w"] = "ds4_ls_up", ["move_s"] = "ds4_ls_down",
        ["move_a"] = "ds4_ls_left", ["move_d"] = "ds4_ls_right", ["lock"] = "ds4_r_thumb", ["chain_cancel"] = "ds4_cross",
    };
}
