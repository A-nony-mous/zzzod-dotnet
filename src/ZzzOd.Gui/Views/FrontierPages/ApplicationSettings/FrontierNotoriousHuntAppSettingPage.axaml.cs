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
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

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

internal sealed class ZzzNotoriousHuntAppSettingViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField PlanListField = new(
        "plan_list",
        typeof(List<ChargePlanItem>),
        new List<ChargePlanItem>(),
        FromConfig,
        ToConfig);
    private static readonly ZzzConfigField WeeklyChallengeStartWeekdayField =
        new("weekly_challenge_start_weekday", typeof(int), 1);
    private static readonly ZzzConfigField LoopField =
        new("loop", typeof(bool), false);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [PlanListField, WeeklyChallengeStartWeekdayField, LoopField];

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private List<ChargePlanItem> _plans = [];
    private IReadOnlyList<ZzzNotoriousHuntOption> _missionTypes = [];
    private IReadOnlyList<ZzzNotoriousHuntOption> _teams = [];
    private IReadOnlyList<ZzzNotoriousHuntOption> _autoBattle = [];

    public ZzzNotoriousHuntAppSettingViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => "notorious-hunt";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public int WeeklyChallengeStartWeekday
    {
        get => GetValue<int>(WeeklyChallengeStartWeekdayField);
        set => SetValue(WeeklyChallengeStartWeekdayField, value);
    }

    public bool Loop
    {
        get => GetValue<bool>(LoopField);
        set => SetValue(LoopField, value);
    }

    public IReadOnlyList<ChargePlanItem> Plans => _plans;

    public IReadOnlyList<ZzzNotoriousHuntOption> WeekdayOptions { get; } = Options(NotoriousHuntWeekday.Options);

    public ZzzNotoriousHuntOption? SelectedWeekday
    {
        get => Find(WeekdayOptions, WeeklyChallengeStartWeekday.ToString(CultureInfo.InvariantCulture));
        set
        {
            if (value is not null
                && int.TryParse(value.Value, CultureInfo.InvariantCulture, out int weekday))
            {
                WeeklyChallengeStartWeekday = weekday;
            }
        }
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        if (LastError is not null)
        {
            _plans = [];
            return;
        }

        LoadCatalog();
        if (LastError is not null)
        {
            _plans = [];
            return;
        }

        _plans = ConfigPlans.Select(plan => plan.Clone()).ToList();
        OnPropertyChanged(nameof(SelectedWeekday));
        OnPropertyChanged(nameof(Loop));
    }

    private void LoadCatalog()
    {
        try
        {
            ZzzBackendResult<ZzzChargePlanCatalogDto> catalogResult = _backend.GetChargePlanCatalog();
            if (!catalogResult.Success || catalogResult.Value is null)
            {
                ReportError(catalogResult.Error ?? "恶名狩猎目录读取失败。");
                return;
            }

            ZzzChargePlanCategoryDto? category = catalogResult.Value.Categories.FirstOrDefault(item =>
                string.Equals(item.Value, "恶名狩猎", StringComparison.Ordinal));
            if (category is null)
            {
                ReportError("真实手册中缺少恶名狩猎目录。");
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

            ReportError(null);
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
    }

    private List<ChargePlanItem> ConfigPlans
    {
        get => GetValue<List<ChargePlanItem>>(PlanListField);
        set => SetValue(PlanListField, value);
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

    public void SetWeekday(int value) => WeeklyChallengeStartWeekday = value;

    public void SetLoop(bool value) => Loop = value;

    public void UpdatePlan(int index, Action<ChargePlanItem> update)
    {
        if (index < 0 || index >= _plans.Count)
        {
            return;
        }

        List<ChargePlanItem> plans = _plans.Select(plan => plan.Clone()).ToList();
        update(plans[index]);
        ConfigPlans = plans;
        _plans = plans;
    }

    public void AddPlan(ChargePlanItem plan)
    {
        List<ChargePlanItem> plans = _plans.Select(item => item.Clone()).ToList();
        plans.Add(plan.Clone());
        ConfigPlans = plans;
        _plans = plans;
    }

    public void DeletePlan(int index)
    {
        if (index < 0 || index >= _plans.Count)
        {
            return;
        }

        List<ChargePlanItem> plans = _plans.Select(item => item.Clone()).ToList();
        plans.RemoveAt(index);
        ConfigPlans = plans;
        _plans = plans;
    }

    public void MoveTop(int index)
    {
        if (index <= 0 || index >= _plans.Count)
        {
            return;
        }

        List<ChargePlanItem> plans = _plans.Select(item => item.Clone()).ToList();
        ChargePlanItem plan = plans[index];
        plans.RemoveAt(index);
        plans.Insert(0, plan);
        ConfigPlans = plans;
        _plans = plans;
    }

    public void MoveTo(int sourceIndex, int insertionIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _plans.Count || insertionIndex < 0 || insertionIndex > _plans.Count)
        {
            return;
        }

        List<ChargePlanItem> plans = _plans.Select(item => item.Clone()).ToList();
        ChargePlanItem plan = plans[sourceIndex];
        plans.RemoveAt(sourceIndex);
        int adjusted = insertionIndex > sourceIndex ? insertionIndex - 1 : insertionIndex;
        plans.Insert(Math.Clamp(adjusted, 0, plans.Count), plan);
        ConfigPlans = plans;
        _plans = plans;
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

    private static List<ChargePlanItem> FromConfig(object? value)
    {
        return value is IEnumerable<ChargePlanItem> plans
            ? plans.Select(plan => plan.Clone()).ToList()
            : [];
    }

    private static List<ChargePlanItem> ToConfig(object? value) => FromConfig(value);
}

internal sealed partial class FrontierNotoriousHuntAppSettingPage : UserControl, IZzzPageLifecycle
{
    private static readonly DataFormat<string> PlanIndexFormat =
        DataFormat.CreateStringApplicationFormat("zzzod.notorious-hunt-plan-index");

    private readonly ZzzNotoriousHuntAppSettingViewModel _viewModel;
    private readonly FAInfoBar _errorBar;
    private readonly ItemsControl _planList;
    private readonly FAContentDialog _addPlanDialog;
    private readonly ContentControl _dialogPlanHost;
    private ZzzNotoriousHuntPlanRowModel? _dragCandidate;
    private Point _dragStart;
    private PointerPressedEventArgs? _dragPointerPressedArgs;
    private bool _loading;

    public FrontierNotoriousHuntAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _viewModel = new ZzzNotoriousHuntAppSettingViewModel(backend, instanceIndex, groupId, ShowError);
        DataContext = _viewModel;
        _planList = Required<ItemsControl>("PlanList");
        _addPlanDialog = Required<FAContentDialog>("AddPlanDialog");
        _dialogPlanHost = Required<ContentControl>("DialogPlanHost");
        Reload();
    }

    internal ZzzNotoriousHuntAppSettingViewModel State => _viewModel;

    public void OnPageShown() => Reload();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage() => _viewModel.DisposePage();

    private void Reload()
    {
        _viewModel.OnPageShown();
        _loading = true;
        RefreshPlans();
        _loading = false;
        ShowError(_viewModel.LastError);
    }

    private void RefreshPlans()
    {
        _planList.ItemsSource = null;
        _planList.ItemsSource = _viewModel.CreateRows();
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
                _dialogPlanHost.Content = _viewModel.CreateRow(row.Plan, -1, showCommands: false);
            }
            return;
        }

            _viewModel.UpdatePlan(row.Index, update);
        if (refresh)
        {
            _loading = true;
            RefreshPlans();
            _loading = false;
        }
        ShowError(_viewModel.LastError);
    }

    private void OnMoveTopClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is { Index: >= 0 } row)
        {
            _viewModel.MoveTop(row.Index);
            RefreshPlans();
            ShowError(_viewModel.LastError);
        }
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (Row(sender) is { Index: >= 0 } row)
        {
            _viewModel.DeletePlan(row.Index);
            RefreshPlans();
            ShowError(_viewModel.LastError);
        }
    }

    private async void OnAddClicked(object? sender, RoutedEventArgs args)
    {
        ZzzNotoriousHuntPlanRowModel row = _viewModel.CreateDialogRow();
        _dialogPlanHost.Content = row;
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            ShowError("当前窗口不可用。");
            return;
        }

        FAContentDialogResult result = await _addPlanDialog.ShowAsync(owner).ConfigureAwait(true);
        if (result == FAContentDialogResult.Primary)
        {
            _viewModel.AddPlan(row.Plan);
            RefreshPlans();
            ShowError(_viewModel.LastError);
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
        _viewModel.MoveTo(sourceIndex, insertionIndex);
        RefreshPlans();
        ShowError(_viewModel.LastError);
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

    private void ShowError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _errorBar.IsOpen = false;
            return;
        }

        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.Severity = FAInfoBarSeverity.Error;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"恶名狩猎设置缺少 {name}。");
}
