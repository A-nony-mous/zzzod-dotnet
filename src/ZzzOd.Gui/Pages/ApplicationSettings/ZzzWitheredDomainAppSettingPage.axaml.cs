using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using OneDragon.Core.Configuration;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzWitheredDomainOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed record ZzzWitheredDomainChallengeChoice(ZzzWitheredDomainChallengeConfigDto Config)
{
    public override string ToString() => Config.ModuleName;
}

internal sealed class ZzzWitheredDomainAppSettingState
{
    private const string ScopeName = "withered-domain";
    private readonly IZzzAppBackend _backend;
    private readonly IZzzWitheredDomainSettingsBackend _settingsBackend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private List<ZzzWitheredDomainChallengeConfigDto> _challengeConfigs = [];
    private string? _originalModuleName;

    public ZzzWitheredDomainAppSettingState(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _backend = backend;
        _settingsBackend = backend as IZzzWitheredDomainSettingsBackend
            ?? throw new InvalidOperationException("当前后端未提供枯萎之都设置服务。");
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    public string MissionName { get; private set; } = string.Empty;

    public string ChallengeConfigName { get; private set; } = string.Empty;

    public int WeeklyPlanTimes { get; private set; }

    public int DailyPlanTimes { get; private set; }

    public string ExtraTask { get; private set; } = string.Empty;

    public string ExtraExit { get; private set; } = string.Empty;

    public string? LastError { get; private set; }

    public ZzzWitheredDomainSettingsCatalogDto? Catalog { get; private set; }

    public ZzzWitheredDomainChallengeConfigDto? SelectedChallenge { get; private set; }

    public IReadOnlyList<ZzzWitheredDomainChallengeConfigDto> ChallengeConfigs => _challengeConfigs;

    public string RunRecordDescription => Catalog?.RunRecord switch
    {
        { PeriodRewardComplete: true } => "已完成刷取周期性奖励 如错误可重置",
        { NoEvalPoint: true } => "已完成刷取业绩 如错误可重置",
        { } record => $"通关次数 本日: {record.DailyRunTimes}, 本周: {record.WeeklyRunTimes}",
        _ => string.Empty,
    };

    public void Reload()
    {
        LastError = null;
        ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto> catalog =
            _settingsBackend.GetWitheredDomainSettingsCatalog(_instanceIndex);
        ZzzBackendResult<ZzzConfigScopeValuesDto> config = _backend.GetConfigScope(
            ScopeName,
            _instanceIndex,
            _groupId);
        if (!catalog.Success || catalog.Value is null || !config.Success || config.Value is null)
        {
            LastError = catalog.Error ?? config.Error ?? "枯萎之都设置读取失败。";
            Catalog = null;
            _challengeConfigs = [];
            SelectedChallenge = null;
            return;
        }

        try
        {
            Catalog = catalog.Value;
            _challengeConfigs = catalog.Value.ChallengeConfigs.ToList();
            IReadOnlyDictionary<string, object?> values = config.Value.Values;
            MissionName = RequiredString(values, "mission_name");
            ChallengeConfigName = RequiredString(values, "challenge_config");
            WeeklyPlanTimes = RequiredInt(values, "weekly_plan_times");
            DailyPlanTimes = RequiredInt(values, "daily_plan_times");
            ExtraTask = RequiredString(values, "extra_task");
            ExtraExit = RequiredString(values, "extra_exit");
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
        }
    }

    public void SaveBase(string key, object value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            _groupId));
        LastError = result.Success ? null : result.Error ?? "枯萎之都配置保存失败。";
        if (!result.Success)
        {
            Reload();
            return;
        }

        switch (key)
        {
            case "mission_name": MissionName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "challenge_config": ChallengeConfigName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "weekly_plan_times": WeeklyPlanTimes = Convert.ToInt32(value, CultureInfo.InvariantCulture); break;
            case "daily_plan_times": DailyPlanTimes = Convert.ToInt32(value, CultureInfo.InvariantCulture); break;
            case "extra_task": ExtraTask = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "extra_exit": ExtraExit = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
        }
    }

    public void SelectChallenge(ZzzWitheredDomainChallengeConfigDto config)
    {
        SelectedChallenge = config;
        _originalModuleName = config.ModuleName;
        LastError = null;
    }

    public void CreateChallenge()
    {
        if (Catalog is null)
        {
            return;
        }

        SelectedChallenge = new ZzzWitheredDomainChallengeConfigDto(
            Catalog.NewModuleName,
            false,
            "全配队通用",
            [],
            [],
            [null, null, null],
            WitheredDomainPathFinding.Default,
            [],
            [],
            [],
            true);
        _originalModuleName = null;
        LastError = null;
    }

    public void CopyChallenge()
    {
        if (SelectedChallenge is null)
        {
            return;
        }

        SelectedChallenge = SelectedChallenge with
        {
            ModuleName = SelectedChallenge.ModuleName + "_copy",
            IsSample = false,
            ValidationError = null,
        };
        _originalModuleName = null;
        LastError = null;
    }

    public void CloseChallenge()
    {
        SelectedChallenge = null;
        _originalModuleName = null;
        LastError = null;
    }

    public void SaveChallenge(ZzzSaveWitheredDomainChallengeConfigRequest request)
    {
        ZzzBackendResult<ZzzWitheredDomainChallengeConfigDto> result =
            _settingsBackend.SaveWitheredDomainChallengeConfig(request with { OriginalModuleName = _originalModuleName });
        if (!result.Success || result.Value is null)
        {
            LastError = result.Error ?? "挑战配置保存失败。";
            return;
        }

        string? oldName = _originalModuleName;
        SelectedChallenge = result.Value;
        _originalModuleName = result.Value.ModuleName;
        int index = _challengeConfigs.FindIndex(item =>
            string.Equals(item.ModuleName, oldName, StringComparison.Ordinal)
            || string.Equals(item.ModuleName, result.Value.ModuleName, StringComparison.Ordinal));
        if (index >= 0)
        {
            _challengeConfigs[index] = result.Value;
        }
        else
        {
            _challengeConfigs.Add(result.Value);
        }

        LastError = result.Value.ValidationError;
    }

    public void DeleteChallenge()
    {
        if (SelectedChallenge is null || SelectedChallenge.IsSample)
        {
            return;
        }

        ZzzBackendResult<IReadOnlyList<ZzzWitheredDomainChallengeConfigDto>> result =
            _settingsBackend.DeleteWitheredDomainChallengeConfig(SelectedChallenge.ModuleName);
        if (!result.Success || result.Value is null)
        {
            LastError = result.Error ?? "挑战配置删除失败。";
            return;
        }

        _challengeConfigs = result.Value.ToList();
        CloseChallenge();
    }

    public void ResetRunRecord()
    {
        ZzzBackendResult<ZzzWitheredDomainRunRecordDto> result =
            _settingsBackend.ResetWitheredDomainRunRecord(_instanceIndex);
        if (!result.Success || result.Value is null || Catalog is null)
        {
            LastError = result.Error ?? "运行记录重置失败。";
            return;
        }

        Catalog = Catalog with { RunRecord = result.Value };
        LastError = null;
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"枯萎之都配置缺少 {key}。");
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"枯萎之都配置缺少 {key}。");
        }
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}

internal sealed partial class ZzzWitheredDomainAppSettingPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzWitheredDomainAppSettingState _state;
    private readonly InfoBar _baseErrorBar;
    private readonly InfoBar _challengeErrorBar;
    private readonly FAComboBox _missionCombo;
    private readonly NumberBox _weeklyTimesNumber;
    private readonly FAComboBox _extraTaskCombo;
    private readonly SettingsExpanderItem _runRecordItem;
    private readonly FAComboBox _baseChallengeCombo;
    private readonly NumberBox _dailyTimesNumber;
    private readonly FAComboBox _extraExitCombo;
    private readonly FAComboBox _existingChallengeCombo;
    private readonly CommandBarButton _createButton;
    private readonly CommandBarButton _copyButton;
    private readonly CommandBarButton _deleteButton;
    private readonly CommandBarButton _closeButton;
    private readonly TextBox _challengeNameText;
    private readonly FAComboBox[] _agentCombos;
    private readonly ToggleSwitch _buyOnlyPriorityToggle;
    private readonly FAComboBox _autoBattleCombo;
    private readonly FAComboBox _pathFindingCombo;
    private readonly Grid _customPathGrid;
    private readonly TextBox _goInOneStepText;
    private readonly TextBox _waypointText;
    private readonly TextBox _avoidText;
    private readonly TextBox _resoniumPriorityText;
    private readonly ContentDialog _deleteDialog;
    private bool _loading;

    public ZzzWitheredDomainAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _state = new ZzzWitheredDomainAppSettingState(backend, instanceIndex, groupId);
        AvaloniaXamlLoader.Load(this);
        _baseErrorBar = Required<InfoBar>("BaseErrorBar");
        _challengeErrorBar = Required<InfoBar>("ChallengeErrorBar");
        _missionCombo = Required<FAComboBox>("MissionCombo");
        _weeklyTimesNumber = Required<NumberBox>("WeeklyTimesNumber");
        _extraTaskCombo = Required<FAComboBox>("ExtraTaskCombo");
        _runRecordItem = Required<SettingsExpanderItem>("RunRecordItem");
        _baseChallengeCombo = Required<FAComboBox>("BaseChallengeCombo");
        _dailyTimesNumber = Required<NumberBox>("DailyTimesNumber");
        _extraExitCombo = Required<FAComboBox>("ExtraExitCombo");
        _existingChallengeCombo = Required<FAComboBox>("ExistingChallengeCombo");
        _createButton = Required<CommandBarButton>("CreateButton");
        _copyButton = Required<CommandBarButton>("CopyButton");
        _deleteButton = Required<CommandBarButton>("DeleteButton");
        _closeButton = Required<CommandBarButton>("CloseButton");
        _challengeNameText = Required<TextBox>("ChallengeNameText");
        _agentCombos = [Required<FAComboBox>("Agent1Combo"), Required<FAComboBox>("Agent2Combo"), Required<FAComboBox>("Agent3Combo")];
        _buyOnlyPriorityToggle = Required<ToggleSwitch>("BuyOnlyPriorityToggle");
        _autoBattleCombo = Required<FAComboBox>("AutoBattleCombo");
        _pathFindingCombo = Required<FAComboBox>("PathFindingCombo");
        _customPathGrid = Required<Grid>("CustomPathGrid");
        _goInOneStepText = Required<TextBox>("GoInOneStepText");
        _waypointText = Required<TextBox>("WaypointText");
        _avoidText = Required<TextBox>("AvoidText");
        _resoniumPriorityText = Required<TextBox>("ResoniumPriorityText");
        _deleteDialog = Required<ContentDialog>("DeleteDialog");
        Reload();
    }

    public void OnPageShown() => Reload();
    public void OnPageHidden() { }
    public void OnPageLeave() { }
    public void DisposePage() { }

    private void Reload()
    {
        _loading = true;
        _state.Reload();
        if (_state.Catalog is { } catalog)
        {
            SetOptions(_missionCombo, catalog.Missions.Select(value => new ZzzWitheredDomainOption(value, value)), _state.MissionName);
            SetOptions(_baseChallengeCombo, catalog.ChallengeConfigs.Select(item => new ZzzWitheredDomainOption(item.ModuleName, item.ModuleName)), _state.ChallengeConfigName);
            SetOptions(_extraTaskCombo, Options(WitheredDomainExtraTask.Options), _state.ExtraTask);
            SetOptions(_extraExitCombo, Options(WitheredDomainExtraExit.Options), _state.ExtraExit);
            _weeklyTimesNumber.Value = _state.WeeklyPlanTimes;
            _dailyTimesNumber.Value = _state.DailyPlanTimes;
            _runRecordItem.Description = _state.RunRecordDescription;
        }
        RefreshChallengeChoices();
        RefreshEditor();
        _loading = false;
        ShowErrors();
    }

    private void RefreshChallengeChoices()
    {
        _existingChallengeCombo.ItemsSource = _state.ChallengeConfigs.Select(item => new ZzzWitheredDomainChallengeChoice(item)).ToArray();
        _existingChallengeCombo.SelectedIndex = -1;
    }

    private void RefreshEditor()
    {
        ZzzWitheredDomainChallengeConfigDto? selected = _state.SelectedChallenge;
        bool chosen = selected is not null;
        bool editable = chosen && selected!.IsSample is false;
        _existingChallengeCombo.IsEnabled = !chosen;
        _createButton.IsEnabled = !chosen;
        _copyButton.IsEnabled = chosen;
        _deleteButton.IsEnabled = editable;
        _closeButton.IsEnabled = chosen;
        IEnumerable<Control> editorControls = new Control[]
        {
            _challengeNameText, _buyOnlyPriorityToggle, _autoBattleCombo, _pathFindingCombo,
            _goInOneStepText, _waypointText, _avoidText, _resoniumPriorityText,
        }.Concat(_agentCombos);
        foreach (Control control in editorControls)
        {
            control.IsEnabled = editable;
        }

        if (selected is null || _state.Catalog is null)
        {
            _challengeNameText.Text = string.Empty;
            _customPathGrid.IsVisible = false;
            return;
        }

        _challengeNameText.Text = selected.ModuleName;
        ZzzWitheredDomainOption[] agentOptions = _state.Catalog.AgentOptions.Select(item => new ZzzWitheredDomainOption(item.Label, item.Value)).ToArray();
        for (int index = 0; index < _agentCombos.Length; index++)
        {
            _agentCombos[index].ItemsSource = agentOptions;
            string value = index < selected.TargetAgents.Count ? selected.TargetAgents[index] ?? string.Empty : string.Empty;
            ZzzWitheredDomainOption? option = agentOptions.FirstOrDefault(item => item.Value == value);
            _agentCombos[index].SelectedItem = option;
            _agentCombos[index].Text = option?.Label ?? value;
        }
        _buyOnlyPriorityToggle.IsChecked = selected.BuyOnlyPriority;
        SetOptions(_autoBattleCombo, _state.Catalog.AutoBattleConfigs.Select(value => new ZzzWitheredDomainOption(value, value)), selected.AutoBattle);
        SetOptions(_pathFindingCombo, _state.Catalog.PathFindingOptions.Select(item => new ZzzWitheredDomainOption(item.Label, item.Value)), selected.PathFinding);
        _goInOneStepText.Text = string.Join('\n', selected.GoInOneStep);
        _waypointText.Text = string.Join('\n', selected.Waypoint);
        _avoidText.Text = string.Join('\n', selected.Avoid);
        _resoniumPriorityText.Text = string.Join('\n', selected.ResoniumPriority);
        _customPathGrid.IsVisible = selected.PathFinding == WitheredDomainPathFinding.Custom;
    }

    private void OnBaseComboChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && sender is FAComboBox { Tag: string key, SelectedItem: ZzzWitheredDomainOption option })
        {
            _state.SaveBase(key, option.Value);
            ShowErrors();
        }
    }

    private void OnBaseNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_loading && sender.Tag is string key)
        {
            _state.SaveBase(key, (int)sender.Value);
            ShowErrors();
        }
    }

    private void OnResetRecordClicked(object? sender, RoutedEventArgs args)
    {
        _state.ResetRunRecord();
        _runRecordItem.Description = _state.RunRecordDescription;
        ShowErrors();
    }

    private void OnExistingChallengeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _existingChallengeCombo.SelectedItem is ZzzWitheredDomainChallengeChoice choice)
        {
            _state.SelectChallenge(choice.Config);
            _loading = true;
            RefreshEditor();
            _loading = false;
            ShowErrors();
        }
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs args) { _state.CreateChallenge(); RefreshEditorSafely(); }
    private void OnCopyClicked(object? sender, RoutedEventArgs args) { _state.CopyChallenge(); RefreshEditorSafely(); }
    private void OnCloseEditorClicked(object? sender, RoutedEventArgs args) { _state.CloseChallenge(); RefreshEditorSafely(); }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is Window owner
            && await _deleteDialog.ShowAsync(owner).ConfigureAwait(true) == ContentDialogResult.Primary)
        {
            _state.DeleteChallenge();
            RefreshChallengeChoices();
            RefreshEditorSafely();
        }
    }

    private void OnChallengeEditorChanged(object? sender, RoutedEventArgs args) => SaveEditor();
    private void OnChallengeEditorChanged(object? sender, SelectionChangedEventArgs args) => SaveEditor();
    private void OnChallengeEditorLostFocus(object? sender, RoutedEventArgs args) => SaveEditor();

    private void OnPathFindingChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading) return;
        _customPathGrid.IsVisible = (_pathFindingCombo.SelectedItem as ZzzWitheredDomainOption)?.Value == WitheredDomainPathFinding.Custom;
        SaveEditor();
    }

    private void SaveEditor()
    {
        if (_loading || _state.SelectedChallenge is null || _state.SelectedChallenge.IsSample) return;
        _state.SaveChallenge(new ZzzSaveWitheredDomainChallengeConfigRequest(
            null,
            _challengeNameText.Text ?? string.Empty,
            (_autoBattleCombo.SelectedItem as ZzzWitheredDomainOption)?.Value ?? string.Empty,
            _resoniumPriorityText.Text ?? string.Empty,
            string.Join('\n', _state.SelectedChallenge.EventPriority),
            _agentCombos.Select(ReadEditableValue).ToArray(),
            (_pathFindingCombo.SelectedItem as ZzzWitheredDomainOption)?.Value ?? WitheredDomainPathFinding.Default,
            _goInOneStepText.Text ?? string.Empty,
            _waypointText.Text ?? string.Empty,
            _avoidText.Text ?? string.Empty,
            _buyOnlyPriorityToggle.IsChecked == true));
        RefreshChallengeChoices();
        ShowErrors();
    }

    private void RefreshEditorSafely()
    {
        _loading = true;
        RefreshEditor();
        _loading = false;
        ShowErrors();
    }

    private void ShowErrors()
    {
        _baseErrorBar.IsOpen = !string.IsNullOrWhiteSpace(_state.LastError) && _state.SelectedChallenge is null;
        _baseErrorBar.Message = _state.LastError;
        _challengeErrorBar.IsOpen = _state.SelectedChallenge?.IsSample == true || (!string.IsNullOrWhiteSpace(_state.LastError) && _state.SelectedChallenge is not null);
        _challengeErrorBar.Severity = _state.SelectedChallenge?.IsSample == true && string.IsNullOrWhiteSpace(_state.LastError)
            ? InfoBarSeverity.Informational : InfoBarSeverity.Error;
        _challengeErrorBar.Message = _state.SelectedChallenge?.IsSample == true && string.IsNullOrWhiteSpace(_state.LastError)
            ? "当前为默认配置，点击复制后可修改" : _state.LastError;
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://one-dragon.com/zzz/zh/feat_one_dragon/hollow_zero.html") { UseShellExecute = true }); }
        catch { }
    }

    private static string? ReadEditableValue(FAComboBox combo) =>
        combo.SelectedItem is ZzzWitheredDomainOption option ? option.Value : string.IsNullOrWhiteSpace(combo.Text) ? null : combo.Text;

    private static IReadOnlyList<ZzzWitheredDomainOption> Options(IReadOnlyList<ConfigItem> options) =>
        options.Select(item => new ZzzWitheredDomainOption(item.Label, item.Value?.ToString() ?? string.Empty)).ToArray();

    private static void SetOptions(FAComboBox combo, IEnumerable<ZzzWitheredDomainOption> options, string value)
    {
        ZzzWitheredDomainOption[] values = options.ToArray();
        combo.ItemsSource = values;
        combo.SelectedItem = values.FirstOrDefault(item => item.Value == value);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"枯萎之都设置缺少 {name}。");
}
