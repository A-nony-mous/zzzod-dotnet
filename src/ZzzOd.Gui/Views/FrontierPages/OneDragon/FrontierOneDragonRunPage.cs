using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Pages.ApplicationSettings;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Pages.OneDragon;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.Gui.Views.FrontierPages.OneDragon;

internal sealed partial class FrontierOneDragonRunPage : UserControl, IZzzPageLifecycle
{
    private static readonly DataFormat<string> AppIdFormat =
        DataFormat.CreateStringApplicationFormat("zzzod.one-dragon-app-id");

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
    private readonly ZzzOneDragonRunSettings _settings;
    private readonly ZzzAppSettingNavigator _appSettingNavigator;
    private readonly FASettingsExpander _appList;
    private readonly FAInfoBar _actionInfoBar;
    private readonly ToggleSwitch _notifyToggle;
    private readonly FAComboBox _instanceRunCombo;
    private readonly FAComboBox _afterDoneCombo;
    private readonly FATeachingTip _appNotifyTip;
    private readonly FAComboBox _appLifecycleCombo;
    private readonly FAComboBox _appDetailCombo;
    private readonly Dictionary<string, Button> _moreButtons = new(StringComparer.Ordinal);
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private CancellationTokenSource? _eventCancellation;
    private ZzzOneDragonAppRowModel? _dragCandidate;
    private Point _dragStart;
    private PointerPressedEventArgs? _dragPointerPressedArgs;
    private string? _notifyAppId;
    private bool _loadingSettings;
    private bool _loadingAppNotify;
    private readonly ZzzGuiOperationTracker _operations;

    public FrontierOneDragonRunPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent, ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _operations = operations ?? new ZzzGuiOperationTracker();
        _settings = new ZzzOneDragonRunSettings(backend);
        _appSettingNavigator = new ZzzAppSettingNavigator(
            backend,
            new FrontierAppSettingPageFactory(backend).Create);
        RunPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.OneDragon,
            "一条龙运行",
            runIntent,
            fixedGroupId: ZOneDragonAppConstants.DefaultGroupId);

        AvaloniaXamlLoader.Load(this);
        _appList = Required<FASettingsExpander>("AppList");
        _actionInfoBar = Required<FAInfoBar>("ActionInfoBar");
        _notifyToggle = Required<ToggleSwitch>("NotifyToggle");
        _instanceRunCombo = Required<FAComboBox>("InstanceRunCombo");
        _afterDoneCombo = Required<FAComboBox>("AfterDoneCombo");
        _appNotifyTip = Required<FATeachingTip>("AppNotifyTip");
        _appLifecycleCombo = Required<FAComboBox>("AppLifecycleCombo");
        _appDetailCombo = Required<FAComboBox>("AppDetailCombo");
        Required<ContentControl>("RunHost").Content = RunPanel;

        _instanceRunCombo.ItemsSource = new[] { "全部实例", "仅运行当前" };
        _afterDoneCombo.ItemsSource = new[] { "无", "关闭游戏", "关机" };
        _appLifecycleCombo.ItemsSource = LifecycleOptions;
        _appDetailCombo.ItemsSource = DetailOptions;
    }

    public ZzzOneDragonRunSettings Settings => _settings;

    public ZzzRunPanel RunPanel { get; }

    public event EventHandler<Control>? SecondaryPageRequested;

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("one-dragon", "activate-one-dragon-run");
        try
        {
            _settings.Reload();
            RefreshRows();
            RefreshSettings();
            StartEvents();
            RunPanel.OnPageShown();
            _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ShowAction(exception.Message, FAInfoBarSeverity.Error);
        }
    }

    public void CancelPageOperations(string reason)
    {
        StopEvents();
        RunPanel.OnPageLeave();
    }

    public void OnPageLeave()
    {
        StopEvents();
        _appNotifyTip.IsOpen = false;
        RunPanel.OnPageLeave();
    }

    public void OnPageHidden()
    {
        StopEvents();
        _appNotifyTip.IsOpen = false;
        RunPanel.OnPageHidden();
    }

    public void DisposePage()
    {
        StopEvents();
        _appNotifyTip.IsOpen = false;
        RunPanel.DisposePage();
    }

    private async void OnRunAppClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is not { } row)
        {
            return;
        }

        ZzzBackendResult<ZzzRunStatusDto> result = await _settings.StartSingleAppAsync(row.AppId).ConfigureAwait(true);
        ShowAction(result.Success ? $"已启动 {row.Name}" : result.Error ?? "应用启动失败。", result.Success ? FAInfoBarSeverity.Success : FAInfoBarSeverity.Error);
    }

    private void OnAppSettingClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button target || Row(target) is not { } row)
        {
            return;
        }

        if (!_appSettingNavigator.Open(
                row.AppId,
                ZOneDragonAppConstants.DefaultGroupId,
                target,
                content => SecondaryPageRequested?.Invoke(this, content)))
        {
            return;
        }
    }

    private void OnAppEnabledClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is ToggleSwitch toggle && toggle.DataContext is ZzzOneDragonAppRowModel row)
        {
            _settings.SetAppEnabled(row.AppId, toggle.IsChecked == true);
            RefreshRows();
            ShowSettingsError();
        }
    }

    private void OnMoveTopClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is not { } row)
        {
            return;
        }

        int index = _settings.AppRows.ToList().FindIndex(candidate => candidate.AppId == row.AppId);
        while (index > 0)
        {
            _settings.MoveApp(row.AppId, -1);
            index--;
        }

        RefreshRows();
        ShowSettingsError();
    }

    private void OnNotifyToggleClicked(object? sender, RoutedEventArgs args)
    {
        if (_loadingSettings)
        {
            return;
        }

        _settings.SetNotifyEnabled(_notifyToggle.IsChecked == true);
        RefreshRows();
        ShowSettingsError();
    }

    private void OnInstanceRunChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loadingSettings && _instanceRunCombo.SelectedItem is string value)
        {
            _settings.SetInstanceRun(value);
            ShowSettingsError();
        }
    }

    private void OnAfterDoneChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loadingSettings && _afterDoneCombo.SelectedItem is string value)
        {
            _settings.SetAfterDone(value);
            ShowSettingsError();
        }
    }

    private void OnGlobalNotifySettingsClicked(object? sender, RoutedEventArgs args)
    {
        if (_settings.InstanceIndex is int instanceIndex)
        {
            SecondaryPageRequested?.Invoke(this, new FrontierNotifySettingsPage(_backend, instanceIndex));
            return;
        }

    }

    private void OnMoreClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Button button && Row(button) is { } row)
        {
            _moreButtons[row.AppId] = button;
        }
    }

    private void OnAppNotifyClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is not { } row || !_moreButtons.TryGetValue(row.AppId, out Button? target))
        {
            return;
        }

        if (!_settings.TryGetAppNotifyModes(row.AppId, out string lifecycle, out string detail))
        {
            ShowSettingsError();
            return;
        }

        _loadingAppNotify = true;
        _notifyAppId = row.AppId;
        _appLifecycleCombo.SelectedItem = FindMode(
            LifecycleOptions,
            lifecycle,
            NotifyLifecycleModes.StartAndFinish);
        _appDetailCombo.SelectedItem = FindMode(
            DetailOptions,
            detail,
            NotifyDetailModes.All);
        _loadingAppNotify = false;
        _appNotifyTip.Target = target;
        _appNotifyTip.Title = row.Name;
        _appNotifyTip.IsOpen = true;
    }

    private void OnAppNotifyModeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loadingAppNotify || string.IsNullOrWhiteSpace(_notifyAppId)
            || _appLifecycleCombo.SelectedItem is not ZzzNotifyModeOption lifecycle
            || _appDetailCombo.SelectedItem is not ZzzNotifyModeOption detail)
        {
            return;
        }

        if (!_settings.SetAppNotifyModes(_notifyAppId, lifecycle.Value, detail.Value))
        {
            ShowSettingsError();
        }
    }

    private void OnAppPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ZzzOneDragonAppRowModel row
            || args.GetCurrentPoint(control).Properties.IsLeftButtonPressed == false
            || IsInteractiveSource(args.Source))
        {
            _dragCandidate = null;
            _dragPointerPressedArgs = null;
            return;
        }

        _dragCandidate = row;
        _dragStart = args.GetPosition(control);
        _dragPointerPressedArgs = args;
    }

    private async void OnAppPointerMoved(object? sender, PointerEventArgs args)
    {
        if (sender is not Control control || _dragCandidate is not { } row || _dragPointerPressedArgs is not { } pressedArgs
            || !args.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point current = args.GetPosition(control);
        if (Math.Abs(current.X - _dragStart.X) + Math.Abs(current.Y - _dragStart.Y) < 10)
        {
            return;
        }

        _dragCandidate = null;
        _dragPointerPressedArgs = null;
        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.Create(AppIdFormat, row.AppId));
        await DragDrop.DoDragDropAsync(pressedArgs, transfer, DragDropEffects.Move).ConfigureAwait(true);
    }

    private void OnAppDragOver(object? sender, DragEventArgs args)
    {
        args.DragEffects = args.DataTransfer.Contains(AppIdFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        args.Handled = true;
    }

    private void OnAppDrop(object? sender, DragEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ZzzOneDragonAppRowModel target)
        {
            return;
        }

        string? sourceAppId = args.DataTransfer.TryGetValue(AppIdFormat);
        List<ZzzOneDragonAppRowModel> rows = _settings.AppRows.ToList();
        int sourceIndex = rows.FindIndex(row => string.Equals(row.AppId, sourceAppId, StringComparison.Ordinal));
        int targetIndex = rows.FindIndex(row => string.Equals(row.AppId, target.AppId, StringComparison.Ordinal));
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        int insertionIndex = targetIndex + (args.GetPosition(control).Y >= control.Bounds.Height / 2 ? 1 : 0);
        if (sourceIndex < insertionIndex)
        {
            insertionIndex--;
        }

        _settings.MoveAppTo(sourceAppId!, Math.Clamp(insertionIndex, 0, rows.Count - 1));
        RefreshRows();
        ShowSettingsError();
        args.DragEffects = DragDropEffects.Move;
        args.Handled = true;
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args) => OpenUrl("https://one-dragon.com/zzz/zh/feat_one_dragon/onedragon.html");

    private void RefreshRows()
    {
        _appList.ItemsSource = null;
        _appList.ItemsSource = _settings.AppRows;
    }

    private void RefreshSettings()
    {
        _loadingSettings = true;
        _notifyToggle.IsChecked = _settings.NotifyEnabled;
        _instanceRunCombo.SelectedItem = _settings.InstanceRun;
        _afterDoneCombo.SelectedItem = _settings.AfterDone;
        _loadingSettings = false;
        ShowSettingsError();
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
                    if (item.Type is "instance.activeChanged" or "instance.changed")
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _settings.Reload();
                            RefreshRows();
                            RefreshSettings();
                        });
                    }
                    else if (item.Type == "run.stateChanged")
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _settings.ReloadApps();
                            RefreshRows();
                            ShowSettingsError();
                        });
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

    private void ShowSettingsError()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastError))
        {
            ShowAction(_settings.LastError, FAInfoBarSeverity.Error);
        }
    }

    private static bool IsInteractiveSource(object? source) => source is Control control
        && (control is Button or ToggleSwitch
            || control.GetVisualAncestors().Any(ancestor => ancestor is Button or ToggleSwitch));

    private static ZzzNotifyModeOption FindMode(
        IReadOnlyList<ZzzNotifyModeOption> options,
        string? value,
        string fallback) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal))
        ?? options.First(option => string.Equals(option.Value, fallback, StringComparison.Ordinal));

    private static ZzzOneDragonAppRowModel? Row(object? sender) => sender is Control control
        ? control.DataContext as ZzzOneDragonAppRowModel ?? control.Tag as ZzzOneDragonAppRowModel
        : null;

    private void ShowAction(string message, FAInfoBarSeverity severity)
    {
        _actionInfoBar.Message = message;
        _actionInfoBar.Severity = severity;
        _actionInfoBar.IsOpen = true;
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

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"一条龙运行页缺少 {name}。");
}
