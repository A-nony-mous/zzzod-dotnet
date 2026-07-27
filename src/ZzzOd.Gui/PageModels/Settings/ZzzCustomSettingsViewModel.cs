using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Services.Config;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.PageModels.Settings;

internal sealed partial class ZzzCustomSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField UiLanguageField = new("ui_language", typeof(string), "auto");
    private static readonly ZzzConfigField ThemeField = new("theme", typeof(string), "Auto");
    private static readonly ZzzConfigField BackgroundTypeField = new("background_type", typeof(string), "version_poster");
    private static readonly ZzzConfigField CloseWindowActionField = new(
        ZzzCloseWindowActionService.ConfigKey,
        typeof(string),
        "tray");
    private static readonly ZzzConfigField CustomThemeColorField = new("custom_theme_color", typeof(bool), false);
    private static readonly ZzzConfigField CustomBannerField = new("custom_banner", typeof(bool), false);
    private static readonly ZzzConfigField GlobalThemeColorField = new("global_theme_color", typeof(string), "0,120,215");
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        UiLanguageField,
        ThemeField,
        BackgroundTypeField,
        CloseWindowActionField,
        CustomThemeColorField,
        CustomBannerField,
        GlobalThemeColorField,
    ];

    private readonly ZzzGuiOperationTracker _operations;

    public ZzzCustomSettingsViewModel(IZzzAppBackend backend, ZzzGuiOperationTracker? operations = null)
        : base(backend)
    {
        _operations = operations ?? new ZzzGuiOperationTracker();

        LanguageOptions = Options(("跟随系统", "auto"), ("简体中文", "zh"), ("English", "en"));
        ThemeOptions = Options(("跟随系统", "Auto"), ("浅色", "Light"), ("深色", "Dark"));
        BackgroundTypeOptions = Options(
            ("版本海报", "version_poster"),
            ("静态背景", "static_background"),
            ("动态背景", "dynamic_background"),
            ("无", "none"));
        CloseWindowActionOptions = Options(("最小化到托盘", "tray"), ("直接退出", "exit"));
        PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override string ScopeName => "custom";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public new bool IsLoading => base.IsLoading;

    public string? ErrorMessage => LastError;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public IReadOnlyList<ZzzCustomOption> LanguageOptions { get; }

    public IReadOnlyList<ZzzCustomOption> ThemeOptions { get; }

    public IReadOnlyList<ZzzCustomOption> BackgroundTypeOptions { get; }

    public IReadOnlyList<ZzzCustomOption> CloseWindowActionOptions { get; }

    public string SelectedLanguageValue
    {
        get => OptionValue(LanguageOptions, GetValue<string>(UiLanguageField));
        set
        {
            string normalized = OptionValue(LanguageOptions, value);
            if (SetValue(UiLanguageField, normalized) && !IsLoading && LastError is null)
            {
                OnPropertyChanged(nameof(SelectedLanguage));
                OnPropertyChanged(nameof(SelectedLanguageIndex));
                RestartRequested?.Invoke(this, normalized);
            }
        }
    }

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

    public string SelectedThemeValue
    {
        get => OptionValue(ThemeOptions, GetValue<string>(ThemeField));
        set
        {
            string normalized = OptionValue(ThemeOptions, value);
            if (!SetValue(ThemeField, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(SelectedThemeIndex));
            if (IsLoading || LastError is not null || Avalonia.Application.Current is null)
            {
                return;
            }

            Avalonia.Application.Current.RequestedThemeVariant = normalized switch
            {
                "Light" => Avalonia.Styling.ThemeVariant.Light,
                "Dark" => Avalonia.Styling.ThemeVariant.Dark,
                _ => Avalonia.Styling.ThemeVariant.Default,
            };
        }
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

    public string SelectedBackgroundTypeValue
    {
        get => OptionValue(BackgroundTypeOptions, GetValue<string>(BackgroundTypeField));
        set
        {
            if (SetValue(BackgroundTypeField, OptionValue(BackgroundTypeOptions, value)))
            {
                OnPropertyChanged(nameof(SelectedBackgroundType));
                OnPropertyChanged(nameof(SelectedBackgroundTypeIndex));
            }
        }
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

    public string SelectedCloseWindowActionValue
    {
        get => NormalizeCloseWindowAction(GetValue<string>(CloseWindowActionField));
        set
        {
            string normalized = NormalizeCloseWindowAction(value);
            if (SetValue(CloseWindowActionField, normalized))
            {
                OnPropertyChanged(nameof(SelectedCloseWindowAction));
                OnPropertyChanged(nameof(SelectedCloseWindowActionIndex));
            }
        }
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

    public bool CustomThemeColor
    {
        get => GetValue<bool>(CustomThemeColorField);
        set => SetValue(CustomThemeColorField, value);
    }

    public bool CustomBanner
    {
        get => GetValue<bool>(CustomBannerField);
        set => SetValue(CustomBannerField, value);
    }

    public string GlobalThemeColor
    {
        get => GetValue<string>(GlobalThemeColorField);
        set => SetValue(GlobalThemeColorField, value);
    }

    public event EventHandler<string>? RestartRequested;

    public event EventHandler? ThemeColorEditorRequested;

    public event EventHandler? CustomBannerSelectionRequested;

    public override void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-custom", "reload-custom-settings");
        try
        {
            _operations.Complete(operationId, Reload() ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            base.ReportError(exception.Message);
        }
    }

    public bool Reload()
    {
        base.OnPageShown();
        RefreshSelectedOptionBindings();
        return LastError is null;
    }

    internal bool SaveThemeColor(string value)
    {
        if (string.Equals(GlobalThemeColor, value, StringComparison.Ordinal))
        {
            return SaveValue(GlobalThemeColorField, value);
        }

        GlobalThemeColor = value;
        return LastError is null;
    }

    internal bool PersistCustomBanner() => SaveValue(CustomBannerField, CustomBanner);

    internal new void ReportError(string message) => base.ReportError(message);

    [RelayCommand]
    private void RequestThemeColorEditor() => ThemeColorEditorRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestCustomBannerSelection() => CustomBannerSelectionRequested?.Invoke(this, EventArgs.Empty);

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values) => RefreshSelectedOptionBindings();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.Equals(args.PropertyName, nameof(LastError), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(HasError));
        }
    }

    private void RefreshSelectedOptionBindings()
    {
        OnPropertyChanged(nameof(SelectedLanguageValue));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedLanguageIndex));
        OnPropertyChanged(nameof(SelectedThemeValue));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedThemeIndex));
        OnPropertyChanged(nameof(SelectedBackgroundTypeValue));
        OnPropertyChanged(nameof(SelectedBackgroundType));
        OnPropertyChanged(nameof(SelectedBackgroundTypeIndex));
        OnPropertyChanged(nameof(SelectedCloseWindowActionValue));
        OnPropertyChanged(nameof(SelectedCloseWindowAction));
        OnPropertyChanged(nameof(SelectedCloseWindowActionIndex));
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

    private static string NormalizeCloseWindowAction(string value) =>
        ZzzCloseWindowActionService.TryParse(value, out ZzzCloseWindowAction action)
            ? ZzzCloseWindowActionService.ToConfigValue(action)
            : "tray";
}
