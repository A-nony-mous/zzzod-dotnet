using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.NotoriousHunt;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.OneDragon;

internal sealed record ZzzChargePlanOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzChargePlanRowModel
{
    public required int Index { get; init; }

    public required ChargePlanItem Plan { get; init; }

    public required bool ShowCommands { get; init; }

    public required IReadOnlyList<ZzzChargePlanOption> CategoryOptions { get; init; }

    public required IReadOnlyList<ZzzChargePlanOption> MissionTypeOptions { get; init; }

    public required IReadOnlyList<ZzzChargePlanOption> MissionOptions { get; init; }

    public required IReadOnlyList<ZzzChargePlanOption> CardNumOptions { get; init; }

    public required IReadOnlyList<ZzzChargePlanOption> BuffOptions { get; init; }

    public required IReadOnlyList<ZzzChargePlanOption> TeamOptions { get; init; }

    public required IReadOnlyList<ZzzChargePlanOption> AutoBattleOptions { get; init; }

    public ZzzChargePlanOption? SelectedCategory { get; set; }

    public ZzzChargePlanOption? SelectedMissionType { get; set; }

    public ZzzChargePlanOption? SelectedMission { get; set; }

    public ZzzChargePlanOption? SelectedCardNum { get; set; }

    public ZzzChargePlanOption? SelectedBuff { get; set; }

    public ZzzChargePlanOption? SelectedTeam { get; set; }

    public ZzzChargePlanOption? SelectedAutoBattle { get; set; }

    public string RunTimesText { get; set; } = string.Empty;

    public string PlanTimesText { get; set; } = string.Empty;

    public bool IsMissionVisible => string.Equals(Plan.CategoryName, "实战模拟室", StringComparison.Ordinal);

    public bool IsCardNumVisible => IsMissionVisible;

    public bool IsBuffVisible => string.Equals(Plan.CategoryName, "恶名狩猎", StringComparison.Ordinal);

    public bool IsAutoBattleVisible => Plan.PredefinedTeamIndex == -1;
}

internal sealed class ZzzChargePlanState
{
    private static readonly IReadOnlyList<ZzzChargePlanOption> CardNumOptions = ChargePlanCardNum.Options
        .Select(item => new ZzzChargePlanOption(item.Label, item.Value?.ToString() ?? string.Empty))
        .ToArray();
    private static readonly IReadOnlyList<ZzzChargePlanOption> BuffOptions = NotoriousHuntBuff.Options
        .Select(item => new ZzzChargePlanOption(item.Label, item.Value?.ToString() ?? string.Empty))
        .ToArray();

    private readonly IZzzAppBackend _backend;
    private List<ChargePlanItem> _plans = [];
    private List<ChargePlanItem>? _backup;
    private ZzzChargePlanCatalogDto _catalog = new([], [], []);

    public ZzzChargePlanState(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public int? InstanceIndex { get; private set; }

    public bool Loop { get; private set; } = true;

    public bool SkipPlan { get; private set; }

    public bool DailyResetPlanTimes { get; private set; }

    public string RestoreCharge { get; private set; } = RestoreChargeMode.None.DisplayName;

    public bool DoubleReward { get; private set; }

    public ChargePlanItem DoubleRewardPlan { get; private set; } = new();

    public bool UndoAvailable => _backup is not null;

    public string? LastError { get; private set; }

    public IReadOnlyList<ChargePlanItem> Plans => _plans;

    public IReadOnlyList<ZzzChargePlanOption> RestoreChargeOptions { get; } = RestoreChargeMode.All
        .Select(mode => new ZzzChargePlanOption(mode.DisplayName, mode.DisplayName))
        .ToArray();

    public void Reload()
    {
        LastError = null;
        ZzzBackendResult<ZzzInstanceDto> instance = _backend.GetCurrentInstance();
        ZzzBackendResult<ZzzChargePlanCatalogDto> catalog = _backend.GetChargePlanCatalog();
        if (!instance.Success || instance.Value is null || !catalog.Success || catalog.Value is null)
        {
            LastError = instance.Error ?? catalog.Error ?? "体力计划数据不可用。";
            InstanceIndex = null;
            _plans = [];
            return;
        }

        InstanceIndex = instance.Value.Index;
        _catalog = catalog.Value;
        ZzzBackendResult<ZzzConfigScopeValuesDto> config = _backend.GetConfigScope(
            "charge-plan",
            InstanceIndex,
            ChargePlanConstants.DefaultGroupId);
        if (!config.Success || config.Value is null)
        {
            LastError = config.Error ?? "体力计划配置读取失败。";
            _plans = [];
            return;
        }

        IReadOnlyDictionary<string, object?> values = config.Value.Values;
        _plans = values.TryGetValue("plan_list", out object? planList) && planList is List<ChargePlanItem> typedPlans
            ? typedPlans.Select(plan => plan.Clone()).ToList()
            : [];
        Loop = Read(values, "loop", true);
        SkipPlan = Read(values, "skip_plan", false);
        DailyResetPlanTimes = Read(values, "daily_reset_plan_times", false);
        RestoreCharge = Read(values, "restore_charge", RestoreChargeMode.None.DisplayName);
        DoubleReward = Read(values, "double_reward", false);
        DoubleRewardPlan = values.TryGetValue("combat_simulation_double_reward_config", out object? doublePlan)
            && doublePlan is ChargePlanItem typedDoublePlan
                ? typedDoublePlan.Clone()
                : new ChargePlanItem();
    }

    public IReadOnlyList<ZzzChargePlanRowModel> CreateRows(bool showCommands = true) =>
        _plans.Select((plan, index) => CreateRow(plan, index, showCommands)).ToArray();

    public ZzzChargePlanRowModel CreateDialogRow() => CreateRow(CreateNewPlan(), -1, showCommands: false);

    public ZzzChargePlanRowModel CreateRow(ChargePlanItem plan, int index, bool showCommands)
    {
        IReadOnlyList<ZzzChargePlanOption> categories = _catalog.Categories
            .Select(item => new ZzzChargePlanOption(item.Label, item.Value))
            .ToArray();
        ZzzChargePlanCategoryDto? category = _catalog.Categories.FirstOrDefault(item => item.Value == plan.CategoryName);
        IReadOnlyList<ZzzChargePlanOption> missionTypes = (category?.MissionTypes ?? [])
            .Select(item => new ZzzChargePlanOption(item.Label, item.Value))
            .ToArray();
        ZzzChargePlanMissionTypeDto? missionType = category?.MissionTypes.FirstOrDefault(item => item.Value == plan.MissionTypeName);
        IReadOnlyList<ZzzChargePlanOption> missions = (missionType?.Missions ?? [])
            .Select(item => new ZzzChargePlanOption(item.Label, item.Value))
            .ToArray();
        IReadOnlyList<ZzzChargePlanOption> teams =
        [
            new("游戏内配队", "-1"),
            .. _catalog.Teams.Select(team => new ZzzChargePlanOption(team.Name, team.Index.ToString())),
        ];
        IReadOnlyList<ZzzChargePlanOption> autoBattle = _catalog.AutoBattleConfigs
            .Select(value => new ZzzChargePlanOption(value, value))
            .ToArray();
        return new ZzzChargePlanRowModel
        {
            Index = index,
            Plan = plan,
            ShowCommands = showCommands,
            CategoryOptions = categories,
            MissionTypeOptions = missionTypes,
            MissionOptions = missions,
            CardNumOptions = CardNumOptions,
            BuffOptions = BuffOptions,
            TeamOptions = teams,
            AutoBattleOptions = autoBattle,
            SelectedCategory = Find(categories, plan.CategoryName),
            SelectedMissionType = Find(missionTypes, plan.MissionTypeName),
            SelectedMission = Find(missions, plan.MissionName),
            SelectedCardNum = Find(CardNumOptions, plan.CardNum),
            SelectedBuff = Find(BuffOptions, plan.NotoriousHuntBuffNum.ToString()),
            SelectedTeam = Find(teams, plan.PredefinedTeamIndex.ToString()),
            SelectedAutoBattle = Find(autoBattle, plan.AutoBattleConfig),
            RunTimesText = plan.RunTimes.ToString(),
            PlanTimesText = plan.PlanTimes.ToString(),
        };
    }

    public void UpdatePlan(int index, Action<ChargePlanItem> update)
    {
        if (index < 0 || index >= _plans.Count)
        {
            return;
        }

        update(_plans[index]);
        SavePlans();
    }

    public void ApplyCategory(ChargePlanItem plan, string categoryName)
    {
        plan.CategoryName = categoryName;
        plan.TabName = "训练";
        ZzzChargePlanCategoryDto? category = _catalog.Categories.FirstOrDefault(item => item.Value == categoryName);
        ZzzChargePlanMissionTypeDto? missionType = category?.MissionTypes.FirstOrDefault();
        plan.MissionTypeName = missionType?.Value ?? string.Empty;
        plan.MissionName = missionType?.Missions.FirstOrDefault()?.Value;
    }

    public void ApplyMissionType(ChargePlanItem plan, string missionTypeName)
    {
        plan.MissionTypeName = missionTypeName;
        ZzzChargePlanCategoryDto? category = _catalog.Categories.FirstOrDefault(item => item.Value == plan.CategoryName);
        ZzzChargePlanMissionTypeDto? missionType = category?.MissionTypes.FirstOrDefault(item => item.Value == missionTypeName);
        plan.MissionName = missionType?.Missions.FirstOrDefault()?.Value;
    }

    public void AddPlan(ChargePlanItem plan)
    {
        _plans.Add(plan.Clone());
        SavePlans();
    }

    public void DeletePlan(int index)
    {
        if (index < 0 || index >= _plans.Count)
        {
            return;
        }

        _plans.RemoveAt(index);
        SavePlans();
    }

    public void MoveTop(int index)
    {
        if (index <= 0 || index >= _plans.Count)
        {
            return;
        }

        ChargePlanItem plan = _plans[index];
        _plans.RemoveAt(index);
        _plans.Insert(0, plan);
        SavePlans();
    }

    public void MoveTo(int sourceIndex, int insertionIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _plans.Count || insertionIndex < 0 || insertionIndex > _plans.Count)
        {
            return;
        }

        ChargePlanItem plan = _plans[sourceIndex];
        _plans.RemoveAt(sourceIndex);
        int adjusted = insertionIndex > sourceIndex ? insertionIndex - 1 : insertionIndex;
        _plans.Insert(Math.Clamp(adjusted, 0, _plans.Count), plan);
        SavePlans();
    }

    public void DeleteCompleted()
    {
        _backup = _plans.Select(plan => plan.Clone()).ToList();
        _plans = _plans.Where(plan => plan.RunTimes < plan.PlanTimes).ToList();
        SavePlans();
    }

    public void DeleteAll()
    {
        _backup = _plans.Select(plan => plan.Clone()).ToList();
        _plans.Clear();
        SavePlans();
    }

    public void UndoBulkDelete()
    {
        if (_backup is null)
        {
            return;
        }

        _plans = _backup.Select(plan => plan.Clone()).ToList();
        _backup = null;
    }

    public void SetLoop(bool value) => SaveScalar("loop", value, () => Loop = value);

    public void SetSkipPlan(bool value) => SaveScalar("skip_plan", value, () => SkipPlan = value);

    public void SetDailyReset(bool value) => SaveScalar("daily_reset_plan_times", value, () => DailyResetPlanTimes = value);

    public void SetRestoreCharge(string value) => SaveScalar("restore_charge", value, () => RestoreCharge = value);

    public void SetDoubleReward(bool value) => SaveScalar("double_reward", value, () => DoubleReward = value);

    public void SetDoubleRewardPlan(ChargePlanItem plan) => SaveScalar(
        "combat_simulation_double_reward_config",
        plan.Clone(),
        () => DoubleRewardPlan = plan.Clone());

    private void SavePlans() => SaveScalar(
        "plan_list",
        _plans.Select(plan => plan.Clone()).ToList(),
        () => { });

    private void SaveScalar(string key, object? value, Action committed)
    {
        if (InstanceIndex is null)
        {
			LastError = "当前实例不可用。";
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "charge-plan",
            new Dictionary<string, object?> { [key] = value },
            InstanceIndex,
            ChargePlanConstants.DefaultGroupId));
        if (result.Success)
        {
            LastError = null;
            committed();
        }
        else
        {
            LastError = result.Error;
            Reload();
        }
    }

    private static ChargePlanItem CreateNewPlan() => new()
    {
        TabName = "训练",
			CategoryName = "实战模拟室",
        MissionTypeName = "基础材料",
        MissionName = "调查专项",
        Level = "默认等级",
        AutoBattleConfig = "全配队通用",
        RunTimes = 0,
        PlanTimes = 1,
        CardNum = ChargePlanCardNum.Default,
        PredefinedTeamIndex = 0,
        NotoriousHuntBuffNum = 1,
    };

    private static ZzzChargePlanOption? Find(IReadOnlyList<ZzzChargePlanOption> options, string? value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private static T Read<T>(IReadOnlyDictionary<string, object?> values, string key, T fallback) =>
        values.TryGetValue(key, out object? value) && value is T typed ? typed : fallback;
}

internal sealed partial class ZzzChargePlanPage : UserControl, IZzzPageLifecycle
{
    private static readonly DataFormat<string> PlanIndexFormat =
        DataFormat.CreateStringApplicationFormat("zzzod.charge-plan-index");

    private readonly ZzzChargePlanState _state;
    private readonly InfoBar _errorBar;
    private readonly ToggleSwitch _loopToggle;
    private readonly ToggleSwitch _skipPlanToggle;
    private readonly ToggleSwitch _dailyResetToggle;
    private readonly FAComboBox _restoreChargeCombo;
    private readonly ToggleSwitch _doubleRewardToggle;
    private readonly FAComboBox _doubleCategoryCombo;
    private readonly FAComboBox _doubleMissionTypeCombo;
    private readonly FAComboBox _doubleMissionCombo;
    private readonly FAComboBox _doubleTeamCombo;
    private readonly FAComboBox _doubleAutoBattleCombo;
    private readonly CommandBarButton _undoButton;
    private readonly ItemsControl _planList;
    private readonly ContentDialog _addPlanDialog;
    private readonly ContentControl _dialogPlanHost;
    private readonly ContentDialog _confirmDialog;
    private readonly TextBlock _confirmMessage;
    private ZzzChargePlanRowModel? _dragCandidate;
    private Point _dragStart;
    private bool _loading;
    private int _loadVersion;

    public ZzzChargePlanPage(IZzzAppBackend backend)
    {
        _state = new ZzzChargePlanState(backend);
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<InfoBar>("ErrorBar");
        _loopToggle = Required<ToggleSwitch>("LoopToggle");
        _skipPlanToggle = Required<ToggleSwitch>("SkipPlanToggle");
        _dailyResetToggle = Required<ToggleSwitch>("DailyResetToggle");
        _restoreChargeCombo = Required<FAComboBox>("RestoreChargeCombo");
        _doubleRewardToggle = Required<ToggleSwitch>("DoubleRewardToggle");
        _doubleCategoryCombo = Required<FAComboBox>("DoubleCategoryCombo");
        _doubleMissionTypeCombo = Required<FAComboBox>("DoubleMissionTypeCombo");
        _doubleMissionCombo = Required<FAComboBox>("DoubleMissionCombo");
        _doubleTeamCombo = Required<FAComboBox>("DoubleTeamCombo");
        _doubleAutoBattleCombo = Required<FAComboBox>("DoubleAutoBattleCombo");
        _undoButton = Required<CommandBarButton>("UndoButton");
        _planList = Required<ItemsControl>("PlanList");
        _addPlanDialog = (ContentDialog)Resources["AddPlanDialog"]!;
        _dialogPlanHost = (ContentControl)_addPlanDialog.Content!;
        _confirmDialog = (ContentDialog)Resources["ConfirmDialog"]!;
        _confirmMessage = (TextBlock)_confirmDialog.Content!;
    }

    public ZzzChargePlanState State => _state;

    public ZzzOneDragonPageModel PageModel => new(
        "one-dragon-charge-plan",
        "一条龙 / 体力计划",
        ["体力计划说明", "循环执行", "跳过计划", "每日重置", "恢复电量", "双倍活动", "撤销", "删除已完成", "删除所有", "新增"],
        [ChargePlanConstants.AppId],
        ["loop", "skip_plan", "daily_reset_plan_times", "restore_charge", "double_reward", "combat_simulation_double_reward_config", "plan_list"],
        _state.Plans.Count);

    public async void OnPageShown()
    {
        int loadVersion = ++_loadVersion;
        _errorBar.IsOpen = false;
        _planList.ItemsSource = null;
        _loading = true;

        await Task.Run(() => _state.Reload()).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (loadVersion == _loadVersion)
            {
                RefreshAll();
            }
        });
    }

    public void OnPageLeave()
    {
        _loadVersion++;
    }

    public void OnPageHidden()
    {
        _loadVersion++;
    }

    public void DisposePage()
    {
    }

    private void RefreshAll()
    {
        _loading = true;
        _loopToggle.IsChecked = _state.Loop;
        _skipPlanToggle.IsChecked = _state.SkipPlan;
        _dailyResetToggle.IsChecked = _state.DailyResetPlanTimes;
        _restoreChargeCombo.ItemsSource = _state.RestoreChargeOptions;
        _restoreChargeCombo.SelectedItem = Find(_state.RestoreChargeOptions, _state.RestoreCharge);
        _doubleRewardToggle.IsChecked = _state.DoubleReward;
        RefreshDoubleReward();
        RefreshPlans();
        _loading = false;
        ShowError();
    }

    private void RefreshPlans()
    {
        _planList.ItemsSource = null;
        _planList.ItemsSource = _state.CreateRows();
        _undoButton.IsEnabled = _state.UndoAvailable;
    }

    private void RefreshDoubleReward()
    {
        ZzzChargePlanRowModel row = _state.CreateRow(_state.DoubleRewardPlan, -1, showCommands: false);
        IReadOnlyList<ZzzChargePlanOption> categories = [new("实战模拟室", "实战模拟室")];
        _doubleCategoryCombo.ItemsSource = categories;
        _doubleCategoryCombo.SelectedItem = categories[0];
        IReadOnlyList<ZzzChargePlanOption> missionTypes = row.MissionTypeOptions
            .Where(option => option.Label != "特训目标" && option.Value != "特训目标")
            .ToArray();
        _doubleMissionTypeCombo.ItemsSource = missionTypes;
        _doubleMissionTypeCombo.SelectedItem = Find(missionTypes, _state.DoubleRewardPlan.MissionTypeName);
        _doubleMissionCombo.ItemsSource = row.MissionOptions;
        _doubleMissionCombo.SelectedItem = Find(row.MissionOptions, _state.DoubleRewardPlan.MissionName);
        _doubleTeamCombo.ItemsSource = row.TeamOptions;
        _doubleTeamCombo.SelectedItem = Find(row.TeamOptions, _state.DoubleRewardPlan.PredefinedTeamIndex.ToString());
        _doubleAutoBattleCombo.ItemsSource = row.AutoBattleOptions;
        _doubleAutoBattleCombo.SelectedItem = Find(row.AutoBattleOptions, _state.DoubleRewardPlan.AutoBattleConfig);
        _doubleAutoBattleCombo.IsVisible = _state.DoubleRewardPlan.PredefinedTeamIndex == -1;
        _doubleMissionTypeCombo.IsEnabled = _state.DoubleReward;
        _doubleMissionCombo.IsEnabled = _state.DoubleReward;
        _doubleTeamCombo.IsEnabled = _state.DoubleReward;
        _doubleAutoBattleCombo.IsEnabled = _state.DoubleReward;
    }

    private void OnLoopChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            _state.SetLoop(_loopToggle.IsChecked == true);
            ShowError();
        }
    }

    private void OnSkipPlanChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            _state.SetSkipPlan(_skipPlanToggle.IsChecked == true);
            ShowError();
        }
    }

    private void OnDailyResetChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            _state.SetDailyReset(_dailyResetToggle.IsChecked == true);
            ShowError();
        }
    }

    private void OnRestoreChargeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _restoreChargeCombo.SelectedItem is ZzzChargePlanOption option)
        {
            _state.SetRestoreCharge(option.Value);
            ShowError();
        }
    }

    private void OnDoubleRewardChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        _state.SetDoubleReward(_doubleRewardToggle.IsChecked == true);
        _loading = true;
        RefreshDoubleReward();
        _loading = false;
        ShowError();
    }

    private void OnDoubleMissionTypeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _doubleMissionTypeCombo.SelectedItem is not ZzzChargePlanOption option)
        {
            return;
        }

        ChargePlanItem plan = _state.DoubleRewardPlan.Clone();
        plan.TabName = "训练";
        _state.ApplyMissionType(plan, option.Value);
        _state.SetDoubleRewardPlan(plan);
        ReloadDoubleRewardControls();
    }

    private void OnDoubleMissionChanged(object? sender, SelectionChangedEventArgs args) =>
        UpdateDoubleReward(plan => plan.MissionName = (_doubleMissionCombo.SelectedItem as ZzzChargePlanOption)?.Value);

    private void OnDoubleTeamChanged(object? sender, SelectionChangedEventArgs args)
    {
        UpdateDoubleReward(plan =>
        {
            if (_doubleTeamCombo.SelectedItem is ZzzChargePlanOption option && int.TryParse(option.Value, out int index))
            {
                plan.PredefinedTeamIndex = index;
            }
        }, refresh: true);
    }

    private void OnDoubleAutoBattleChanged(object? sender, SelectionChangedEventArgs args) =>
        UpdateDoubleReward(plan => plan.AutoBattleConfig = (_doubleAutoBattleCombo.SelectedItem as ZzzChargePlanOption)?.Value ?? plan.AutoBattleConfig);

    private void UpdateDoubleReward(Action<ChargePlanItem> update, bool refresh = false)
    {
        if (_loading)
        {
            return;
        }

        ChargePlanItem plan = _state.DoubleRewardPlan.Clone();
        update(plan);
        if (SamePlan(plan, _state.DoubleRewardPlan))
        {
            return;
        }

        _state.SetDoubleRewardPlan(plan);
        if (refresh)
        {
            ReloadDoubleRewardControls();
        }
        else
        {
            ShowError();
        }
    }

    private void ReloadDoubleRewardControls()
    {
        _loading = true;
        RefreshDoubleReward();
        _loading = false;
        ShowError();
    }

    private void OnCategoryChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzChargePlanRowModel? row, out ZzzChargePlanOption? option))
        {
            UpdateRow(row, plan =>
            {
                _state.ApplyCategory(plan, option.Value);
            }, refresh: true);
        }
    }

    private void OnMissionTypeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzChargePlanRowModel? row, out ZzzChargePlanOption? option))
        {
            UpdateRow(row, plan =>
            {
                _state.ApplyMissionType(plan, option.Value);
            }, refresh: true);
        }
    }

    private void OnMissionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzChargePlanRowModel? row, out ZzzChargePlanOption? option))
        {
            UpdateRow(row, plan => plan.MissionName = option.Value);
        }
    }

    private void OnCardNumChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzChargePlanRowModel? row, out ZzzChargePlanOption? option))
        {
            UpdateRow(row, plan => plan.CardNum = option.Value);
        }
    }

    private void OnBuffChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzChargePlanRowModel? row, out ZzzChargePlanOption? option)
            && int.TryParse(option.Value, out int buff))
        {
            UpdateRow(row, plan => plan.NotoriousHuntBuffNum = buff);
        }
    }

    private void OnTeamChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzChargePlanRowModel? row, out ZzzChargePlanOption? option)
            && int.TryParse(option.Value, out int teamIndex))
        {
            UpdateRow(row, plan => plan.PredefinedTeamIndex = teamIndex, refresh: true);
        }
    }

    private void OnAutoBattleChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzChargePlanRowModel? row, out ZzzChargePlanOption? option))
        {
            UpdateRow(row, plan => plan.AutoBattleConfig = option.Value);
        }
    }

    private void OnRunTimesChanged(object? sender, TextChangedEventArgs args)
    {
        if (!_loading && sender is TextBox { DataContext: ZzzChargePlanRowModel row } box
            && int.TryParse(box.Text, out int value))
        {
            UpdateRow(row, plan => plan.RunTimes = value);
        }
    }

    private void OnPlanTimesChanged(object? sender, TextChangedEventArgs args)
    {
        if (!_loading && sender is TextBox { DataContext: ZzzChargePlanRowModel row } box
            && int.TryParse(box.Text, out int value))
        {
            UpdateRow(row, plan => plan.PlanTimes = value);
        }
    }

    private void UpdateRow(ZzzChargePlanRowModel row, Action<ChargePlanItem> update, bool refresh = false)
    {
        if (_loading)
        {
            return;
        }

        ChargePlanItem before = row.Plan.Clone();
        update(row.Plan);
        if (SamePlan(before, row.Plan))
        {
            return;
        }

        if (row.Index < 0)
        {
            if (refresh)
            {
                _dialogPlanHost.Content = _state.CreateRow(row.Plan, -1, showCommands: false);
            }
            return;
        }

        _state.UpdatePlan(row.Index, _ => { });
        if (refresh)
        {
            _loading = true;
            RefreshPlans();
            _loading = false;
        }
        ShowError();
    }

    private static bool SamePlan(ChargePlanItem left, ChargePlanItem right) =>
        left.TabName == right.TabName
        && left.CategoryName == right.CategoryName
        && left.MissionTypeName == right.MissionTypeName
        && left.MissionName == right.MissionName
        && left.Level == right.Level
        && left.AutoBattleConfig == right.AutoBattleConfig
        && left.RunTimes == right.RunTimes
        && left.PlanTimes == right.PlanTimes
        && left.CardNum == right.CardNum
        && left.PredefinedTeamIndex == right.PredefinedTeamIndex
        && left.NotoriousHuntBuffNum == right.NotoriousHuntBuffNum
        && left.PlanId == right.PlanId
        && left.Skipped == right.Skipped;

    private void OnMovePlanTopClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is { Index: >= 0 } row)
        {
            _state.MoveTop(row.Index);
            RefreshPlans();
            ShowError();
        }
    }

    private void OnDeletePlanClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is { Index: >= 0 } row)
        {
            _state.DeletePlan(row.Index);
            RefreshPlans();
            ShowError();
        }
    }

    private async void OnAddClicked(object? sender, RoutedEventArgs args)
    {
        ZzzChargePlanRowModel row = _state.CreateDialogRow();
        _dialogPlanHost.Content = row;
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
			ShowError("当前窗口不可用。", InfoBarSeverity.Error);
            return;
        }

        ContentDialogResult result = await _addPlanDialog.ShowAsync(owner).ConfigureAwait(true);
        if (result == ContentDialogResult.Primary)
        {
            _state.AddPlan(row.Plan);
            RefreshPlans();
            ShowError();
        }
    }

    private async void OnDeleteCompletedClicked(object? sender, RoutedEventArgs args)
    {
        if (await ConfirmAsync("是否删除所有已完成的体力计划？").ConfigureAwait(true))
        {
            _state.DeleteCompleted();
            RefreshPlans();
            ShowError();
        }
    }

    private async void OnDeleteAllClicked(object? sender, RoutedEventArgs args)
    {
        if (await ConfirmAsync("是否删除所有体力计划？").ConfigureAwait(true))
        {
            _state.DeleteAll();
            RefreshPlans();
            ShowError();
        }
    }

    private void OnUndoClicked(object? sender, RoutedEventArgs args)
    {
        _state.UndoBulkDelete();
        RefreshPlans();
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
			ShowError("当前窗口不可用。", InfoBarSeverity.Error);
            return false;
        }

        _confirmMessage.Text = message;
        return await _confirmDialog.ShowAsync(owner).ConfigureAwait(true) == ContentDialogResult.Primary;
    }

    private void OnPlanPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ZzzChargePlanRowModel { Index: >= 0 } row
            || !args.GetCurrentPoint(control).Properties.IsLeftButtonPressed || IsInteractiveSource(args.Source))
        {
            _dragCandidate = null;
            return;
        }

        _dragCandidate = row;
        _dragStart = args.GetPosition(control);
    }

    private async void OnPlanPointerMoved(object? sender, PointerEventArgs args)
    {
        if (sender is not Control control || _dragCandidate is not { } row
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
        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.Create(PlanIndexFormat, row.Index.ToString()));
        await DragDrop.DoDragDropAsync(args, transfer, DragDropEffects.Move).ConfigureAwait(true);
    }

    private void OnPlanDragOver(object? sender, DragEventArgs args)
    {
        args.DragEffects = args.DataTransfer.Contains(PlanIndexFormat) ? DragDropEffects.Move : DragDropEffects.None;
        args.Handled = true;
    }

    private void OnPlanDrop(object? sender, DragEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ZzzChargePlanRowModel { Index: >= 0 } target
            || !int.TryParse(args.DataTransfer.TryGetValue(PlanIndexFormat), out int sourceIndex))
        {
            return;
        }

        int insertionIndex = target.Index + (args.GetPosition(control).Y >= control.Bounds.Height / 2 ? 1 : 0);
        _state.MoveTo(sourceIndex, insertionIndex);
        RefreshPlans();
        ShowError();
        args.DragEffects = DragDropEffects.Move;
        args.Handled = true;
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        OpenUrl("https://one-dragon.com/zzz/zh/feat_one_dragon/charge_plan.html");

    private bool TryRowOption(
        object? sender,
        out ZzzChargePlanRowModel row,
        out ZzzChargePlanOption option)
    {
        row = null!;
        option = null!;
        if (_loading || sender is not FAComboBox { DataContext: ZzzChargePlanRowModel model, SelectedItem: ZzzChargePlanOption selected })
        {
            return false;
        }

        row = model;
        option = selected;
        return true;
    }

    private static ZzzChargePlanRowModel? Row(object? sender) =>
        sender is Control control ? control.DataContext as ZzzChargePlanRowModel : null;

    private static bool IsInteractiveSource(object? source) => source is Control control
        && (control is Button or ToggleSwitch or TextBox or FAComboBox
            || control.GetVisualAncestors().Any(ancestor => ancestor is Button or ToggleSwitch or TextBox or FAComboBox));

    private static ZzzChargePlanOption? Find(IReadOnlyList<ZzzChargePlanOption> options, string? value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private void ShowError()
    {
        if (!string.IsNullOrWhiteSpace(_state.LastError))
        {
            ShowError(_state.LastError, InfoBarSeverity.Error);
        }
        else
        {
            _errorBar.IsOpen = false;
        }
    }

    private void ShowError(string message, InfoBarSeverity severity)
    {
        _errorBar.Message = message;
        _errorBar.Severity = severity;
        _errorBar.IsOpen = true;
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
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"体力计划页缺少 {name}。");
}

