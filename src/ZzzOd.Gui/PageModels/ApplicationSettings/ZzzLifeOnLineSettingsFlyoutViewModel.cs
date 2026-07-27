using System.Globalization;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.LifeOnLine;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzLifeOnLineTeamOption(string Label, int Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzLifeOnLineSettingsFlyoutViewModel : ZzzConfigSectionViewModel
{
    internal const string ScopeNameValue = "life-on-line";

    private static readonly ZzzConfigField DailyPlanTimesField =
        new("daily_plan_times", typeof(int), 20);
    private static readonly ZzzConfigField PredefinedTeamIndexField =
        new("predefined_team_idx", typeof(int), -1);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        DailyPlanTimesField,
        PredefinedTeamIndexField,
    ];

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private int _dailyRunTimes;
    private IReadOnlyList<ZzzLifeOnLineTeamOption> _teamOptions = [];

    public ZzzLifeOnLineSettingsFlyoutViewModel(
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

    protected override string ScopeName => ScopeNameValue;

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public double DailyPlanTimes
    {
        get => GetValue<int>(DailyPlanTimesField);
        set => SetValue(DailyPlanTimesField, Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    public int PredefinedTeamIndex
    {
        get => GetValue<int>(PredefinedTeamIndexField);
        set
        {
            if (SetValue(PredefinedTeamIndexField, value))
            {
                OnPropertyChanged(nameof(SelectedTeam));
            }
        }
    }

    public int DailyRunTimes
    {
        get => _dailyRunTimes;
        private set
        {
            if (SetProperty(ref _dailyRunTimes, value))
            {
                OnPropertyChanged(nameof(DoneText));
            }
        }
    }

    public string DoneText => $"当日: {DailyRunTimes}";

    public IReadOnlyList<ZzzLifeOnLineTeamOption> TeamOptions
    {
        get => _teamOptions;
        private set
        {
            if (SetProperty(ref _teamOptions, value))
            {
                OnPropertyChanged(nameof(SelectedTeam));
            }
        }
    }

    public ZzzLifeOnLineTeamOption? SelectedTeam
    {
        get => TeamOptions.FirstOrDefault(option => option.Value == PredefinedTeamIndex);
        set
        {
            if (value is not null)
            {
                PredefinedTeamIndex = value.Value;
            }
        }
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        if (LastError is not null)
        {
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> teamConfig = _backend.GetConfigScope("team", _instanceIndex);
        if (!teamConfig.Success || teamConfig.Value is null)
        {
            ReportError(teamConfig.Error ?? "预备编队配置读取失败。");
            return;
        }

        ZzzBackendResult<ZzzLifeOnLineRunRecordDto> runRecord =
            _backend.GetLifeOnLineRunRecord(_instanceIndex);
        if (!runRecord.Success || runRecord.Value is null)
        {
            ReportError(runRecord.Error ?? "生命热线运行记录读取失败。");
            return;
        }

        if (!teamConfig.Value.Values.TryGetValue("team_list", out object? rawTeams)
            || rawTeams is not List<PredefinedTeamInfo> teams)
        {
            ReportError("预备编队配置缺少 team_list?");
            return;
        }

        TeamOptions =
        [
            new("游戏内配队", -1),
            .. teams.Select(team => new ZzzLifeOnLineTeamOption(team.Name, team.Idx)),
        ];
        DailyRunTimes = runRecord.Value.DailyRunTimes;
        OnPropertyChanged(nameof(DailyPlanTimes));
        OnPropertyChanged(nameof(SelectedTeam));
        ReportError(null);
    }

    internal bool SaveForTest(string key, object value)
    {
        switch (key)
        {
            case "daily_plan_times":
                DailyPlanTimes = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            case "predefined_team_idx":
                PredefinedTeamIndex = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "未知的生命热线配置字段。");
        }

        return LastError is null;
    }
}
