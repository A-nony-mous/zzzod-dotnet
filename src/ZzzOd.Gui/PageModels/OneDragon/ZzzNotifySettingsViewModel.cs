using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.OneDragon;

internal sealed class ZzzNotifySettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField MergeErrorField = new("merge_error_immediate_notify", typeof(bool), false);
    private static readonly ZzzConfigField ApplicationsField = new(
        "applications",
        typeof(Dictionary<string, NotifyApplicationSetting>),
        new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal),
        FromConfig: value => ZzzNotifySettingsReader.ReadApplications(new Dictionary<string, object?> { ["applications"] = value }));
    private static readonly IReadOnlyList<ZzzConfigField> FieldList = [MergeErrorField, ApplicationsField];
    private static readonly IReadOnlyList<ZzzNotifyModeOption> LifecycleOptions =
    [
        new("关闭", NotifyLifecycleModes.Off),
        new("仅结束", NotifyLifecycleModes.FinishOnly),
        new("开始和结束", NotifyLifecycleModes.StartAndFinish),
    ];
    private static readonly IReadOnlyList<ZzzNotifyModeOption> DetailOptions =
    [
        new("关闭", NotifyDetailModes.Off),
        new("仅失败", NotifyDetailModes.ErrorOnly),
        new("逐条", NotifyDetailModes.All),
        new("合并", NotifyDetailModes.Merge),
    ];

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private IReadOnlyList<ZzzNotifyAppRowModel> _rows = [];
    private bool _valuesAvailable;
    private string? _validationError;

    public ZzzNotifySettingsViewModel(IZzzAppBackend backend, int instanceIndex, Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
    }

    protected override string ScopeName => "notify";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    public IReadOnlyList<ZzzNotifyAppRowModel> Rows => _rows;

    public bool ValuesAvailable
    {
        get => _valuesAvailable;
        private set => SetProperty(ref _valuesAvailable, value);
    }

    public bool MergeErrorImmediateNotify
    {
        get => GetValue<bool>(MergeErrorField);
        set => SetValue(MergeErrorField, value);
    }

    public override void OnPageShown()
    {
        ValuesAvailable = false;
        _validationError = null;
        ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> apps = _backend.GetOneDragonApps(_instanceIndex);
        if (!apps.Success || apps.Value is null)
        {
            ReportError(apps.Error ?? "通知应用列表读取失败。");
            return;
        }

        _availableApps = apps.Value;
        base.OnPageShown();
        OnPropertyChanged(nameof(MergeErrorImmediateNotify));
        if (_validationError is not null)
        {
            ReportError(_validationError);
        }
    }

    public bool SaveApplicationMode(ZzzNotifyAppRowModel row)
    {
        if (row.SelectedLifecycle is null || row.SelectedDetail is null)
        {
            return false;
        }

        Dictionary<string, NotifyApplicationSetting> applications = CloneApplications(
            GetValue<Dictionary<string, NotifyApplicationSetting>>(ApplicationsField));
        applications[row.AppId] = new NotifyApplicationSetting
        {
            Lifecycle = row.SelectedLifecycle.Value,
            Detail = row.SelectedDetail.Value,
        };
        return SetValue(ApplicationsField, applications, nameof(Rows)) && LastError is null;
    }

    internal static Dictionary<string, NotifyApplicationSetting> ReadApplications(IReadOnlyDictionary<string, object?> values) =>
        ZzzNotifySettingsReader.ReadApplications(values);

    private IReadOnlyList<ZzzOneDragonAppDto> _availableApps = [];

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        string[] missing = FieldList
            .Where(field => !values.Values.ContainsKey(field.Key))
            .Select(field => $"通知配置缺少 {field.Key}。")
            .ToArray();
        _validationError = missing.Length == 0 ? null : string.Join(Environment.NewLine, missing);
        ValuesAvailable = missing.Length == 0;
        _rows = _availableApps
            .Where(app => app.NotifyVisible)
            .Select(app => CreateRow(app, GetValue<Dictionary<string, NotifyApplicationSetting>>(ApplicationsField)))
            .ToArray();
        OnPropertyChanged(nameof(Rows));
    }

    private static ZzzNotifyAppRowModel CreateRow(
        ZzzOneDragonAppDto app,
        IReadOnlyDictionary<string, NotifyApplicationSetting> applications)
    {
        applications.TryGetValue(app.AppId, out NotifyApplicationSetting? setting);
        string lifecycle = setting?.Lifecycle ?? NotifyLifecycleModes.StartAndFinish;
        string detail = setting?.Detail ?? NotifyDetailModes.All;
        return new ZzzNotifyAppRowModel
        {
            AppId = app.AppId,
            Name = app.Name,
            LifecycleOptions = LifecycleOptions,
            DetailOptions = DetailOptions,
            SelectedLifecycle = LifecycleOptions.FirstOrDefault(option => option.Value == lifecycle)
                ?? LifecycleOptions.First(option => option.Value == NotifyLifecycleModes.StartAndFinish),
            SelectedDetail = DetailOptions.FirstOrDefault(option => option.Value == detail)
                ?? DetailOptions.First(option => option.Value == NotifyDetailModes.All),
        };
    }

    private static Dictionary<string, NotifyApplicationSetting> CloneApplications(
        IReadOnlyDictionary<string, NotifyApplicationSetting> applications) =>
        applications.ToDictionary(
            pair => pair.Key,
            pair => new NotifyApplicationSetting { Lifecycle = pair.Value.Lifecycle, Detail = pair.Value.Detail },
            StringComparer.Ordinal);
}
