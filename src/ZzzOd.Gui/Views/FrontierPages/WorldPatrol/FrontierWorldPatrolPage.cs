using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Pages.Devtools;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.Gui.Views.FrontierPages.WorldPatrol;

internal sealed partial class FrontierWorldPatrolPage : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "world-patrol";
    private readonly IZzzAppBackend _backend;
    private readonly IZzzWorldPatrolSettingsBackend _worldPatrolBackend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private readonly FAInfoBar _settingsErrorBar;
    private readonly FAInfoBar _routeListErrorBar;
    private readonly FAInfoBar _largeMapErrorBar;
    private readonly FAInfoBar _largeMapStatusBar;
    private readonly FAInfoBar _routeEditorErrorBar;
    private readonly FATabView _settingsTabView;
    private readonly FASettingsExpander _runRecordItem;
    private readonly FAComboBox _autoBattleCombo;
    private readonly FAComboBox _routeListConfigCombo;
    private readonly FAComboBox _uiDisappearActionCombo;
    private readonly FAComboBox _routeRetryActionCombo;
    private readonly FANumberBox _uiDisappearSecondsBox;
    private readonly FANumberBox _routeRetryTimesBox;
    private readonly FANumberBox _dailyLoopCountBox;
    private readonly FANumberBox _loopIntervalSecondsBox;
    private readonly FAComboBox _editorRouteListCombo;
    private readonly FAComboBox _listTypeCombo;
    private readonly FAComboBox _entryCombo;
    private readonly FAComboBox _areaCombo;
    private readonly ListBox _chosenRouteList;
    private readonly ListBox _availableRouteList;
    private readonly FAComboBox _largeMapEntryCombo;
    private readonly FAComboBox _largeMapAreaCombo;
    private readonly FANumberBox _largeMapIconThresholdBox;
    private readonly FANumberBox _largeMapScaleBox;
    private readonly FANumberBox _largeMapHorizontalMoveBox;
    private readonly FANumberBox _largeMapVerticalMoveBox;
    private readonly TextBlock _largeMapPositionText;
    private readonly Image _largeMapMiniMap1Image;
    private readonly Image _largeMapMiniMap2Image;
    private readonly Image _largeMapMiniMapMergedImage;
    private readonly FrontierWorldPatrolImageViewer _largeMapViewer;
    private readonly FACommandBarButton _loadLargeMapButton;
    private readonly FACommandBarButton _saveLargeMapButton;
    private readonly FACommandBarButton _deleteLargeMapButton;
    private readonly FACommandBarButton _cancelLargeMapButton;
    private readonly FACommandBarButton _editLargeMapIconsButton;
    private readonly ZzzLogDisplayCard _largeMapLogCard;
    private readonly ZzzLogDisplayCard _routeRecorderLogCard;
    private readonly FAComboBox _routeEntryCombo;
    private readonly FAComboBox _routeAreaCombo;
    private readonly FAComboBox _routeCombo;
    private readonly FAComboBox _transportPointCombo;
    private readonly Image _routeMiniMapRoadImage;
    private readonly FrontierWorldPatrolImageViewer _routeMapViewer;
    private readonly ToggleSwitch _autoAddRouteClickSwitch;
    private readonly FAContentDialog _routeOperationDialog;
    private readonly FAContentDialog _deleteRouteOperationDialog;
    private readonly FAInfoBar _routeOperationErrorBar;
    private readonly ListBox _routeOperationList;
    private readonly FANumberBox _debugStartBox;
    private readonly Button[] _requiresListButtons;
    private readonly Button[] _requiresChosenRouteButtons;
    private ZzzWorldPatrolCatalogDto? _catalog;
    private ZzzWorldPatrolRouteListDto? _currentRouteList;
    private ZzzWorldPatrolRouteDto? _currentRoute;
    private ZzzWorldPatrolLargeMapRecorderStateDto? _largeMapState;
    private FrontierWorldPatrolLargeMapIconEditorWindow? _largeMapIconEditorWindow;
    private List<ZzzWorldPatrolEditableOperation> _routeOperations = [];
    private List<ZzzWorldPatrolEditableOperation> _routeOperationDraft = [];
    private ZzzWorldPatrolRoutePositionDto? _calculatedPosition;
    private IDisposable? _largeMapHotkeySubscription;
    private IDisposable? _routeHotkeySubscription;
    private double _appliedLargeMapIconThreshold = 0.7;
    private int _dailyLoopCount = 1;
    private bool _pageShown;
    private bool _loading;
    private bool _routeMapAvailable;

    public FrontierWorldPatrolPage(
        IZzzAppBackend backend,
        IZzzWorldPatrolSettingsBackend worldPatrolBackend,
        int instanceIndex,
        string groupId)
    {
        _backend = backend;
        _worldPatrolBackend = worldPatrolBackend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        AvaloniaXamlLoader.Load(this);
        _settingsErrorBar = Required<FAInfoBar>("SettingsErrorBar");
        _routeListErrorBar = Required<FAInfoBar>("RouteListErrorBar");
        _largeMapErrorBar = Required<FAInfoBar>("LargeMapErrorBar");
        _largeMapStatusBar = Required<FAInfoBar>("LargeMapStatusBar");
        _routeEditorErrorBar = Required<FAInfoBar>("RouteEditorErrorBar");
        _settingsTabView = Required<FATabView>("SettingsTabView");
        _runRecordItem = Required<FASettingsExpander>("RunRecordItem");
        _autoBattleCombo = Required<FAComboBox>("AutoBattleCombo");
        _routeListConfigCombo = Required<FAComboBox>("RouteListConfigCombo");
        _uiDisappearActionCombo = Required<FAComboBox>("UiDisappearActionCombo");
        _routeRetryActionCombo = Required<FAComboBox>("RouteRetryActionCombo");
        _uiDisappearSecondsBox = Required<FANumberBox>("UiDisappearSecondsBox");
        _routeRetryTimesBox = Required<FANumberBox>("RouteRetryTimesBox");
        _dailyLoopCountBox = Required<FANumberBox>("DailyLoopCountBox");
        _loopIntervalSecondsBox = Required<FANumberBox>("LoopIntervalSecondsBox");
        _editorRouteListCombo = Required<FAComboBox>("EditorRouteListCombo");
        _listTypeCombo = Required<FAComboBox>("ListTypeCombo");
        _entryCombo = Required<FAComboBox>("EntryCombo");
        _areaCombo = Required<FAComboBox>("AreaCombo");
        _chosenRouteList = Required<ListBox>("ChosenRouteList");
        _availableRouteList = Required<ListBox>("AvailableRouteList");
        _largeMapEntryCombo = Required<FAComboBox>("LargeMapEntryCombo");
        _largeMapAreaCombo = Required<FAComboBox>("LargeMapAreaCombo");
        _largeMapIconThresholdBox = Required<FANumberBox>("LargeMapIconThresholdBox");
        _largeMapScaleBox = Required<FANumberBox>("LargeMapScaleBox");
        _largeMapHorizontalMoveBox = Required<FANumberBox>("LargeMapHorizontalMoveBox");
        _largeMapVerticalMoveBox = Required<FANumberBox>("LargeMapVerticalMoveBox");
        _largeMapPositionText = Required<TextBlock>("LargeMapPositionText");
        _largeMapMiniMap1Image = Required<Image>("LargeMapMiniMap1Image");
        _largeMapMiniMap2Image = Required<Image>("LargeMapMiniMap2Image");
        _largeMapMiniMapMergedImage = Required<Image>("LargeMapMiniMapMergedImage");
        _largeMapViewer = Required<FrontierWorldPatrolImageViewer>("LargeMapViewer");
        _loadLargeMapButton = Required<FACommandBarButton>("LoadLargeMapButton");
        _saveLargeMapButton = Required<FACommandBarButton>("SaveLargeMapButton");
        _deleteLargeMapButton = Required<FACommandBarButton>("DeleteLargeMapButton");
        _cancelLargeMapButton = Required<FACommandBarButton>("CancelLargeMapButton");
        _editLargeMapIconsButton = Required<FACommandBarButton>("EditLargeMapIconsButton");
        _largeMapLogCard = new ZzzLogDisplayCard(_backend);
        Required<ContentControl>("LargeMapLogHost").Content = _largeMapLogCard;
        _routeRecorderLogCard = new ZzzLogDisplayCard(_backend);
        Required<ContentControl>("RouteRecorderLogHost").Content = _routeRecorderLogCard;
        _routeEntryCombo = Required<FAComboBox>("RouteEntryCombo");
        _routeAreaCombo = Required<FAComboBox>("RouteAreaCombo");
        _routeCombo = Required<FAComboBox>("RouteCombo");
        _transportPointCombo = Required<FAComboBox>("TransportPointCombo");
        _routeMiniMapRoadImage = Required<Image>("RouteMiniMapRoadImage");
        _routeMapViewer = Required<FrontierWorldPatrolImageViewer>("RouteMapViewer");
        _autoAddRouteClickSwitch = Required<ToggleSwitch>("AutoAddRouteClickSwitch");
        _routeOperationDialog = Required<FAContentDialog>("RouteOperationDialog");
        _deleteRouteOperationDialog = Required<FAContentDialog>("DeleteRouteOperationDialog");
        _routeOperationErrorBar = Required<FAInfoBar>("RouteOperationErrorBar");
        _routeOperationList = Required<ListBox>("RouteOperationList");
        _debugStartBox = Required<FANumberBox>("DebugStartBox");
        _requiresListButtons =
        [
            Required<Button>("SaveListButton"),
            Required<Button>("DeleteListButton"),
            Required<Button>("CancelListButton"),
            Required<Button>("AddAreaButton"),
            Required<Button>("AddRouteButton"),
        ];
        _requiresChosenRouteButtons =
        [
            Required<Button>("MoveUpButton"),
            Required<Button>("MoveDownButton"),
            Required<Button>("RemoveButton"),
        ];

        _listTypeCombo.ItemsSource = new[]
        {
            new ZzzWorldPatrolOption("白名单", "whitelist"),
            new ZzzWorldPatrolOption("黑名单", "blacklist"),
        };
        _uiDisappearActionCombo.ItemsSource = new[]
        {
            new ZzzWorldPatrolOption("静默失败", "silent_fail"),
            new ZzzWorldPatrolOption("重开游戏并跳过路线", "restart_and_skip"),
            new ZzzWorldPatrolOption("重开游戏并重试路线", "restart_and_retry"),
        };
        _routeRetryActionCombo.ItemsSource = new[]
        {
            new ZzzWorldPatrolOption("若再次卡住则跳过脱困", "skip_on_stuck_again"),
            new ZzzWorldPatrolOption("若再次卡住仍尝试脱困", "retry_on_stuck_again"),
        };
        _debugStartBox.Value = 0;
        Reload();
    }

    public void OnPageShown()
    {
        _pageShown = true;
        Reload();
        UpdateLargeMapHotkeySubscription();
        UpdateRouteHotkeySubscription();
    }

    public void OnPageHidden()
    {
        _pageShown = false;
        DisposeLargeMapHotkeySubscription();
        DisposeRouteHotkeySubscription();
    }

    public void OnPageLeave()
    {
        _pageShown = false;
        DisposeLargeMapHotkeySubscription();
        DisposeRouteHotkeySubscription();
    }

    public void DisposePage()
    {
        _pageShown = false;
        DisposeLargeMapHotkeySubscription();
        DisposeRouteHotkeySubscription();
        _largeMapLogCard.DisposePage();
        _routeRecorderLogCard.DisposePage();
    }

    internal ZzzWorldPatrolRouteListDto? CurrentRouteList => _currentRouteList;

    internal void BeginNewListForTest(string name) => BeginNewList(name);

    internal void SaveListForTest() => SaveCurrentList();

    internal void ResetRecordForTest() => ResetRecord();

    internal int RouteOperationCountForTest => _routeOperations.Count;

    internal void LoadRouteForTest(string fullId)
    {
        ZzzWorldPatrolRouteDto route = _catalog?.Routes.First(item => string.Equals(item.FullId, fullId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"路线不存在 {fullId}");
        Select(_routeEntryCombo, route.EntryId);
        OnRouteEntryChanged(_routeEntryCombo, null!);
        Select(_routeAreaCombo, route.AreaId);
        OnRouteAreaChanged(_routeAreaCombo, null!);
        LoadRoute(route);
    }

    internal bool HandleRouteRecorderKeyForTest(string key) => HandleRouteRecorderKey(key);

    internal void SaveRouteForTest() => SaveCurrentRoute();

    private void Reload()
    {
        _loading = true;
        _settingsErrorBar.IsOpen = false;
        _routeListErrorBar.IsOpen = false;
        _largeMapErrorBar.IsOpen = false;
        _routeEditorErrorBar.IsOpen = false;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> configResult =
                _backend.GetConfigScope(ScopeName, _instanceIndex, _groupId);
            ZzzBackendResult<ZzzWorldPatrolCatalogDto> catalogResult =
                _worldPatrolBackend.GetWorldPatrolCatalog(_instanceIndex);
            if (!configResult.Success || configResult.Value is null)
            {
                ShowSettingsError(configResult.Error ?? "锄大地配置读取失败。");
                return;
            }

            if (!catalogResult.Success || catalogResult.Value is null)
            {
                ShowSettingsError(catalogResult.Error ?? "锄大地路线读取失败。");
                return;
            }

            _catalog = catalogResult.Value;
            ApplyCatalog(_catalog);
            IReadOnlyDictionary<string, object?> values = configResult.Value.Values;
            Select(_autoBattleCombo, RequiredString(values, "auto_battle"));
            Select(_routeListConfigCombo, RequiredString(values, "route_list"));
            Select(_uiDisappearActionCombo, RequiredString(values, "ui_disappear_action"));
            Select(_routeRetryActionCombo, RequiredString(values, "route_retry_action"));
            _uiDisappearSecondsBox.Value = RequiredInt(values, "ui_disappear_seconds");
            _routeRetryTimesBox.Value = RequiredInt(values, "route_retry_times");
            _dailyLoopCount = RequiredInt(values, "daily_loop_count");
            _dailyLoopCountBox.Value = _dailyLoopCount;
            _loopIntervalSecondsBox.Value = RequiredInt(values, "loop_interval_seconds");
            UpdateRunRecordDisplay(_catalog.RunRecord);
        }
        catch (InvalidOperationException exception)
        {
            ShowSettingsError(exception.Message);
        }
        finally
        {
            _loading = false;
            UpdateEditorButtons();
            UpdateRouteEditorButtons();
        }
    }

    private void ApplyCatalog(ZzzWorldPatrolCatalogDto catalog)
    {
        _autoBattleCombo.ItemsSource = catalog.AutoBattleConfigs.Select(value => new ZzzWorldPatrolOption(value, value)).ToArray();
        _routeListConfigCombo.ItemsSource = new[] { new ZzzWorldPatrolOption("全部", string.Empty) }
            .Concat(catalog.RouteLists.Select(item => new ZzzWorldPatrolOption(item.Name, item.Name)))
            .ToArray();
        _editorRouteListCombo.ItemsSource = catalog.RouteLists
            .Select(item => new ZzzWorldPatrolOption($"{item.Name} ({item.ListType})", item.Name))
            .ToArray();
        _entryCombo.ItemsSource = catalog.Entries.Select(item => new ZzzWorldPatrolOption(item.Name, item.Id)).ToArray();
        _largeMapEntryCombo.ItemsSource = catalog.Entries.Select(item => new ZzzWorldPatrolOption(item.Name, item.Id)).ToArray();
        _routeEntryCombo.ItemsSource = catalog.Entries.Select(item => new ZzzWorldPatrolOption(item.Name, item.Id)).ToArray();
        _currentRouteList = null;
        _chosenRouteList.ItemsSource = null;
        _availableRouteList.ItemsSource = null;
        _currentRoute = null;
        _largeMapAreaCombo.ItemsSource = null;
        _routeAreaCombo.ItemsSource = null;
        _routeCombo.ItemsSource = null;
        _transportPointCombo.ItemsSource = null;
        _routeOperations = [];
        RefreshRouteOperations();
    }

    private void OnConfigComboChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && sender is FAComboBox { Tag: string key, SelectedItem: ZzzWorldPatrolOption option })
        {
            SaveConfig(key, option.Value);
        }
    }

    private void OnConfigNumberChanged(FANumberBox sender, FANumberBoxValueChangedEventArgs args)
    {
        if (_loading || sender.Tag is not string key)
        {
            return;
        }

        int value = (int)sender.Value;
        SaveConfig(key, value);
        if (key == "daily_loop_count")
        {
            _dailyLoopCount = value;
            if (_catalog is not null)
            {
                UpdateRunRecordDisplay(_catalog.RunRecord);
            }
        }
    }

    private void SaveConfig(string key, object value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            _groupId));
        if (!result.Success)
        {
            ShowSettingsError(result.Error ?? "锄大地配置保存失败。");
        }
    }

    private async void OnHelpClicked(object? sender, RoutedEventArgs args)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://one-dragon.com/zzz/zh/feat_one_dragon/world_patrol.html") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            ShowSettingsError(exception.Message);
        }

        await Task.CompletedTask;
    }

    private void OnResetRecordClicked(object? sender, RoutedEventArgs args) => ResetRecord();

    private void ResetRecord()
    {
        ZzzBackendResult<ZzzWorldPatrolRunRecordDto> result = _worldPatrolBackend.ResetWorldPatrolRunRecord(_instanceIndex);
        if (!result.Success || result.Value is null)
        {
            ShowSettingsError(result.Error ?? "锄大地运行记录重置失败。");
            return;
        }

        if (_catalog is not null)
        {
            _catalog = _catalog with { RunRecord = result.Value };
        }

        UpdateRunRecordDisplay(result.Value);
    }

    private void UpdateRunRecordDisplay(ZzzWorldPatrolRunRecordDto record)
    {
        double partial = record.RoutesPerRound > 0
            ? record.Finished.Count % record.RoutesPerRound / (double)record.RoutesPerRound
            : 0d;
        double progress = Math.Min(record.CompletedRounds + partial, _dailyLoopCount);
        _runRecordItem.Description = $"当日进度 {progress:F2}/{_dailyLoopCount}";
    }

    private void OnEditorRouteListChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _catalog is null || _editorRouteListCombo.SelectedItem is not ZzzWorldPatrolOption option)
        {
            return;
        }

        ZzzWorldPatrolRouteListDto? selected = _catalog.RouteLists.FirstOrDefault(item => Equals(item.Name, option.Value));
        _currentRouteList = selected is null
            ? null
            : new ZzzWorldPatrolRouteListDto(selected.Name, selected.ListType, selected.RouteItems.ToArray());
        Select(_listTypeCombo, _currentRouteList?.ListType ?? "whitelist");
        UpdateChosenRoutes();
        UpdateEditorButtons();
    }

    private void OnListTypeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _currentRouteList is not null && _listTypeCombo.SelectedItem is ZzzWorldPatrolOption option)
        {
            _currentRouteList = _currentRouteList with { ListType = Convert.ToString(option.Value, CultureInfo.InvariantCulture) ?? "whitelist" };
        }
    }

    private async void OnNewListClicked(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            ShowRouteListError("当前窗口不可用。");
            return;
        }

        TextBox input = new() { PlaceholderText = "请输入列表名称" };
        FAContentDialog dialog = new()
        {
            Title = "新建路线列表",
            Content = input,
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync(owner).ConfigureAwait(true) == FAContentDialogResult.Primary
            && !string.IsNullOrWhiteSpace(input.Text))
        {
            BeginNewList(input.Text);
        }
    }

    private void BeginNewList(string name)
    {
        if (_currentRouteList is not null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _currentRouteList = new ZzzWorldPatrolRouteListDto(name.Trim(), "whitelist", []);
        Select(_listTypeCombo, "whitelist");
        UpdateChosenRoutes();
        UpdateEditorButtons();
    }

    private void OnSaveListClicked(object? sender, RoutedEventArgs args) => SaveCurrentList();

    private void SaveCurrentList()
    {
        if (_currentRouteList is null)
        {
            return;
        }

        ZzzBackendResult<ZzzWorldPatrolCatalogDto> result = _worldPatrolBackend.SaveWorldPatrolRouteList(
            new ZzzSaveWorldPatrolRouteListRequest(
                _instanceIndex,
                _currentRouteList.Name,
                _currentRouteList.ListType,
                _currentRouteList.RouteItems));
        ApplyEditorResult(result, "保存路线列表失败。");
    }

    private void OnDeleteListClicked(object? sender, RoutedEventArgs args)
    {
        if (_currentRouteList is null)
        {
            return;
        }

        ApplyEditorResult(
            _worldPatrolBackend.DeleteWorldPatrolRouteList(_instanceIndex, _currentRouteList.Name),
            "删除路线列表失败。");
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs args)
    {
        _currentRouteList = null;
        _editorRouteListCombo.SelectedItem = null;
        UpdateChosenRoutes();
        UpdateEditorButtons();
    }

    private void ApplyEditorResult(ZzzBackendResult<ZzzWorldPatrolCatalogDto> result, string fallback)
    {
        if (!result.Success || result.Value is null)
        {
            ShowRouteListError(result.Error ?? fallback);
            return;
        }

        _catalog = result.Value;
        ApplyCatalog(result.Value);
        _routeListErrorBar.IsOpen = false;
        UpdateEditorButtons();
    }

    private void OnEntryChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_catalog is null || _entryCombo.SelectedItem is not ZzzWorldPatrolOption entry)
        {
            _areaCombo.ItemsSource = null;
            return;
        }

        _areaCombo.ItemsSource = _catalog.Areas
            .Where(area => Equals(area.EntryId, entry.Value))
            .Select(area => new ZzzWorldPatrolOption(area.Name, area.Id))
            .ToArray();
    }

    private void OnAreaChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_catalog is null || _areaCombo.SelectedItem is not ZzzWorldPatrolOption area)
        {
            _availableRouteList.ItemsSource = null;
        }
        else
        {
            _availableRouteList.ItemsSource = _catalog.Routes
                .Where(route => Equals(route.AreaId, area.Value))
                .Select(route => new ZzzWorldPatrolRouteOption(
                    $"{route.Index:00}. {route.TransportPoint} ({route.OperationCount}步)",
                    route.FullId))
                .ToArray();
        }

        UpdateEditorButtons();
    }

    private void OnAddAreaClicked(object? sender, RoutedEventArgs args)
    {
        if (_currentRouteList is null || _availableRouteList.ItemsSource is null)
        {
            return;
        }

        _currentRouteList = _currentRouteList with
        {
            RouteItems = _currentRouteList.RouteItems
                .Concat(_availableRouteList.ItemsSource.OfType<ZzzWorldPatrolRouteOption>().Select(route => route.FullId))
                .ToArray(),
        };
        UpdateChosenRoutes();
    }

    private void OnAddRouteClicked(object? sender, RoutedEventArgs args)
    {
        if (_currentRouteList is null || _availableRouteList.SelectedItem is not ZzzWorldPatrolRouteOption route)
        {
            return;
        }

        _currentRouteList = _currentRouteList with { RouteItems = _currentRouteList.RouteItems.Append(route.FullId).ToArray() };
        UpdateChosenRoutes();
    }

    private void OnMoveUpClicked(object? sender, RoutedEventArgs args) => MoveChosenRoute(-1);

    private void OnMoveDownClicked(object? sender, RoutedEventArgs args) => MoveChosenRoute(1);

    private void MoveChosenRoute(int delta)
    {
        if (_currentRouteList is null || _chosenRouteList.SelectedIndex < 0)
        {
            return;
        }

        int from = _chosenRouteList.SelectedIndex;
        int to = from + delta;
        if (to < 0 || to >= _currentRouteList.RouteItems.Count)
        {
            return;
        }

        List<string> items = _currentRouteList.RouteItems.ToList();
        (items[from], items[to]) = (items[to], items[from]);
        _currentRouteList = _currentRouteList with { RouteItems = items };
        UpdateChosenRoutes();
        _chosenRouteList.SelectedIndex = to;
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs args)
    {
        if (_currentRouteList is null || _chosenRouteList.SelectedIndex < 0)
        {
            return;
        }

        List<string> items = _currentRouteList.RouteItems.ToList();
        items.RemoveAt(_chosenRouteList.SelectedIndex);
        _currentRouteList = _currentRouteList with { RouteItems = items };
        UpdateChosenRoutes();
    }

    private void UpdateChosenRoutes()
    {
        if (_currentRouteList is null || _catalog is null)
        {
            _chosenRouteList.ItemsSource = null;
            return;
        }

        IReadOnlyDictionary<string, ZzzWorldPatrolRouteDto> routeMap = _catalog.Routes.ToDictionary(route => route.FullId, StringComparer.Ordinal);
        _chosenRouteList.ItemsSource = _currentRouteList.RouteItems
            .Where(routeMap.ContainsKey)
            .Select(id =>
            {
                ZzzWorldPatrolRouteDto route = routeMap[id];
                return new ZzzWorldPatrolRouteOption($"{route.AreaName} {route.Index}", id);
            })
            .ToArray();
    }

    private void OnRouteSelectionChanged(object? sender, SelectionChangedEventArgs args) => UpdateEditorButtons();

    private void OnAvailableRouteSelectionChanged(object? sender, SelectionChangedEventArgs args) => UpdateEditorButtons();

    private void UpdateEditorButtons()
    {
        bool hasList = _currentRouteList is not null;
        foreach (Button button in _requiresListButtons)
        {
            button.IsEnabled = hasList;
        }

        Required<Button>("NewListButton").IsEnabled = !hasList;
        Required<Button>("AddAreaButton").IsEnabled = hasList && _areaCombo.SelectedItem is not null;
        Required<Button>("AddRouteButton").IsEnabled = hasList && _availableRouteList.SelectedItem is not null;
        foreach (Button button in _requiresChosenRouteButtons)
        {
            button.IsEnabled = hasList && _chosenRouteList.SelectedIndex >= 0;
        }
    }

    private void OnLargeMapEntryChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_catalog is null || _largeMapEntryCombo.SelectedItem is not ZzzWorldPatrolOption entry)
        {
            _largeMapAreaCombo.ItemsSource = null;
            return;
        }

        string entryId = Convert.ToString(entry.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        _largeMapAreaCombo.ItemsSource = _catalog.Areas
            .Where(area => string.Equals(area.EntryId, entryId, StringComparison.Ordinal))
            .Select(area => new ZzzWorldPatrolOption(area.Name, area.Id))
            .ToArray();
    }

    private void OnLargeMapAreaChanged(object? sender, SelectionChangedEventArgs args) =>
        UpdateLargeMapButtons();

    private void OnLoadLargeMapClicked(object? sender, RoutedEventArgs args)
    {
        if (_largeMapAreaCombo.SelectedItem is not ZzzWorldPatrolOption area)
        {
            ShowLargeMapError("请先选择区域。");
            return;
        }

        ApplyLargeMapResult(
            _worldPatrolBackend.LoadWorldPatrolLargeMapRecorder(
                _instanceIndex,
                Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty),
            "大地图加载失败。");
    }

    private void OnSaveLargeMapClicked(object? sender, RoutedEventArgs args) =>
        ApplyLargeMapResult(
            _worldPatrolBackend.SaveWorldPatrolLargeMapRecorder(_instanceIndex),
            "大地图保存失败。");

    private void OnDeleteLargeMapClicked(object? sender, RoutedEventArgs args) =>
        ApplyLargeMapResult(
            _worldPatrolBackend.DeleteWorldPatrolLargeMapRecorder(_instanceIndex),
            "大地图删除失败。");

    private void OnCancelLargeMapClicked(object? sender, RoutedEventArgs args) =>
        ApplyLargeMapResult(
            _worldPatrolBackend.CancelWorldPatrolLargeMapRecorder(_instanceIndex),
            "大地图取消失败。");

    private async void OnCaptureLargeMapClicked(object? sender, RoutedEventArgs args) =>
        await CaptureLargeMapAsync().ConfigureAwait(true);

    private async Task CaptureLargeMapAsync()
    {
        if (_largeMapState?.IsLoaded != true)
        {
            return;
        }

        ApplyLargeMapResult(
            await _worldPatrolBackend.CaptureWorldPatrolLargeMapRecorderAsync(
                _instanceIndex,
                _appliedLargeMapIconThreshold).ConfigureAwait(true),
            "大地图截图失败。");
    }

    private void OnCalculateLargeMapPositionClicked(object? sender, RoutedEventArgs args) =>
        CalculateLargeMapPosition();

    private void CalculateLargeMapPosition()
    {
        if (_largeMapState?.IsLoaded != true)
        {
            return;
        }

        ApplyLargeMapResult(
            _worldPatrolBackend.CalculateWorldPatrolLargeMapRecorderPosition(
                _instanceIndex,
                Required<ToggleSwitch>("UseLargeMapIconsSwitch").IsChecked == true),
            "大地图定位失败。");
    }

    private void OnToggleLargeMapOverlapClicked(object? sender, RoutedEventArgs args) =>
        ToggleLargeMapOverlap();

    private void ToggleLargeMapOverlap()
    {
        if (_largeMapState?.IsLoaded != true)
        {
            return;
        }

        ApplyLargeMapResult(
            _worldPatrolBackend.ToggleWorldPatrolLargeMapRecorderOverlap(_instanceIndex),
            "大地图重叠显示切换失败。");
    }

    private void OnMergeLargeMapClicked(object? sender, RoutedEventArgs args) => MergeLargeMap();

    private void MergeLargeMap()
    {
        if (_largeMapState?.IsLoaded != true)
        {
            return;
        }

        ApplyLargeMapResult(
            _worldPatrolBackend.MergeWorldPatrolLargeMapRecorder(_instanceIndex),
            "大地图合并失败。");
    }

    private void OnUndoLargeMapClicked(object? sender, RoutedEventArgs args) =>
        ApplyLargeMapResult(
            _worldPatrolBackend.UndoWorldPatrolLargeMapRecorder(_instanceIndex),
            "大地图回退失败。");

    private void OnEditLargeMapIconsClicked(object? sender, RoutedEventArgs args)
    {
        if (_largeMapState?.HasLargeMap != true)
        {
            return;
        }

        if (_largeMapIconEditorWindow is not null)
        {
            _largeMapIconEditorWindow.Activate();
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        _largeMapIconEditorWindow = new FrontierWorldPatrolLargeMapIconEditorWindow(_largeMapState.Icons)
        {
            CurrentPositionRequested = () => _largeMapState?.CalculatedPosition ?? _largeMapState?.CurrentPosition,
        };
        _largeMapIconEditorWindow.IconSelected += OnLargeMapIconSelected;
        _largeMapIconEditorWindow.IconsSaved += OnLargeMapIconsSaved;
        _largeMapIconEditorWindow.Closed += OnLargeMapIconEditorClosed;
        _largeMapIconEditorWindow.Show(owner);
    }

    private void OnLargeMapIconSelected(int index)
    {
        ApplyLargeMapResult(
            _worldPatrolBackend.SelectWorldPatrolLargeMapRecorderIcon(_instanceIndex, index),
            "大地图图标选择失败。");
    }

    private void OnLargeMapIconsSaved(IReadOnlyList<ZzzWorldPatrolLargeMapIconDto> icons)
    {
        ApplyLargeMapResult(
            _worldPatrolBackend.UpdateWorldPatrolLargeMapRecorderIcons(_instanceIndex, icons),
            "大地图图标保存失败。");
    }

    private void OnLargeMapIconEditorClosed(object? sender, EventArgs args)
    {
        if (_largeMapIconEditorWindow is not null)
        {
            _largeMapIconEditorWindow.IconSelected -= OnLargeMapIconSelected;
            _largeMapIconEditorWindow.IconsSaved -= OnLargeMapIconsSaved;
            _largeMapIconEditorWindow.Closed -= OnLargeMapIconEditorClosed;
            _largeMapIconEditorWindow = null;
        }

        if (_largeMapState?.IsLoaded == true)
        {
            ApplyLargeMapResult(
                _worldPatrolBackend.SelectWorldPatrolLargeMapRecorderIcon(_instanceIndex, -1),
                "大地图图标高亮清除失败。");
        }
    }

    private void OnApplyLargeMapIconThresholdClicked(object? sender, RoutedEventArgs args)
    {
        _appliedLargeMapIconThreshold = _largeMapIconThresholdBox.Value;
        _largeMapStatusBar.Message = $"图标匹配阈值已更新为: {_appliedLargeMapIconThreshold:0.0}";
        _largeMapStatusBar.IsOpen = true;
        _largeMapLogCard.AppendLine(_largeMapStatusBar.Message);
    }

    private void OnApplyLargeMapScaleClicked(object? sender, RoutedEventArgs args) =>
        ApplyLargeMapResult(
            _worldPatrolBackend.ScaleWorldPatrolLargeMapRecorder(_instanceIndex, (int)_largeMapScaleBox.Value),
            "大地图缩放失败。");

    private void OnMoveLargeMapHorizontalClicked(object? sender, RoutedEventArgs args) =>
        MoveLargeMap((int)_largeMapHorizontalMoveBox.Value, 0);

    private void OnMoveLargeMapVerticalClicked(object? sender, RoutedEventArgs args) =>
        MoveLargeMap(0, (int)_largeMapVerticalMoveBox.Value);

    private void MoveLargeMap(int deltaX, int deltaY)
    {
        if (_largeMapState?.IsLoaded != true)
        {
            return;
        }

        ApplyLargeMapResult(
            _worldPatrolBackend.MoveWorldPatrolLargeMapRecorder(_instanceIndex, deltaX, deltaY),
            "大地图坐标移动失败。");
    }

    private void OnLargeMapViewerPointClicked(object? sender, FrontierWorldPatrolImagePointEventArgs args)
    {
        if (_largeMapState?.HasLargeMap != true)
        {
            return;
        }

        ZzzWorldPatrolLargeMapIconDto? selectedIcon = _largeMapState.Icons.FirstOrDefault(icon =>
        {
            int dx = icon.LargeMapPosition.X - args.X;
            int dy = icon.LargeMapPosition.Y - args.Y;
            return Math.Sqrt((dx * dx) + (dy * dy)) <= 20;
        });
        if (selectedIcon is not null)
        {
            int index = _largeMapState.Icons
                .Select((icon, iconIndex) => (icon, iconIndex))
                .First(item => ReferenceEquals(item.icon, selectedIcon) || item.icon == selectedIcon)
                .iconIndex;
            _largeMapIconEditorWindow?.HighlightIcon(index);
            if (_largeMapState.HighlightedIconIndex != index)
            {
                ApplyLargeMapResult(
                    _worldPatrolBackend.SelectWorldPatrolLargeMapRecorderIcon(_instanceIndex, index),
                    "大地图图标选择失败。");
            }

            _largeMapStatusBar.Message = $"[图标] 选中图标 {selectedIcon.TemplateId} 位置 ({selectedIcon.LargeMapPosition.X}, {selectedIcon.LargeMapPosition.Y})";
            _largeMapStatusBar.IsOpen = true;
            return;
        }

        ApplyLargeMapResult(
            _worldPatrolBackend.SetWorldPatrolLargeMapRecorderPosition(_instanceIndex, args.X, args.Y),
            "大地图坐标更新失败。");
    }

    private void ApplyLargeMapResult(
        ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> result,
        string fallback)
    {
        if (!result.Success || result.Value is null)
        {
            ShowLargeMapError(result.Error ?? fallback);
            return;
        }

        _largeMapErrorBar.IsOpen = false;
        ApplyLargeMapState(result.Value);
    }

    private void ApplyLargeMapState(ZzzWorldPatrolLargeMapRecorderStateDto state)
    {
        _largeMapState = state;
        _loadLargeMapButton.Label = state.IsLoaded ? "重置" : "加载";
        _saveLargeMapButton.IsEnabled = state.IsLoaded;
        _deleteLargeMapButton.IsEnabled = state.IsLoaded;
        _cancelLargeMapButton.IsEnabled = state.IsLoaded;
        _editLargeMapIconsButton.IsEnabled = state.HasLargeMap;

        ZzzWorldPatrolRoutePositionDto? position = state.CalculatedPosition ?? state.CurrentPosition;
        _largeMapPositionText.Text = position is null ? string.Empty : $"({position.X}, {position.Y})";
        ApplyLargeMapImage(_largeMapMiniMap1Image, state.MiniMap1?.Bytes);
        ApplyLargeMapImage(_largeMapMiniMap2Image, state.MiniMap2?.Bytes);
        ApplyLargeMapImage(_largeMapMiniMapMergedImage, state.MiniMapMerged?.Bytes);
        _largeMapViewer.SetImage(state.LargeMap?.Bytes);
        _largeMapStatusBar.Message = state.Status;
        _largeMapStatusBar.IsOpen = !string.IsNullOrWhiteSpace(state.Status);
        if (!string.IsNullOrWhiteSpace(state.Status))
        {
            _largeMapLogCard.AppendLine(state.Status);
        }
        UpdateLargeMapButtons();
    }

    private static void ApplyLargeMapImage(Image image, byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            image.Source = null;
            return;
        }

        ZzzDevtoolsImageLoader.TryLoadBitmap(image, bytes);
    }

    private void UpdateLargeMapButtons()
    {
        _loadLargeMapButton.IsEnabled = _largeMapAreaCombo.SelectedItem is not null;
        bool loaded = _largeMapState?.IsLoaded == true;
        _saveLargeMapButton.IsEnabled = loaded;
        _deleteLargeMapButton.IsEnabled = loaded;
        _cancelLargeMapButton.IsEnabled = loaded;
        _editLargeMapIconsButton.IsEnabled = _largeMapState?.HasLargeMap == true;
    }

    private void OnRouteEntryChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_catalog is null || _routeEntryCombo.SelectedItem is not ZzzWorldPatrolOption entry)
        {
            _routeAreaCombo.ItemsSource = null;
            return;
        }

        _routeAreaCombo.ItemsSource = _catalog.Areas
            .Where(area => Equals(area.EntryId, entry.Value))
            .Select(area => new ZzzWorldPatrolOption(area.Name, area.Id))
            .ToArray();
    }

    private void OnRouteAreaChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_catalog is null || _routeAreaCombo.SelectedItem is not ZzzWorldPatrolOption area)
        {
            _routeCombo.ItemsSource = null;
            _transportPointCombo.ItemsSource = null;
            UpdateRouteEditorButtons();
            return;
        }

        string areaId = Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        _routeCombo.ItemsSource = _catalog.Routes
            .Where(route => string.Equals(route.AreaId, areaId, StringComparison.Ordinal))
            .Select(route => new ZzzWorldPatrolRouteEditorOption(
                $"{route.Index:00} - {route.TransportPoint} ({route.OperationCount}步)",
                route))
            .ToArray();
        _transportPointCombo.ItemsSource = _catalog.TransportPoints
            .Where(point => string.Equals(point.AreaId, areaId, StringComparison.Ordinal))
            .Select(point => new ZzzWorldPatrolOption(point.Name, point.Name))
            .ToArray();
        RenderRouteRecorder();
        UpdateRouteEditorButtons();
    }

    private void OnRouteChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_routeCombo.SelectedItem is not ZzzWorldPatrolRouteEditorOption option)
        {
            return;
        }

        LoadRoute(option.Route);
    }

    private void LoadRoute(ZzzWorldPatrolRouteDto route)
    {
        _currentRoute = route;
        _calculatedPosition = null;
        Select(_transportPointCombo, route.TransportPoint);
        _routeOperations = route.Operations
            .Select((operation, index) => new ZzzWorldPatrolEditableOperation
            {
                Index = index,
                OpType = operation.OpType,
                Data1 = operation.Data.ElementAtOrDefault(0) ?? string.Empty,
                Data2 = operation.Data.ElementAtOrDefault(1) ?? string.Empty,
            })
            .ToList();
        RefreshRouteOperations();
        _debugStartBox.Maximum = _routeOperations.Count;
        RenderRouteRecorder();
        UpdateRouteEditorButtons();
    }

    private void OnRouteEditorSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(sender, _transportPointCombo))
        {
            RenderRouteRecorder();
        }

        UpdateRouteEditorButtons();
    }

    private void OnNewRouteClicked(object? sender, RoutedEventArgs args)
    {
        if (_routeAreaCombo.SelectedItem is not ZzzWorldPatrolOption area
            || _transportPointCombo.SelectedItem is not ZzzWorldPatrolOption transportPoint)
        {
            ShowRouteEditorError("请先选择区域和真实传送点。");
            return;
        }

        _currentRoute = new ZzzWorldPatrolRouteDto(
            string.Empty,
            string.Empty,
            Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            area.Label,
            0,
            Convert.ToString(transportPoint.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            [],
            SelectedTransportPoint()?.Position);
        _calculatedPosition = null;
        _routeOperations = [];
        RefreshRouteOperations();
        RenderRouteRecorder();
        UpdateRouteEditorButtons();
    }

    private void OnSaveRouteClicked(object? sender, RoutedEventArgs args) => SaveCurrentRoute();

    private void SaveCurrentRoute()
    {
        if (_currentRoute is null || _routeAreaCombo.SelectedItem is not ZzzWorldPatrolOption area
            || _transportPointCombo.SelectedItem is not ZzzWorldPatrolOption transportPoint)
        {
            return;
        }

        ZzzBackendResult<ZzzWorldPatrolCatalogDto> result = _worldPatrolBackend.SaveWorldPatrolRoute(
            new ZzzSaveWorldPatrolRouteRequest(
                _instanceIndex,
                string.IsNullOrWhiteSpace(_currentRoute.FullId) ? null : _currentRoute.FullId,
                Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                _currentRoute.Index,
                Convert.ToString(transportPoint.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                _routeOperations.Select(operation => new ZzzWorldPatrolOperationDto(
                    operation.OpType,
                    [operation.Data1, operation.Data2])).ToArray()));
        ApplyRouteEditorResult(result, "保存路线失败。");
    }

    private void OnDeleteRouteClicked(object? sender, RoutedEventArgs args)
    {
        if (_currentRoute is null || string.IsNullOrWhiteSpace(_currentRoute.FullId))
        {
            return;
        }

        ApplyRouteEditorResult(
            _worldPatrolBackend.DeleteWorldPatrolRoute(_instanceIndex, _currentRoute.FullId),
            "删除路线失败。");
    }

    private void OnCancelRouteClicked(object? sender, RoutedEventArgs args)
    {
        _currentRoute = null;
        _calculatedPosition = null;
        _routeCombo.SelectedItem = null;
        _routeOperations = [];
        RefreshRouteOperations();
        RenderRouteRecorder();
        UpdateRouteEditorButtons();
    }

    private async void OnEditOperationsClicked(object? sender, RoutedEventArgs args)
    {
        if (_currentRoute is null)
        {
            return;
        }

        _routeOperationErrorBar.IsOpen = false;
        _routeOperationDraft = CloneOperations(_routeOperations);
        RefreshRouteOperationDraft();
        await _routeOperationDialog.ShowAsync(TopLevel.GetTopLevel(this)).ConfigureAwait(true);
        _routeOperationDraft = [];
        RefreshRouteOperations();
        UpdateRouteEditorButtons();
    }

    private void OnSaveRouteOperationsClicked(FAContentDialog sender, FAContentDialogButtonClickEventArgs args)
    {
        for (int index = 0; index < _routeOperationDraft.Count; index++)
        {
            ZzzWorldPatrolEditableOperation operation = _routeOperationDraft[index];
            if (!double.TryParse(operation.Data1, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                || !double.TryParse(operation.Data2, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                _routeOperationErrorBar.Message = $"第{index}个操作的坐标数据必须是数字";
                _routeOperationErrorBar.IsOpen = true;
                args.Cancel = true;
                return;
            }
        }

        _routeOperations = CloneOperations(_routeOperationDraft);
        RefreshRouteOperations();
        RenderRouteRecorder();
        UpdateRouteEditorButtons();
        _routeRecorderLogCard.AppendLine($"路线操作已更新，当前操作数: {_routeOperations.Count}");
    }

    private async void OnDebugRouteClicked(object? sender, RoutedEventArgs args)
    {
        if (_currentRoute is null || string.IsNullOrWhiteSpace(_currentRoute.FullId))
        {
            return;
        }

        ZzzBackendResult<ZzzWorldPatrolRouteDebugDto> result = await _worldPatrolBackend.DebugWorldPatrolRouteAsync(
            new ZzzDebugWorldPatrolRouteRequest(
                _instanceIndex,
                _groupId,
                _currentRoute.FullId,
                (int)_debugStartBox.Value)).ConfigureAwait(true);
        if (!result.Success)
        {
            ShowRouteEditorError(result.Error ?? "路线调试失败。");
        }
    }

    private void OnAddOperationClicked(object? sender, RoutedEventArgs args)
    {
        _routeOperationDraft.Add(new ZzzWorldPatrolEditableOperation
        {
            Index = _routeOperationDraft.Count,
            OpType = "move",
            Data1 = "0",
            Data2 = "0",
        });
        RefreshRouteOperationDraft();
        UpdateRouteEditorButtons();
    }

    private async void OnDeleteOperationClicked(object? sender, RoutedEventArgs args)
    {
        int index = _routeOperationList.SelectedIndex;
        if (index < 0 || index >= _routeOperationDraft.Count)
        {
            return;
        }

        _deleteRouteOperationDialog.Content = $"确定要删除第{index}个操作吗？";
        if (await _deleteRouteOperationDialog.ShowAsync(TopLevel.GetTopLevel(this)).ConfigureAwait(true)
            != FAContentDialogResult.Primary)
        {
            return;
        }

        _routeOperationDraft.RemoveAt(index);
        ReindexRouteOperationDraft();
    }

    private void OnMoveOperationUpClicked(object? sender, RoutedEventArgs args) => MoveOperation(-1);

    private void OnMoveOperationDownClicked(object? sender, RoutedEventArgs args) => MoveOperation(1);

    private void MoveOperation(int delta)
    {
        int from = _routeOperationList.SelectedIndex;
        int to = from + delta;
        if (from < 0 || to < 0 || to >= _routeOperationDraft.Count)
        {
            return;
        }

        (_routeOperationDraft[from], _routeOperationDraft[to]) = (_routeOperationDraft[to], _routeOperationDraft[from]);
        ReindexRouteOperationDraft();
        _routeOperationList.SelectedIndex = to;
    }

    private void ReindexRouteOperationDraft()
    {
        _routeOperationDraft = _routeOperationDraft.Select((operation, index) => new ZzzWorldPatrolEditableOperation
        {
            Index = index,
            OpType = operation.OpType,
            Data1 = operation.Data1,
            Data2 = operation.Data2,
        }).ToList();
        RefreshRouteOperationDraft();
        UpdateRouteEditorButtons();
    }

    private void RefreshRouteOperationDraft(int selectedIndex = -1)
    {
        _routeOperationList.ItemsSource = null;
        _routeOperationList.ItemsSource = _routeOperationDraft;
        _routeOperationList.SelectedIndex = selectedIndex;
    }

    private static List<ZzzWorldPatrolEditableOperation> CloneOperations(
        IEnumerable<ZzzWorldPatrolEditableOperation> operations) => operations
        .Select((operation, index) => new ZzzWorldPatrolEditableOperation
        {
            Index = index,
            OpType = operation.OpType,
            Data1 = operation.Data1,
            Data2 = operation.Data2,
        })
        .ToList();

    private void ReindexOperations()
    {
        _routeOperations = _routeOperations.Select((operation, index) => new ZzzWorldPatrolEditableOperation
        {
            Index = index,
            OpType = operation.OpType,
            Data1 = operation.Data1,
            Data2 = operation.Data2,
        }).ToList();
        RefreshRouteOperations();
        RenderRouteRecorder();
        UpdateRouteEditorButtons();
    }

    private void RefreshRouteOperations()
    {
        _routeOperationList.ItemsSource = null;
        _routeOperationList.ItemsSource = _routeOperations;
        _debugStartBox.Maximum = _routeOperations.Count;
    }

    private void OnSettingsTabSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        UpdateLargeMapHotkeySubscription();
        UpdateRouteHotkeySubscription();
    }

    private void UpdateLargeMapHotkeySubscription()
    {
        bool shouldListen = _pageShown && _settingsTabView.SelectedIndex == 2;
        if (shouldListen && _largeMapHotkeySubscription is null)
        {
            _largeMapHotkeySubscription = ScreenshotHelperGlobalInputSource.Subscribe(HandleLargeMapRecorderKey);
            _largeMapLogCard.OnPageShown();
        }
        else if (!shouldListen)
        {
            DisposeLargeMapHotkeySubscription();
        }
    }

    private void DisposeLargeMapHotkeySubscription()
    {
        _largeMapHotkeySubscription?.Dispose();
        _largeMapHotkeySubscription = null;
        _largeMapLogCard.OnPageHidden();
    }

    private bool HandleLargeMapRecorderKey(string key)
    {
        Action? action = key switch
        {
            "1" => () => _ = CaptureLargeMapAsync(),
            "2" => CalculateLargeMapPosition,
            "3" => ToggleLargeMapOverlap,
            "4" => MergeLargeMap,
            _ => null,
        };
        if (action is null)
        {
            return false;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }

        return true;
    }

    private void UpdateRouteHotkeySubscription()
    {
        bool shouldListen = _pageShown && _settingsTabView.SelectedIndex == 3;
        if (shouldListen && _routeHotkeySubscription is null)
        {
            _routeHotkeySubscription = ScreenshotHelperGlobalInputSource.Subscribe(HandleRouteRecorderKey);
            _routeRecorderLogCard.OnPageShown();
        }
        else if (!shouldListen)
        {
            DisposeRouteHotkeySubscription();
        }
    }

    private void DisposeRouteHotkeySubscription()
    {
        _routeHotkeySubscription?.Dispose();
        _routeHotkeySubscription = null;
        _routeRecorderLogCard.OnPageHidden();
    }

    private bool HandleRouteRecorderKey(string key)
    {
        Action? action = key switch
        {
            "1" => CaptureAndAppendMove,
            "2" => null,
            "3" => null,
            "4" => () => AddMoveOperation(clearCalculatedPosition: true),
            "5" => UndoLastOperation,
            _ => null,
        };
        if (action is null)
        {
            return false;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }

        return true;
    }

    private void OnCapturePositionClicked(object? sender, RoutedEventArgs args) => CaptureAndAppendMove();

    private void CaptureAndAppendMove()
    {
        if (_currentRoute is null
            || _routeAreaCombo.SelectedItem is not ZzzWorldPatrolOption area
            || _transportPointCombo.SelectedItem is not ZzzWorldPatrolOption transportPoint)
        {
            ShowRouteEditorError("请先选择正在编辑的路线。");
            return;
        }

        ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> result = _worldPatrolBackend.CaptureWorldPatrolRoutePosition(
            new ZzzCaptureWorldPatrolRoutePositionRequest(
                Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(transportPoint.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                BuildOperationDtos()));
        if (!result.Success || result.Value is null)
        {
            ShowRouteEditorError(result.Error ?? "当前游戏截图定位失败。");
            return;
        }

        _calculatedPosition = result.Value;
        ApplyLargeMapImage(_routeMiniMapRoadImage, result.Value.MiniMapRoad?.Bytes);
        _routeMapViewer.SetImage(result.Value.RouteMap?.Bytes);
        AppendMove(result.Value);
        _routeEditorErrorBar.IsOpen = false;
    }

    private void OnAddMoveClicked(object? sender, RoutedEventArgs args) =>
        AddMoveOperation(clearCalculatedPosition: true);

    private void AddMoveOperation(bool clearCalculatedPosition)
    {
        if (_currentRoute is null)
        {
            ShowRouteEditorError("请先新建或选择路线。");
            return;
        }

        ZzzWorldPatrolRoutePositionDto? position = _calculatedPosition ?? ResolveCurrentPosition();
        if (position is null)
        {
            ShowRouteEditorError("当前坐标不可用，请先截图定位。");
            return;
        }

        AppendMove(position);
        if (clearCalculatedPosition)
        {
            _calculatedPosition = null;
        }
    }

    private void AppendMove(ZzzWorldPatrolRoutePositionDto position)
    {
        _routeOperations.Add(new ZzzWorldPatrolEditableOperation
        {
            Index = _routeOperations.Count,
            OpType = "move",
            Data1 = position.X.ToString(CultureInfo.InvariantCulture),
            Data2 = position.Y.ToString(CultureInfo.InvariantCulture),
        });
        RefreshRouteOperations();
        RenderRouteRecorder();
        UpdateRouteEditorButtons();
    }

    private void OnUndoMoveClicked(object? sender, RoutedEventArgs args) => UndoLastOperation();

    private void UndoLastOperation()
    {
        if (_currentRoute is null || _routeOperations.Count == 0)
        {
            return;
        }

        _routeOperations.RemoveAt(_routeOperations.Count - 1);
        _calculatedPosition = null;
        ReindexOperations();
    }

    private ZzzWorldPatrolRoutePositionDto? ResolveCurrentPosition()
    {
        for (int index = _routeOperations.Count - 1; index >= 0; index--)
        {
            ZzzWorldPatrolEditableOperation operation = _routeOperations[index];
            if (string.Equals(operation.OpType, "move", StringComparison.Ordinal)
                && int.TryParse(operation.Data1, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                && int.TryParse(operation.Data2, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                return new ZzzWorldPatrolRoutePositionDto(x, y);
            }
        }

        return _currentRoute?.LastPosition ?? SelectedTransportPoint()?.Position;
    }

    private ZzzWorldPatrolTransportPointDto? SelectedTransportPoint()
    {
        if (_catalog is null
            || _routeAreaCombo.SelectedItem is not ZzzWorldPatrolOption area
            || _transportPointCombo.SelectedItem is not ZzzWorldPatrolOption transportPoint)
        {
            return null;
        }

        string areaId = Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        string name = Convert.ToString(transportPoint.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        return _catalog.TransportPoints.FirstOrDefault(point =>
            string.Equals(point.AreaId, areaId, StringComparison.Ordinal)
            && string.Equals(point.Name, name, StringComparison.Ordinal));
    }

    private ZzzWorldPatrolOperationDto[] BuildOperationDtos() =>
        _routeOperations.Select(operation => new ZzzWorldPatrolOperationDto(
            operation.OpType,
            [operation.Data1, operation.Data2])).ToArray();

    private void OnRouteMapViewerPointClicked(object? sender, FrontierWorldPatrolImagePointEventArgs args)
    {
        if (_currentRoute is null
            || _routeAreaCombo.SelectedItem is not ZzzWorldPatrolOption area
            || _transportPointCombo.SelectedItem is not ZzzWorldPatrolOption transportPoint)
        {
            return;
        }

        ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> result =
            _worldPatrolBackend.ConvertWorldPatrolRouteRecorderClick(
                new ZzzWorldPatrolRouteMapClickRequest(
                    Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(transportPoint.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                    BuildOperationDtos(),
                    args.DisplayX,
                    args.DisplayY,
                    args.DisplayWidth,
                    args.DisplayHeight));
        if (!result.Success || result.Value is null)
        {
            ShowRouteEditorError(result.Error ?? "路线地图点击坐标换算失败。");
            return;
        }

        _calculatedPosition = result.Value;
        if (_autoAddRouteClickSwitch.IsChecked == true)
        {
            AppendMove(result.Value);
        }
        else
        {
            RenderRouteRecorder();
        }

        _routeRecorderLogCard.AppendLine("[位置] 更新为点击位置");
    }

    private void RenderRouteRecorder()
    {
        if (_routeAreaCombo.SelectedItem is not ZzzWorldPatrolOption area)
        {
            _routeMapAvailable = false;
            _routeMapViewer.SetImage(null);
            UpdateRouteEditorButtons();
            return;
        }

        string transportPoint = _transportPointCombo.SelectedItem is ZzzWorldPatrolOption point
            ? Convert.ToString(point.Value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
        ZzzBackendResult<ZzzWorldPatrolRouteVisualDto> result =
            _worldPatrolBackend.RenderWorldPatrolRouteRecorder(
                new ZzzWorldPatrolRouteVisualRequest(
                    Convert.ToString(area.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                    transportPoint,
                    BuildOperationDtos()));
        if (!result.Success || result.Value is null)
        {
            _routeMapAvailable = false;
            ShowRouteEditorError(result.Error ?? "路线大地图渲染失败。");
            UpdateRouteEditorButtons();
            return;
        }

        _routeMapAvailable = true;
        _routeMapViewer.SetImage(result.Value.LargeMap.Bytes);
        _routeEditorErrorBar.IsOpen = false;
        UpdateRouteEditorButtons();
    }

    private void ApplyRouteEditorResult(ZzzBackendResult<ZzzWorldPatrolCatalogDto> result, string fallback)
    {
        if (!result.Success || result.Value is null)
        {
            ShowRouteEditorError(result.Error ?? fallback);
            return;
        }

        _catalog = result.Value;
        ApplyCatalog(result.Value);
        _routeEditorErrorBar.IsOpen = false;
        UpdateRouteEditorButtons();
    }

    private void UpdateRouteEditorButtons()
    {
        bool hasArea = _routeAreaCombo.SelectedItem is not null;
        bool hasTransportPoint = _transportPointCombo.SelectedItem is not null;
        bool hasRoute = _currentRoute is not null;
        bool savedRoute = hasRoute && !string.IsNullOrWhiteSpace(_currentRoute!.FullId);
        Required<Button>("NewRouteButton").IsEnabled = hasArea && hasTransportPoint && !hasRoute;
        Required<Button>("SaveRouteButton").IsEnabled = hasRoute;
        Required<Button>("DeleteRouteButton").IsEnabled = savedRoute;
        Required<Button>("CancelRouteButton").IsEnabled = hasRoute;
        Required<Button>("EditOperationsButton").IsEnabled = hasRoute;
        Required<Button>("DebugRouteButton").IsEnabled = savedRoute;
        Required<Button>("AddOperationButton").IsEnabled = hasRoute;
        Required<Button>("CapturePositionButton").IsEnabled = hasRoute && hasArea && hasTransportPoint && _routeMapAvailable;
        Required<Button>("AddMoveButton").IsEnabled = hasRoute;
        Required<Button>("UndoMoveButton").IsEnabled = hasRoute && _routeOperations.Count > 0;
        bool operationSelected = _routeOperationList.SelectedIndex >= 0;
        Required<Button>("DeleteOperationButton").IsEnabled = hasRoute && operationSelected;
        Required<Button>("MoveOperationUpButton").IsEnabled = hasRoute && operationSelected;
        Required<Button>("MoveOperationDownButton").IsEnabled = hasRoute && operationSelected;
    }

    private void ShowSettingsError(string message)
    {
        _settingsErrorBar.Message = message;
        _settingsErrorBar.IsOpen = true;
    }

    private void ShowRouteListError(string message)
    {
        _routeListErrorBar.Message = message;
        _routeListErrorBar.IsOpen = true;
    }

    private void ShowLargeMapError(string message)
    {
        _largeMapErrorBar.Message = message;
        _largeMapErrorBar.IsOpen = true;
        _largeMapLogCard.AppendLine(message);
    }

    private void ShowRouteEditorError(string message)
    {
        _routeEditorErrorBar.Message = message;
        _routeEditorErrorBar.IsOpen = true;
    }

    private static void Select(SelectingItemsControl combo, object value)
    {
        combo.SelectedItem = combo.ItemsSource?.OfType<ZzzWorldPatrolOption>()
            .FirstOrDefault(option => Equals(option.Value, value));
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"锄大地配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"锄大地配置缺少 {key}。");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"锄大地设置缺少 {name}。");
}
