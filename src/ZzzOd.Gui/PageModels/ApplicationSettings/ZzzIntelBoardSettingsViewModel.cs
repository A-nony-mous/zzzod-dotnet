using CommunityToolkit.Mvvm.Input;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzIntelBoardSettingOption(string Label, object Value)
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzIntelBoardSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField PredefinedTeamIndexField =
        new("predefined_team_idx", typeof(int), -1);
    private static readonly ZzzConfigField AutoBattleConfigField =
        new("auto_battle_config", typeof(string), "全配队通用");
    private static readonly ZzzConfigField ExpGrindModeField =
        new("exp_grind_mode", typeof(bool), false);

    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        PredefinedTeamIndexField,
        AutoBattleConfigField,
        ExpGrindModeField,
    ];

    private readonly IZzzAppBackend _backend;
    private readonly IZzzIntelBoardProgressBackend _progressBackend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private IReadOnlyList<ZzzIntelBoardSettingOption> _predefinedTeamOptions = [];
    private IReadOnlyList<ZzzIntelBoardSettingOption> _autoBattleOptions = [];
    private string _resetButtonText = "重置进度";
    private bool _resetButtonEnabled = true;

    public ZzzIntelBoardSettingsViewModel(
        IZzzAppBackend backend,
        IZzzIntelBoardProgressBackend progressBackend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
        _progressBackend = progressBackend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => "intel-board";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzIntelBoardSettingOption> PredefinedTeamOptions
    {
        get => _predefinedTeamOptions;
        private set => SetProperty(ref _predefinedTeamOptions, value);
    }

    public IReadOnlyList<ZzzIntelBoardSettingOption> AutoBattleOptions
    {
        get => _autoBattleOptions;
        private set => SetProperty(ref _autoBattleOptions, value);
    }

    public ZzzIntelBoardSettingOption? SelectedPredefinedTeam
    {
        get => FindOption(PredefinedTeamOptions, PredefinedTeamIndex);
        set
        {
            if (value is null)
            {
                return;
            }

            PredefinedTeamIndex = (int)value.Value;
            OnPropertyChanged(nameof(AutoBattleVisible));
        }
    }

    public ZzzIntelBoardSettingOption? SelectedAutoBattle
    {
        get => FindOption(AutoBattleOptions, AutoBattleConfig);
        set
        {
            if (value is not null)
            {
                AutoBattleConfig = (string)value.Value;
            }
        }
    }

    public bool ExpGrindMode
    {
        get => GetValue<bool>(ExpGrindModeField);
        set => SetValue(ExpGrindModeField, value);
    }

    public bool AutoBattleVisible => PredefinedTeamIndex == -1;

    public string ResetButtonText
    {
        get => _resetButtonText;
        private set => SetProperty(ref _resetButtonText, value);
    }

    public bool ResetButtonEnabled
    {
        get => _resetButtonEnabled;
        private set => SetProperty(ref _resetButtonEnabled, value);
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

    internal void ResetProgressForTest() => ResetProgress();

    internal void SaveForTest(string key, object? value)
    {
        ZzzConfigField field = Fields.SingleOrDefault(candidate => candidate.Key == key)
            ?? throw new ArgumentOutOfRangeException(nameof(key), key, "未知的情报板配置字段。");
        SaveValue(field, value);
    }

    private int PredefinedTeamIndex
    {
        get => GetValue<int>(PredefinedTeamIndexField);
        set => SetValue(PredefinedTeamIndexField, value);
    }

    private string AutoBattleConfig
    {
        get => GetValue<string>(AutoBattleConfigField);
        set => SetValue(AutoBattleConfigField, value);
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
                .. teams.Select(team => new ZzzIntelBoardSettingOption(team.Name, team.Idx)),
            ];
            AutoBattleOptions = catalogResult.Value.AutoBattle
                .Select(value => new ZzzIntelBoardSettingOption(value, value))
                .ToArray();
            ReportError(null);
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
    }

    [RelayCommand]
    private void ResetProgress()
    {
        try
        {
            ZzzBackendResult<bool> result = _progressBackend.ResetIntelBoardProgress(_instanceIndex);
            if (!result.Success)
            {
                ReportError(result.Error ?? "情报板进度重置失败。");
                return;
            }

            ReportError(null);
            ResetButtonText = "已重置";
            ResetButtonEnabled = false;
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
    }

    private void NotifySelections()
    {
        OnPropertyChanged(nameof(SelectedPredefinedTeam));
        OnPropertyChanged(nameof(SelectedAutoBattle));
        OnPropertyChanged(nameof(ExpGrindMode));
        OnPropertyChanged(nameof(AutoBattleVisible));
    }

    private static ZzzIntelBoardSettingOption? FindOption(
        IReadOnlyList<ZzzIntelBoardSettingOption> options,
        object value) => options.FirstOrDefault(option => Equals(option.Value, value));
}
