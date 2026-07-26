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

