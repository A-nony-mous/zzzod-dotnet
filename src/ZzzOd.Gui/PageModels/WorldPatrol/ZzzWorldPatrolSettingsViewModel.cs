using System.Globalization;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.ApplicationSettings;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.WorldPatrol;

internal sealed class ZzzWorldPatrolSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField AutoBattleField = new("auto_battle", typeof(string), string.Empty);
    private static readonly ZzzConfigField RouteListField = new("route_list", typeof(string), string.Empty);
    private static readonly ZzzConfigField UiDisappearActionField = new("ui_disappear_action", typeof(string), "silent_fail");
    private static readonly ZzzConfigField RouteRetryActionField = new("route_retry_action", typeof(string), "skip_on_stuck_again");
    private static readonly ZzzConfigField UiDisappearSecondsField = IntField("ui_disappear_seconds", 10);
    private static readonly ZzzConfigField RouteRetryTimesField = IntField("route_retry_times", 1);
    private static readonly ZzzConfigField DailyLoopCountField = IntField("daily_loop_count", 1);
    private static readonly ZzzConfigField LoopIntervalSecondsField = IntField("loop_interval_seconds", 0);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        AutoBattleField,
        RouteListField,
        UiDisappearActionField,
        RouteRetryActionField,
        UiDisappearSecondsField,
        RouteRetryTimesField,
        DailyLoopCountField,
        LoopIntervalSecondsField,
    ];

    private static readonly IReadOnlyList<ZzzWorldPatrolOption> UiDisappearActionOptions =
    [
        new("静默失败", "silent_fail"),
        new("重开游戏并跳过路线", "restart_and_skip"),
        new("重开游戏并重试路线", "restart_and_retry"),
    ];

    private static readonly IReadOnlyList<ZzzWorldPatrolOption> RouteRetryActionOptions =
    [
        new("若再次卡住则跳过脱困", "skip_on_stuck_again"),
        new("若再次卡住仍尝试脱困", "retry_on_stuck_again"),
    ];

    private readonly int _instanceIndex;
    private readonly string _groupId;
    private IReadOnlyList<ZzzWorldPatrolOption> _autoBattleOptions = [];
    private IReadOnlyList<ZzzWorldPatrolOption> _routeListOptions = [];

    public ZzzWorldPatrolSettingsViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => "world-patrol";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzWorldPatrolOption> AutoBattleOptions => _autoBattleOptions;

    public IReadOnlyList<ZzzWorldPatrolOption> RouteListOptions => _routeListOptions;

    public IReadOnlyList<ZzzWorldPatrolOption> UiDisappearActionOptionsList => UiDisappearActionOptions;

    public IReadOnlyList<ZzzWorldPatrolOption> RouteRetryActionOptionsList => RouteRetryActionOptions;

    public string AutoBattle
    {
        get => GetValue<string>(AutoBattleField);
        set => SetValue(AutoBattleField, value);
    }

    public ZzzWorldPatrolOption? SelectedAutoBattle
    {
        get => FindOption(AutoBattleOptions, AutoBattle);
        set
        {
            if (value is not null)
            {
                AutoBattle = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }

    public string RouteList
    {
        get => GetValue<string>(RouteListField);
        set => SetValue(RouteListField, value);
    }

    public ZzzWorldPatrolOption? SelectedRouteList
    {
        get => FindOption(RouteListOptions, RouteList);
        set
        {
            if (value is not null)
            {
                RouteList = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }

    public string UiDisappearAction
    {
        get => GetValue<string>(UiDisappearActionField);
        set => SetValue(UiDisappearActionField, value);
    }

    public ZzzWorldPatrolOption? SelectedUiDisappearAction
    {
        get => FindOption(UiDisappearActionOptions, UiDisappearAction);
        set
        {
            if (value is not null)
            {
                UiDisappearAction = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }

    public string RouteRetryAction
    {
        get => GetValue<string>(RouteRetryActionField);
        set => SetValue(RouteRetryActionField, value);
    }

    public ZzzWorldPatrolOption? SelectedRouteRetryAction
    {
        get => FindOption(RouteRetryActionOptions, RouteRetryAction);
        set
        {
            if (value is not null)
            {
                RouteRetryAction = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }

    public double UiDisappearSeconds
    {
        get => GetValue<double>(UiDisappearSecondsField);
        set => SetValue(UiDisappearSecondsField, value);
    }

    public double RouteRetryTimes
    {
        get => GetValue<double>(RouteRetryTimesField);
        set => SetValue(RouteRetryTimesField, value);
    }

    public double DailyLoopCount
    {
        get => GetValue<double>(DailyLoopCountField);
        set => SetValue(DailyLoopCountField, value);
    }

    public double LoopIntervalSeconds
    {
        get => GetValue<double>(LoopIntervalSecondsField);
        set => SetValue(LoopIntervalSecondsField, value);
    }

    public void SetCatalog(ZzzWorldPatrolCatalogDto catalog)
    {
        _autoBattleOptions = catalog.AutoBattleConfigs
            .Select(value => new ZzzWorldPatrolOption(value, value))
            .ToArray();
        _routeListOptions = new[] { new ZzzWorldPatrolOption("全部", string.Empty) }
            .Concat(catalog.RouteLists.Select(item => new ZzzWorldPatrolOption(item.Name, item.Name)))
            .ToArray();
        OnPropertyChanged(nameof(AutoBattleOptions));
        OnPropertyChanged(nameof(RouteListOptions));
        OnPropertyChanged(nameof(SelectedAutoBattle));
        OnPropertyChanged(nameof(SelectedRouteList));
    }

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        OnPropertyChanged(nameof(SelectedAutoBattle));
        OnPropertyChanged(nameof(SelectedRouteList));
        OnPropertyChanged(nameof(SelectedUiDisappearAction));
        OnPropertyChanged(nameof(SelectedRouteRetryAction));
    }

    private static ZzzConfigField IntField(string key, int defaultValue) => new(
        key,
        typeof(double),
        (double)defaultValue,
        ToConfig: value => Convert.ToInt32(value, CultureInfo.InvariantCulture));

    private static ZzzWorldPatrolOption? FindOption(
        IReadOnlyList<ZzzWorldPatrolOption> options,
        string value) => options.FirstOrDefault(option =>
        string.Equals(Convert.ToString(option.Value, CultureInfo.InvariantCulture), value, StringComparison.Ordinal));
}
