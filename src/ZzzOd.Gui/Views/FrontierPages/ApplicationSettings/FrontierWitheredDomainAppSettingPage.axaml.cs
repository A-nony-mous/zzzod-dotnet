using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using OneDragon.Core.Configuration;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.Gui.Services.Config;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.ApplicationSettings;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed record ZzzWitheredDomainOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed record ZzzWitheredDomainChallengeChoice(ZzzWitheredDomainChallengeConfigDto Config)
{
    public override string ToString() => Config.ModuleName;
}

internal sealed partial class ZzzWitheredDomainAppSettingViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField MissionNameField =
        new("mission_name", typeof(string), "旧都列车-内部");
    private static readonly ZzzConfigField ChallengeConfigNameField =
        new("challenge_config", typeof(string), "默认-专属空洞-艾莲");
    private static readonly ZzzConfigField WeeklyPlanTimesField =
        new("weekly_plan_times", typeof(int), 2);
    private static readonly ZzzConfigField DailyPlanTimesField =
        new("daily_plan_times", typeof(int), 99);
    private static readonly ZzzConfigField ExtraTaskField =
        new("extra_task", typeof(string), "刷满周期奖励");
    private static readonly ZzzConfigField ExtraExitField =
        new("extra_exit", typeof(string), "通关");
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        MissionNameField,
        ChallengeConfigNameField,
        WeeklyPlanTimesField,
        DailyPlanTimesField,
        ExtraTaskField,
        ExtraExitField,
    ];

    private readonly IZzzWitheredDomainSettingsBackend _settingsBackend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private List<ZzzWitheredDomainChallengeConfigDto> _challengeConfigs = [];
    private string? _originalModuleName;

    public ZzzWitheredDomainAppSettingViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _settingsBackend = backend as IZzzWitheredDomainSettingsBackend
            ?? throw new InvalidOperationException("当前后端未提供枯萎之都设置服务。");
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => "withered-domain";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public string MissionName
    {
        get => GetValue<string>(MissionNameField);
        set => SetValue(MissionNameField, value);
    }

    public string ChallengeConfigName
    {
        get => GetValue<string>(ChallengeConfigNameField);
        set => SetValue(ChallengeConfigNameField, value);
    }

    public double WeeklyPlanTimes
    {
        get => GetValue<int>(WeeklyPlanTimesField);
        set => SetValue(WeeklyPlanTimesField, (int)value);
    }

    public double DailyPlanTimes
    {
        get => GetValue<int>(DailyPlanTimesField);
        set => SetValue(DailyPlanTimesField, (int)value);
    }

    public string ExtraTask
    {
        get => GetValue<string>(ExtraTaskField);
        set => SetValue(ExtraTaskField, value);
    }

    public string ExtraExit
    {
        get => GetValue<string>(ExtraExitField);
        set => SetValue(ExtraExitField, value);
    }

    public ZzzWitheredDomainSettingsCatalogDto? Catalog { get; private set; }

    public ZzzWitheredDomainChallengeConfigDto? SelectedChallenge { get; private set; }

    public IReadOnlyList<ZzzWitheredDomainChallengeConfigDto> ChallengeConfigs => _challengeConfigs;

    public IReadOnlyList<ZzzWitheredDomainOption> MissionOptions => Catalog?.Missions
        .Select(value => new ZzzWitheredDomainOption(value, value))
        .ToArray() ?? [];

    public IReadOnlyList<ZzzWitheredDomainOption> BaseChallengeOptions => Catalog?.ChallengeConfigs
        .Select(item => new ZzzWitheredDomainOption(item.ModuleName, item.ModuleName))
        .ToArray() ?? [];

    public IReadOnlyList<ZzzWitheredDomainOption> ExtraTaskOptions { get; } =
        ToOptions(WitheredDomainExtraTask.Options);

    public IReadOnlyList<ZzzWitheredDomainOption> ExtraExitOptions { get; } =
        ToOptions(WitheredDomainExtraExit.Options);

    public ZzzWitheredDomainOption? SelectedMission
    {
        get => Find(MissionOptions, MissionName);
        set
        {
            if (value is not null)
            {
                MissionName = value.Value;
            }
        }
    }

    public ZzzWitheredDomainOption? SelectedBaseChallenge
    {
        get => Find(BaseChallengeOptions, ChallengeConfigName);
        set
        {
            if (value is not null)
            {
                ChallengeConfigName = value.Value;
            }
        }
    }

    public ZzzWitheredDomainOption? SelectedExtraTask
    {
        get => Find(ExtraTaskOptions, ExtraTask);
        set
        {
            if (value is not null)
            {
                ExtraTask = value.Value;
            }
        }
    }

    public ZzzWitheredDomainOption? SelectedExtraExit
    {
        get => Find(ExtraExitOptions, ExtraExit);
        set
        {
            if (value is not null)
            {
                ExtraExit = value.Value;
            }
        }
    }

    public string RunRecordDescription => Catalog?.RunRecord switch
    {
        { PeriodRewardComplete: true } => "已完成刷取周期性奖励 如错误可重置",
        { NoEvalPoint: true } => "已完成刷取业绩 如错误可重置",
        { } record => $"通关次数 本日: {record.DailyRunTimes}, 本周: {record.WeeklyRunTimes}",
        _ => string.Empty,
    };

    private void NotifyBaseBindings()
    {
        OnPropertyChanged(nameof(MissionOptions));
        OnPropertyChanged(nameof(BaseChallengeOptions));
        OnPropertyChanged(nameof(SelectedMission));
        OnPropertyChanged(nameof(SelectedBaseChallenge));
        OnPropertyChanged(nameof(SelectedExtraTask));
        OnPropertyChanged(nameof(SelectedExtraExit));
        OnPropertyChanged(nameof(WeeklyPlanTimes));
        OnPropertyChanged(nameof(DailyPlanTimes));
        OnPropertyChanged(nameof(RunRecordDescription));
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        if (LastError is not null)
        {
            Catalog = null;
            _challengeConfigs = [];
            SelectedChallenge = null;
            return;
        }

        ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto> catalog =
            _settingsBackend.GetWitheredDomainSettingsCatalog(_instanceIndex);
        if (!catalog.Success || catalog.Value is null)
        {
            ReportError(catalog.Error ?? "枯萎之都设置读取失败。");
            Catalog = null;
            _challengeConfigs = [];
            SelectedChallenge = null;
            return;
        }

        Catalog = catalog.Value;
        _challengeConfigs = catalog.Value.ChallengeConfigs.ToList();
        ReportError(null);
        NotifyBaseBindings();
    }

    public void SaveBase(string key, object value)
    {
        switch (key)
        {
            case "mission_name": MissionName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "challenge_config": ChallengeConfigName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "weekly_plan_times": WeeklyPlanTimes = Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
            case "daily_plan_times": DailyPlanTimes = Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
            case "extra_task": ExtraTask = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "extra_exit": ExtraExit = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            default: throw new ArgumentOutOfRangeException(nameof(key), key, "未知的枯萎之都配置字段。");
        }
    }

    public void SelectChallenge(ZzzWitheredDomainChallengeConfigDto config)
    {
        SelectedChallenge = config;
        _originalModuleName = config.ModuleName;
        ReportError(null);
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
        ReportError(null);
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
        ReportError(null);
    }

    public void CloseChallenge()
    {
        SelectedChallenge = null;
        _originalModuleName = null;
        ReportError(null);
    }

    public void SaveChallenge(ZzzSaveWitheredDomainChallengeConfigRequest request)
    {
        ZzzBackendResult<ZzzWitheredDomainChallengeConfigDto> result =
            _settingsBackend.SaveWitheredDomainChallengeConfig(request with { OriginalModuleName = _originalModuleName });
        if (!result.Success || result.Value is null)
        {
            ReportError(result.Error ?? "挑战配置保存失败。");
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

        ReportError(result.Value.ValidationError);
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
            ReportError(result.Error ?? "挑战配置删除失败。");
            return;
        }

        _challengeConfigs = result.Value.ToList();
        CloseChallenge();
    }

    [RelayCommand]
    public void ResetRunRecord()
    {
        ZzzBackendResult<ZzzWitheredDomainRunRecordDto> result =
            _settingsBackend.ResetWitheredDomainRunRecord(_instanceIndex);
        if (!result.Success || result.Value is null || Catalog is null)
        {
            ReportError(result.Error ?? "运行记录重置失败。");
            return;
        }

        Catalog = Catalog with { RunRecord = result.Value };
        ReportError(null);
        OnPropertyChanged(nameof(RunRecordDescription));
    }

    private static IReadOnlyList<ZzzWitheredDomainOption> ToOptions(IReadOnlyList<ConfigItem> options) =>
        options.Select(item => new ZzzWitheredDomainOption(item.Label, item.Value?.ToString() ?? string.Empty)).ToArray();

    private static ZzzWitheredDomainOption? Find(
        IReadOnlyList<ZzzWitheredDomainOption> options,
        string value) => options.FirstOrDefault(item => item.Value == value);

}

internal sealed partial class FrontierWitheredDomainAppSettingPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzWitheredDomainAppSettingViewModel _viewModel;
    private readonly FAInfoBar _baseErrorBar;
    private readonly FAInfoBar _challengeErrorBar;
    private readonly FAComboBox _existingChallengeCombo;
    private readonly FACommandBarButton _createButton;
    private readonly FACommandBarButton _copyButton;
    private readonly FACommandBarButton _deleteButton;
    private readonly FACommandBarButton _closeButton;
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
    private readonly FAContentDialog _deleteDialog;
    private bool _loading;

    public FrontierWitheredDomainAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        AvaloniaXamlLoader.Load(this);
        _baseErrorBar = Required<FAInfoBar>("BaseErrorBar");
        _challengeErrorBar = Required<FAInfoBar>("ChallengeErrorBar");
        _viewModel = new ZzzWitheredDomainAppSettingViewModel(
            backend,
            instanceIndex,
            groupId,
            _ => ShowErrors());
        DataContext = _viewModel;
        _existingChallengeCombo = Required<FAComboBox>("ExistingChallengeCombo");
        _createButton = Required<FACommandBarButton>("CreateButton");
        _copyButton = Required<FACommandBarButton>("CopyButton");
        _deleteButton = Required<FACommandBarButton>("DeleteButton");
        _closeButton = Required<FACommandBarButton>("CloseButton");
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
        _deleteDialog = Required<FAContentDialog>("DeleteDialog");
        Reload();
    }

    public void OnPageShown() => Reload();
    public void OnPageHidden() { }
    public void OnPageLeave() { }
    public void DisposePage() => _viewModel.DisposePage();

    private void Reload()
    {
        _loading = true;
        _viewModel.OnPageShown();
        RefreshChallengeChoices();
        RefreshEditor();
        _loading = false;
        ShowErrors();
    }

    private void RefreshChallengeChoices()
    {
        _existingChallengeCombo.ItemsSource = _viewModel.ChallengeConfigs.Select(item => new ZzzWitheredDomainChallengeChoice(item)).ToArray();
        _existingChallengeCombo.SelectedIndex = -1;
    }

    private void RefreshEditor()
    {
        ZzzWitheredDomainChallengeConfigDto? selected = _viewModel.SelectedChallenge;
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

        if (selected is null || _viewModel.Catalog is null)
        {
            _challengeNameText.Text = string.Empty;
            _customPathGrid.IsVisible = false;
            return;
        }

        _challengeNameText.Text = selected.ModuleName;
        ZzzWitheredDomainOption[] agentOptions = _viewModel.Catalog.AgentOptions.Select(item => new ZzzWitheredDomainOption(item.Label, item.Value)).ToArray();
        for (int index = 0; index < _agentCombos.Length; index++)
        {
            _agentCombos[index].ItemsSource = agentOptions;
            string value = index < selected.TargetAgents.Count ? selected.TargetAgents[index] ?? string.Empty : string.Empty;
            ZzzWitheredDomainOption? option = agentOptions.FirstOrDefault(item => item.Value == value);
            _agentCombos[index].SelectedItem = option;
            _agentCombos[index].Text = option?.Label ?? value;
        }
        _buyOnlyPriorityToggle.IsChecked = selected.BuyOnlyPriority;
        SetOptions(_autoBattleCombo, _viewModel.Catalog.AutoBattleConfigs.Select(value => new ZzzWitheredDomainOption(value, value)), selected.AutoBattle);
        SetOptions(_pathFindingCombo, _viewModel.Catalog.PathFindingOptions.Select(item => new ZzzWitheredDomainOption(item.Label, item.Value)), selected.PathFinding);
        _goInOneStepText.Text = string.Join('\n', selected.GoInOneStep);
        _waypointText.Text = string.Join('\n', selected.Waypoint);
        _avoidText.Text = string.Join('\n', selected.Avoid);
        _resoniumPriorityText.Text = string.Join('\n', selected.ResoniumPriority);
        _customPathGrid.IsVisible = selected.PathFinding == WitheredDomainPathFinding.Custom;
    }

    private void OnExistingChallengeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _existingChallengeCombo.SelectedItem is ZzzWitheredDomainChallengeChoice choice)
        {
            _viewModel.SelectChallenge(choice.Config);
            _loading = true;
            RefreshEditor();
            _loading = false;
            ShowErrors();
        }
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs args) { _viewModel.CreateChallenge(); RefreshEditorSafely(); }
    private void OnCopyClicked(object? sender, RoutedEventArgs args) { _viewModel.CopyChallenge(); RefreshEditorSafely(); }
    private void OnCloseEditorClicked(object? sender, RoutedEventArgs args) { _viewModel.CloseChallenge(); RefreshEditorSafely(); }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is Window owner
            && await _deleteDialog.ShowAsync(owner).ConfigureAwait(true) == FAContentDialogResult.Primary)
        {
            _viewModel.DeleteChallenge();
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
        if (_loading || _viewModel.SelectedChallenge is null || _viewModel.SelectedChallenge.IsSample) return;
        _viewModel.SaveChallenge(new ZzzSaveWitheredDomainChallengeConfigRequest(
            null,
            _challengeNameText.Text ?? string.Empty,
            (_autoBattleCombo.SelectedItem as ZzzWitheredDomainOption)?.Value ?? string.Empty,
            _resoniumPriorityText.Text ?? string.Empty,
            string.Join('\n', _viewModel.SelectedChallenge.EventPriority),
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
        _baseErrorBar.IsOpen = !string.IsNullOrWhiteSpace(_viewModel.LastError) && _viewModel.SelectedChallenge is null;
        _baseErrorBar.Message = _viewModel.LastError;
        _challengeErrorBar.IsOpen = _viewModel.SelectedChallenge?.IsSample == true || (!string.IsNullOrWhiteSpace(_viewModel.LastError) && _viewModel.SelectedChallenge is not null);
        _challengeErrorBar.Severity = _viewModel.SelectedChallenge?.IsSample == true && string.IsNullOrWhiteSpace(_viewModel.LastError)
            ? FAInfoBarSeverity.Informational : FAInfoBarSeverity.Error;
        _challengeErrorBar.Message = _viewModel.SelectedChallenge?.IsSample == true && string.IsNullOrWhiteSpace(_viewModel.LastError)
            ? "当前为默认配置，点击复制后可修改" : _viewModel.LastError;
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://one-dragon.com/zzz/zh/feat_one_dragon/hollow_zero.html") { UseShellExecute = true }); }
        catch { }
    }

    private static string? ReadEditableValue(FAComboBox combo) =>
        combo.SelectedItem is ZzzWitheredDomainOption option ? option.Value : string.IsNullOrWhiteSpace(combo.Text) ? null : combo.Text;

    private static void SetOptions(FAComboBox combo, IEnumerable<ZzzWitheredDomainOption> options, string value)
    {
        ZzzWitheredDomainOption[] values = options.ToArray();
        combo.ItemsSource = values;
        combo.SelectedItem = values.FirstOrDefault(item => item.Value == value);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"枯萎之都设置缺少 {name}。");
}
