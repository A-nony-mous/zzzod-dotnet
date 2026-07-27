using CommunityToolkit.Mvvm.Input;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Services.Config;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed partial class ZzzShiyuDefenseAppSettingViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField TeamListField = new(
        "team_list",
        typeof(List<ShiyuDefenseTeamConfig>),
        new List<ShiyuDefenseTeamConfig>(),
        FromConfig,
        ToConfig);

    private static readonly IReadOnlyList<ZzzConfigField> FieldList = [TeamListField];

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private IReadOnlyList<ZzzShiyuDefenseTeamRowModel> _rows = [];

    public ZzzShiyuDefenseAppSettingViewModel(
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

    protected override string ScopeName => "shiyu-defense";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzShiyuDefenseTeamRowModel> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        if (LastError is not null)
        {
            Rows = [];
            return;
        }

        LoadRows();
    }

    internal void SaveForTest(string key, object? value)
    {
        ZzzConfigField field = Fields.SingleOrDefault(candidate => candidate.Key == key)
            ?? throw new ArgumentOutOfRangeException(nameof(key), key, "未知的式舆防卫战配置字段。");
        SaveValue(field, value);
    }

    internal void ResetRunRecordForTest() => ResetRunRecord();

    [RelayCommand]
    private void ResetRunRecord()
    {
        try
        {
            ZzzBackendResult<ZzzShiyuDefenseRunRecordDto> result =
                _backend.ResetShiyuDefenseRunRecord(_instanceIndex);
            ReportError(result.Success ? null : result.Error ?? "式舆防卫战运行记录重置失败。");
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
    }

    private List<ShiyuDefenseTeamConfig> TeamConfigs
    {
        get => GetValue<List<ShiyuDefenseTeamConfig>>(TeamListField);
        set => SetValue(TeamListField, value);
    }

    private void LoadRows()
    {
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> teamResult =
                _backend.GetConfigScope("team", _instanceIndex);
            if (!teamResult.Success || teamResult.Value is null)
            {
                ReportError(teamResult.Error ?? "预备编队配置读取失败。");
                Rows = [];
                return;
            }

            List<PredefinedTeamInfo> teams = RequiredList<PredefinedTeamInfo>(teamResult.Value.Values, "team_list");
            Dictionary<int, ShiyuDefenseTeamConfig> configs = TeamConfigs
                .Select(Clone)
                .ToDictionary(item => item.TeamIndex);
            Rows = teams.Select(team => CreateRow(team, configs.GetValueOrDefault(team.Idx))).ToList();
        }
        catch (Exception exception)
        {
            Rows = [];
            ReportError(exception.Message);
        }
    }

    private ZzzShiyuDefenseTeamRowModel CreateRow(
        PredefinedTeamInfo team,
        ShiyuDefenseTeamConfig? config)
    {
        HashSet<DmgTypeEnum> weaknesses = config?.WeaknessList.ToHashSet() ?? [];
        return new ZzzShiyuDefenseTeamRowModel(
            team.Idx,
            team.Name,
            team.AutoBattle,
            config?.ForCritical == true,
            weaknesses.Contains(DmgTypeEnum.ELECTRIC),
            weaknesses.Contains(DmgTypeEnum.ETHER),
            weaknesses.Contains(DmgTypeEnum.PHYSICAL),
            weaknesses.Contains(DmgTypeEnum.FIRE),
            weaknesses.Contains(DmgTypeEnum.ICE),
            weaknesses.Contains(DmgTypeEnum.WIND),
            OnRowChanged);
    }

    private void OnRowChanged(int teamIndex, DmgTypeEnum? weakness, bool value)
    {
        List<ShiyuDefenseTeamConfig> configs = TeamConfigs.Select(Clone).ToList();
        ShiyuDefenseTeamConfig? config = configs.FirstOrDefault(item => item.TeamIndex == teamIndex);
        if (config is null)
        {
            config = new ShiyuDefenseTeamConfig { TeamIndex = teamIndex };
            configs.Add(config);
        }

        if (weakness is null)
        {
            if (config.ForCritical == value)
            {
                return;
            }

            config.ForCritical = value;
        }
        else
        {
            List<DmgTypeEnum> weaknesses = config.WeaknessList;
            bool changed = value ? !weaknesses.Contains(weakness.Value) : weaknesses.Remove(weakness.Value);
            if (!changed)
            {
                return;
            }

            if (value)
            {
                weaknesses.Add(weakness.Value);
            }

            config.WeaknessList = weaknesses;
        }

        TeamConfigs = configs;
    }

    private static List<ShiyuDefenseTeamConfig> FromConfig(object? value)
    {
        return value is IEnumerable<ShiyuDefenseTeamConfig> configs
            ? configs.Select(Clone).ToList()
            : [];
    }

    private static List<ShiyuDefenseTeamConfig> ToConfig(object? value) => FromConfig(value);

    private static ShiyuDefenseTeamConfig Clone(ShiyuDefenseTeamConfig value) => new()
    {
        TeamIndex = value.TeamIndex,
        ForCritical = value.ForCritical,
        WeaknessListRaw = value.WeaknessListRaw.ToList(),
    };

    private static List<T> RequiredList<T>(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value) || value is not IEnumerable<T> list)
        {
            throw new InvalidOperationException($"配置缺少 {key}。");
        }

        return list.ToList();
    }
}
