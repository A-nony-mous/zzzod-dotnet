using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using System.Globalization;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Application.CommissionAssistant;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.PageModels.GameAssistant;
using ZzzOd.Gui.Services.Config;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.GameAssistant;

internal sealed record ZzzGameAssistantBindingSpec(string Label, string Scope, string Key, string? GroupId = null);

internal sealed record ZzzGameAssistantPageModel(
    string Key,
    string Title,
    IReadOnlyList<string> ModeHeaders,
    IReadOnlyList<string> SettingLabels,
    IReadOnlyList<ZzzGameAssistantBindingSpec> Bindings,
    IReadOnlyList<string> RunAppIds,
    IReadOnlyList<string> StatusLabels,
    string SelectedAppId);

internal sealed record ZzzBattleAssistantStateRowModel(
    string StateName,
    string TriggerSecondsText,
    string ValueText,
    IBrush? StateBackground,
    IBrush? TriggerBackground,
    IBrush? ValueBackground);

internal sealed partial class FrontierBattleAssistantPage : UserControl, IZzzPageLifecycle
{
    private readonly IZzzAppBackend _backend;
    private readonly FrontierBattleAssistantSettings _settings;
    private readonly Grid _taskDisplay;
    private readonly TextBlock _taskTriggerValue;
    private readonly TextBlock _taskExpressionValue;
    private readonly TextBlock _taskDurationValue;
    private readonly TextBox _battleStateFilter;
    private readonly ItemsControl _battleStateRows;
    private readonly FAInfoBar _runtimeErrorBar;
    private readonly DispatcherTimer _refreshTimer;
    private IReadOnlyList<ZzzBattleAssistantStateRowModel> _latestStateRows = [];
    private IReadOnlyDictionary<string, ZzzBattleAssistantStateDto> _previousStates = new Dictionary<string, ZzzBattleAssistantStateDto>();
    private readonly Dictionary<string, List<double>> _stateTriggerHistory = new(StringComparer.Ordinal);
    private readonly Action _operationLoadedCallback;
    private bool _runtimeEventsSubscribed;
    private bool _isShown;

    public FrontierBattleAssistantPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
        : this(new FrontierBattleAssistantSettings(backend), backend, runIntent)
    {
    }

    private FrontierBattleAssistantPage(FrontierBattleAssistantSettings settings, IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _backend = backend;
        _settings = settings;
        RunPanel = new ZzzRunPanel(backend, title: "战斗助手运行", runIntent: runIntent, appIdProvider: () => settings.SelectedAppId);
        AvaloniaXamlLoader.Load(this);
        ContentControl settingsHost = this.FindControl<ContentControl>("SettingsHost")
            ?? throw new InvalidOperationException("战斗助手缺少设置区域。");
        ContentControl runHost = this.FindControl<ContentControl>("RunHost")
            ?? throw new InvalidOperationException("战斗助手缺少运行区域。");
        _taskDisplay = this.FindControl<Grid>("TaskDisplay")
            ?? throw new InvalidOperationException("战斗助手缺少 TaskDisplay?");
        _taskTriggerValue = this.FindControl<TextBlock>("TaskTriggerValue")
            ?? throw new InvalidOperationException("战斗助手缺少触发器显示。");
        _taskExpressionValue = this.FindControl<TextBlock>("TaskExpressionValue")
            ?? throw new InvalidOperationException("战斗助手缺少条件集显示。");
        _taskDurationValue = this.FindControl<TextBlock>("TaskDurationValue")
            ?? throw new InvalidOperationException("战斗助手缺少持续时间显示。");
        _battleStateFilter = this.FindControl<TextBox>("BattleStateFilter")
            ?? throw new InvalidOperationException("战斗助手缺少状态过滤输入框。");
        _battleStateRows = this.FindControl<ItemsControl>("BattleStateRows")
            ?? throw new InvalidOperationException("战斗助手缺少状态行列表。");
        _runtimeErrorBar = this.FindControl<FAInfoBar>("RuntimeErrorBar")
            ?? throw new InvalidOperationException("战斗助手缺少运行状态错误 InfoBar?");
        settingsHost.Content = settings;
        runHost.Content = RunPanel;
        settings.SelectedAppIdChanged += OnSelectedAppIdChanged;
        _battleStateFilter.TextChanged += OnBattleStateFilterChanged;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _operationLoadedCallback = OnOperationLoaded;
        ApplyModeVisibility();
    }

    public Control LeftContent => _settings;

    public ZzzRunPanel RunPanel { get; }

    public bool IsTaskDisplayVisible => _taskDisplay.IsVisible;

    public bool IsRuntimeRefreshActive => _refreshTimer.IsEnabled;

    public string TaskTriggerText => _taskTriggerValue.Text ?? string.Empty;

    public string TaskExpressionText => _taskExpressionValue.Text ?? string.Empty;

    public string TaskDurationText => _taskDurationValue.Text ?? string.Empty;

    public IReadOnlyList<ZzzBattleAssistantStateRowModel> DisplayedStateRows =>
        (_battleStateRows.ItemsSource as IEnumerable<ZzzBattleAssistantStateRowModel>)?.ToArray() ?? [];

    public void SetBattleStateFilter(string text)
    {
        _battleStateFilter.Text = text;
        ApplyStateFilter();
    }

    public void OnPageShown()
    {
        _isShown = true;
        _settings.OnPageShown();
        RunPanel.OnPageShown();
        StartRuntimeEvents();
        RefreshRuntimeState();
        ApplyModeVisibility();
    }

    public void OnPageHidden()
    {
        _isShown = false;
        _refreshTimer.Stop();
        StopRuntimeEvents();
        _settings.OnPageHidden();
        RunPanel.OnPageHidden();
    }

    public void OnPageLeave()
    {
        _isShown = false;
        _refreshTimer.Stop();
        StopRuntimeEvents();
        _settings.OnPageLeave();
        RunPanel.OnPageLeave();
    }

    public void DisposePage()
    {
        _isShown = false;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        StopRuntimeEvents();
        _settings.SelectedAppIdChanged -= OnSelectedAppIdChanged;
        _battleStateFilter.TextChanged -= OnBattleStateFilterChanged;
        _settings.DisposePage();
        RunPanel.DisposePage();
    }

    public void RefreshRuntimeState()
    {
        ZzzBackendResult<ZzzBattleAssistantRuntimeDto> result = _backend.GetBattleAssistantRuntime();
        if (!result.Success || result.Value is null)
        {
            _runtimeErrorBar.Message = result.Error ?? "战斗助手运行状态读取失败。";
            _runtimeErrorBar.IsOpen = true;
            _refreshTimer.Stop();
            return;
        }

        _runtimeErrorBar.IsOpen = false;
        ZzzBattleAssistantRuntimeDto runtime = result.Value;
        if (!runtime.IsRunning)
        {
            ResetTaskDisplay();
            _latestStateRows = [];
            ApplyStateFilter();
            _refreshTimer.Stop();
            return;
        }

        if (runtime.TriggerDisplay is not null
            && runtime.ExpressionDisplay is not null
            && runtime.ExecutionDurationSeconds is not null)
        {
            _taskTriggerValue.Text = runtime.TriggerDisplay;
            _taskExpressionValue.Text = runtime.ExpressionDisplay;
            _taskDurationValue.Text = Math.Round(runtime.ExecutionDurationSeconds.Value, 4)
                .ToString("0.####", CultureInfo.InvariantCulture);
        }

        _latestStateRows = BuildStateRows(runtime.States);
        ApplyStateFilter();
        if (_isShown && !_refreshTimer.IsEnabled)
        {
            _refreshTimer.Start();
        }
    }

    private void OnSelectedAppIdChanged(object? sender, EventArgs args) => ApplyModeVisibility();

    private void ApplyModeVisibility() =>
        _taskDisplay.IsVisible = string.Equals(_settings.SelectedAppId, ZzzApplicationIds.AutoBattle, StringComparison.Ordinal);

    private void OnRefreshTimerTick(object? sender, EventArgs args) => RefreshRuntimeState();

    private void OnBattleStateFilterChanged(object? sender, TextChangedEventArgs args) => ApplyStateFilter();

    private void ApplyStateFilter()
    {
        string[] keywords = (_battleStateFilter.Text ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _battleStateRows.ItemsSource = _latestStateRows
            .OrderBy(row => keywords.Length > 0 && keywords.Any(keyword => row.StateName.Contains(keyword, StringComparison.Ordinal)) ? 0 : 1)
            .ThenBy(row => row.StateName, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<ZzzBattleAssistantStateRowModel> BuildStateRows(IReadOnlyList<ZzzBattleAssistantStateDto> states)
    {
        IBrush[] palette = CreateStatePalette();
        var rows = new List<ZzzBattleAssistantStateRowModel>(states.Count);
        foreach (ZzzBattleAssistantStateDto state in states)
        {
            _previousStates.TryGetValue(state.StateName, out ZzzBattleAssistantStateDto? previous);
            bool triggerChanged = previous is null || previous.TriggerTime != state.TriggerTime;
            IBrush? triggerBackground = triggerChanged ? GetTriggerBackground(state, palette) : null;
            IBrush? valueBackground = previous is null || previous.Value != state.Value
                ? triggerBackground ?? GetTriggerBackground(state, palette)
                : null;
            rows.Add(new ZzzBattleAssistantStateRowModel(
                state.StateName,
                state.TriggerSecondsText,
                state.ValueText,
                previous is null ? palette[3] : null,
                triggerBackground,
                valueBackground));
        }

        _previousStates = states.ToDictionary(state => state.StateName, StringComparer.Ordinal);
        return rows;
    }

    private IBrush GetTriggerBackground(ZzzBattleAssistantStateDto state, IReadOnlyList<IBrush> palette)
    {
        if (!_stateTriggerHistory.TryGetValue(state.StateName, out List<double>? history))
        {
            history = [];
            _stateTriggerHistory[state.StateName] = history;
        }

        if (history.Count == 0 || history[^1] != state.TriggerTime)
        {
            history.Add(state.TriggerTime);
            if (history.Count > 5)
            {
                history.RemoveAt(0);
            }
        }

        int rank = history.AsEnumerable().Reverse().ToList().FindIndex(time => time == state.TriggerTime);
        return palette[Math.Clamp(rank < 0 ? 4 : rank, 0, 4)];
    }

    private IBrush[] CreateStatePalette()
    {
        var theme = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Default
            && Avalonia.Application.Current is { } application
                ? application.ActualThemeVariant
                : ActualThemeVariant;
        string[] keys =
        [
            "ZzzBattleStateRecentBrush1",
            "ZzzBattleStateRecentBrush2",
            "ZzzBattleStateRecentBrush3",
            "ZzzBattleStateRecentBrush4",
            "ZzzBattleStateRecentBrush5",
        ];
        return keys.Select(key =>
        {
            if ((TryGetResource(key, theme, out object? resource)
                    || Avalonia.Application.Current?.TryGetResource(key, theme, out resource) == true)
                && resource is IBrush brush)
            {
                return brush;
            }

            throw new InvalidOperationException($"缺少战斗状态画刷令牌: {key}");
        }).ToArray();
    }

    private void ResetTaskDisplay()
    {
        _taskTriggerValue.Text = "/";
        _taskExpressionValue.Text = "/";
        _taskDurationValue.Text = "/";
    }

    private void StartRuntimeEvents()
    {
        StopRuntimeEvents();
        _backend.SubscribeBattleAssistantOperationLoaded(_operationLoadedCallback);
        _runtimeEventsSubscribed = true;
    }

    private void StopRuntimeEvents()
    {
        if (_runtimeEventsSubscribed)
        {
            _backend.UnsubscribeBattleAssistantOperationLoaded(_operationLoadedCallback);
        }

        _runtimeEventsSubscribed = false;
    }

    private void OnOperationLoaded()
    {
        void RefreshWhenShown()
        {
            if (_isShown)
            {
                RefreshRuntimeState();
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshWhenShown();
        }
        else
        {
            Dispatcher.UIThread.Post(RefreshWhenShown);
        }
    }
}

internal sealed partial class FrontierCommissionAssistantPage : UserControl, IZzzPageLifecycle
{
    private readonly FrontierCommissionAssistantSettings _settings;

    public FrontierCommissionAssistantPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _settings = new FrontierCommissionAssistantSettings(backend);
        RunPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.CommissionAssistant,
            runIntent: runIntent,
            fixedGroupId: CommissionAssistantConstants.DefaultGroupId);
        AvaloniaXamlLoader.Load(this);
        ContentControl settingsHost = this.FindControl<ContentControl>("CommissionSettingsHost")
            ?? throw new InvalidOperationException("委托助手缺少设置区域。");
        ContentControl runHost = this.FindControl<ContentControl>("CommissionRunHost")
            ?? throw new InvalidOperationException("委托助手缺少运行区域。");
        settingsHost.Content = _settings;
        runHost.Content = RunPanel;
    }

    public Control LeftContent => _settings;

    public ZzzRunPanel RunPanel { get; }

    public void OnPageShown()
    {
        _settings.OnPageShown();
        RunPanel.OnPageShown();
    }

    public void OnPageHidden()
    {
        _settings.OnPageHidden();
        RunPanel.OnPageHidden();
    }

    public void OnPageLeave()
    {
        _settings.OnPageLeave();
        RunPanel.OnPageLeave();
    }

    public void DisposePage()
    {
        _settings.DisposePage();
        RunPanel.DisposePage();
    }
}

internal sealed partial class FrontierBattleAssistantSettings : UserControl, IZzzPageLifecycle
{
    private readonly ToggleButton _autoBattleModeButton;
    private readonly ToggleButton _dodgeAssistantModeButton;
    private readonly Border _autoBattleSettingsPanel;
    private readonly Border _dodgeAssistantSettingsPanel;
    private readonly FAContentDialog _helpDialog;
    private readonly ZzzBattleAssistantSettingsViewModel _viewModel;

    public FrontierBattleAssistantSettings(IZzzAppBackend backend)
    {
        _viewModel = new ZzzBattleAssistantSettingsViewModel(backend);
        AvaloniaXamlLoader.Load(this);
        _autoBattleModeButton = this.FindControl<ToggleButton>("AutoBattleModeButton")
            ?? throw new InvalidOperationException("战斗助手缺少自动战斗模式按钮。");
        _dodgeAssistantModeButton = this.FindControl<ToggleButton>("DodgeAssistantModeButton")
            ?? throw new InvalidOperationException("战斗助手缺少闪避助手模式按钮。");
        _autoBattleSettingsPanel = this.FindControl<Border>("AutoBattleSettingsPanel")
            ?? throw new InvalidOperationException("战斗助手缺少自动战斗设置区。");
        _dodgeAssistantSettingsPanel = this.FindControl<Border>("DodgeAssistantSettingsPanel")
            ?? throw new InvalidOperationException("战斗助手缺少闪避助手设置区。");
        _helpDialog = this.FindControl<FAContentDialog>("BattleHelpDialog")
            ?? throw new InvalidOperationException("战斗助手缺少使用说明 ContentDialog?");
        DataContext = _viewModel;
        string? evidenceTab = ZzzGuiEvidenceSelection.FromEnvironment().Tab;
        SetSelectedMode(string.Equals(evidenceTab, "闪避助手", StringComparison.Ordinal));
        ReloadSettings();
        ApplySelectedMode();
    }

    public string SelectedAppId { get; private set; } = ZzzApplicationIds.AutoBattle;

    public event EventHandler? SelectedAppIdChanged;

    public ZzzGameAssistantPageModel PageModel => ZzzGameAssistantPageModels.CreateBattle(SelectedAppId);

    public bool SelectModeByHeader(string header)
    {
        int index = string.Equals(header, "闪避助手", StringComparison.Ordinal) ? 1
            : string.Equals(header, "自动战斗", StringComparison.Ordinal) ? 0
            : -1;
        if (index < 0)
        {
            return false;
        }

        SetSelectedMode(index == 1);
        return true;
    }

    public void OnPageShown()
    {
        ReloadSettings();
        ApplySelectedMode();
    }

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
        _viewModel.DisposePage();
    }

    public IReadOnlyList<string> AutoBattleOptions => _viewModel.AutoBattleOptions;

    public IReadOnlyList<string> DodgeOptions => _viewModel.DodgeOptions;

    public string? SelectedAutoBattleConfig => _viewModel.SelectedAutoBattleConfig;

    public string? SelectedDodgeConfig => _viewModel.SelectedDodgeConfig;

    public void DeleteSelectedAutoBattleConfig() =>
        _viewModel.DeleteSelectedAutoBattleConfig();

    public void DeleteSelectedDodgeConfig() =>
        _viewModel.DeleteSelectedDodgeConfig();

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    private async void OnHelpClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        Window? owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
        {
            await _helpDialog.ShowAsync(owner).ConfigureAwait(true);
        }
    }

    private void OnGuideClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        OpenUrl("https://one-dragon.com/zzz/zh/feat_game_assistant.html");

    private void OnCommunityClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        OpenUrl("https://pd.qq.com/g/onedrag00n");

    private void OnDeleteAutoBattleConfigClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        DeleteSelectedAutoBattleConfig();

    private void OnDeleteDodgeConfigClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        DeleteSelectedDodgeConfig();

    private void ReloadSettings() => _viewModel.OnPageShown();

    private void OnAutoBattleModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        SetSelectedMode(false);

    private void OnDodgeAssistantModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        SetSelectedMode(true);

    private void SetSelectedMode(bool dodge)
    {
        _autoBattleModeButton.IsChecked = !dodge;
        _dodgeAssistantModeButton.IsChecked = dodge;
        _autoBattleSettingsPanel.IsVisible = !dodge;
        _dodgeAssistantSettingsPanel.IsVisible = dodge;
        ApplySelectedMode();
    }

    private void ApplySelectedMode()
    {
        bool auto = _autoBattleModeButton.IsChecked == true;
        SelectedAppId = auto ? ZzzApplicationIds.AutoBattle : ZzzApplicationIds.DodgeAssistant;
        SelectedAppIdChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed partial class FrontierCommissionAssistantSettings : UserControl, IZzzPageLifecycle
{
    private readonly FAInfoBar _errorBar;
    private readonly TextBox _dodgeSwitchKeyBox;
    private readonly TextBox _autoBattleSwitchKeyBox;
    private readonly ZzzCommissionAssistantSettingsViewModel _viewModel;

    public FrontierCommissionAssistantSettings(IZzzAppBackend backend)
    {
        _viewModel = new ZzzCommissionAssistantSettingsViewModel(backend, ShowError);
        AvaloniaXamlLoader.Load(this);
        _errorBar = this.FindControl<FAInfoBar>("CommissionErrorBar")
            ?? throw new InvalidOperationException("委托助手缺少错误 InfoBar?");
        _dodgeSwitchKeyBox = this.FindControl<TextBox>("DodgeSwitchKeyBox")
            ?? throw new InvalidOperationException("委托助手缺少闪避按键输入框。");
        _autoBattleSwitchKeyBox = this.FindControl<TextBox>("AutoBattleSwitchKeyBox")
            ?? throw new InvalidOperationException("委托助手缺少自动战斗按键输入框。");

        _dodgeSwitchKeyBox.KeyDown += OnKeyBoxKeyDown;
        _autoBattleSwitchKeyBox.KeyDown += OnKeyBoxKeyDown;
        DataContext = _viewModel;
        ReloadSettings();
    }

    public ZzzGameAssistantPageModel PageModel => ZzzGameAssistantPageModels.CreateCommission();

    public IReadOnlyList<string> DodgeOptions => _viewModel.DodgeOptions;

    public IReadOnlyList<string> AutoBattleOptions => _viewModel.AutoBattleOptions;

    public string? SelectedDodgeConfig => _viewModel.SelectedDodgeConfig;

    public string? SelectedAutoBattleConfig => _viewModel.SelectedAutoBattleConfig;

    public void OnPageShown() => ReloadSettings();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
        _dodgeSwitchKeyBox.KeyDown -= OnKeyBoxKeyDown;
        _autoBattleSwitchKeyBox.KeyDown -= OnKeyBoxKeyDown;
        _viewModel.DisposePage();
    }

    private void ReloadSettings() => _viewModel.OnPageShown();

    private void OnKeyBoxKeyDown(object? sender, KeyEventArgs args)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        string value = FormatKey(args.Key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (ReferenceEquals(textBox, _dodgeSwitchKeyBox))
        {
            _viewModel.DodgeSwitch = value;
        }
        else
        {
            _viewModel.AutoBattleSwitch = value;
        }

        args.Handled = true;
    }

    private void ShowError(string? error)
    {
        _errorBar.Message = error ?? string.Empty;
        _errorBar.IsOpen = !string.IsNullOrWhiteSpace(error);
    }

    private static string FormatKey(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)key - (int)Key.D0).ToString(CultureInfo.InvariantCulture);
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return ((int)key - (int)Key.NumPad0).ToString(CultureInfo.InvariantCulture);
        }

        return key is Key.None or Key.System ? string.Empty : key.ToString().ToLowerInvariant();
    }

    private void OnGuideClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://one-dragon.com/zzz/zh/feat_game_assistant.html")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }
}

internal sealed class LocalBinding<T> : IZzzConfigBinding<T>
{
    private T _value;

    public LocalBinding(T value)
    {
        _value = value;
    }

    public T Read() => _value;

    public void Save(T value)
    {
        _value = value;
    }
}

internal static class ZzzGameAssistantPageModels
{
    public static ZzzGameAssistantPageModel CreateBattle(string selectedAppId) =>
        new(
            "game-assistant-battle",
            "游戏助手 / 战斗助手",
            ["自动战斗", "闪避助手"],
            ["GPU运算", "截图间隔", "操作方式", "战斗配置", "终结技一好就放", "闪避方式", "使用说明"],
            [
                new("GPU运算", "model", "flash_classifier_gpu"),
                new("截图间隔", "battle-assistant", "screenshot_interval"),
                new("操作方式", "battle-assistant", "control_method"),
                new("战斗配置", "battle-assistant", "auto_battle_config"),
                new("终结技一好就放", "battle-assistant", "auto_ultimate_enabled"),
                new("闪避方式", "battle-assistant", "dodge_assistant_config"),
            ],
            [ZzzApplicationIds.AutoBattle, ZzzApplicationIds.DodgeAssistant],
            ["任务状态", "战斗状态"],
            selectedAppId);

    public static ZzzGameAssistantPageModel CreateCommission() =>
        new(
            "game-assistant-commission",
            "游戏助手 / 委托助手",
            [],
            ["游戏在后台时暂停", "对话选项优先级", "对话点击间隔", "剧情模式", "无内容时等待时间", "自动闪避", "自动闪避开关", "自动战斗", "自动战斗开关"],
            [
                new("游戏在后台时暂停", "commission-assistant", "pause_in_background", CommissionAssistantConstants.DefaultGroupId),
                new("对话选项优先级", "commission-assistant", "dialog_option", CommissionAssistantConstants.DefaultGroupId),
                new("对话点击间隔", "commission-assistant", "dialog_click_interval", CommissionAssistantConstants.DefaultGroupId),
                new("剧情模式", "commission-assistant", "story_mode", CommissionAssistantConstants.DefaultGroupId),
                new("无内容时等待时间", "commission-assistant", "sleep_after_empty_screen", CommissionAssistantConstants.DefaultGroupId),
                new("自动闪避", "commission-assistant", "dodge_config", CommissionAssistantConstants.DefaultGroupId),
                new("自动闪避开关", "commission-assistant", "dodge_switch", CommissionAssistantConstants.DefaultGroupId),
                new("自动战斗", "commission-assistant", "auto_battle", CommissionAssistantConstants.DefaultGroupId),
                new("自动战斗开关", "commission-assistant", "auto_battle_switch", CommissionAssistantConstants.DefaultGroupId),
            ],
            [ZzzApplicationIds.CommissionAssistant],
            ["运行状态", "日志"],
            ZzzApplicationIds.CommissionAssistant);
}
