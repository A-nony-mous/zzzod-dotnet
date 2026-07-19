using System.Globalization;
using Avalonia;
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

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzNotoriousHuntOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzNotoriousHuntPlanRowModel
{
    public required int Index { get; init; }

    public required ChargePlanItem Plan { get; init; }

    public required bool ShowCommands { get; init; }

    public required IReadOnlyList<ZzzNotoriousHuntOption> MissionTypeOptions { get; init; }

    public required IReadOnlyList<ZzzNotoriousHuntOption> LevelOptions { get; init; }

    public required IReadOnlyList<ZzzNotoriousHuntOption> TeamOptions { get; init; }

    public required IReadOnlyList<ZzzNotoriousHuntOption> AutoBattleOptions { get; init; }

    public required IReadOnlyList<ZzzNotoriousHuntOption> BuffOptions { get; init; }

    public ZzzNotoriousHuntOption? SelectedMissionType { get; set; }

    public ZzzNotoriousHuntOption? SelectedLevel { get; set; }

    public ZzzNotoriousHuntOption? SelectedTeam { get; set; }

    public ZzzNotoriousHuntOption? SelectedAutoBattle { get; set; }

    public ZzzNotoriousHuntOption? SelectedBuff { get; set; }

    public string RunTimesText { get; set; } = string.Empty;

    public string PlanTimesText { get; set; } = string.Empty;

    public bool IsAutoBattleVisible => Plan.PredefinedTeamIndex == -1;
}

internal sealed class ZzzNotoriousHuntAppSettingState
{
    private const string ScopeName = "notorious-hunt";
    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private List<ChargePlanItem> _plans = [];
    private IReadOnlyList<ZzzNotoriousHuntOption> _missionTypes = [];
    private IReadOnlyList<ZzzNotoriousHuntOption> _teams = [];
    private IReadOnlyList<ZzzNotoriousHuntOption> _autoBattle = [];

    public ZzzNotoriousHuntAppSettingState(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    public int WeeklyChallengeStartWeekday { get; private set; }

    public bool Loop { get; private set; }

    public string? LastError { get; private set; }

    public IReadOnlyList<ChargePlanItem> Plans => _plans;

    public IReadOnlyList<ZzzNotoriousHuntOption> WeekdayOptions { get; } = Options(NotoriousHuntWeekday.Options);

    public void Reload()
    {
        LastError = null;
        ZzzBackendResult<ZzzChargePlanCatalogDto> catalogResult = _backend.GetChargePlanCatalog();
        if (!catalogResult.Success || catalogResult.Value is null)
        {
            Fail(catalogResult.Error ?? "恶名狩猎目录读取失败。");
            return;
        }

        ZzzChargePlanCategoryDto? category = catalogResult.Value.Categories.FirstOrDefault(item =>
            string.Equals(item.Value, "恶名狩猎", StringComparison.Ordinal));
        if (category is null)
        {
            Fail("真实手册中缺少恶名狩猎目录。");
            return;
        }

        _missionTypes = category.MissionTypes
            .Select(item => new ZzzNotoriousHuntOption(item.Label, item.Value))
            .ToArray();
        _teams =
        [
            new ZzzNotoriousHuntOption("游戏内配队", "-1"),
            .. catalogResult.Value.Teams.Select(team => new ZzzNotoriousHuntOption(team.Name, team.Index.ToString(CultureInfo.InvariantCulture))),
        ];
        _autoBattle = catalogResult.Value.AutoBattleConfigs
            .Select(value => new ZzzNotoriousHuntOption(value, value))
            .ToArray();

        ZzzBackendResult<ZzzConfigScopeValuesDto> configResult = _backend.GetConfigScope(
            ScopeName,
            _instanceIndex,
            _groupId);
        if (!configResult.Success || configResult.Value is null)
        {
            Fail(configResult.Error ?? "恶名狩猎配置读取失败。");
            return;
        }

        try
        {
            IReadOnlyDictionary<string, object?> values = configResult.Value.Values;
            _plans = RequiredPlans(values, "plan_list");
            WeeklyChallengeStartWeekday = RequiredInt(values, "weekly_challenge_start_weekday");
            Loop = RequiredBool(values, "loop");
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    public IReadOnlyList<ZzzNotoriousHuntPlanRowModel> CreateRows() =>
        _plans.Select((plan, index) => CreateRow(plan, index, showCommands: true)).ToArray();

    public ZzzNotoriousHuntPlanRowModel CreateDialogRow() =>
        CreateRow(CreateNewPlan(), -1, showCommands: false);

    public ZzzNotoriousHuntPlanRowModel CreateRow(ChargePlanItem plan, int index, bool showCommands)
    {
        IReadOnlyList<ZzzNotoriousHuntOption> levelOptions = Options(NotoriousHuntLevel.Options);
        IReadOnlyList<ZzzNotoriousHuntOption> buffOptions = Options(NotoriousHuntBuff.Options);
        return new ZzzNotoriousHuntPlanRowModel
        {
            Index = index,
            Plan = plan,
            ShowCommands = showCommands,
            MissionTypeOptions = _missionTypes,
            LevelOptions = levelOptions,
            TeamOptions = _teams,
            AutoBattleOptions = _autoBattle,
            BuffOptions = buffOptions,
            SelectedMissionType = Find(_missionTypes, plan.MissionTypeName),
            SelectedLevel = Find(levelOptions, plan.Level),
            SelectedTeam = Find(_teams, plan.PredefinedTeamIndex.ToString(CultureInfo.InvariantCulture)),
            SelectedAutoBattle = Find(_autoBattle, plan.AutoBattleConfig),
            SelectedBuff = Find(buffOptions, plan.NotoriousHuntBuffNum.ToString(CultureInfo.InvariantCulture)),
            RunTimesText = plan.RunTimes.ToString(CultureInfo.InvariantCulture),
            PlanTimesText = plan.PlanTimes.ToString(CultureInfo.InvariantCulture),
        };
    }

    public void SetWeekday(int value) => SaveScalar("weekly_challenge_start_weekday", value, () => WeeklyChallengeStartWeekday = value);

    public void SetLoop(bool value) => SaveScalar("loop", value, () => Loop = value);

    public void UpdatePlan(int index, Action<ChargePlanItem> update)
    {
        if (index < 0 || index >= _plans.Count)
        {
            return;
        }

        update(_plans[index]);
        SavePlans();
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

    private void SavePlans() => SaveScalar(
        "plan_list",
        _plans.Select(plan => plan.Clone()).ToList(),
        () => { });

    private void SaveScalar(string key, object? value, Action committed)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            _groupId));
        if (result.Success)
        {
            LastError = null;
            committed();
            return;
        }

        string error = result.Error ?? "恶名狩猎配置保存失败。";
        Reload();
        LastError = error;
    }

    private void Fail(string message)
    {
        LastError = message;
        _plans = [];
        _missionTypes = [];
        _teams = [];
        _autoBattle = [];
    }

    private static ChargePlanItem CreateNewPlan() => new()
    {
        TabName = "训练",
        CategoryName = "恶名狩猎",
        MissionTypeName = "初生死路屠夫",
        MissionName = null,
        Level = NotoriousHuntLevel.Default,
        AutoBattleConfig = "全配队通用",
        RunTimes = 0,
        PlanTimes = 1,
        PredefinedTeamIndex = -1,
        NotoriousHuntBuffNum = 1,
    };

    private static IReadOnlyList<ZzzNotoriousHuntOption> Options(
        IReadOnlyList<global::OneDragon.Core.Configuration.ConfigItem> options) =>
        options.Select(item => new ZzzNotoriousHuntOption(
            item.Label,
            Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? string.Empty)).ToArray();

    private static ZzzNotoriousHuntOption? Find(IReadOnlyList<ZzzNotoriousHuntOption> options, string? value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private static List<ChargePlanItem> RequiredPlans(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value) || value is not IEnumerable<ChargePlanItem> plans)
        {
            throw new InvalidOperationException($"恶名狩猎配置缺少 {key}。");
        }

        return plans.Select(plan => plan.Clone()).ToList();
    }

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"恶名狩猎配置缺少 {key}。");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"恶名狩猎配置缺少 {key}。");
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }
}

internal sealed partial class ZzzNotoriousHuntAppSettingPage : UserControl, IZzzPageLifecycle
{
    private static readonly DataFormat<string> PlanIndexFormat =
        DataFormat.CreateStringApplicationFormat("zzzod.notorious-hunt-plan-index");

    private readonly ZzzNotoriousHuntAppSettingState _state;
    private readonly FAInfoBar _errorBar;
    private readonly FAComboBox _weekdayCombo;
    private readonly ToggleSwitch _loopToggle;
    private readonly ItemsControl _planList;
    private readonly FAContentDialog _addPlanDialog;
    private readonly ContentControl _dialogPlanHost;
    private ZzzNotoriousHuntPlanRowModel? _dragCandidate;
    private Point _dragStart;
    private PointerPressedEventArgs? _dragPointerPressedArgs;
    private bool _loading;

    public ZzzNotoriousHuntAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _state = new ZzzNotoriousHuntAppSettingState(backend, instanceIndex, groupId);
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _weekdayCombo = Required<FAComboBox>("WeekdayCombo");
        _loopToggle = Required<ToggleSwitch>("LoopToggle");
        _planList = Required<ItemsControl>("PlanList");
        _addPlanDialog = Required<FAContentDialog>("AddPlanDialog");
        _dialogPlanHost = Required<ContentControl>("DialogPlanHost");
        Reload();
    }

    internal ZzzNotoriousHuntAppSettingState State => _state;

    public void OnPageShown() => Reload();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
    }

    private void Reload()
    {
        _state.Reload();
        _loading = true;
        _weekdayCombo.ItemsSource = _state.WeekdayOptions;
        _weekdayCombo.SelectedItem = Find(
            _state.WeekdayOptions,
            _state.WeeklyChallengeStartWeekday.ToString(CultureInfo.InvariantCulture));
        _loopToggle.IsChecked = _state.Loop;
        RefreshPlans();
        _loading = false;
        ShowError();
    }

    private void RefreshPlans()
    {
        _planList.ItemsSource = null;
        _planList.ItemsSource = _state.CreateRows();
    }

    private void OnWeekdayChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _weekdayCombo.SelectedItem is ZzzNotoriousHuntOption option
            && int.TryParse(option.Value, CultureInfo.InvariantCulture, out int value))
        {
            _state.SetWeekday(value);
            ShowError();
        }
    }

    private void OnLoopChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            _state.SetLoop(_loopToggle.IsChecked == true);
            ShowError();
        }
    }

    private void OnMissionTypeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzNotoriousHuntPlanRowModel row, out ZzzNotoriousHuntOption option))
        {
            UpdateRow(row, plan => plan.MissionTypeName = option.Value);
        }
    }

    private void OnLevelChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzNotoriousHuntPlanRowModel row, out ZzzNotoriousHuntOption option))
        {
            UpdateRow(row, plan => plan.Level = option.Value);
        }
    }

    private void OnTeamChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzNotoriousHuntPlanRowModel row, out ZzzNotoriousHuntOption option)
            && int.TryParse(option.Value, CultureInfo.InvariantCulture, out int value))
        {
            UpdateRow(row, plan => plan.PredefinedTeamIndex = value, refresh: true);
        }
    }

    private void OnAutoBattleChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzNotoriousHuntPlanRowModel row, out ZzzNotoriousHuntOption option))
        {
            UpdateRow(row, plan => plan.AutoBattleConfig = option.Value);
        }
    }

    private void OnBuffChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (TryRowOption(sender, out ZzzNotoriousHuntPlanRowModel row, out ZzzNotoriousHuntOption option)
            && int.TryParse(option.Value, CultureInfo.InvariantCulture, out int value))
        {
            UpdateRow(row, plan => plan.NotoriousHuntBuffNum = value);
        }
    }

    private void OnRunTimesChanged(object? sender, TextChangedEventArgs args)
    {
        if (!_loading && sender is TextBox { DataContext: ZzzNotoriousHuntPlanRowModel row } box
            && int.TryParse(box.Text, CultureInfo.InvariantCulture, out int value))
        {
            UpdateRow(row, plan => plan.RunTimes = value);
        }
    }

    private void OnPlanTimesChanged(object? sender, TextChangedEventArgs args)
    {
        if (!_loading && sender is TextBox { DataContext: ZzzNotoriousHuntPlanRowModel row } box
            && int.TryParse(box.Text, CultureInfo.InvariantCulture, out int value))
        {
            UpdateRow(row, plan => plan.PlanTimes = value);
        }
    }

    private void UpdateRow(ZzzNotoriousHuntPlanRowModel row, Action<ChargePlanItem> update, bool refresh = false)
    {
        if (_loading)
        {
            return;
        }

        if (row.Index < 0)
        {
            update(row.Plan);
            if (refresh)
            {
                _dialogPlanHost.Content = _state.CreateRow(row.Plan, -1, showCommands: false);
            }
            return;
        }

        _state.UpdatePlan(row.Index, update);
        if (refresh)
        {
            _loading = true;
            RefreshPlans();
            _loading = false;
        }
        ShowError();
    }

    private void OnMoveTopClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is { Index: >= 0 } row)
        {
            _state.MoveTop(row.Index);
            RefreshPlans();
            ShowError();
        }
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs args)
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
        ZzzNotoriousHuntPlanRowModel row = _state.CreateDialogRow();
        _dialogPlanHost.Content = row;
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            ShowError("当前窗口不可用。");
            return;
        }

        FAContentDialogResult result = await _addPlanDialog.ShowAsync(owner).ConfigureAwait(true);
        if (result == FAContentDialogResult.Primary)
        {
            _state.AddPlan(row.Plan);
            RefreshPlans();
            ShowError();
        }
    }

    private void OnPlanPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ZzzNotoriousHuntPlanRowModel { Index: >= 0 } row
            || !args.GetCurrentPoint(control).Properties.IsLeftButtonPressed || IsInteractiveSource(args.Source))
        {
            _dragCandidate = null;
            _dragPointerPressedArgs = null;
            return;
        }

        _dragCandidate = row;
        _dragStart = args.GetPosition(control);
        _dragPointerPressedArgs = args;
    }

    private async void OnPlanPointerMoved(object? sender, PointerEventArgs args)
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
        transfer.Add(DataTransferItem.Create(PlanIndexFormat, row.Index.ToString(CultureInfo.InvariantCulture)));
        await DragDrop.DoDragDropAsync(pressedArgs, transfer, DragDropEffects.Move).ConfigureAwait(true);
    }

    private void OnPlanDragOver(object? sender, DragEventArgs args)
    {
        args.DragEffects = args.DataTransfer.Contains(PlanIndexFormat) ? DragDropEffects.Move : DragDropEffects.None;
        args.Handled = true;
    }

    private void OnPlanDrop(object? sender, DragEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ZzzNotoriousHuntPlanRowModel { Index: >= 0 } target
            || !int.TryParse(args.DataTransfer.TryGetValue(PlanIndexFormat), CultureInfo.InvariantCulture, out int sourceIndex))
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

    private bool TryRowOption(
        object? sender,
        out ZzzNotoriousHuntPlanRowModel row,
        out ZzzNotoriousHuntOption option)
    {
        row = null!;
        option = null!;
        if (_loading || sender is not FAComboBox
            {
                DataContext: ZzzNotoriousHuntPlanRowModel model,
                SelectedItem: ZzzNotoriousHuntOption selected,
            })
        {
            return false;
        }

        row = model;
        option = selected;
        return true;
    }

    private static ZzzNotoriousHuntPlanRowModel? Row(object? sender) =>
        sender is Control control ? control.DataContext as ZzzNotoriousHuntPlanRowModel : null;

    private static bool IsInteractiveSource(object? source) => source is Control control
        && (control is Button or ToggleSwitch or TextBox or FAComboBox
            || control.GetVisualAncestors().Any(ancestor => ancestor is Button or ToggleSwitch or TextBox or FAComboBox));

    private static ZzzNotoriousHuntOption? Find(IReadOnlyList<ZzzNotoriousHuntOption> options, string? value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private void ShowError()
    {
        if (string.IsNullOrWhiteSpace(_state.LastError))
        {
            _errorBar.IsOpen = false;
            return;
        }

        ShowError(_state.LastError);
    }

    private void ShowError(string message)
    {
        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.Severity = FAInfoBarSeverity.Error;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"恶名狩猎设置缺少 {name}。");
}
