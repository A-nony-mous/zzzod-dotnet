using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.GameAssistant;

internal sealed record ZzzBattleControlMethodOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzBattleAssistantSettingsViewModel : ZzzPageViewModel
{
    private readonly IZzzAppBackend _backend;
    private readonly BattleSection _battle;
    private readonly ModelSection _model;
    private readonly Action<string?>? _errorReporter;
    private IReadOnlyList<string> _autoBattleOptions = [];
    private IReadOnlyList<string> _dodgeOptions = [];
    private string? _lastError;
    private string? _catalogError;
    private bool _loadingSections;

    public ZzzBattleAssistantSettingsViewModel(
        IZzzAppBackend backend,
        Action<string?>? errorReporter = null)
    {
        _backend = backend;
        _errorReporter = errorReporter;
        _battle = new BattleSection(backend, OnSectionError);
        _model = new ModelSection(backend, OnSectionError);
        ControlMethodOptions =
        [
            new("键鼠", BattleAssistantConfig.ControlMethodKeyboard),
            new("Xbox", BattleAssistantConfig.ControlMethodXbox),
            new("DS4", BattleAssistantConfig.ControlMethodDs4),
        ];
        _battle.PropertyChanged += (_, args) =>
        {
            OnPropertyChanged(args.PropertyName);
            if (args.PropertyName is nameof(BattleSection.AutoBattleConfig))
            {
                OnPropertyChanged(nameof(SelectedAutoBattleConfig));
                OnPropertyChanged(nameof(CanDeleteAutoBattleConfig));
            }
            else if (args.PropertyName is nameof(BattleSection.DodgeAssistantConfig))
            {
                OnPropertyChanged(nameof(SelectedDodgeConfig));
                OnPropertyChanged(nameof(CanDeleteDodgeConfig));
            }
            else if (args.PropertyName is nameof(BattleSection.ControlMethod))
            {
                OnPropertyChanged(nameof(SelectedControlMethod));
            }
        };
        _model.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
    }

    public IReadOnlyList<string> AutoBattleOptions => _autoBattleOptions;

    public IReadOnlyList<string> DodgeOptions => _dodgeOptions;

    public IReadOnlyList<ZzzBattleControlMethodOption> ControlMethodOptions { get; }

    public string? SelectedAutoBattleConfig
    {
        get => Contains(AutoBattleOptions, _battle.AutoBattleConfig) ? _battle.AutoBattleConfig : null;
        set
        {
            if (value is not null && !string.Equals(value, _battle.AutoBattleConfig, StringComparison.Ordinal))
            {
                _battle.AutoBattleConfig = value;
            }
        }
    }

    public string? SelectedDodgeConfig
    {
        get => Contains(DodgeOptions, _battle.DodgeAssistantConfig) ? _battle.DodgeAssistantConfig : null;
        set
        {
            if (value is not null && !string.Equals(value, _battle.DodgeAssistantConfig, StringComparison.Ordinal))
            {
                _battle.DodgeAssistantConfig = value;
            }
        }
    }

    public bool AutoUltimateEnabled
    {
        get => _battle.AutoUltimateEnabled;
        set => _battle.AutoUltimateEnabled = value;
    }

    public bool UseMergedFile
    {
        get => _battle.UseMergedFile;
        set => _battle.UseMergedFile = value;
    }

    public bool FlashClassifierGpu
    {
        get => _model.FlashClassifierGpu;
        set => _model.FlashClassifierGpu = value;
    }

    public double ScreenshotInterval
    {
        get => _battle.ScreenshotInterval;
        set => _battle.ScreenshotInterval = value;
    }

    public ZzzBattleControlMethodOption? SelectedControlMethod
    {
        get => ControlMethodOptions.FirstOrDefault(option =>
            string.Equals(option.Value, _battle.ControlMethod, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(value.Value, _battle.ControlMethod, StringComparison.Ordinal))
            {
                _battle.ControlMethod = value.Value;
            }
        }
    }

    public bool BattleValuesAvailable => _battle.ValuesAvailable;

    public bool ModelValuesAvailable => _model.ValuesAvailable;

    public bool CanDeleteAutoBattleConfig => SelectedAutoBattleConfig is not null;

    public bool CanDeleteDodgeConfig => SelectedDodgeConfig is not null;

    public bool HasError => !string.IsNullOrWhiteSpace(LastError);

    public string? LastError
    {
        get => _lastError;
        private set
        {
            if (SetProperty(ref _lastError, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        _loadingSections = true;
        try
        {
            _battle.OnPageShown();
            _model.OnPageShown();
        }
        finally
        {
            _loadingSections = false;
        }

        ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> catalog = _backend.GetBattleAssistantConfigCatalog();
        if (catalog.Success && catalog.Value is not null)
        {
            ApplyCatalog(catalog.Value);
            _catalogError = null;
        }
        else
        {
            ApplyCatalog(new ZzzBattleAssistantConfigCatalogDto([], []));
            _catalogError = catalog.Error ?? "配置目录读取失败。";
        }

        RefreshError();
        NotifyAllProperties();
    }

    public void DeleteSelectedAutoBattleConfig() =>
        DeleteSelectedConfig(ZzzBattleAssistantConfigKind.AutoBattle, SelectedAutoBattleConfig);

    public void DeleteSelectedDodgeConfig() =>
        DeleteSelectedConfig(ZzzBattleAssistantConfigKind.Dodge, SelectedDodgeConfig);

    protected override void DisposePageCore()
    {
        _battle.DisposePage();
        _model.DisposePage();
    }

    private void DeleteSelectedConfig(ZzzBattleAssistantConfigKind kind, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> result =
            _backend.DeleteBattleAssistantConfig(new ZzzDeleteBattleAssistantConfigRequest(kind, name));
        if (!result.Success || result.Value is null)
        {
            _catalogError = result.Error ?? "配置删除失败。";
            RefreshError();
            return;
        }

        _catalogError = null;
        ApplyCatalog(result.Value);
        RefreshError();
    }

    private void ApplyCatalog(ZzzBattleAssistantConfigCatalogDto catalog)
    {
        _autoBattleOptions = catalog.AutoBattle.ToArray();
        _dodgeOptions = catalog.Dodge.ToArray();
        OnPropertyChanged(nameof(AutoBattleOptions));
        OnPropertyChanged(nameof(DodgeOptions));
        OnPropertyChanged(nameof(SelectedAutoBattleConfig));
        OnPropertyChanged(nameof(SelectedDodgeConfig));
        OnPropertyChanged(nameof(CanDeleteAutoBattleConfig));
        OnPropertyChanged(nameof(CanDeleteDodgeConfig));
    }

    private void OnSectionError(string? error)
    {
        if (!_loadingSections)
        {
            RefreshError();
        }
    }

    private void RefreshError()
    {
        string[] errors =
        [
            .. Split(_battle.ValidationError ?? _battle.LastError),
            .. Split(_model.ValidationError ?? _model.LastError),
            .. Split(_catalogError),
        ];
        LastError = errors.Length == 0 ? null : string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal));
        _errorReporter?.Invoke(LastError);
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(SelectedAutoBattleConfig));
        OnPropertyChanged(nameof(SelectedDodgeConfig));
        OnPropertyChanged(nameof(AutoUltimateEnabled));
        OnPropertyChanged(nameof(UseMergedFile));
        OnPropertyChanged(nameof(FlashClassifierGpu));
        OnPropertyChanged(nameof(ScreenshotInterval));
        OnPropertyChanged(nameof(SelectedControlMethod));
        OnPropertyChanged(nameof(BattleValuesAvailable));
        OnPropertyChanged(nameof(ModelValuesAvailable));
        OnPropertyChanged(nameof(CanDeleteAutoBattleConfig));
        OnPropertyChanged(nameof(CanDeleteDodgeConfig));
    }

    private static bool Contains(IReadOnlyList<string> values, string? target) =>
        target is not null && values.Contains(target, StringComparer.Ordinal);

    private static IEnumerable<string> Split(string? error) => string.IsNullOrWhiteSpace(error)
        ? []
        : error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed class BattleSection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField AutoBattleConfigField = new("auto_battle_config", typeof(string), "全配队通用");
        private static readonly ZzzConfigField DodgeAssistantConfigField = new("dodge_assistant_config", typeof(string), "闪避");
        private static readonly ZzzConfigField AutoUltimateEnabledField = new("auto_ultimate_enabled", typeof(bool), false);
        private static readonly ZzzConfigField UseMergedFileField = new("use_merged_file", typeof(bool), true);
        private static readonly ZzzConfigField ScreenshotIntervalField = new("screenshot_interval", typeof(double), 0.02d);
        private static readonly ZzzConfigField ControlMethodField = new("control_method", typeof(string), BattleAssistantConfig.ControlMethodKeyboard);
        private static readonly IReadOnlyList<ZzzConfigField> FieldList =
        [
            AutoBattleConfigField,
            DodgeAssistantConfigField,
            AutoUltimateEnabledField,
            UseMergedFileField,
            ScreenshotIntervalField,
            ControlMethodField,
        ];

        public BattleSection(IZzzAppBackend backend, Action<string?> errorReporter) : base(backend, errorReporter)
        {
        }

        protected override string ScopeName => "battle-assistant";

        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        public string AutoBattleConfig { get => GetValue<string>(AutoBattleConfigField); set => SetValue(AutoBattleConfigField, value); }
        public string DodgeAssistantConfig { get => GetValue<string>(DodgeAssistantConfigField); set => SetValue(DodgeAssistantConfigField, value); }
        public bool AutoUltimateEnabled { get => GetValue<bool>(AutoUltimateEnabledField); set => SetValue(AutoUltimateEnabledField, value); }
        public bool UseMergedFile { get => GetValue<bool>(UseMergedFileField); set => SetValue(UseMergedFileField, value); }
        public double ScreenshotInterval { get => GetValue<double>(ScreenshotIntervalField); set => SetValue(ScreenshotIntervalField, value); }
        public string ControlMethod { get => GetValue<string>(ControlMethodField); set => SetValue(ControlMethodField, value); }

        public bool ValuesAvailable { get; private set; }

        public string? ValidationError { get; private set; }

        public override void OnPageShown()
        {
            ValuesAvailable = false;
            ValidationError = null;
            base.OnPageShown();
        }

        protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
        {
            string[] missing = FieldList
                .Where(field => !values.Values.ContainsKey(field.Key))
                .Select(field => $"战斗助手配置缺少 {field.Key}。")
                .ToArray();
            string? controlMethod = values.Values.GetValueOrDefault(ControlMethodField.Key)?.ToString();
            bool knownControlMethod = controlMethod is BattleAssistantConfig.ControlMethodKeyboard
                or BattleAssistantConfig.ControlMethodXbox
                or BattleAssistantConfig.ControlMethodDs4;
            string validationError = string.Join(
                Environment.NewLine,
                missing.Concat(knownControlMethod || controlMethod is null
                    ? []
                    : [$"战斗助手配置包含未知 control_method：{controlMethod}。"]));
            ValidationError = string.IsNullOrWhiteSpace(validationError) ? null : validationError;
            ValuesAvailable = missing.Length == 0;
        }
    }

    private sealed class ModelSection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField FlashClassifierGpuField = new("flash_classifier_gpu", typeof(bool), false);
        private static readonly IReadOnlyList<ZzzConfigField> FieldList = [FlashClassifierGpuField];

        public ModelSection(IZzzAppBackend backend, Action<string?> errorReporter) : base(backend, errorReporter)
        {
        }

        protected override string ScopeName => "model";

        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        public bool FlashClassifierGpu
        {
            get => GetValue<bool>(FlashClassifierGpuField);
            set => SetValue(FlashClassifierGpuField, value);
        }

        public bool ValuesAvailable { get; private set; }

        public string? ValidationError { get; private set; }

        public override void OnPageShown()
        {
            ValuesAvailable = false;
            ValidationError = null;
            base.OnPageShown();
        }

        protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
        {
            ValuesAvailable = values.Values.ContainsKey(FlashClassifierGpuField.Key);
            ValidationError = ValuesAvailable ? null : "模型配置缺少 flash_classifier_gpu。";
        }
    }
}
