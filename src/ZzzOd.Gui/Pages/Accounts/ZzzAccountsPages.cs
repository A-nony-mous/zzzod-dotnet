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

    public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(int index, string? name, bool? activeInOneDragon) =>
        CanSwitch ? Apply(_backend.UpdateInstance(new ZzzUpdateInstanceRequest(index, name, activeInOneDragon))) : Blocked<IReadOnlyList<ZzzInstanceDto>>();

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

internal sealed partial class ZzzAccountsPage : UserControl, IZzzPageLifecycle
{
    private static readonly IReadOnlyList<ZzzAccountOption> RegionOptions =
    [
        new("国服", "cn"),
        new("B服", "cn_b"),
        new("美服", "us"),
        new("欧服", "eu"),
        new("亚服", "asia"),
        new("港澳台服", "twhkmo"),
    ];

    private static readonly IReadOnlyList<ZzzAccountRunOption> RunOptions =
    [
        new("一条龙中运行", true),
        new("一条龙中不运行", false),
    ];

    private readonly IZzzAppBackend _backend;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly ObservableCollection<ZzzAccountInstanceRow> _rows = [];
    private readonly ItemsControl _instanceList;
    private readonly InfoBar _actionBar;
    private readonly SettingsExpanderItem _gamePathItem;
    private readonly ToggleSwitch _useCustomWindowTitleToggle;
    private readonly TextBox _customWindowTitleInput;
    private readonly FAComboBox _gameRegionCombo;
    private readonly SettingsExpanderItem _accountItem;
    private readonly TextBox _accountInput;
    private readonly SettingsExpanderItem _passwordItem;
    private readonly TextBox _passwordInput;
    private readonly SettingsExpanderItem _bilibiliHelpItem;
    private readonly SettingsExpanderItem _bilibiliAccountItem;
    private readonly TextBox _bilibiliAccountInput;
    private readonly Button _addInstanceButton;
    private readonly ContentDialog _protectionDialog;
    private readonly TextBox _protectionPasswordInput;
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private CancellationTokenSource? _eventCancellation;
    private bool _loading;

    public ZzzAccountsPage(IZzzAppBackend backend, ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _operations = operations ?? new ZzzGuiOperationTracker();
        InstanceManagement = new ZzzInstanceManagementPage(backend);
        CurrentAccountSettings = new ZzzCurrentAccountSettingsPage(backend);
        AvaloniaXamlLoader.Load(this);
        _instanceList = Required<ItemsControl>("InstanceList");
        _actionBar = Required<InfoBar>("ActionBar");
        _gamePathItem = Required<SettingsExpanderItem>("GamePathItem");
        _useCustomWindowTitleToggle = Required<ToggleSwitch>("UseCustomWindowTitleToggle");
        _customWindowTitleInput = Required<TextBox>("CustomWindowTitleInput");
        _gameRegionCombo = Required<FAComboBox>("GameRegionCombo");
        _accountItem = Required<SettingsExpanderItem>("AccountItem");
        _accountInput = Required<TextBox>("AccountInput");
        _passwordItem = Required<SettingsExpanderItem>("PasswordItem");
        _passwordInput = Required<TextBox>("PasswordInput");
        _bilibiliHelpItem = Required<SettingsExpanderItem>("BilibiliHelpItem");
        _bilibiliAccountItem = Required<SettingsExpanderItem>("BilibiliAccountItem");
        _bilibiliAccountInput = Required<TextBox>("BilibiliAccountInput");
        _addInstanceButton = Required<Button>("AddInstanceButton");
        _protectionDialog = Required<ContentDialog>("ProtectionDialog");
        _protectionPasswordInput = Required<TextBox>("ProtectionPasswordInput");
        _instanceList.ItemsSource = _rows;
        _gameRegionCombo.ItemsSource = RegionOptions;
    }

    public ZzzInstanceManagementPage InstanceManagement { get; }

    public ZzzCurrentAccountSettingsPage CurrentAccountSettings { get; }

    public void OnPageShown()
    {
        Reload();
        StartEvents();
    }

    public void CancelPageOperations(string reason) => StopEvents();

    public void OnPageHidden() => StopEvents();

    public void OnPageLeave() => StopEvents();

    public void DisposePage()
    {
        StopEvents();
    }

    internal void Reload()
    {
        Guid operationId = _operations.Start("accounts", "reload-accounts");
        _loading = true;
        try
        {
            _actionBar.IsOpen = false;
            InstanceManagement.Reload();
            CurrentAccountSettings.Reload();
            _rows.Clear();
            foreach (ZzzInstanceDto instance in InstanceManagement.Instances)
            {
                _rows.Add(new ZzzAccountInstanceRow(instance, InstanceManagement.CanSwitch, InstanceManagement.Instances.Count, RunOptions));
            }

            _addInstanceButton.IsEnabled = InstanceManagement.CanSwitch;
            _gamePathItem.Description = CurrentAccountSettings.ReadString("game_path");
            _useCustomWindowTitleToggle.IsChecked = CurrentAccountSettings.ReadBool("use_custom_win_title");
            _customWindowTitleInput.Text = CurrentAccountSettings.ReadString("custom_win_title");
            _gameRegionCombo.SelectedItem = RegionOptions.FirstOrDefault(option => option.Value == CurrentAccountSettings.GameRegion);
            _accountInput.Text = CurrentAccountSettings.ReadString("account");
            _passwordInput.Text = CurrentAccountSettings.ReadString("password");
            _bilibiliAccountInput.Text = CurrentAccountSettings.ReadString("bilibili_account_name");
            ApplyRegionVisibility();

            if (!InstanceManagement.CanSwitch)
            {
                ShowAction("一条龙运行中，不能切换实例。", InfoBarSeverity.Warning);
            }

            _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            throw;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        OpenUrl("https://one-dragon.com/zzz/zh/config.html");

    private async void OnChooseGamePathClicked(object? sender, RoutedEventArgs args)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
			ShowAction("当前窗口不可用。", InfoBarSeverity.Error);
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择你的 game.exe",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Exe") { Patterns = ["*.exe"] },
            ],
        }).ConfigureAwait(true);
        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SaveAccountValue("game_path", Path.GetFullPath(path));
            _gamePathItem.Description = Path.GetFullPath(path);
        }
    }

    private void OnUseCustomWindowTitleChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            SaveAccountValue("use_custom_win_title", _useCustomWindowTitleToggle.IsChecked == true);
        }
    }

    private void OnCustomWindowTitleLostFocus(object? sender, RoutedEventArgs args) =>
        SaveAccountValue("custom_win_title", _customWindowTitleInput.Text ?? string.Empty);

    private void OnGameRegionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _gameRegionCombo.SelectedItem is not ZzzAccountOption option)
        {
            return;
        }

        SaveAccountValue("game_region", option.Value);
        ApplyRegionVisibility();
    }

    private void OnAccountLostFocus(object? sender, RoutedEventArgs args) =>
        SaveAccountValue("account", _accountInput.Text ?? string.Empty);

    private void OnPasswordLostFocus(object? sender, RoutedEventArgs args) =>
        SaveAccountValue("password", _passwordInput.Text ?? string.Empty);

    private void OnBilibiliAccountLostFocus(object? sender, RoutedEventArgs args) =>
        SaveAccountValue("bilibili_account_name", _bilibiliAccountInput.Text ?? string.Empty);

    private void OnInstanceNameChanged(object? sender, TextChangedEventArgs args)
    {
        if (_loading
            || sender is not TextBox { DataContext: ZzzAccountInstanceRow row }
            || !row.HasPendingNameChange)
        {
            return;
        }

        SaveInstanceRow(row, row.Name, null);
    }

    private void OnInstanceRunChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading
            || sender is not Control { DataContext: ZzzAccountInstanceRow row }
            || !row.HasPendingRunOptionChange)
        {
            return;
        }

        SaveInstanceRow(row, null, row.SelectedRunOption.Value);
    }

    private void SaveInstanceRow(ZzzAccountInstanceRow row, string? name, bool? activeInOneDragon)
    {
        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = InstanceManagement.UpdateInstance(row.Index, name, activeInOneDragon);
        if (result.Success)
        {
            ZzzInstanceDto? persisted = result.Value?.FirstOrDefault(instance => instance.Index == row.Index);
            if (persisted is not null)
            {
                row.SynchronizePersistedValues(persisted);
            }
        }

        ShowResult(result);
    }

    private void OnActivateInstanceClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzAccountInstanceRow row })
        {
            ShowResult(InstanceManagement.ActivateInstance(row.Index));
            Reload();
        }
    }

    private void OnLoginInstanceClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzAccountInstanceRow row })
        {
            ZzzBackendResult<ZzzRunStatusDto> result = InstanceManagement.LoginInstance(row.Index);
            ShowAction(result.Success ? $"实例 {row.Index:00} 登录已启动。" : result.Error ?? "登录失败。", result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
    }

    private void OnDeleteInstanceClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzAccountInstanceRow row })
        {
            ShowResult(InstanceManagement.DeleteInstance(row.Index));
            Reload();
        }
    }

    private async void OnAddInstanceClicked(object? sender, RoutedEventArgs args)
    {
        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result;
        if (InstanceManagement.Instances.Count < 5)
        {
            result = InstanceManagement.AddInstance();
        }
        else
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
				ShowAction("当前窗口不可用。", InfoBarSeverity.Error);
                return;
            }

            _protectionPasswordInput.Text = string.Empty;
            ContentDialogResult dialogResult = await _protectionDialog.ShowAsync(owner).ConfigureAwait(true);
            if (dialogResult != ContentDialogResult.Primary)
            {
                return;
            }

            result = InstanceManagement.AddProtectedInstance(_protectionPasswordInput.Text ?? string.Empty);
        }

        ShowResult(result);
        Reload();
    }

    private void SaveAccountValue(string key, object? value)
    {
        if (_loading)
        {
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = CurrentAccountSettings.Save(key, value);
        if (!result.Success)
        {
            ShowAction(result.Error ?? "账户设置保存失败。", InfoBarSeverity.Error);
        }
    }

    private void ApplyRegionVisibility()
    {
        bool bilibili = CurrentAccountSettings.BilibiliVisible;
        _accountItem.IsVisible = !bilibili;
        _passwordItem.IsVisible = !bilibili;
        _bilibiliHelpItem.IsVisible = bilibili;
        _bilibiliAccountItem.IsVisible = bilibili;
    }

    private void ShowResult<T>(ZzzBackendResult<T> result)
    {
        if (!result.Success)
        {
            ShowAction(result.Error ?? "操作失败。", InfoBarSeverity.Error);
        }
    }

    private void ShowAction(string message, InfoBarSeverity severity)
    {
        _actionBar.Message = message;
        _actionBar.Severity = severity;
        _actionBar.IsOpen = true;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void StartEvents()
    {
        StopEvents();
        _eventReader = _backend.SubscribeEvents();
        _eventCancellation = new CancellationTokenSource();
        ChannelReader<ZzzBackendEvent> reader = _eventReader;
        CancellationToken token = _eventCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (ZzzBackendEvent item in reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    if (item.Type is "instance.activeChanged" or "instance.changed" or "run.stateChanged")
                    {
                        await Dispatcher.UIThread.InvokeAsync(Reload);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ChannelClosedException)
            {
            }
        });
    }

    private void StopEvents()
    {
        _eventCancellation?.Cancel();
        if (_eventReader is not null)
        {
            _backend.UnsubscribeEvents(_eventReader);
        }

        _eventCancellation?.Dispose();
        _eventCancellation = null;
        _eventReader = null;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"账户页缺少 {name}。");
}

