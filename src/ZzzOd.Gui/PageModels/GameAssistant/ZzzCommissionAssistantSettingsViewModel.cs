using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.CommissionAssistant;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.GameAssistant;

internal sealed class ZzzCommissionAssistantSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField PauseInBackgroundField = new("pause_in_background", typeof(bool), true);
    private static readonly ZzzConfigField DialogClickIntervalField = new("dialog_click_interval", typeof(double), 0.5d);
    private static readonly ZzzConfigField StoryModeField = new("story_mode", typeof(string), CommissionAssistantStoryMode.Click.Value);
    private static readonly ZzzConfigField DialogOptionField = new("dialog_option", typeof(string), CommissionAssistantDialogOption.Last.Value);
    private static readonly ZzzConfigField DodgeConfigField = new("dodge_config", typeof(string), "闪避");
    private static readonly ZzzConfigField DodgeSwitchField = new("dodge_switch", typeof(string), "5");
    private static readonly ZzzConfigField AutoBattleField = new("auto_battle", typeof(string), "全配队通用");
    private static readonly ZzzConfigField AutoBattleSwitchField = new("auto_battle_switch", typeof(string), "6");
    private static readonly ZzzConfigField SleepAfterEmptyScreenField = new("sleep_after_empty_screen", typeof(double), 0.5d);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        PauseInBackgroundField,
        DialogClickIntervalField,
        StoryModeField,
        DialogOptionField,
        DodgeConfigField,
        DodgeSwitchField,
        AutoBattleField,
        AutoBattleSwitchField,
        SleepAfterEmptyScreenField,
    ];

    private readonly IZzzAppBackend _backend;
    private readonly Action<string?>? _errorReporter;
    private IReadOnlyList<string> _dodgeOptions = [];
    private IReadOnlyList<string> _autoBattleOptions = [];
    private bool _configValuesAvailable;
    private string? _validationError;
    private string? _catalogError;

    public ZzzCommissionAssistantSettingsViewModel(
        IZzzAppBackend backend,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
        _errorReporter = errorReporter;
        DialogOptions = ["第一个", "最后一个"];
        StoryModes = ["自动点击", "等待剧情自动播放", "跳过剧情"];
    }

    protected override string ScopeName => "commission-assistant";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override string? GroupId => CommissionAssistantConstants.DefaultGroupId;

    public IReadOnlyList<string> DialogOptions { get; }

    public IReadOnlyList<string> StoryModes { get; }

    public IReadOnlyList<string> DodgeOptions => _dodgeOptions;

    public IReadOnlyList<string> AutoBattleOptions => _autoBattleOptions;

    public bool ConfigValuesAvailable
    {
        get => _configValuesAvailable;
        private set => SetProperty(ref _configValuesAvailable, value);
    }

    public bool PauseInBackground
    {
        get => GetValue<bool>(PauseInBackgroundField);
        set => SetValue(PauseInBackgroundField, value);
    }

    public double DialogClickInterval
    {
        get => GetValue<double>(DialogClickIntervalField);
        set => SetValue(DialogClickIntervalField, value);
    }

    public double SleepAfterEmptyScreen
    {
        get => GetValue<double>(SleepAfterEmptyScreenField);
        set => SetValue(SleepAfterEmptyScreenField, value);
    }

    public string StoryMode
    {
        get => GetValue<string>(StoryModeField);
        set => SetValue(StoryModeField, value);
    }

    public string DialogOption
    {
        get => GetValue<string>(DialogOptionField);
        set => SetValue(DialogOptionField, value);
    }

    public string DodgeSwitch
    {
        get => GetValue<string>(DodgeSwitchField);
        set => SetValue(DodgeSwitchField, value);
    }

    public string AutoBattleSwitch
    {
        get => GetValue<string>(AutoBattleSwitchField);
        set => SetValue(AutoBattleSwitchField, value);
    }

    public string? SelectedDodgeConfig
    {
        get => Contains(DodgeOptions, GetValue<string>(DodgeConfigField)) ? GetValue<string>(DodgeConfigField) : null;
        set
        {
            if (value is not null)
            {
                SetValue(DodgeConfigField, value);
            }
        }
    }

    public string? SelectedAutoBattleConfig
    {
        get => Contains(AutoBattleOptions, GetValue<string>(AutoBattleField)) ? GetValue<string>(AutoBattleField) : null;
        set
        {
            if (value is not null)
            {
                SetValue(AutoBattleField, value);
            }
        }
    }

    public override void OnPageShown()
    {
        ConfigValuesAvailable = false;
        _validationError = null;
        base.OnPageShown();

        ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> catalog = _backend.GetBattleAssistantConfigCatalog();
        if (catalog.Success && catalog.Value is not null)
        {
            ApplyCatalog(catalog.Value);
            _catalogError = null;
        }
        else
        {
            ApplyCatalog(new ZzzBattleAssistantConfigCatalogDto([], []));
            _catalogError = catalog.Error ?? "战斗配置目录读取失败。";
        }

        NotifyAllProperties();
        _errorReporter?.Invoke(LastError ?? _validationError ?? _catalogError);
    }

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        List<string> errors = FieldList
            .Where(field => !values.Values.ContainsKey(field.Key))
            .Select(field => $"委托助手配置缺少 {field.Key}。")
            .ToList();
        string storyMode = GetValue<string>(StoryModeField);
        if (!StoryModes.Contains(storyMode, StringComparer.Ordinal))
        {
            errors.Add($"委托助手配置包含未知 story_mode：{storyMode}。");
        }

        string dialogOption = GetValue<string>(DialogOptionField);
        if (!DialogOptions.Contains(dialogOption, StringComparer.Ordinal))
        {
            errors.Add($"委托助手配置包含未知 dialog_option：{dialogOption}。");
        }

        _validationError = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        ConfigValuesAvailable = errors.Count == 0;
    }

    private void ApplyCatalog(ZzzBattleAssistantConfigCatalogDto catalog)
    {
        _dodgeOptions = catalog.Dodge.ToArray();
        _autoBattleOptions = catalog.AutoBattle.ToArray();
        OnPropertyChanged(nameof(DodgeOptions));
        OnPropertyChanged(nameof(AutoBattleOptions));
        OnPropertyChanged(nameof(SelectedDodgeConfig));
        OnPropertyChanged(nameof(SelectedAutoBattleConfig));
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(PauseInBackground));
        OnPropertyChanged(nameof(DialogClickInterval));
        OnPropertyChanged(nameof(SleepAfterEmptyScreen));
        OnPropertyChanged(nameof(StoryMode));
        OnPropertyChanged(nameof(DialogOption));
        OnPropertyChanged(nameof(DodgeSwitch));
        OnPropertyChanged(nameof(AutoBattleSwitch));
        OnPropertyChanged(nameof(SelectedDodgeConfig));
        OnPropertyChanged(nameof(SelectedAutoBattleConfig));
    }

    private static bool Contains(IReadOnlyList<string> values, string? target) =>
        target is not null && values.Contains(target, StringComparer.Ordinal);
}
