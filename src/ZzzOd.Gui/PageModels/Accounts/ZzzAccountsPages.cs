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
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Services.Config;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.PageModels.Accounts;

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

internal sealed class ZzzInstanceManagementPage : ObservableObject
{
    private readonly IZzzAppBackend _backend;
    private readonly Action<string?>? _errorReporter;
    private IReadOnlyList<ZzzInstanceDto> _instances = [];
    private bool _canSwitch = true;
    private string? _blockedReason;
    private bool _forceLoginBeforeRun;

    public ZzzInstanceManagementPage(IZzzAppBackend backend, Action<string?>? errorReporter = null)
    {
        _backend = backend;
        _errorReporter = errorReporter;
    }

    public IReadOnlyList<ZzzInstanceDto> Instances => _instances;

    public ObservableCollection<ZzzAccountInstanceRow> Rows { get; } = [];

    public bool CanSwitch
    {
        get => _canSwitch;
        private set => SetProperty(ref _canSwitch, value);
    }

    public bool CanAdd => CanSwitch;

    public string? BlockedReason
    {
        get => _blockedReason;
        private set => SetProperty(ref _blockedReason, value);
    }

    public bool ForceLoginBeforeRun
    {
        get => _forceLoginBeforeRun;
        private set => SetProperty(ref _forceLoginBeforeRun, value);
    }

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

    public bool Reload()
    {
        ZzzBackendResult<ZzzRunStatusDto> runResult = _backend.GetCurrentRun();
        if (!runResult.Success)
        {
            _errorReporter?.Invoke(runResult.Error ?? "运行状态读取失败。");
            return false;
        }

        ZzzRunStatusDto run = runResult.Value ?? new ZzzRunStatusDto(ZzzRunState.Idle);
        CanSwitch = run.State is not (ZzzRunState.Starting or ZzzRunState.Running or ZzzRunState.Paused or ZzzRunState.Stopping);
        BlockedReason = CanSwitch ? null : $"当前状态为 {run.State}。停止运行后再切换账户。";
        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = _backend.GetInstances();
        if (!result.Success || result.Value is null)
        {
            _errorReporter?.Invoke(result.Error ?? "账户列表读取失败。");
            return false;
        }

        IReadOnlyList<ZzzInstanceDto> backendInstances = result.Value;
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

        Rows.Clear();
        foreach (ZzzInstanceDto instance in _instances)
        {
            Rows.Add(new ZzzAccountInstanceRow(instance, CanSwitch, _instances.Count, AccountRunOptions));
        }

        ForceLoginBeforeRun = _instances.FirstOrDefault(instance => instance.Active)?.ForceLoginBeforeRun ?? false;
        OnPropertyChanged(nameof(Instances));
        OnPropertyChanged(nameof(CanAdd));
        _errorReporter?.Invoke(null);
        return true;
    }

    private static readonly IReadOnlyList<ZzzAccountRunOption> AccountRunOptions =
    [
        new("一条龙中运行", true),
        new("一条龙中不运行", false),
    ];

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

internal sealed class ZzzCurrentAccountSettingsPage : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField GamePathField = new("game_path", typeof(string), string.Empty);
    private static readonly ZzzConfigField UseCustomWindowTitleField = new("use_custom_win_title", typeof(bool), false);
    private static readonly ZzzConfigField CustomWindowTitleField = new("custom_win_title", typeof(string), string.Empty);
    private static readonly ZzzConfigField GameRegionField = new("game_region", typeof(string), string.Empty);
    private static readonly ZzzConfigField AccountField = new("account", typeof(string), string.Empty);
    private static readonly ZzzConfigField PasswordField = new("password", typeof(string), string.Empty);
    private static readonly ZzzConfigField BilibiliAccountNameField = new("bilibili_account_name", typeof(string), string.Empty);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        GamePathField,
        UseCustomWindowTitleField,
        CustomWindowTitleField,
        GameRegionField,
        AccountField,
        PasswordField,
        BilibiliAccountNameField,
    ];

    private readonly IZzzAppBackend _backend;

    public ZzzCurrentAccountSettingsPage(
        IZzzAppBackend backend,
        ZzzInstanceManagementPage? instanceManagement = null,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
        InstanceManagement = instanceManagement ?? new ZzzInstanceManagementPage(backend, errorReporter);
    }

    protected override string ScopeName => "instance";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => ActiveInstanceIndex;

    public ZzzInstanceManagementPage InstanceManagement { get; }

    public IReadOnlyList<ZzzAccountOption> RegionOptions { get; } =
    [
        new("国服", "cn"),
        new("B服", "cn_b"),
        new("美服", "us"),
        new("欧服", "eu"),
        new("亚服", "asia"),
        new("港澳台服", "twhkmo"),
    ];

    public int ActiveInstanceIndex { get; private set; }

    public string GamePath
    {
        get => GetValue<string>(GamePathField);
        set => SetValue(GamePathField, value);
    }

    public bool UseCustomWindowTitle
    {
        get => GetValue<bool>(UseCustomWindowTitleField);
        set => SetValue(UseCustomWindowTitleField, value);
    }

    public string CustomWindowTitle
    {
        get => GetValue<string>(CustomWindowTitleField);
        set => SetValue(CustomWindowTitleField, value);
    }

    public string GameRegion
    {
        get => GetValue<string>(GameRegionField);
        set
        {
            if (SetValue(GameRegionField, value))
            {
                OnPropertyChanged(nameof(SelectedGameRegion));
                OnPropertyChanged(nameof(AccountPasswordVisible));
                OnPropertyChanged(nameof(BilibiliVisible));
            }
        }
    }

    public ZzzAccountOption? SelectedGameRegion
    {
        get => RegionOptions.FirstOrDefault(option => string.Equals(option.Value, GameRegion, StringComparison.Ordinal));
        set
        {
            if (value is not null)
            {
                GameRegion = value.Value;
            }
        }
    }

    public string Account
    {
        get => GetValue<string>(AccountField);
        set => SetValue(AccountField, value);
    }

    public string Password
    {
        get => GetValue<string>(PasswordField);
        set => SetValue(PasswordField, value);
    }

    public string BilibiliAccountName
    {
        get => GetValue<string>(BilibiliAccountNameField);
        set => SetValue(BilibiliAccountNameField, value);
    }

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

    public override void OnPageShown() => Reload();

    public void Reload()
    {
        ZzzBackendResult<ZzzInstanceDto> current = _backend.GetCurrentInstance();
        ActiveInstanceIndex = current.Success
            ? current.Value?.Index ?? InstanceManagement.Instances.FirstOrDefault(instance => instance.Active)?.Index ?? 0
            : InstanceManagement.Instances.FirstOrDefault(instance => instance.Active)?.Index ?? 0;
        OnPropertyChanged(nameof(ActiveInstanceIndex));
        base.OnPageShown();
    }

    public void SaveStringForTest(string key, string value) => Save(key, value);

    public void SaveBoolForTest(string key, bool value) => Save(key, value);

    public void SetGameRegionForTest(string value) => GameRegion = value;

    public string ReadStringForTest(string key) => ReadString(key);

    public bool ReadBoolForTest(string key) => ReadBool(key);

    public string ReadString(string key) => key switch
    {
        "game_path" => GamePath,
        "custom_win_title" => CustomWindowTitle,
        "game_region" => GameRegion,
        "account" => Account,
        "password" => Password,
        "bilibili_account_name" => BilibiliAccountName,
        _ => string.Empty,
    };

    public bool ReadBool(string key) => key == "use_custom_win_title" && UseCustomWindowTitle;

    public bool Save(string key, object? value)
    {
        switch (key)
        {
            case "game_path": GamePath = value?.ToString() ?? string.Empty; break;
            case "use_custom_win_title": UseCustomWindowTitle = value is true; break;
            case "custom_win_title": CustomWindowTitle = value?.ToString() ?? string.Empty; break;
            case "game_region": GameRegion = value?.ToString() ?? string.Empty; break;
            case "account": Account = value?.ToString() ?? string.Empty; break;
            case "password": Password = value?.ToString() ?? string.Empty; break;
            case "bilibili_account_name": BilibiliAccountName = value?.ToString() ?? string.Empty; break;
            default: throw new ArgumentOutOfRangeException(nameof(key), key, "未知账户配置项。");
        }

        return LastError is null;
    }

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        OnPropertyChanged(nameof(SelectedGameRegion));
        OnPropertyChanged(nameof(AccountPasswordVisible));
        OnPropertyChanged(nameof(BilibiliVisible));
    }
}
