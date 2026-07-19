using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.OneDragon;

internal sealed record ZzzNotifyModeOption(string Label, string Value);

internal sealed class ZzzNotifyAppRowModel
{
    public required string AppId { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<ZzzNotifyModeOption> LifecycleOptions { get; init; }

    public required IReadOnlyList<ZzzNotifyModeOption> DetailOptions { get; init; }

    public ZzzNotifyModeOption? SelectedLifecycle { get; set; }

    public ZzzNotifyModeOption? SelectedDetail { get; set; }
}

internal sealed partial class ZzzNotifySettingsPage : UserControl, IZzzPageLifecycle
{
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
    private readonly FAInfoBar _errorBar;
    private readonly ToggleSwitch _mergeErrorToggle;
    private readonly ItemsControl _appNotifyList;
    private bool _loading;

    public ZzzNotifySettingsPage(IZzzAppBackend backend, int instanceIndex)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _mergeErrorToggle = Required<ToggleSwitch>("MergeErrorToggle");
        _appNotifyList = Required<ItemsControl>("AppNotifyList");
    }

    public void OnPageShown() => Reload();

    public void OnPageLeave()
    {
    }

    public void OnPageHidden()
    {
    }

    public void DisposePage()
    {
    }

    private void Reload()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> configResult = _backend.GetConfigScope("notify", _instanceIndex);
        ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> appResult = _backend.GetOneDragonApps(_instanceIndex);
        if (!configResult.Success || configResult.Value is null || !appResult.Success || appResult.Value is null)
        {
            ShowError(configResult.Error ?? appResult.Error ?? "通知设置读取失败。");
            return;
        }

        IReadOnlyDictionary<string, object?> values = configResult.Value.Values;
        Dictionary<string, NotifyApplicationSetting> applications = ReadApplications(values);
        _loading = true;
        _mergeErrorToggle.IsChecked = values.TryGetValue("merge_error_immediate_notify", out object? merge)
            && merge is bool enabled && enabled;
        _appNotifyList.ItemsSource = appResult.Value
            .Where(app => app.NotifyVisible)
            .Select(app => CreateRow(app, applications))
            .ToArray();
        _loading = false;
        _errorBar.IsOpen = false;
    }

    private void OnMergeErrorChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save(new Dictionary<string, object?>
            {
                ["merge_error_immediate_notify"] = _mergeErrorToggle.IsChecked == true,
            });
        }
    }

    private void OnApplicationModeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || sender is not Control { DataContext: ZzzNotifyAppRowModel row }
            || row.SelectedLifecycle is null || row.SelectedDetail is null)
        {
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> current = _backend.GetConfigScope("notify", _instanceIndex);
        if (!current.Success || current.Value is null)
        {
            ShowError(current.Error ?? "通知设置读取失败。");
            return;
        }

        Dictionary<string, NotifyApplicationSetting> applications = ReadApplications(current.Value.Values);
        applications[row.AppId] = new NotifyApplicationSetting
        {
            Lifecycle = row.SelectedLifecycle.Value,
            Detail = row.SelectedDetail.Value,
        };
        Save(new Dictionary<string, object?> { ["applications"] = applications });
    }

    private void Save(IReadOnlyDictionary<string, object?> values)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "notify",
            values,
            _instanceIndex));
        if (!result.Success)
        {
            ShowError(result.Error ?? "通知设置保存失败。");
        }
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

    internal static Dictionary<string, NotifyApplicationSetting> ReadApplications(IReadOnlyDictionary<string, object?> values)
    {
        if (values.TryGetValue("applications", out object? value)
            && value is Dictionary<string, NotifyApplicationSetting> applications)
        {
            return applications.ToDictionary(
                pair => pair.Key,
                pair => new NotifyApplicationSetting
                {
                    Lifecycle = pair.Value.Lifecycle,
                    Detail = pair.Value.Detail,
                },
                StringComparer.Ordinal);
        }

        return new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal);
    }

    private void ShowError(string message)
    {
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"通知设置页缺少 {name}。");
}

