using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;

namespace ZzzOd.Gui.Pages.OneDragon;

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
    int? RunStatus)
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

internal sealed class ZzzOneDragonRunSettings
{
    private readonly IZzzAppBackend _backend;
    private OneDragonConfig _settings = new();
    private IReadOnlyList<ZzzOneDragonAppRowModel> _appRows = [];
    private int? _instanceIndex;
    private bool _notifyEnabled;

    public ZzzOneDragonRunSettings(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public IReadOnlyList<ZzzOneDragonAppRowModel> AppRows => _appRows;

    public int? InstanceIndex => _instanceIndex;

    public bool NotifyEnabled => _notifyEnabled;

    public string InstanceRun => _settings.InstanceRun;

    public string AfterDone => _settings.AfterDone;

    public string? LastError { get; private set; }

    public ZzzOneDragonPageModel PageModel => new(
        "one-dragon-run",
        "一条龙 / 一条龙运行",
        ["应用列表", "单项运行", "启用开关", "应用设置", "通知设置", "运行实例", "结束后", "开始/停止", "日志显示"],
        [ZzzApplicationIds.OneDragon, .. _appRows.Select(row => row.AppId)],
        ["instance_run", "after_done", "enable_notify", "applications", "app_list"],
        _appRows.Count);

    public void Reload()
    {
        LastError = null;
        ZzzBackendResult<ZzzInstanceDto> current = _backend.GetCurrentInstance();
        _instanceIndex = current.Success ? current.Value?.Index : null;
        if (!current.Success)
        {
            LastError = current.Error;
            _appRows = [];
            return;
        }

        _settings = LoadSettings();
        _notifyEnabled = LoadNotifyEnabled();
        _appRows = LoadAppRows();
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
        _appRows = rows;
        SaveAppRows();
    }

    public void SetAppEnabled(string appId, bool enabled)
    {
        _appRows = _appRows
            .Select(row => string.Equals(row.AppId, appId, StringComparison.Ordinal) ? row with { Enabled = enabled } : row)
            .ToArray();
        SaveAppRows();
    }

    public void SetNotifyEnabled(bool enabled)
    {
        if (_instanceIndex is null)
        {
            LastError = "当前实例不可用。";
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "notify",
            new Dictionary<string, object?> { ["enable_notify"] = enabled },
            _instanceIndex));
        if (!result.Success)
        {
            string? error = result.Error;
            Reload();
            LastError = error;
            return;
        }

        _notifyEnabled = enabled;
        _appRows = _appRows.Select(row => row with { NotifyEnabled = row.NotifyVisible && enabled }).ToArray();
    }

    public bool TryGetAppNotifyModes(string appId, out string lifecycle, out string detail)
    {
        lifecycle = NotifyLifecycleModes.StartAndFinish;
        detail = NotifyDetailModes.All;
        LastError = null;
        if (_instanceIndex is null)
        {
            LastError = "当前实例不可用。";
            return false;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("notify", _instanceIndex);
        if (!result.Success || result.Value is null)
        {
            LastError = result.Error ?? "通知设置读取失败。";
            return false;
        }

        Dictionary<string, NotifyApplicationSetting> applications = ZzzNotifySettingsPage.ReadApplications(result.Value.Values);
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

        ZzzBackendResult<ZzzConfigScopeValuesDto> current = _backend.GetConfigScope("notify", _instanceIndex);
        if (!current.Success || current.Value is null)
        {
            LastError = current.Error ?? "通知设置读取失败。";
            return false;
        }

        Dictionary<string, NotifyApplicationSetting> applications = ZzzNotifySettingsPage.ReadApplications(current.Value.Values);
        applications[appId] = new NotifyApplicationSetting
        {
            Lifecycle = lifecycle,
            Detail = detail,
        };
        ZzzBackendResult<ZzzConfigScopeValuesDto> saved = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "notify",
            new Dictionary<string, object?> { ["applications"] = applications },
            _instanceIndex));
        LastError = saved.Success ? null : saved.Error ?? "通知设置保存失败。";
        return saved.Success;
    }

    public void SetInstanceRun(string value)
    {
        if (SaveSettings("instance_run", value))
        {
            _settings.InstanceRun = value;
        }
    }

    public void SetAfterDone(string value)
    {
        if (SaveSettings("after_done", value))
        {
            _settings.AfterDone = value;
        }
    }

    public void ReloadApps()
    {
        LastError = null;
        _appRows = LoadAppRows();
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

    private OneDragonConfig LoadSettings()
    {
        OneDragonConfig config = new();
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("one-dragon");
        if (!result.Success || result.Value is null)
        {
            LastError = result.Error;
            return config;
        }

        if (result.Value.Values.TryGetValue("instance_run", out object? instanceRun) && instanceRun is not null)
        {
            config.InstanceRun = instanceRun.ToString()!;
        }

        if (result.Value.Values.TryGetValue("after_done", out object? afterDone) && afterDone is not null)
        {
            config.AfterDone = afterDone.ToString()!;
        }

        return config;
    }

    private bool LoadNotifyEnabled()
    {
        if (_instanceIndex is null)
        {
            return false;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("notify", _instanceIndex);
        if (!result.Success || result.Value is null)
        {
            LastError = result.Error;
            return false;
        }

        return result.Value.Values.TryGetValue("enable_notify", out object? value) && value is bool enabled && enabled;
    }

    private IReadOnlyList<ZzzOneDragonAppRowModel> LoadAppRows()
    {
        ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> result = _backend.GetOneDragonApps(_instanceIndex);
        if (!result.Success || result.Value is null)
        {
            LastError = result.Error;
            return [];
        }

        return result.Value.Select(app => new ZzzOneDragonAppRowModel(
            app.AppId,
            app.Name,
            app.Enabled,
            app.NeedNotify,
            app.NotifyVisible,
            app.NotifyVisible && _notifyEnabled,
            app.SettingVisible,
            app.RunAvailable,
            app.LastRunTime,
            app.RunStatus)).ToArray();
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
            LastError = error;
            return;
        }

        _appRows = result.Value.Select(app => new ZzzOneDragonAppRowModel(
            app.AppId,
            app.Name,
            app.Enabled,
            app.NeedNotify,
            app.NotifyVisible,
            app.NotifyVisible && _notifyEnabled,
            app.SettingVisible,
            app.RunAvailable,
            app.LastRunTime,
            app.RunStatus)).ToArray();
    }

    private bool SaveSettings(string key, object? value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "one-dragon",
            new Dictionary<string, object?> { [key] = value }));
        LastError = result.Success ? null : result.Error;
        return result.Success;
    }
}
