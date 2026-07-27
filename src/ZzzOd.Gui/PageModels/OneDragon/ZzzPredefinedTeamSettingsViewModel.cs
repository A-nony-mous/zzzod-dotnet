using System.Collections.ObjectModel;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.OneDragon;

internal sealed class ZzzPredefinedTeamSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField TeamListField = new(
        "team_list",
        typeof(List<PredefinedTeamInfo>),
        new List<PredefinedTeamInfo>(),
        FromConfig: value => value is List<PredefinedTeamInfo> teams ? CloneTeams(teams) : new List<PredefinedTeamInfo>());
    private readonly IZzzAppBackend _backend;
    private readonly ObservableCollection<ZzzPredefinedTeamRowModel> _rows = [];
    private IReadOnlyList<ZzzPredefinedTeamOption> _autoBattleOptions = [];
    private bool _valuesAvailable;
    private bool _loading;

    public ZzzPredefinedTeamSettingsViewModel(IZzzAppBackend backend, Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
    }

    public IReadOnlyList<ZzzPredefinedTeamRowModel> Rows => _rows;

    public IReadOnlyList<ZzzPredefinedTeamOption> AutoBattleOptions => _autoBattleOptions;

    public IReadOnlyList<ZzzPredefinedTeamOption> AgentOptions { get; } = CreateAgentOptions();

    public bool ValuesAvailable
    {
        get => _valuesAvailable;
        private set => SetProperty(ref _valuesAvailable, value);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(LastError);

    protected override string ScopeName => "team";

    protected override IReadOnlyList<ZzzConfigField> Fields => [TeamListField];

    public override void OnPageShown()
    {
        base.OnPageShown();
        _loading = true;
        try
        {
            ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> catalog = _backend.GetBattleAssistantConfigCatalog();
            if (!catalog.Success || catalog.Value is null)
            {
                _rows.Clear();
                _autoBattleOptions = [];
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(AutoBattleOptions));
                ReportError(catalog.Error ?? "自动战斗配置读取失败。");
                return;
            }

            _autoBattleOptions = catalog.Value.AutoBattle
                .Select(value => new ZzzPredefinedTeamOption(value, value))
                .ToArray();
            OnPropertyChanged(nameof(AutoBattleOptions));
            RebuildRows();
            ReportError(ValuesAvailable ? null : "预备编队配置读取失败。");
        }
        finally
        {
            _loading = false;
        }
    }

    public void SaveTeam(ZzzPredefinedTeamRowModel changedRow)
    {
        if (_loading || !changedRow.HasChanges || !ValuesAvailable)
        {
            return;
        }

        List<PredefinedTeamInfo> teams = _rows.Select(row => new PredefinedTeamInfo(
            row.Index,
            row.Name,
            row.AutoBattleValue,
            [row.Agent1Value, row.Agent2Value, row.Agent3Value])).ToList();
        SaveValue(TeamListField, teams);
        if (LastError is null)
        {
            foreach (ZzzPredefinedTeamRowModel row in _rows)
            {
                row.MarkSaved();
            }
        }
    }

    internal static IReadOnlyList<ZzzPredefinedTeamOption> CreateAgentOptions() =>
    [
        new("代理人", "unknown"),
        .. AgentEnum.Values.Select(item => new ZzzPredefinedTeamOption(item.Value.AgentName, item.Value.AgentId)),
    ];

    internal static bool IsTeamNameWithinLimit(string value) =>
        value.Sum(character => character > 127 ? 2 : 1) <= 14;

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        ValuesAvailable = values.Values.ContainsKey(TeamListField.Key)
            && GetValue<List<PredefinedTeamInfo>>(TeamListField) is not null;
        OnPropertyChanged(nameof(ValuesAvailable));
    }

    private void RebuildRows()
    {
        _rows.Clear();
        foreach (PredefinedTeamInfo team in GetValue<List<PredefinedTeamInfo>>(TeamListField))
        {
            team.EnsureThreeAgents();
            _rows.Add(new ZzzPredefinedTeamRowModel(team, _autoBattleOptions, AgentOptions));
        }

        OnPropertyChanged(nameof(Rows));
    }

    private static List<PredefinedTeamInfo> CloneTeams(IEnumerable<PredefinedTeamInfo> teams) =>
        teams.Select(team => new PredefinedTeamInfo(
            team.Idx,
            team.Name,
            team.AutoBattle,
            team.AgentIdList.ToList())).ToList();
}
