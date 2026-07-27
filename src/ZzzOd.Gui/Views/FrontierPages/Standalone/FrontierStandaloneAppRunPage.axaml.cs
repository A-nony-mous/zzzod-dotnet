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
using ZzzOd.Gui.PageModels.ApplicationSettings;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.Gui.Views.FrontierPages.Standalone;

internal sealed partial class FrontierStandaloneAppRunPage : UserControl, IZzzPageLifecycle
{
    private static readonly DataFormat<string> AppIdFormat =
        DataFormat.CreateStringApplicationFormat("zzzod.standalone-app-id");

    private readonly IZzzAppBackend _backend;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly ZzzAppSettingNavigator _appSettingNavigator;
    private readonly ZzzStandaloneRunSettingsViewModel _settings;
    private readonly ItemsControl _appList;
    private readonly FAInfoBar _actionInfoBar;
    private readonly Button _addAppButton;
    private readonly List<ZzzAppDto> _availableApps = [];
    private readonly List<ZzzStandaloneAppRowModel> _appRows = [];
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private CancellationTokenSource? _eventCancellation;
    private ZzzStandaloneAppRowModel? _dragCandidate;
    private Point _dragStart;
    private PointerPressedEventArgs? _dragPointerPressedArgs;

    public FrontierStandaloneAppRunPage(
        IZzzAppBackend backend,
        ZzzGuiRunIntentService runIntent,
        ZzzGuiOperationTracker? operations = null,
        Func<string, int, string, Control?>? appSettingFactory = null)
    {
        _backend = backend;
        _operations = operations ?? new ZzzGuiOperationTracker();
        _settings = new ZzzStandaloneRunSettingsViewModel(backend, error =>
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                ShowError(error);
            }
        });
        _appSettingNavigator = new ZzzAppSettingNavigator(
            backend,
            appSettingFactory ?? new FrontierAppSettingPageFactory(backend).Create);
        RunPanel = new ZzzRunPanel(
            backend,
            title: "应用运行",
            runIntent: runIntent,
            appIdProvider: () => SelectedAppId,
            fixedGroupId: ZOneDragonAppConstants.DefaultGroupId);

        AvaloniaXamlLoader.Load(this);
        _appList = Required<ItemsControl>("AppList");
        _actionInfoBar = Required<FAInfoBar>("ActionInfoBar");
        _addAppButton = Required<Button>("AddAppButton");
        Required<ContentControl>("RunHost").Content = RunPanel;
        DataContext = _settings;
    }

    public IReadOnlyList<ZzzStandaloneAppRowModel> AppRows => _appRows;

    public string? SelectedAppId => _settings.SelectedAppId;

    public ZzzRunPanel RunPanel { get; }

    public event EventHandler<Control>? SecondaryPageRequested;

    public void AddAppForTest(string appId) => AddApps([appId]);

    public void RemoveAppForTest(string appId) => RemoveApp(appId);

    public void SelectAppForTest(string appId) => SelectApp(appId);

    public void MoveAppForTest(string appId, int direction)
    {
        int index = _appRows.FindIndex(row => string.Equals(row.AppId, appId, StringComparison.Ordinal));
        MoveAppTo(appId, index + direction);
    }

    public void MoveAppToForTest(string appId, int targetIndex) => MoveAppTo(appId, targetIndex);

    public void OnPageShown()
    {
        Reload();
        StartEvents();
        RunPanel.OnPageShown();
    }

    public void CancelPageOperations(string reason)
    {
        StopEvents();
        RunPanel.OnPageLeave();
    }

    public void OnPageHidden()
    {
        StopEvents();
        RunPanel.OnPageHidden();
    }

    public void OnPageLeave()
    {
        StopEvents();
        RunPanel.OnPageLeave();
    }

    public void DisposePage()
    {
        StopEvents();
        RunPanel.DisposePage();
    }

    private void Reload()
    {
        Guid operationId = _operations.Start("standalone", "reload-apps");
        _actionInfoBar.IsOpen = false;
        ZzzBackendResult<IReadOnlyList<ZzzAppDto>> appsResult = _backend.GetStandaloneApps();
        if (!appsResult.Success || appsResult.Value is null)
        {
            _availableApps.Clear();
            _appRows.Clear();
            RefreshRows();
            ShowError(appsResult.Error ?? "应用列表读取失败。");
            _operations.Complete(operationId, ZzzGuiOperationState.Failed);
            return;
        }

        _availableApps.Clear();
        _availableApps.AddRange(appsResult.Value
            .Where(app => app.DefaultGroup)
            .Where(app => !string.Equals(app.AppId, ZzzApplicationIds.OneDragon, StringComparison.Ordinal)));

        _settings.OnPageShown();
        if (_settings.LastError is not null)
        {
            _appRows.Clear();
            RefreshRows();
            _operations.Complete(operationId, ZzzGuiOperationState.Failed);
            return;
        }

        Dictionary<string, ZzzAppDto> appMap = _availableApps.ToDictionary(app => app.AppId, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        _appRows.Clear();
        foreach (string appId in _settings.AppIds)
        {
            if (seen.Add(appId) && appMap.TryGetValue(appId, out ZzzAppDto? app))
            {
                _appRows.Add(ToRow(app, false));
            }
        }

        _settings.NormalizeSelection(_appRows.Select(row => row.AppId));
        ApplySelection();

        RefreshRows();
        _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
    }

    private async void OnAddAppClicked(object? sender, RoutedEventArgs args)
    {
        ZzzStandaloneAppOption[] available = _availableApps
            .Where(app => _appRows.All(row => !string.Equals(row.AppId, app.AppId, StringComparison.Ordinal)))
            .Select(app => new ZzzStandaloneAppOption(app.AppId, app.Name))
            .ToArray();
        if (available.Length == 0)
        {
            return;
        }

        ListBox list = new()
        {
            ItemsSource = available,
            SelectionMode = SelectionMode.Multiple,
            MinWidth = 480,
            MinHeight = 400,
        };
        FAContentDialog dialog = new()
        {
            Title = "添加应用",
            Content = list,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
        };
        if (TopLevel.GetTopLevel(this) is not { } owner)
        {
            return;
        }

        FAContentDialogResult result = await dialog.ShowAsync(owner).ConfigureAwait(true);
        if (result != FAContentDialogResult.Primary)
        {
            return;
        }

        AddApps(list.SelectedItems?.OfType<ZzzStandaloneAppOption>().Select(option => option.AppId) ?? []);
    }

    private void AddApps(IEnumerable<string> appIds)
    {
        HashSet<string> existing = _appRows.Select(row => row.AppId).ToHashSet(StringComparer.Ordinal);
        foreach (ZzzAppDto app in OrderRequestedApps(_availableApps, appIds))
        {
            if (existing.Add(app.AppId))
            {
                _appRows.Add(ToRow(app, false));
            }
        }

        string? selectedAppId = SelectedAppId ?? _appRows.FirstOrDefault()?.AppId;
        _settings.SaveConfiguration(_appRows.Select(row => row.AppId).ToArray(), selectedAppId);
        ApplySelection();
        RefreshRows();
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is { } row)
        {
            RemoveApp(row.AppId);
        }
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

    private void RemoveApp(string appId)
    {
        if (_appRows.RemoveAll(row => string.Equals(row.AppId, appId, StringComparison.Ordinal)) == 0)
        {
            return;
        }

        if (string.Equals(SelectedAppId, appId, StringComparison.Ordinal))
        {
            _settings.SaveConfiguration(
                _appRows.Select(row => row.AppId).ToArray(),
                _appRows.FirstOrDefault()?.AppId);
        }
        else
        {
            _settings.SaveConfiguration(_appRows.Select(row => row.AppId).ToArray(), SelectedAppId);
        }

        ApplySelection();
        RefreshRows();
    }

    private void OnAppPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ZzzStandaloneAppRowModel row
            || args.GetCurrentPoint(control).Properties.IsLeftButtonPressed == false
            || IsInteractiveSource(args.Source))
        {
            _dragCandidate = null;
            _dragPointerPressedArgs = null;
            return;
        }

        SelectApp(row.AppId);
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
        if (sender is not Control control || control.DataContext is not ZzzStandaloneAppRowModel target)
        {
            return;
        }

        string? sourceAppId = args.DataTransfer.TryGetValue(AppIdFormat);
        int sourceIndex = _appRows.FindIndex(row => string.Equals(row.AppId, sourceAppId, StringComparison.Ordinal));
        int targetIndex = _appRows.FindIndex(row => string.Equals(row.AppId, target.AppId, StringComparison.Ordinal));
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        int insertionIndex = targetIndex + (args.GetPosition(control).Y >= control.Bounds.Height / 2 ? 1 : 0);
        if (sourceIndex < insertionIndex)
        {
            insertionIndex--;
        }

        MoveAppTo(sourceAppId!, Math.Clamp(insertionIndex, 0, _appRows.Count - 1));
        args.DragEffects = DragDropEffects.Move;
        args.Handled = true;
    }

    private void MoveAppTo(string appId, int targetIndex)
    {
        int sourceIndex = _appRows.FindIndex(row => string.Equals(row.AppId, appId, StringComparison.Ordinal));
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= _appRows.Count || sourceIndex == targetIndex)
        {
            return;
        }

        ZzzStandaloneAppRowModel row = _appRows[sourceIndex];
        _appRows.RemoveAt(sourceIndex);
        _appRows.Insert(targetIndex, row);
        _settings.SaveConfiguration(_appRows.Select(item => item.AppId).ToArray(), SelectedAppId);
        RefreshRows();
    }

    private void SelectApp(string appId)
    {
        if (_appRows.All(row => !string.Equals(row.AppId, appId, StringComparison.Ordinal)))
        {
            return;
        }

        _settings.SaveActiveSelection(appId);
        ApplySelection();
        RefreshRows();
    }

    private void ApplySelection()
    {
        for (int index = 0; index < _appRows.Count; index++)
        {
            ZzzStandaloneAppRowModel row = _appRows[index];
            _appRows[index] = row with
            {
                IsSelected = string.Equals(row.AppId, SelectedAppId, StringComparison.Ordinal),
            };
        }

        RunPanel.RefreshRunTargetForCurrentSelection();
    }

    private void RefreshRows()
    {
        _appList.ItemsSource = null;
        _appList.ItemsSource = _appRows.ToArray();
        _addAppButton.IsEnabled = true;
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

    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        OpenUrl("https://one-dragon.com/zzz/zh/feat_standalone_app.html");

    private void ShowError(string message)
    {
        _actionInfoBar.Message = message;
        _actionInfoBar.Severity = FAInfoBarSeverity.Error;
        _actionInfoBar.IsOpen = true;
    }

    internal static IReadOnlyList<ZzzAppDto> OrderRequestedApps(
        IEnumerable<ZzzAppDto> availableApps,
        IEnumerable<string> requestedAppIds)
    {
        HashSet<string> requested = requestedAppIds.ToHashSet(StringComparer.Ordinal);
        return availableApps.Where(app => requested.Contains(app.AppId)).ToArray();
    }

    private static ZzzStandaloneAppRowModel ToRow(ZzzAppDto app, bool isSelected) =>
        new(
            app.AppId,
            app.Name,
            app.RunAvailable,
            ZzzAppSettingProviderRegistry.TryGetImplemented(app.AppId, out _),
            isSelected);

    private static bool IsInteractiveSource(object? source) => source is Control control
        && (control is Button || control.GetVisualAncestors().Any(ancestor => ancestor is Button));

    private static ZzzStandaloneAppRowModel? Row(object? sender) => sender is Control control
        ? control.DataContext as ZzzStandaloneAppRowModel ?? control.Tag as ZzzStandaloneAppRowModel
        : null;

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
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"独立运行页缺少 {name}。");
}

internal sealed record ZzzStandaloneAppRowModel(
    string AppId,
    string Name,
    bool RunAvailable,
    bool SettingVisible,
    bool IsSelected)
{
    public double Opacity => IsSelected ? 1 : 0.5;
}

internal sealed record ZzzStandaloneAppOption(string AppId, string Name)
{
    public override string ToString() => Name;
}
