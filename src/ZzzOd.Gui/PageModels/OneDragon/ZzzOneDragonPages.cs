using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Architecture;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.OneDragon;

internal sealed record ZzzOneDragonPageModel(
    string Key,
    string Title,
    IReadOnlyList<string> ControlLabels,
    IReadOnlyList<string> AppIds,
    IReadOnlyList<string> ConfigKeys,
    int ItemCount = 0);

internal sealed record ZzzOneDragonAppRowModel(
    string AppId,
    string Name,
    bool Enabled,
    bool NeedNotify,
    bool NotifyVisible,
    bool NotifyEnabled,
    bool SettingVisible,
    bool RunAvailable,
    string? LastRunTime,
    int? RunStatus,
    bool IsMigrated = false)
{
    public string LastRunText => string.IsNullOrWhiteSpace(LastRunTime) ? string.Empty : $"上次运行 {LastRunTime}";

    public string StatusGlyph => RunStatus switch
    {
        ZApplicationRunStatus.Success => "\uE73E",
        ZApplicationRunStatus.Running => "\uE823",
        ZApplicationRunStatus.Fail => "\uE783",
        _ => "\uE7FC",
    };
}

internal sealed class ZzzOneDragonRunSettings : ZzzPageViewModel
{
    private readonly IZzzAppBackend _backend;
    private readonly OneDragonSection _oneDragon;
    private readonly NotifySection _notify;
    private readonly Action<string?>? _errorReporter;
    private IReadOnlyList<ZzzOneDragonAppRowModel> _appRows = [];
    private int? _instanceIndex;
    private string? _lastError;
    private bool _loadingSections;

    public ZzzOneDragonRunSettings(IZzzAppBackend backend, Action<string?>? errorReporter = null)
    {
        _backend = backend;
        _errorReporter = errorReporter;
        _oneDragon = new OneDragonSection(backend, OnSectionError);
        _notify = new NotifySection(backend, OnSectionError);
        _oneDragon.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(OneDragonSection.InstanceRun))
            {
                OnPropertyChanged(nameof(InstanceRun));
            }
            else if (args.PropertyName is nameof(OneDragonSection.AfterDone))
            {
                OnPropertyChanged(nameof(AfterDone));
            }
        };
        _notify.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(NotifySection.EnableNotify))
            {
                OnPropertyChanged(nameof(NotifyEnabled));
                SetAppRows(_appRows
                    .Select(row => row with { NotifyEnabled = row.NotifyVisible && _notify.EnableNotify })
                    .ToArray());
            }
        };
    }

    public IReadOnlyList<ZzzOneDragonAppRowModel> AppRows => _appRows;

    public int? InstanceIndex => _instanceIndex;

    public IReadOnlyList<string> InstanceRunOptions { get; } = ["全部实例", "仅运行当前"];

    public IReadOnlyList<string> AfterDoneOptions { get; } = ["无", "关闭游戏", "关机"];

    public bool NotifyEnabled
    {
        get => _notify.EnableNotify;
        set
        {
            if (_instanceIndex is null)
            {
                ReportError("当前实例不可用。");
                return;
            }

            _notify.EnableNotify = value;
        }
    }

    public string InstanceRun
    {
        get => _oneDragon.InstanceRun;
        set => _oneDragon.InstanceRun = value;
    }

    public string AfterDone
    {
        get => _oneDragon.AfterDone;
        set => _oneDragon.AfterDone = value;
    }

    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public ZzzOneDragonPageModel PageModel => new(
        "one-dragon-run",
        "一条龙 / 一条龙运行",
        ["应用列表", "单项运行", "启用开关", "应用设置", "通知设置", "运行实例", "结束后", "开始/停止", "日志显示"],
        [ZzzApplicationIds.OneDragon, .. _appRows.Select(row => row.AppId)],
        ["instance_run", "after_done", "enable_notify", "applications", "app_list"],
        _appRows.Count);

    public void Reload()
    {
        ReportError(null);
        ZzzBackendResult<ZzzInstanceDto> current = _backend.GetCurrentInstance();
        SetProperty(ref _instanceIndex, current.Success ? current.Value?.Index : null, nameof(InstanceIndex));
        if (!current.Success)
        {
            SetAppRows([]);
            ReportError(current.Error ?? "当前实例读取失败。");
            return;
        }

        _loadingSections = true;
        try
        {
            _oneDragon.OnPageShown();
            if (_instanceIndex is not null)
            {
                _notify.CurrentInstanceIndex = _instanceIndex;
                _notify.OnPageShown();
            }
        }
        finally
        {
            _loadingSections = false;
        }

        ReportError(_oneDragon.LastError ?? _notify.LastError);
        OnPropertyChanged(nameof(InstanceRun));
        OnPropertyChanged(nameof(AfterDone));
        OnPropertyChanged(nameof(NotifyEnabled));
        SetAppRows(LoadAppRows());
    }

    public Task<ZzzBackendResult<ZzzRunStatusDto>> StartSingleAppAsync(string appId) =>
        _backend.StartRunAsync(new ZzzStartRunRequest(appId, _instanceIndex, ZOneDragonAppConstants.DefaultGroupId));

    public void MoveApp(string appId, int direction)
    {
        int index = _appRows.ToList().FindIndex(row => string.Equals(row.AppId, appId, StringComparison.Ordinal));
        MoveAppTo(appId, index + direction);
    }

    public void MoveAppTo(string appId, int targetIndex)
    {
        List<ZzzOneDragonAppRowModel> rows = _appRows.ToList();
        int index = rows.FindIndex(row => string.Equals(row.AppId, appId, StringComparison.Ordinal));
        if (index < 0 || targetIndex < 0 || targetIndex >= rows.Count || index == targetIndex)
        {
            return;
        }

        ZzzOneDragonAppRowModel item = rows[index];
        rows.RemoveAt(index);
        rows.Insert(targetIndex, item);
        SetAppRows(rows);
        SaveAppRows();
    }

    public void SetAppEnabled(string appId, bool enabled)
    {
        SetAppRows(_appRows
            .Select(row => string.Equals(row.AppId, appId, StringComparison.Ordinal) ? row with { Enabled = enabled } : row)
            .ToArray());
        SaveAppRows();
    }

    public void SetNotifyEnabled(bool enabled) => NotifyEnabled = enabled;

    public bool TryGetAppNotifyModes(string appId, out string lifecycle, out string detail)
    {
        lifecycle = NotifyLifecycleModes.StartAndFinish;
        detail = NotifyDetailModes.All;
        ReportError(null);
        if (_instanceIndex is null || !_notify.IsLoaded)
        {
            ReportError(_instanceIndex is null ? "当前实例不可用。" : "通知设置读取失败。");
            return false;
        }

        Dictionary<string, NotifyApplicationSetting> applications = _notify.Applications;
        if (applications.TryGetValue(appId, out NotifyApplicationSetting? setting))
        {
            lifecycle = setting.Lifecycle;
            detail = setting.Detail;
        }

        return true;
    }

    public bool SetAppNotifyModes(string appId, string lifecycle, string detail)
    {
        if (!TryGetAppNotifyModes(appId, out _, out _))
        {
            return false;
        }

        Dictionary<string, NotifyApplicationSetting> applications = _notify.Applications;
        applications[appId] = new NotifyApplicationSetting
        {
            Lifecycle = lifecycle,
            Detail = detail,
        };
        return _notify.SaveApplications(applications);
    }

    public void SetInstanceRun(string value) => InstanceRun = value;

    public void SetAfterDone(string value) => AfterDone = value;

    public void ReloadApps()
    {
        ReportError(null);
        SetAppRows(LoadAppRows());
    }

    public void MoveAppForTest(string appId, int direction) => MoveApp(appId, direction);

    public void MoveAppToForTest(string appId, int targetIndex) => MoveAppTo(appId, targetIndex);

    public void SetAppEnabledForTest(string appId, bool enabled) => SetAppEnabled(appId, enabled);

    public void SetNotifyEnabledForTest(bool enabled) => SetNotifyEnabled(enabled);

    public bool TryGetAppNotifyModesForTest(string appId, out string lifecycle, out string detail) =>
        TryGetAppNotifyModes(appId, out lifecycle, out detail);

    public bool SetAppNotifyModesForTest(string appId, string lifecycle, string detail) =>
        SetAppNotifyModes(appId, lifecycle, detail);

    public void SetInstanceRunForTest(string value) => SetInstanceRun(value);

    public void SetAfterDoneForTest(string value) => SetAfterDone(value);

    private IReadOnlyList<ZzzOneDragonAppRowModel> LoadAppRows()
    {
        ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> result = _backend.GetOneDragonApps(_instanceIndex);
        if (!result.Success || result.Value is null)
        {
            ReportError(result.Error ?? "一条龙应用列表读取失败。");
            return [];
        }

        return result.Value.Select(app => new ZzzOneDragonAppRowModel(
            app.AppId,
            app.Name,
            app.Enabled,
            app.NeedNotify,
            app.NotifyVisible,
            app.NotifyVisible && NotifyEnabled,
            app.SettingVisible,
            app.RunAvailable,
            app.LastRunTime,
            app.RunStatus,
            app.IsMigrated)).ToArray();
    }

    private void SaveAppRows()
    {
        ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> result = _backend.SaveOneDragonApps(new ZzzSaveOneDragonAppsRequest(
            _appRows.Select(row => new ZzzOneDragonAppUpdateDto(row.AppId, row.Enabled)).ToArray(),
            _instanceIndex));
        if (!result.Success || result.Value is null)
        {
            string? error = result.Error;
            Reload();
            ReportError(error ?? "一条龙应用列表保存失败。");
            return;
        }

        SetAppRows(result.Value.Select(app => new ZzzOneDragonAppRowModel(
            app.AppId,
            app.Name,
            app.Enabled,
            app.NeedNotify,
            app.NotifyVisible,
            app.NotifyVisible && NotifyEnabled,
            app.SettingVisible,
            app.RunAvailable,
            app.LastRunTime,
            app.RunStatus,
            app.IsMigrated)).ToArray());
    }

    protected override void DisposePageCore()
    {
        _oneDragon.DisposePage();
        _notify.DisposePage();
    }

    private void SetAppRows(IReadOnlyList<ZzzOneDragonAppRowModel> rows)
    {
        _appRows = rows;
        OnPropertyChanged(nameof(AppRows));
    }

    private void OnSectionError(string? error)
    {
        if (!_loadingSections)
        {
            ReportError(error);
        }
    }

    private void ReportError(string? error)
    {
        LastError = error;
        try
        {
            _errorReporter?.Invoke(error);
        }
        catch
        {
            // 错误展示回调不能破坏运行页配置保存或刷新。
        }
    }

    private sealed class OneDragonSection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField InstanceRunField = new("instance_run", typeof(string), "全部实例");
        private static readonly ZzzConfigField AfterDoneField = new("after_done", typeof(string), "无");
        private static readonly IReadOnlyList<ZzzConfigField> FieldList = [InstanceRunField, AfterDoneField];

        public OneDragonSection(IZzzAppBackend backend, Action<string?> errorReporter)
            : base(backend, errorReporter)
        {
        }

        protected override string ScopeName => "one-dragon";

        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        public string InstanceRun
        {
            get => GetValue<string>(InstanceRunField);
            set => SetValue(InstanceRunField, value);
        }

        public string AfterDone
        {
            get => GetValue<string>(AfterDoneField);
            set => SetValue(AfterDoneField, value);
        }
    }

    private sealed class NotifySection : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField EnableNotifyField = new("enable_notify", typeof(bool), false);
        private static readonly ZzzConfigField ApplicationsField = new(
            "applications",
            typeof(Dictionary<string, NotifyApplicationSetting>),
            new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal),
            FromConfig: ReadApplications);
        private static readonly IReadOnlyList<ZzzConfigField> FieldList = [EnableNotifyField, ApplicationsField];
        private bool _isLoaded;

        public NotifySection(IZzzAppBackend backend, Action<string?> errorReporter)
            : base(backend, errorReporter)
        {
        }

        protected override string ScopeName => "notify";

        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        protected override int? InstanceIndex => CurrentInstanceIndex;

        public int? CurrentInstanceIndex { get; set; }

        public bool IsLoaded => _isLoaded;

        public bool EnableNotify
        {
            get => GetValue<bool>(EnableNotifyField);
            set => SetValue(EnableNotifyField, value);
        }

        public Dictionary<string, NotifyApplicationSetting> Applications =>
            CloneApplications(GetValue<Dictionary<string, NotifyApplicationSetting>>(ApplicationsField));

        public override void OnPageShown()
        {
            _isLoaded = false;
            base.OnPageShown();
        }

        public bool SaveApplications(Dictionary<string, NotifyApplicationSetting> applications)
        {
            SetValue(ApplicationsField, CloneApplications(applications), nameof(Applications));
            return LastError is null;
        }

        protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values) => _isLoaded = true;

        private static object ReadApplications(object? value) => value is Dictionary<string, NotifyApplicationSetting> applications
            ? CloneApplications(applications)
            : new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal);

        private static Dictionary<string, NotifyApplicationSetting> CloneApplications(
            IReadOnlyDictionary<string, NotifyApplicationSetting> applications) =>
            applications.ToDictionary(
                pair => pair.Key,
                pair => new NotifyApplicationSetting
                {
                    Lifecycle = pair.Value.Lifecycle,
                    Detail = pair.Value.Detail,
                },
                StringComparer.Ordinal);
    }
}
