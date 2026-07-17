using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed class ZzzShiyuDefenseTeamRowModel
{
    public required int TeamIndex { get; init; }

    public required string TeamName { get; init; }

    public required string AutoBattleConfig { get; init; }

    public bool ForCritical { get; set; }

    public bool Electric { get; set; }

    public bool Ether { get; set; }

    public bool Physical { get; set; }

    public bool Fire { get; set; }

    public bool Ice { get; set; }

    public bool Wind { get; set; }

    public bool IsWeakness(DmgTypeEnum type) => type switch
    {
        DmgTypeEnum.ELECTRIC => Electric,
        DmgTypeEnum.ETHER => Ether,
        DmgTypeEnum.PHYSICAL => Physical,
        DmgTypeEnum.FIRE => Fire,
        DmgTypeEnum.ICE => Ice,
        DmgTypeEnum.WIND => Wind,
        _ => false,
    };
}

internal sealed class ZzzShiyuDefenseAppSettingState
{
    private const string ScopeName = "shiyu-defense";
    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private List<ShiyuDefenseTeamConfig> _configs = [];
    private List<ZzzShiyuDefenseTeamRowModel> _rows = [];

    public ZzzShiyuDefenseAppSettingState(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    public string? LastError { get; private set; }

    public IReadOnlyList<ZzzShiyuDefenseTeamRowModel> Rows => _rows;

    public void Reload()
    {
        LastError = null;
        ZzzBackendResult<ZzzConfigScopeValuesDto> teamResult = _backend.GetConfigScope("team", _instanceIndex);
        if (!teamResult.Success || teamResult.Value is null)
        {
            Fail(teamResult.Error ?? "预备编队配置读取失败。");
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> shiyuResult = _backend.GetConfigScope(
            ScopeName,
            _instanceIndex,
            _groupId);
        if (!shiyuResult.Success || shiyuResult.Value is null)
        {
            Fail(shiyuResult.Error ?? "式舆防卫战配置读取失败。");
            return;
        }

        try
        {
            List<PredefinedTeamInfo> teams = RequiredList<PredefinedTeamInfo>(teamResult.Value.Values, "team_list");
            _configs = RequiredList<ShiyuDefenseTeamConfig>(shiyuResult.Value.Values, "team_list")
                .Select(Clone)
                .ToList();
            _rows = teams.Select(CreateRow).ToList();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    public void SetForCritical(int teamIndex, bool value)
    {
        ShiyuDefenseTeamConfig config = GetOrCreate(teamIndex);
        if (config.ForCritical == value)
        {
            return;
        }

        config.ForCritical = value;
        Save();
    }

    public void SetWeakness(int teamIndex, DmgTypeEnum type, bool enabled)
    {
        ShiyuDefenseTeamConfig config = GetOrCreate(teamIndex);
        List<DmgTypeEnum> weaknesses = config.WeaknessList;
        bool changed;
        if (enabled)
        {
            changed = !weaknesses.Contains(type);
            if (changed)
            {
                weaknesses.Add(type);
            }
        }
        else
        {
            changed = weaknesses.Remove(type);
        }

        if (!changed)
        {
            return;
        }

        config.WeaknessList = weaknesses;
        Save();
    }

    public void ResetRunRecord()
    {
        ZzzBackendResult<ZzzShiyuDefenseRunRecordDto> result = _backend.ResetShiyuDefenseRunRecord(_instanceIndex);
        LastError = result.Success ? null : result.Error ?? "式舆防卫战运行记录重置失败。";
    }

    private ZzzShiyuDefenseTeamRowModel CreateRow(PredefinedTeamInfo team)
    {
        ShiyuDefenseTeamConfig? config = _configs.FirstOrDefault(item => item.TeamIndex == team.Idx);
        HashSet<DmgTypeEnum> weaknesses = config?.WeaknessList.ToHashSet() ?? [];
        return new ZzzShiyuDefenseTeamRowModel
        {
            TeamIndex = team.Idx,
            TeamName = team.Name,
            AutoBattleConfig = team.AutoBattle,
            ForCritical = config?.ForCritical == true,
            Electric = weaknesses.Contains(DmgTypeEnum.ELECTRIC),
            Ether = weaknesses.Contains(DmgTypeEnum.ETHER),
            Physical = weaknesses.Contains(DmgTypeEnum.PHYSICAL),
            Fire = weaknesses.Contains(DmgTypeEnum.FIRE),
            Ice = weaknesses.Contains(DmgTypeEnum.ICE),
            Wind = weaknesses.Contains(DmgTypeEnum.WIND),
        };
    }

    private ShiyuDefenseTeamConfig GetOrCreate(int teamIndex)
    {
        ShiyuDefenseTeamConfig? config = _configs.FirstOrDefault(item => item.TeamIndex == teamIndex);
        if (config is not null)
        {
            return config;
        }

        config = new ShiyuDefenseTeamConfig { TeamIndex = teamIndex };
        _configs.Add(config);
        return config;
    }

    private void Save()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?>
            {
                ["team_list"] = _configs.Select(Clone).ToList(),
            },
            _instanceIndex,
            _groupId));
        if (result.Success)
        {
            LastError = null;
            return;
        }

        string error = result.Error ?? "式舆防卫战配置保存失败。";
        Reload();
        LastError = error;
    }

    private void Fail(string message)
    {
        LastError = message;
        _configs = [];
        _rows = [];
    }

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

internal sealed partial class ZzzShiyuDefenseAppSettingPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzShiyuDefenseAppSettingState _state;
    private readonly InfoBar _errorBar;
    private readonly ListBox _teamList;
    private bool _loading;

    public ZzzShiyuDefenseAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _state = new ZzzShiyuDefenseAppSettingState(backend, instanceIndex, groupId);
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<InfoBar>("ErrorBar");
        _teamList = Required<ListBox>("TeamList");
        Reload();
    }

    internal ZzzShiyuDefenseAppSettingState State => _state;

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
        _loading = true;
        _state.Reload();
        _teamList.ItemsSource = _state.Rows;
        _loading = false;
        ShowError();
    }

    private void OnCriticalChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && sender is CheckBox { DataContext: ZzzShiyuDefenseTeamRowModel row } checkBox)
        {
            _state.SetForCritical(row.TeamIndex, checkBox.IsChecked == true);
            ShowError();
        }
    }

    private void OnWeaknessChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading || sender is not CheckBox
            {
                DataContext: ZzzShiyuDefenseTeamRowModel row,
                Tag: string typeName,
            } checkBox
            || !Enum.TryParse(typeName, out DmgTypeEnum type)
            || type == DmgTypeEnum.UNKNOWN)
        {
            return;
        }

        _state.SetWeakness(row.TeamIndex, type, checkBox.IsChecked == true);
        ShowError();
    }

    private void OnCriticalResetClicked(object? sender, RoutedEventArgs args)
    {
        _state.ResetRunRecord();
        ShowError();
    }

    private void ShowError()
    {
        if (string.IsNullOrWhiteSpace(_state.LastError))
        {
            _errorBar.IsOpen = false;
            return;
        }

        _errorBar.Title = "错误";
        _errorBar.Message = _state.LastError;
        _errorBar.Severity = InfoBarSeverity.Error;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"式舆防卫战设置缺少 {name}。");
}
