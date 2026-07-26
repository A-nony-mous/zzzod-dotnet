using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Settings;

internal sealed partial class ZzzCustomSettingsViewModel : ZzzPageViewModel
{
    private readonly IZzzAppBackend _backend;
    private readonly ZzzGuiOperationTracker _operations;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _selectedLanguageValue = "auto";

    [ObservableProperty]
    private string _selectedThemeValue = "Auto";

    [ObservableProperty]
    private string _selectedShellPresetValue = "frontier";

    [ObservableProperty]
    private string _selectedBackgroundTypeValue = "version_poster";

    [ObservableProperty]
    private string _selectedCloseWindowActionValue = "tray";

    [ObservableProperty]
    private bool _customThemeColor;

    [ObservableProperty]
    private bool _customBanner;

    [ObservableProperty]
    private string _globalThemeColor = "0,120,215";

    public ZzzCustomSettingsViewModel(IZzzAppBackend backend, ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _operations = operations ?? new ZzzGuiOperationTracker();

        LanguageOptions = Options(("跟随系统", "auto"), ("简体中文", "zh"), ("English", "en"));
        ThemeOptions = Options(("跟随系统", "Auto"), ("浅色", "Light"), ("深色", "Dark"));
        ShellPresetOptions = Options(("经典", "classic"), ("前卫", "frontier"));
        BackgroundTypeOptions = Options(
            ("版本海报", "version_poster"),
            ("静态背景", "static_background"),
            ("动态背景", "dynamic_background"),
            ("无", "none"));
        CloseWindowActionOptions = Options(("最小化到托盘", "tray"), ("直接退出", "exit"));
    }

    public IReadOnlyList<ZzzCustomOption> LanguageOptions { get; }

    public IReadOnlyList<ZzzCustomOption> ThemeOptions { get; }

    public IReadOnlyList<ZzzCustomOption> ShellPresetOptions { get; }

    public IReadOnlyList<ZzzCustomOption> BackgroundTypeOptions { get; }

    public IReadOnlyList<ZzzCustomOption> CloseWindowActionOptions { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ZzzCustomOption SelectedLanguage
    {
        get => SelectedOption(LanguageOptions, SelectedLanguageValue);
        set
        {
            if (value is not null)
            {
                SelectedLanguageValue = value.Value;
            }
        }
    }

    public int SelectedLanguageIndex
    {
        get => OptionIndex(LanguageOptions, SelectedLanguageValue);
        set => SetOptionValue(LanguageOptions, value, selectedValue => SelectedLanguageValue = selectedValue);
    }

    public ZzzCustomOption SelectedTheme
    {
        get => SelectedOption(ThemeOptions, SelectedThemeValue);
        set
        {
            if (value is not null)
            {
                SelectedThemeValue = value.Value;
            }
        }
    }

    public int SelectedThemeIndex
    {
        get => OptionIndex(ThemeOptions, SelectedThemeValue);
        set => SetOptionValue(ThemeOptions, value, selectedValue => SelectedThemeValue = selectedValue);
    }

    public ZzzCustomOption SelectedShellPreset
    {
        get => SelectedOption(ShellPresetOptions, SelectedShellPresetValue);
        set
        {
            if (value is not null)
            {
                SelectedShellPresetValue = value.Value;
            }
        }
    }

    public int SelectedShellPresetIndex
    {
        get => OptionIndex(ShellPresetOptions, SelectedShellPresetValue);
        set => SetOptionValue(ShellPresetOptions, value, selectedValue => SelectedShellPresetValue = selectedValue);
    }

    public ZzzCustomOption SelectedBackgroundType
    {
        get => SelectedOption(BackgroundTypeOptions, SelectedBackgroundTypeValue);
        set
        {
            if (value is not null)
            {
                SelectedBackgroundTypeValue = value.Value;
            }
        }
    }

    public int SelectedBackgroundTypeIndex
    {
        get => OptionIndex(BackgroundTypeOptions, SelectedBackgroundTypeValue);
        set => SetOptionValue(BackgroundTypeOptions, value, selectedValue => SelectedBackgroundTypeValue = selectedValue);
    }

    public ZzzCustomOption SelectedCloseWindowAction
    {
        get => SelectedOption(CloseWindowActionOptions, SelectedCloseWindowActionValue);
        set
        {
            if (value is not null)
            {
                SelectedCloseWindowActionValue = value.Value;
            }
        }
    }

    public int SelectedCloseWindowActionIndex
    {
        get => OptionIndex(CloseWindowActionOptions, SelectedCloseWindowActionValue);
        set => SetOptionValue(CloseWindowActionOptions, value, selectedValue => SelectedCloseWindowActionValue = selectedValue);
    }

    public event EventHandler<string>? RestartRequested;

    public event EventHandler? ShellRestartRequested;

    public event EventHandler? ThemeColorEditorRequested;

    public event EventHandler? CustomBannerSelectionRequested;

    public override void OnPageShown()
    {
        base.OnPageShown();
        Guid operationId = _operations.Start("settings-custom", "reload-custom-settings");
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
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("custom");
            if (!result.Success || result.Value is null)
            {
                ErrorMessage = result.Error ?? "自定义设置读取失败。";
                return false;
            }

            IReadOnlyDictionary<string, object?> values = result.Value.Values;
            SelectedLanguageValue = OptionValue(LanguageOptions, ReadString(values, "ui_language", "auto"));
            SelectedThemeValue = OptionValue(ThemeOptions, ReadString(values, "theme", "Auto"));
            string shellPreset = ReadString(values, ZzzGuiShellPresetService.ConfigKey, "frontier");
            SelectedShellPresetValue = ZzzGuiShellPresetService.TryParse(shellPreset, out ZzzGuiShellPreset preset)
                ? ZzzGuiShellPresetService.ToConfigValue(preset)
                : "frontier";
            SelectedBackgroundTypeValue = OptionValue(BackgroundTypeOptions, ReadString(values, "background_type", "version_poster"));
            string closeWindowAction = ReadString(values, ZzzCloseWindowActionService.ConfigKey, "tray");
            SelectedCloseWindowActionValue = ZzzCloseWindowActionService.TryParse(closeWindowAction, out ZzzCloseWindowAction action)
                ? ZzzCloseWindowActionService.ToConfigValue(action)
                : "tray";
            CustomThemeColor = ReadBool(values, "custom_theme_color", false);
            CustomBanner = ReadBool(values, "custom_banner", false);
            GlobalThemeColor = ReadString(values, "global_theme_color", "0,120,215");
            return true;
        }
        finally
        {
            IsLoading = false;
            RefreshSelectedOptionBindings();
        }
    }

    internal bool SaveThemeColor(string value)
    {
        GlobalThemeColor = value;
        return Save("global_theme_color", value);
    }

    internal bool PersistCustomBanner() => Save("custom_banner", CustomBanner);

    internal void ReportError(string message) => ErrorMessage = message;

    [RelayCommand]
    private void RequestThemeColorEditor() => ThemeColorEditorRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestCustomBannerSelection() => CustomBannerSelectionRequested?.Invoke(this, EventArgs.Empty);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnSelectedLanguageValueChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedLanguageIndex));
        if (!IsLoading && Save("ui_language", value))
        {
            RestartRequested?.Invoke(this, value);
        }
    }

    partial void OnSelectedThemeValueChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedThemeIndex));
        if (IsLoading || !Save("theme", value) || Avalonia.Application.Current is null)
        {
            return;
        }

        Avalonia.Application.Current.RequestedThemeVariant = value switch
        {
            "Light" => Avalonia.Styling.ThemeVariant.Light,
            "Dark" => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };
    }

    partial void OnSelectedShellPresetValueChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedShellPreset));
        OnPropertyChanged(nameof(SelectedShellPresetIndex));
        if (!IsLoading
            && ZzzGuiShellPresetService.TryParse(value, out ZzzGuiShellPreset preset)
            && Save(ZzzGuiShellPresetService.ConfigKey, ZzzGuiShellPresetService.ToConfigValue(preset)))
        {
            ShellRestartRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnSelectedBackgroundTypeValueChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedBackgroundType));
        OnPropertyChanged(nameof(SelectedBackgroundTypeIndex));
        if (!IsLoading)
        {
            Save("background_type", value);
        }
    }

    partial void OnSelectedCloseWindowActionValueChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedCloseWindowAction));
        OnPropertyChanged(nameof(SelectedCloseWindowActionIndex));
        if (!IsLoading && ZzzCloseWindowActionService.TryParse(value, out ZzzCloseWindowAction action))
        {
            Save(ZzzCloseWindowActionService.ConfigKey, ZzzCloseWindowActionService.ToConfigValue(action));
        }
    }

    partial void OnCustomThemeColorChanged(bool value)
    {
        if (!IsLoading)
        {
            Save("custom_theme_color", value);
        }
    }

    partial void OnCustomBannerChanged(bool value)
    {
        if (!IsLoading)
        {
            Save("custom_banner", value);
        }
    }

    private void RefreshSelectedOptionBindings()
    {
        OnPropertyChanged(nameof(SelectedLanguageValue));
        OnPropertyChanged(nameof(SelectedThemeValue));
        OnPropertyChanged(nameof(SelectedShellPresetValue));
        OnPropertyChanged(nameof(SelectedBackgroundTypeValue));
        OnPropertyChanged(nameof(SelectedCloseWindowActionValue));
    }

    private bool Save(string key, object? value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "custom",
            new Dictionary<string, object?> { [key] = value }));
        if (result.Success)
        {
            return true;
        }

        ErrorMessage = result.Error ?? "自定义设置保存失败。";
        return false;
    }

    private static IReadOnlyList<ZzzCustomOption> Options(params (string Label, string Value)[] values) =>
        values.Select(value => new ZzzCustomOption(value.Label, value.Value)).ToArray();

    private static string OptionValue(IReadOnlyList<ZzzCustomOption> options, string value) =>
        SelectedOption(options, value).Value;

    private static ZzzCustomOption SelectedOption(IReadOnlyList<ZzzCustomOption> options, string value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
        ?? options[0];

    private static int OptionIndex(IReadOnlyList<ZzzCustomOption> options, string value)
    {
        int index = options.ToList().FindIndex(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : 0;
    }

    private static void SetOptionValue(IReadOnlyList<ZzzCustomOption> options, int index, Action<string> setValue)
    {
        if (index >= 0 && index < options.Count)
        {
            setValue(options[index].Value);
        }
    }

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
}
