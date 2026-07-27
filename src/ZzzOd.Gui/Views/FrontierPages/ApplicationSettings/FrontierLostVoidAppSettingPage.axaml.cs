using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.ApplicationSettings;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed record ZzzLostVoidChallengeChoice(string ModuleName, bool IsSample)
{
    public override string ToString() => ModuleName;
}

internal sealed partial class FrontierLostVoidAppSettingPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzLostVoidAppSettingViewModel _viewModel;
    private readonly FAInfoBar _actionBar;
    private readonly FAComboBox _existingConfigCombo;
    private readonly FACommandBarButton _createButton;
    private readonly FACommandBarButton _copyButton;
    private readonly FACommandBarButton _deleteButton;
    private readonly FACommandBarButton _closeButton;
    private readonly FASettingsExpander _nameItem;
    private readonly TextBox _nameTextBox;
    private readonly FASettingsExpander _predefinedTeamItem;
    private readonly FAComboBox _predefinedTeamCombo;
    private readonly FASettingsExpander _priorityTeamItem;
    private readonly ToggleSwitch _priorityTeamToggle;
    private readonly FASettingsExpander _manualAgentItem;
    private readonly ToggleSwitch _manualAgentToggle;
    private readonly FASettingsExpander _agentTeamItem;
    private readonly FAComboBox[] _agentCombos;
    private readonly FASettingsExpander _autoBattleItem;
    private readonly FAComboBox _autoBattleCombo;
    private readonly FASettingsExpander _chaseNewItem;
    private readonly ToggleSwitch _chaseNewToggle;
    private readonly FASettingsExpander _investigationItem;
    private readonly FAComboBox _investigationCombo;
    private readonly FASettingsExpander _periodBuffItem;
    private readonly FAComboBox _periodBuffCombo;
    private readonly FASettingsExpander _storeGoldItem;
    private readonly ToggleSwitch _storeGoldToggle;
    private readonly FASettingsExpander _storeBloodItem;
    private readonly ToggleSwitch _storeBloodToggle;
    private readonly FASettingsExpander _storeBloodMinItem;
    private readonly TextBox _storeBloodMinText;
    private readonly FASettingsExpander _priorityNewItem;
    private readonly ToggleSwitch _priorityNewToggle;
    private readonly FASettingsExpander _buyPriority1Item;
    private readonly TextBox _buyPriority1Text;
    private readonly FASettingsExpander _buyPriority2Item;
    private readonly TextBox _buyPriority2Text;
    private readonly TextBox _artifactPriorityText;
    private readonly TextBox _artifactPriority2Text;
    private readonly TextBox _regionPriorityText;
    private readonly FAContentDialog _deleteDialog;
    private bool _loading;

    public FrontierLostVoidAppSettingPage(
        IZzzAppBackend backend,
        IZzzLostVoidSettingsBackend lostVoidBackend,
        int instanceIndex,
        string groupId)
    {
        AvaloniaXamlLoader.Load(this);
        _actionBar = Required<FAInfoBar>("ActionBar");
        _viewModel = new ZzzLostVoidAppSettingViewModel(
            backend,
            lostVoidBackend,
            instanceIndex,
            groupId,
            ShowStatus);
        DataContext = _viewModel;
        _existingConfigCombo = Required<FAComboBox>("ExistingConfigCombo");
        _createButton = Required<FACommandBarButton>("CreateButton");
        _copyButton = Required<FACommandBarButton>("CopyButton");
        _deleteButton = Required<FACommandBarButton>("DeleteButton");
        _closeButton = Required<FACommandBarButton>("CloseButton");
        _nameItem = Required<FASettingsExpander>("NameItem");
        _nameTextBox = Required<TextBox>("NameTextBox");
        _predefinedTeamItem = Required<FASettingsExpander>("PredefinedTeamItem");
        _predefinedTeamCombo = Required<FAComboBox>("PredefinedTeamCombo");
        _priorityTeamItem = Required<FASettingsExpander>("PriorityTeamItem");
        _priorityTeamToggle = Required<ToggleSwitch>("PriorityTeamToggle");
        _manualAgentItem = Required<FASettingsExpander>("ManualAgentItem");
        _manualAgentToggle = Required<ToggleSwitch>("ManualAgentToggle");
        _agentTeamItem = Required<FASettingsExpander>("AgentTeamItem");
        _agentCombos = [Required<FAComboBox>("Agent1Combo"), Required<FAComboBox>("Agent2Combo"), Required<FAComboBox>("Agent3Combo")];
        _autoBattleItem = Required<FASettingsExpander>("AutoBattleItem");
        _autoBattleCombo = Required<FAComboBox>("AutoBattleCombo");
        _chaseNewItem = Required<FASettingsExpander>("ChaseNewItem");
        _chaseNewToggle = Required<ToggleSwitch>("ChaseNewToggle");
        _investigationItem = Required<FASettingsExpander>("InvestigationItem");
        _investigationCombo = Required<FAComboBox>("InvestigationCombo");
        _periodBuffItem = Required<FASettingsExpander>("PeriodBuffItem");
        _periodBuffCombo = Required<FAComboBox>("PeriodBuffCombo");
        _storeGoldItem = Required<FASettingsExpander>("StoreGoldItem");
        _storeGoldToggle = Required<ToggleSwitch>("StoreGoldToggle");
        _storeBloodItem = Required<FASettingsExpander>("StoreBloodItem");
        _storeBloodToggle = Required<ToggleSwitch>("StoreBloodToggle");
        _storeBloodMinItem = Required<FASettingsExpander>("StoreBloodMinItem");
        _storeBloodMinText = Required<TextBox>("StoreBloodMinText");
        _priorityNewItem = Required<FASettingsExpander>("PriorityNewItem");
        _priorityNewToggle = Required<ToggleSwitch>("PriorityNewToggle");
        _buyPriority1Item = Required<FASettingsExpander>("BuyPriority1Item");
        _buyPriority1Text = Required<TextBox>("BuyPriority1Text");
        _buyPriority2Item = Required<FASettingsExpander>("BuyPriority2Item");
        _buyPriority2Text = Required<TextBox>("BuyPriority2Text");
        _artifactPriorityText = Required<TextBox>("ArtifactPriorityText");
        _artifactPriority2Text = Required<TextBox>("ArtifactPriority2Text");
        _regionPriorityText = Required<TextBox>("RegionPriorityText");
        _deleteDialog = Required<FAContentDialog>("DeleteDialog");
        Reload();
    }

    internal ZzzLostVoidAppSettingViewModel ViewModel => _viewModel;

    public void OnPageShown() => Reload();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage() => _viewModel.DisposePage();

    private void Reload()
    {
        _loading = true;
        _viewModel.OnPageShown();
        _viewModel.ReloadChallengeCatalog();
        RefreshChallengeChoices();
        RefreshEditor();
        _loading = false;
        ShowStatus();
    }

    private void RefreshChallengeChoices()
    {
        _existingConfigCombo.ItemsSource = (_viewModel.ChallengeCatalog?.Configs ?? [])
            .Select(config => new ZzzLostVoidChallengeChoice(config.ModuleName, config.IsSample))
            .ToArray();
        _existingConfigCombo.SelectedIndex = -1;
    }

    private void RefreshEditor()
    {
        ZzzLostVoidChallengeConfigDto? config = _viewModel.ChosenConfig;
        ZzzLostVoidChallengeCatalogDto? catalog = _viewModel.ChallengeCatalog;
        bool chosen = config is not null;
        bool editable = chosen && !config!.IsSample;
        _existingConfigCombo.IsEnabled = !chosen;
        _createButton.IsEnabled = !chosen;
        _copyButton.IsEnabled = chosen;
        _deleteButton.IsEnabled = editable && config!.Exists;
        _closeButton.IsEnabled = chosen;

        if (config is null || catalog is null)
        {
            _nameTextBox.Text = string.Empty;
            SetEditorEnabled(false, null);
            return;
        }

        _nameTextBox.Text = config.ModuleName;
        SetUiOptions(_predefinedTeamCombo,
            new[] { new ZzzLostVoidUiOption("游戏内配队", -1) }
                .Concat(catalog.Teams.Select(team => new ZzzLostVoidUiOption(team.Name, team.Index))),
            config.PredefinedTeamIndex);
        _priorityTeamToggle.IsChecked = config.ChooseTeamByPriority;
        _manualAgentToggle.IsChecked = config.ManuallyChooseAgent;
        ZzzLostVoidUiOption[] agentOptions = catalog.Agents
            .Select(agent => new ZzzLostVoidUiOption(agent.Label, agent.Value))
            .ToArray();
        for (int index = 0; index < _agentCombos.Length; index++)
        {
            object value = index < config.TeamInfo.Count ? config.TeamInfo[index] : "unknown";
            SetUiOptions(_agentCombos[index], agentOptions, value);
        }

        SetStringOptions(_autoBattleCombo, catalog.AutoBattleConfigs, config.AutoBattle);
        _chaseNewToggle.IsChecked = config.ChaseNewMode;
        SetStringOptions(_investigationCombo, catalog.InvestigationStrategies, config.InvestigationStrategy);
        SetUiOptions(_periodBuffCombo, _viewModel.PeriodBuffOptions, config.PeriodBuffNo);
        _storeGoldToggle.IsChecked = config.StoreGold;
        _storeBloodToggle.IsChecked = config.StoreBlood;
        _storeBloodMinText.Text = config.StoreBloodMin.ToString(CultureInfo.InvariantCulture);
        _priorityNewToggle.IsChecked = config.ArtifactPriorityNew;
        _buyPriority1Text.Text = config.BuyOnlyPriority1.ToString(CultureInfo.InvariantCulture);
        _buyPriority2Text.Text = config.BuyOnlyPriority2.ToString(CultureInfo.InvariantCulture);
        _artifactPriorityText.Text = string.Join('\n', config.ArtifactPriority);
        _artifactPriority2Text.Text = string.Join('\n', config.ArtifactPriority2);
        _regionPriorityText.Text = string.Join('\n', config.RegionTypePriority);
        SetEditorEnabled(editable, config);
    }

    private void SetEditorEnabled(bool editable, ZzzLostVoidChallengeConfigDto? config)
    {
        bool chosen = config is not null;
        _nameItem.IsEnabled = editable;
        _autoBattleItem.IsEnabled = editable;
        _chaseNewItem.IsEnabled = editable;
        _periodBuffItem.IsEnabled = editable;
        _storeGoldItem.IsEnabled = editable;
        _storeBloodItem.IsEnabled = editable;
        _storeBloodMinItem.IsEnabled = editable;
        _priorityNewItem.IsEnabled = editable;
        _buyPriority1Item.IsEnabled = editable;
        _buyPriority2Item.IsEnabled = editable;
        _artifactPriorityText.IsEnabled = editable;
        _artifactPriority2Text.IsEnabled = editable;
        _regionPriorityText.IsEnabled = editable;
        _priorityTeamItem.IsEnabled = editable && config?.ManuallyChooseAgent != true;
        _manualAgentItem.IsEnabled = editable && config?.ChooseTeamByPriority != true;
        _predefinedTeamItem.IsEnabled = editable
            && config?.ChooseTeamByPriority != true
            && config?.ManuallyChooseAgent != true;
        _agentTeamItem.IsEnabled = editable && config?.ManuallyChooseAgent == true;
        _investigationItem.IsEnabled = editable && config?.ChaseNewMode != true;
        if (!chosen)
        {
            _nameTextBox.Text = string.Empty;
        }
    }

    private void OnExistingConfigChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _existingConfigCombo.SelectedItem is ZzzLostVoidChallengeChoice choice)
        {
            _viewModel.ChooseConfig(choice.ModuleName);
            RefreshEditorSafely();
        }
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs args)
    {
        _viewModel.CreateConfig();
        RefreshEditorSafely();
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs args)
    {
        _viewModel.CopyConfig();
        RefreshEditorSafely();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs args)
    {
        _viewModel.CloseConfig();
        RefreshEditorSafely();
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is Window owner
            && await _deleteDialog.ShowAsync(owner).ConfigureAwait(true) == FAContentDialogResult.Primary)
        {
            _viewModel.DeleteConfig();
            RefreshChallengeChoices();
            RefreshEditorSafely();
        }
    }

    private void OnNameLostFocus(object? sender, RoutedEventArgs args)
    {
        if (!_loading && _viewModel.ChosenConfig is { } config)
        {
            _viewModel.UpdateConfig(value => value with { ModuleName = (_nameTextBox.Text ?? config.ModuleName).Trim() });
            RefreshChallengeChoices();
            RefreshEditorSafely();
        }
    }

    private void OnChallengeComboChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || sender is not FAComboBox { Tag: string key } combo || _viewModel.ChosenConfig is null)
        {
            return;
        }

        object? value = combo.SelectedItem switch
        {
            ZzzLostVoidUiOption option => option.Value,
            string text => text,
            _ => null,
        };
        if (value is null)
        {
            return;
        }

        _viewModel.UpdateConfig(config => key switch
        {
            "predefined_team_idx" => config with { PredefinedTeamIndex = Convert.ToInt32(value, CultureInfo.InvariantCulture) },
            "auto_battle" => config with { AutoBattle = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty },
            "investigation_strategy" => config with { InvestigationStrategy = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty },
            _ => config with { PeriodBuffNo = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty },
        });
        ShowStatus();
    }

    private void OnChallengeToggleChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading || sender is not ToggleSwitch { Tag: string key } toggle)
        {
            return;
        }

        bool value = toggle.IsChecked == true;
        _viewModel.UpdateConfig(config => key switch
        {
            "choose_team_by_priority" => config with { ChooseTeamByPriority = value },
            "manually_choose_agent" => config with { ManuallyChooseAgent = value },
            "chase_new_mode" => config with { ChaseNewMode = value },
            "store_gold" => config with { StoreGold = value },
            "store_blood" => config with { StoreBlood = value },
            _ => config with { ArtifactPriorityNew = value },
        });
        RefreshEditorSafely();
    }

    private void OnAgentChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || sender is not FAComboBox { Tag: string indexText, SelectedItem: ZzzLostVoidUiOption option }
            || !int.TryParse(indexText, out int index)
            || _viewModel.ChosenConfig is null)
        {
            return;
        }

        _viewModel.UpdateConfig(config =>
        {
            List<string> agents = config.TeamInfo.ToList();
            while (agents.Count < 3)
            {
                agents.Add("unknown");
            }

            agents[index] = Convert.ToString(option.Value, CultureInfo.InvariantCulture) ?? "unknown";
            return config with { TeamInfo = agents };
        });
        ShowStatus();
    }

    private void OnIntegerTextLostFocus(object? sender, RoutedEventArgs args)
    {
        if (_loading || sender is not TextBox { Tag: string key } textBox
            || !int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return;
        }

        _viewModel.UpdateConfig(config => key switch
        {
            "store_blood_min" => config with { StoreBloodMin = value },
            "buy_only_priority_1" => config with { BuyOnlyPriority1 = value },
            _ => config with { BuyOnlyPriority2 = value },
        });
        ShowStatus();
    }

    private void OnArtifactPriorityChanged(object? sender, TextChangedEventArgs args) =>
        SavePriority(ZzzLostVoidPriorityKind.ArtifactPriority, _artifactPriorityText.Text ?? string.Empty);

    private void OnArtifactPriority2Changed(object? sender, TextChangedEventArgs args) =>
        SavePriority(ZzzLostVoidPriorityKind.ArtifactPriority2, _artifactPriority2Text.Text ?? string.Empty);

    private void OnRegionPriorityChanged(object? sender, TextChangedEventArgs args) =>
        SavePriority(ZzzLostVoidPriorityKind.RegionTypePriority, _regionPriorityText.Text ?? string.Empty);

    private void SavePriority(ZzzLostVoidPriorityKind kind, string text)
    {
        if (!_loading)
        {
            _viewModel.UpdatePriority(kind, text);
            ShowStatus();
        }
    }

    private void RefreshEditorSafely()
    {
        _loading = true;
        RefreshEditor();
        _loading = false;
        ShowStatus();
    }

    private void ShowStatus(string? _ = null)
    {
        string? error = _viewModel.Error;
        bool sample = _viewModel.ChosenConfig?.IsSample == true;
        _actionBar.IsOpen = sample || !string.IsNullOrWhiteSpace(error);
        _actionBar.Title = sample ? "挑战配置" : "错误";
        _actionBar.Message = sample && string.IsNullOrWhiteSpace(error)
            ? "当前为默认配置，点击复制后可修改"
            : error;
        _actionBar.Severity = sample && string.IsNullOrWhiteSpace(error)
            ? FAInfoBarSeverity.Informational
            : FAInfoBarSeverity.Error;
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://one-dragon.com/zzz/zh/feat_one_dragon/hollow_zero.html")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            _actionBar.Title = "错误";
            _actionBar.Message = exception.Message;
            _actionBar.Severity = FAInfoBarSeverity.Error;
            _actionBar.IsOpen = true;
        }
    }

    private static void SetStringOptions(FAComboBox combo, IEnumerable<string> options, string selected)
    {
        string[] values = options.ToArray();
        combo.ItemsSource = values;
        combo.SelectedItem = values.FirstOrDefault(value => string.Equals(value, selected, StringComparison.Ordinal));
    }

    private static void SetUiOptions(FAComboBox combo, IEnumerable<ZzzLostVoidUiOption> options, object selected)
    {
        ZzzLostVoidUiOption[] values = options.ToArray();
        combo.ItemsSource = values;
        combo.SelectedItem = values.FirstOrDefault(option => Equals(option.Value, selected));
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"迷失之地设置缺少 {name}。");
}
