using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Coffee;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzCoffeeSettingOption(string Label, object Value, string Description = "")
{
    public override string ToString() => Label;
}

internal sealed class ZzzCoffeeAppSettingViewModel : ZzzConfigSectionViewModel
{
    internal const string CoffeeScopeName = "coffee";

    private const string DefaultTransportPoint = "六分街 - 咖啡店";
    private const string DefaultAutoBattle = "全配队通用";

    private static readonly ZzzConfigField TransportPointField =
        new("transport_point", typeof(string), DefaultTransportPoint);
    private static readonly ZzzConfigField ChooseWayField =
        new("choose_way", typeof(string), CoffeeChooseWay.PlanPriority);
    private static readonly ZzzConfigField ChallengeWayField =
        new("challenge_way", typeof(string), CoffeeChallengeWay.All);
    private static readonly ZzzConfigField CardNumField =
        new("card_num", typeof(string), CoffeeCardNum.Num1);
    private static readonly ZzzConfigField PredefinedTeamIndexField =
        new("predefined_team_idx", typeof(int), -1);
    private static readonly ZzzConfigField AutoBattleField =
        new("auto_battle", typeof(string), DefaultAutoBattle);
    private static readonly ZzzConfigField RunChargePlanAfterwardsField =
        new("run_charge_plan_afterwards", typeof(bool), false);

    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        TransportPointField,
        ChooseWayField,
        ChallengeWayField,
        CardNumField,
        PredefinedTeamIndexField,
        AutoBattleField,
        RunChargePlanAfterwardsField,
    ];

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private IReadOnlyList<ZzzCoffeeSettingOption> _predefinedTeamOptions = [];
    private IReadOnlyList<ZzzCoffeeSettingOption> _autoBattleOptions = [];

    public ZzzCoffeeAppSettingViewModel(
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

    protected override string ScopeName => CoffeeScopeName;

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzCoffeeSettingOption> TransportPointOptions { get; } =
    [
        new("六分街 - 咖啡店", "六分街 - 咖啡店"),
        new("澄辉坪 - 汀曼咖啡", "澄辉坪 - 汀曼咖啡"),
    ];

    public IReadOnlyList<ZzzCoffeeSettingOption> ChooseWayOptions { get; } =
    [
        new(
            CoffeeChooseWay.PlanPriority,
            CoffeeChooseWay.PlanPriority,
            "优先选择符合体力计划的咖啡，实战模拟室计划会选浓缩咖啡，没有匹配时选择汀曼特调"),
        new(CoffeeChooseWay.TinmanOnly, CoffeeChooseWay.TinmanOnly, "只选择汀曼特调"),
        new(CoffeeChooseWay.EspressoOnly, CoffeeChooseWay.EspressoOnly, "只选择浓缩咖啡"),
    ];

    public IReadOnlyList<ZzzCoffeeSettingOption> ChallengeWayOptions { get; } = CoffeeChallengeWay.Options
        .Select(option => new ZzzCoffeeSettingOption(option.Label, option.Value ?? option.Label))
        .ToArray();

    public IReadOnlyList<ZzzCoffeeSettingOption> CardNumOptions { get; } =
    [
        new(CoffeeCardNum.Default, CoffeeCardNum.Default, "挑战体力计划外的副本时，按游戏内设数量"),
        new(CoffeeCardNum.Num1, CoffeeCardNum.Num1, "挑战体力计划外的副本时，选择最少数量"),
    ];

    public IReadOnlyList<ZzzCoffeeSettingOption> PredefinedTeamOptions
    {
        get => _predefinedTeamOptions;
        private set => SetProperty(ref _predefinedTeamOptions, value);
    }

    public IReadOnlyList<ZzzCoffeeSettingOption> AutoBattleOptions
    {
        get => _autoBattleOptions;
        private set => SetProperty(ref _autoBattleOptions, value);
    }

    public ZzzCoffeeSettingOption? SelectedTransportPoint
    {
        get => FindOption(TransportPointOptions, TransportPoint);
        set => SetSelected(value, selected => TransportPoint = (string)selected.Value);
    }

    public ZzzCoffeeSettingOption? SelectedChooseWay
    {
        get => FindOption(ChooseWayOptions, ChooseWay);
        set
        {
            SetSelected(value, selected => ChooseWay = (string)selected.Value);
            OnPropertyChanged(nameof(ChooseWayDescription));
        }
    }

    public ZzzCoffeeSettingOption? SelectedChallengeWay
    {
        get => FindOption(ChallengeWayOptions, ChallengeWay);
        set => SetSelected(value, selected => ChallengeWay = (string)selected.Value);
    }

    public ZzzCoffeeSettingOption? SelectedCardNum
    {
        get => FindOption(CardNumOptions, CardNum);
        set
        {
            SetSelected(value, selected => CardNum = (string)selected.Value);
            OnPropertyChanged(nameof(CardNumDescription));
        }
    }

    public ZzzCoffeeSettingOption? SelectedPredefinedTeam
    {
        get => FindOption(PredefinedTeamOptions, PredefinedTeamIndex);
        set
        {
            SetSelected(value, selected => PredefinedTeamIndex = (int)selected.Value);
            OnPropertyChanged(nameof(AutoBattleVisible));
        }
    }

    public ZzzCoffeeSettingOption? SelectedAutoBattle
    {
        get => FindOption(AutoBattleOptions, AutoBattle);
        set => SetSelected(value, selected => AutoBattle = (string)selected.Value);
    }

    public string ChooseWayDescription => SelectedChooseWay?.Description ?? string.Empty;

    public string CardNumDescription => SelectedCardNum?.Description ?? string.Empty;

    public bool AutoBattleVisible => PredefinedTeamIndex == -1;

    public bool RunChargePlanAfterwards
    {
        get => GetValue<bool>(RunChargePlanAfterwardsField);
        set => SetValue(RunChargePlanAfterwardsField, value);
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        if (LastError is not null)
        {
            return;
        }

        LoadAuxiliaryOptions();
        NotifySelections();
    }

    internal void SaveForTest(string key, object? value)
    {
        ZzzConfigField field = Fields.SingleOrDefault(candidate => candidate.Key == key)
            ?? throw new ArgumentOutOfRangeException(nameof(key), key, "未知的咖啡计划配置字段。");
        SaveValue(field, value);
    }

    private string TransportPoint
    {
        get => GetValue<string>(TransportPointField);
        set => SetValue(TransportPointField, value);
    }

    private string ChooseWay
    {
        get => GetValue<string>(ChooseWayField);
        set => SetValue(ChooseWayField, value);
    }

    private string ChallengeWay
    {
        get => GetValue<string>(ChallengeWayField);
        set => SetValue(ChallengeWayField, value);
    }

    private string CardNum
    {
        get => GetValue<string>(CardNumField);
        set => SetValue(CardNumField, value);
    }

    private int PredefinedTeamIndex
    {
        get => GetValue<int>(PredefinedTeamIndexField);
        set => SetValue(PredefinedTeamIndexField, value);
    }

    private string AutoBattle
    {
        get => GetValue<string>(AutoBattleField);
        set => SetValue(AutoBattleField, value);
    }

    private void LoadAuxiliaryOptions()
    {
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> teamResult =
                _backend.GetConfigScope("team", _instanceIndex);
            if (!teamResult.Success
                || teamResult.Value is null
                || !teamResult.Value.Values.TryGetValue("team_list", out object? rawTeams)
                || rawTeams is not List<PredefinedTeamInfo> teams)
            {
                ReportError(teamResult.Error ?? "预备编队配置读取失败。");
                return;
            }

            ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> catalogResult =
                _backend.GetBattleAssistantConfigCatalog();
            if (!catalogResult.Success || catalogResult.Value is null)
            {
                ReportError(catalogResult.Error ?? "自动战斗配置读取失败。");
                return;
            }

            PredefinedTeamOptions =
            [
                new("游戏内配队", -1),
                .. teams.Select(team => new ZzzCoffeeSettingOption(team.Name, team.Idx)),
            ];
            AutoBattleOptions = catalogResult.Value.AutoBattle
                .Select(value => new ZzzCoffeeSettingOption(value, value))
                .ToArray();
            ReportError(null);
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
    }

    private void NotifySelections()
    {
        OnPropertyChanged(nameof(SelectedTransportPoint));
        OnPropertyChanged(nameof(SelectedChooseWay));
        OnPropertyChanged(nameof(SelectedChallengeWay));
        OnPropertyChanged(nameof(SelectedCardNum));
        OnPropertyChanged(nameof(SelectedPredefinedTeam));
        OnPropertyChanged(nameof(SelectedAutoBattle));
        OnPropertyChanged(nameof(ChooseWayDescription));
        OnPropertyChanged(nameof(CardNumDescription));
        OnPropertyChanged(nameof(AutoBattleVisible));
        OnPropertyChanged(nameof(RunChargePlanAfterwards));
    }

    private void SetSelected(ZzzCoffeeSettingOption? selected, Action<ZzzCoffeeSettingOption> apply)
    {
        if (selected is null)
        {
            return;
        }

        apply(selected);
    }

    private static ZzzCoffeeSettingOption? FindOption(
        IReadOnlyList<ZzzCoffeeSettingOption> options,
        object value) => options.FirstOrDefault(option => Equals(option.Value, value));
}
