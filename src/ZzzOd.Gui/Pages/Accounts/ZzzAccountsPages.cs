using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Accounts;

internal sealed record ZzzAccountOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed record ZzzAccountRunOption(string Label, bool Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzAccountInstanceRow : INotifyPropertyChanged
{
    private string _name;
    private ZzzAccountRunOption _selectedRunOption;
    private string _persistedName;
    private bool _persistedActiveInOneDragon;

    public ZzzAccountInstanceRow(
        ZzzInstanceDto instance,
        bool canSwitch,
        int instanceCount,
        IReadOnlyList<ZzzAccountRunOption> runOptions)
    {
        Index = instance.Index;
        _name = instance.Name;
        _persistedName = instance.Name;
        IsActive = instance.Active;
        CanEdit = canSwitch;
        CanActivate = canSwitch && !instance.Active;
        CanDelete = canSwitch && instanceCount > 1;
        RunOptions = runOptions;
        _selectedRunOption = runOptions.First(option => option.Value == instance.ActiveInOneDragon);
        _persistedActiveInOneDragon = instance.ActiveInOneDragon;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get; }

    public string DisplayIndex => $"{Index:00}" + (IsActive ? " 当前" : string.Empty);

    public bool IsActive { get; }

    public bool CanEdit { get; }

    public bool CanActivate { get; }

    public bool CanDelete { get; }

    public IReadOnlyList<ZzzAccountRunOption> RunOptions { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public ZzzAccountRunOption SelectedRunOption
    {
        get => _selectedRunOption;
        set => SetField(ref _selectedRunOption, value);
    }

    public bool HasPendingNameChange => !string.Equals(_name, _persistedName, StringComparison.Ordinal);

    public bool HasPendingRunOptionChange => _selectedRunOption.Value != _persistedActiveInOneDragon;

    public void SynchronizePersistedValues(ZzzInstanceDto instance)
    {
        _persistedName = instance.Name;
        _persistedActiveInOneDragon = instance.ActiveInOneDragon;
        Name = instance.Name;
        SelectedRunOption = RunOptions.First(option => option.Value == instance.ActiveInOneDragon);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed record ZzzAccountPageModel(
    string Key,
    string Title,
    IReadOnlyList<string> Controls,
    IReadOnlyList<string> ConfigKeys,
    int ItemCount,
    int ActiveInstanceIndex,
    bool CanSwitch,
    string? BlockedReason);

internal sealed class ZzzInstanceManagementPage
{
    private readonly IZzzAppBackend _backend;
    private IReadOnlyList<ZzzInstanceDto> _instances = [];

    public ZzzInstanceManagementPage(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public IReadOnlyList<ZzzInstanceDto> Instances => _instances;

    public bool CanSwitch { get; private set; } = true;

    public string? BlockedReason { get; private set; }

    public ZzzAccountPageModel PageModel => new(
        "accounts",
        "账户管理",
        ["使用说明", "当前账户设置", "账户列表", "新增", "启用", "登录", "删除", "实例名称", "一条龙中运行"],
        ["instance_list", "idx", "name", "active", "active_in_od"],
        _instances.Count,
        _instances.FirstOrDefault(instance => instance.Active)?.Index ?? 0,
        CanSwitch,
        BlockedReason);

    public void OnPageShown() => Reload();

    public void Reload()
    {
        ZzzRunStatusDto run = _backend.GetCurrentRun().Value ?? new ZzzRunStatusDto(ZzzRunState.Idle);
        CanSwitch = run.State is not (ZzzRunState.Starting or ZzzRunState.Running or ZzzRunState.Paused or ZzzRunState.Stopping);
        BlockedReason = CanSwitch ? null : $"当前状态为 {run.State}。停止运行后再切换账户。";
        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = _backend.GetInstances();
        IReadOnlyList<ZzzInstanceDto> backendInstances = result.Success && result.Value is not null ? result.Value : [];
        ZzzBackendResult<ZzzConfigScopeValuesDto> configured = _backend.GetConfigScope("one-dragon");
        if (configured.Success
            && configured.Value?.Values.TryGetValue("instance_list", out object? rawList) == true
            && rawList is List<OneDragonInstanceConfigItem> configuredInstances)
        {
            HashSet<int> configuredIndexes = configuredInstances.Select(item => item.Idx).ToHashSet();
            _instances = backendInstances.Where(instance => configuredIndexes.Contains(instance.Index)).ToArray();
        }
        else
        {
            _instances = backendInstances;
        }
    }

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> AddInstanceForTest() => AddInstance();

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> ActivateInstanceForTest(int index) => ActivateInstance(index);

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstanceForTest(int index, string? name = null, bool? activeInOneDragon = null) =>
        UpdateInstance(index, name, activeInOneDragon);

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> DeleteInstanceForTest(int index) => DeleteInstance(index);

    public ZzzBackendResult<ZzzRunStatusDto> LoginInstanceForTest(int index) => LoginInstance(index);

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> AddInstance()
    {
        if (!CanSwitch)
        {
            return Blocked<IReadOnlyList<ZzzInstanceDto>>();
        }

        if (_instances.Count >= 5)
        {
            return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(
                ZzzBackendErrorCode.Conflict,
                "添加超过5个账号需要完成密码验证。");
        }

        return Apply(_backend.CreateInstance());
    }

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> AddProtectedInstance(string password)
    {
        if (!CanSwitch)
        {
            return Blocked<IReadOnlyList<ZzzInstanceDto>>();
        }

        if (!VerifyProtectionPassword(password))
        {
            return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Validation, "密码错误");
        }

        return Apply(_backend.CreateInstance());
    }

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> ActivateInstance(int index) =>
        CanSwitch ? Apply(_backend.ActivateInstance(index)) : Blocked<IReadOnlyList<ZzzInstanceDto>>();

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(int index, string? name, bool? activeInOneDragon, bool? forceLoginBeforeRun = null) =>
        CanSwitch ? Apply(_backend.UpdateInstance(new ZzzUpdateInstanceRequest(index, name, activeInOneDragon, forceLoginBeforeRun))) : Blocked<IReadOnlyList<ZzzInstanceDto>>();

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> DeleteInstance(int index) =>
        CanSwitch ? Apply(_backend.DeleteInstance(index)) : Blocked<IReadOnlyList<ZzzInstanceDto>>();

    public ZzzBackendResult<ZzzRunStatusDto> LoginInstance(int index) =>
        CanSwitch ? _backend.LoginInstance(index) : Blocked<ZzzRunStatusDto>();

    private static bool VerifyProtectionPassword(string password)
    {
        byte[] provided = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(Convert.FromBase64String("QlYxcTJKUXpyRW1B"))));
        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    private ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> Apply(ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result)
    {
        if (result.Success && result.Value is not null)
        {
            Reload();
        }

        return result;
    }

    private ZzzBackendResult<T> Blocked<T>() =>
        ZzzBackendResult<T>.Fail(ZzzBackendErrorCode.Conflict, BlockedReason ?? "运行中不能切换实例。");
}

internal sealed class ZzzCurrentAccountSettingsPage
{
    private readonly IZzzAppBackend _backend;
    private IReadOnlyDictionary<string, object?> _values = new Dictionary<string, object?>(StringComparer.Ordinal);

    public ZzzCurrentAccountSettingsPage(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public int ActiveInstanceIndex { get; private set; }

    public string GameRegion { get; private set; } = string.Empty;

    public bool AccountPasswordVisible => !string.Equals(GameRegion, "cn_b", StringComparison.Ordinal);

    public bool BilibiliVisible => string.Equals(GameRegion, "cn_b", StringComparison.Ordinal);

    public ZzzAccountPageModel PageModel => new(
        "accounts",
        "当前账户设置",
        ["游戏路径", "自定义窗口标题", "游戏区服", "账号", "密码", "B服使用提示", "B服用户名"],
        ["game_path", "use_custom_win_title", "custom_win_title", "game_region", "account", "password", "bilibili_account_name"],
        7,
        ActiveInstanceIndex,
        true,
        null);

    public void OnPageShown() => Reload();

    public void Reload()
    {
        ActiveInstanceIndex = _backend.GetCurrentInstance().Value?.Index
            ?? _backend.GetInstances().Value?.FirstOrDefault(instance => instance.Active)?.Index
            ?? 0;
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("instance", ActiveInstanceIndex);
        _values = result.Success && result.Value is not null
            ? new Dictionary<string, object?>(result.Value.Values, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        GameRegion = ReadString("game_region");
    }

    public void SaveStringForTest(string key, string value) => Save(key, value);

    public void SaveBoolForTest(string key, bool value) => Save(key, value);

    public void SetGameRegionForTest(string value)
    {
        Save("game_region", value);
        GameRegion = value;
    }

    public string ReadStringForTest(string key) => ReadString(key);

    public bool ReadBoolForTest(string key) => ReadBool(key);

    public string ReadString(string key)
    {
        return _values.TryGetValue(key, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    public bool ReadBool(string key)
    {
        return _values.TryGetValue(key, out object? value)
            && value is bool boolean
            && boolean;
    }

    public ZzzBackendResult<ZzzConfigScopeValuesDto> Save(string key, object? value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "instance",
            new Dictionary<string, object?> { [key] = value },
            ActiveInstanceIndex));
        if (result.Success)
        {
            _values = result.Value is not null
                ? new Dictionary<string, object?>(result.Value.Values, StringComparer.Ordinal)
                : new Dictionary<string, object?>(_values, StringComparer.Ordinal) { [key] = value };
            if (key == "game_region")
            {
                GameRegion = value?.ToString() ?? string.Empty;
            }
        }

        return result;
    }
}
