using System.Globalization;
using OneDragon.Core.Runtime;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Application.Devtools.OperationDebug;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.Devtools;

internal sealed class ZzzOperationDebugSettingsViewModel : ZzzPageViewModel
{
    private static readonly IReadOnlyList<ZzzOperationDebugOption> ControlMethods =
    [
        new("键鼠", BattleAssistantConfig.ControlMethodKeyboard),
        new("Xbox", BattleAssistantConfig.ControlMethodXbox),
        new("DS4", BattleAssistantConfig.ControlMethodDs4),
    ];

    private readonly IZzzAppBackend _backend;
    private readonly OperationSection _operation;
    private readonly BattleSection _battle;
    private readonly Action<string?>? _errorReporter;
    private IReadOnlyList<string> _operationTemplates = [];
    private int? _activeInstanceIndex;
    private string? _runRoot;
    private string? _lastError;
    private string? _catalogError;
    private bool _loading;
    private bool _valuesAvailable;

    public ZzzOperationDebugSettingsViewModel(
        IZzzAppBackend backend,
        Action<string?>? errorReporter = null)
    {
        _backend = backend;
        _errorReporter = errorReporter;
        _operation = new OperationSection(backend, OnSectionError);
        _battle = new BattleSection(backend, OnSectionError);
        _operation.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _battle.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
    }

    public IReadOnlyList<string> OperationTemplates => _operationTemplates;

    public IReadOnlyList<ZzzOperationDebugOption> ControlMethodOptions => ControlMethods;

    public string OperationTemplate
    {
        get => _operation.OperationTemplate;
        set
        {
            if (_operation.OperationTemplate != value)
            {
                _operation.OperationTemplate = value;
                OnPropertyChanged();
            }
        }
    }

    public bool RepeatEnabled
    {
        get => _operation.RepeatEnabled;
        set => _operation.RepeatEnabled = value;
    }

    public ZzzOperationDebugOption? SelectedControlMethod
    {
        get => ControlMethods.FirstOrDefault(option => string.Equals(option.Value, _battle.ControlMethod, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(value.Value, _battle.ControlMethod, StringComparison.Ordinal))
            {
                _battle.ControlMethod = value.Value;
            }
        }
    }

    public int? ActiveInstanceIndex => _activeInstanceIndex;

    public string? RunRoot => _runRoot;

    public bool ValuesAvailable
    {
        get => _valuesAvailable;
        private set => SetProperty(ref _valuesAvailable, value);
    }

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
        ValuesAvailable = false;
        _catalogError = null;
        try
        {
            ZzzBackendResult<ZzzInstanceDto> instance = _backend.GetCurrentInstance();
            if (!instance.Success || instance.Value is null)
            {
                _activeInstanceIndex = null;
                _runRoot = null;
                ReportError(instance.Error ?? "当前实例不可用。");
                return;
            }

            ZzzBackendResult<ZzzHealthDto> health = _backend.GetHealth();
            if (!health.Success || health.Value is null || string.IsNullOrWhiteSpace(health.Value.RunRoot))
            {
                ReportError(health.Error ?? "运行根目录不可用。");
                return;
            }

            _activeInstanceIndex = instance.Value.Index;
            _runRoot = Path.GetFullPath(health.Value.RunRoot);
            _operation.ActiveInstanceIndex = _activeInstanceIndex;
            _battle.ActiveInstanceIndex = _activeInstanceIndex;
            _loading = true;
            try
            {
                _operation.OnPageShown();
                _battle.OnPageShown();
            }
            finally
            {
                _loading = false;
            }

            _operationTemplates = new OperationTemplateConfigProvider(new OneDragonEnvironment(_runRoot))
                .GetOperationTemplateConfigList()
                .Select(item => Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? item.Label)
                .ToArray();
            OnPropertyChanged(nameof(OperationTemplates));
            OnPropertyChanged(nameof(OperationTemplate));
            OnPropertyChanged(nameof(SelectedControlMethod));
            ValuesAvailable = _operation.ValuesAvailable && _battle.ValuesAvailable;
            ReportError(CombineErrors(_operation.ValidationError, _battle.ValidationError, _catalogError));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _catalogError = exception.Message;
            ReportError(_catalogError);
        }
    }

    public bool SaveOperationTemplate(string value)
    {
        OperationTemplate = value;
        return string.IsNullOrWhiteSpace(LastError);
    }

    public bool SaveRepeat(bool value)
    {
        RepeatEnabled = value;
        return string.IsNullOrWhiteSpace(LastError);
    }

    public bool SaveControlMethod(string value)
    {
        ZzzOperationDebugOption? option = ControlMethods.FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.Ordinal));
        if (option is null)
        {
            ReportError($"未知操作方式：{value}。");
            return false;
        }

        SelectedControlMethod = option;
        return string.IsNullOrWhiteSpace(LastError);
    }

    protected override void DisposePageCore()
    {
        _operation.DisposePage();
        _battle.DisposePage();
    }

    private void OnSectionError(string? error)
    {
        if (!_loading)
        {
            ReportError(CombineErrors(_operation.ValidationError, _battle.ValidationError, error));
        }
    }

    private void ReportError(string? error)
    {
        LastError = error;
        _errorReporter?.Invoke(error);
    }

    private static string? CombineErrors(params string?[] errors)
    {
        string[] messages = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .SelectMany(error => error!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return messages.Length == 0 ? null : string.Join(Environment.NewLine, messages);
    }

    private sealed class OperationSection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField OperationTemplateField = new("operation_template", typeof(string), "安比-3A特殊攻击");
        private static readonly ZzzConfigField RepeatEnabledField = new("repeat_enabled", typeof(bool), true);
        private static readonly IReadOnlyList<ZzzConfigField> FieldList = [OperationTemplateField, RepeatEnabledField];

        public OperationSection(IZzzAppBackend backend, Action<string?> errorReporter) : base(backend, errorReporter)
        {
        }

        public int? ActiveInstanceIndex { get; set; }

        protected override string ScopeName => "operation-debug";

        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        protected override int? InstanceIndex => ActiveInstanceIndex;

        protected override string? GroupId => OperationDebugConstants.DefaultGroupId;

        public string OperationTemplate
        {
            get => GetValue<string>(OperationTemplateField);
            set => SetValue(OperationTemplateField, value);
        }

        public bool RepeatEnabled
        {
            get => GetValue<bool>(RepeatEnabledField);
            set => SetValue(RepeatEnabledField, value);
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
            string[] missing = FieldList.Where(field => !values.Values.ContainsKey(field.Key))
                .Select(field => $"指令调试配置缺少 {field.Key}。")
                .ToArray();
            ValidationError = missing.Length == 0 ? null : string.Join(Environment.NewLine, missing);
            ValuesAvailable = missing.Length == 0;
            OnPropertyChanged(nameof(ValuesAvailable));
            OnPropertyChanged(nameof(ValidationError));
        }
    }

    private sealed class BattleSection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField ControlMethodField = new("control_method", typeof(string), BattleAssistantConfig.ControlMethodKeyboard);
        private static readonly IReadOnlyList<ZzzConfigField> FieldList = [ControlMethodField];

        public BattleSection(IZzzAppBackend backend, Action<string?> errorReporter) : base(backend, errorReporter)
        {
        }

        public int? ActiveInstanceIndex { get; set; }

        protected override string ScopeName => "battle-assistant";

        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        protected override int? InstanceIndex => ActiveInstanceIndex;

        public string ControlMethod
        {
            get => GetValue<string>(ControlMethodField);
            set => SetValue(ControlMethodField, value);
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
            bool present = values.Values.ContainsKey(ControlMethodField.Key);
            string? value = values.Values.GetValueOrDefault(ControlMethodField.Key)?.ToString();
            bool known = value is BattleAssistantConfig.ControlMethodKeyboard
                or BattleAssistantConfig.ControlMethodXbox
                or BattleAssistantConfig.ControlMethodDs4;
            ValidationError = present && known ? null : $"操作方式配置无效：{value ?? "缺少 control_method"}。";
            ValuesAvailable = present && known;
            OnPropertyChanged(nameof(ValuesAvailable));
            OnPropertyChanged(nameof(ValidationError));
        }
    }
}
