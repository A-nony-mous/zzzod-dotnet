using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.Devtools;

internal sealed class ZzzScreenshotHelperSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField FrequencySecondField = new("frequency_second", typeof(double), 0.1d);
    private static readonly ZzzConfigField LengthSecondField = new("length_second", typeof(double), 1d);
    private static readonly ZzzConfigField KeySaveField = new("key_save", typeof(string), "1");
    private static readonly ZzzConfigField DodgeDetectField = new("dodge_detect", typeof(bool), true);
    private static readonly ZzzConfigField ScreenshotBeforeKeyField = new("screenshot_before_key", typeof(bool), true);
    private static readonly ZzzConfigField MiniMapAngleDetectField = new("mini_map_angle_detect", typeof(bool), false);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        FrequencySecondField,
        LengthSecondField,
        KeySaveField,
        DodgeDetectField,
        ScreenshotBeforeKeyField,
        MiniMapAngleDetectField,
    ];

    private readonly IZzzAppBackend _backend;
    private int? _instanceIndex;
    private bool _valuesAvailable;
    private string? _validationError;

    public ZzzScreenshotHelperSettingsViewModel(
        IZzzAppBackend backend,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
    }

    protected override string ScopeName => "screenshot-helper";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => ScreenshotHelperConstants.DefaultGroupId;

    public int? ActiveInstanceIndex => _instanceIndex;

    public bool ValuesAvailable
    {
        get => _valuesAvailable;
        private set => SetProperty(ref _valuesAvailable, value);
    }

    public double FrequencySecond
    {
        get => GetValue<double>(FrequencySecondField);
        set => SetValue(FrequencySecondField, value);
    }

    public double LengthSecond
    {
        get => GetValue<double>(LengthSecondField);
        set => SetValue(LengthSecondField, value);
    }

    public string KeySave
    {
        get => GetValue<string>(KeySaveField);
        set
        {
            if (SetValue(KeySaveField, value))
            {
                OnPropertyChanged(nameof(KeySaveLabel));
            }
        }
    }

    public string KeySaveLabel => KeySave.ToUpperInvariant();

    public bool DodgeDetect
    {
        get => GetValue<bool>(DodgeDetectField);
        set => SetValue(DodgeDetectField, value);
    }

    public bool ScreenshotBeforeKey
    {
        get => GetValue<bool>(ScreenshotBeforeKeyField);
        set => SetValue(ScreenshotBeforeKeyField, value);
    }

    public bool MiniMapAngleDetect
    {
        get => GetValue<bool>(MiniMapAngleDetectField);
        set => SetValue(MiniMapAngleDetectField, value);
    }

    public override void OnPageShown()
    {
        ValuesAvailable = false;
        _validationError = null;
        try
        {
            ZzzBackendResult<ZzzInstanceDto> current = _backend.GetCurrentInstance();
            if (!current.Success || current.Value is null)
            {
                _instanceIndex = null;
                OnPropertyChanged(nameof(ActiveInstanceIndex));
                ReportError(current.Error ?? "当前实例读取失败。");
                return;
            }

            _instanceIndex = current.Value.Index;
            OnPropertyChanged(nameof(ActiveInstanceIndex));
            base.OnPageShown();
            NotifyAllProperties();
            if (_validationError is not null)
            {
                ReportError(_validationError);
            }
        }
        catch (Exception exception)
        {
            _instanceIndex = null;
            OnPropertyChanged(nameof(ActiveInstanceIndex));
            ReportError(exception.Message);
        }
    }

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        string[] missing = FieldList
            .Where(field => !values.Values.ContainsKey(field.Key))
            .Select(field => $"缺少配置 {field.Key}。")
            .ToArray();
        _validationError = missing.Length == 0
            ? null
            : $"截图助手配置读取失败：{string.Join(Environment.NewLine, missing)}";
        ValuesAvailable = missing.Length == 0;
        OnPropertyChanged(nameof(KeySaveLabel));
    }

    protected override void OnFieldSaved(ZzzConfigField field, ZzzConfigScopeValuesDto values)
    {
        ApplyScopeValues(values);
        NotifyAllProperties();
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(FrequencySecond));
        OnPropertyChanged(nameof(LengthSecond));
        OnPropertyChanged(nameof(KeySave));
        OnPropertyChanged(nameof(KeySaveLabel));
        OnPropertyChanged(nameof(DodgeDetect));
        OnPropertyChanged(nameof(ScreenshotBeforeKey));
        OnPropertyChanged(nameof(MiniMapAngleDetect));
        OnPropertyChanged(nameof(ValuesAvailable));
    }
}
