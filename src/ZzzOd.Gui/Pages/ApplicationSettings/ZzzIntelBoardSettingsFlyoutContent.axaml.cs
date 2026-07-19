using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzIntelBoardSettingOption(string Label, object Value)
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzIntelBoardSettingsFlyoutContent : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "intel-board";
    private readonly IZzzAppBackend _backend;
    private readonly IZzzIntelBoardProgressBackend _progressBackend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private readonly FAInfoBar _errorBar;
    private readonly FASettingsExpanderItem _autoBattleItem;
    private readonly FAComboBox _predefinedTeamCombo;
    private readonly FAComboBox _autoBattleCombo;
    private readonly ToggleSwitch _expGrindToggle;
    private readonly Button _resetProgressButton;
    private bool _loading;

    public ZzzIntelBoardSettingsFlyoutContent(
        IZzzAppBackend backend,
        IZzzIntelBoardProgressBackend progressBackend,
        int instanceIndex,
        string groupId)
    {
        _backend = backend;
        _progressBackend = progressBackend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _autoBattleItem = Required<FASettingsExpanderItem>("AutoBattleItem");
        _predefinedTeamCombo = Required<FAComboBox>("PredefinedTeamCombo");
        _autoBattleCombo = Required<FAComboBox>("AutoBattleCombo");
        _expGrindToggle = Required<ToggleSwitch>("ExpGrindToggle");
        _resetProgressButton = Required<Button>("ResetProgressButton");
        Reload();
    }

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

    internal bool AutoBattleVisible => _autoBattleItem.IsVisible;

    internal string ResetButtonText => Convert.ToString(_resetProgressButton.Content, CultureInfo.InvariantCulture) ?? string.Empty;

    internal bool ResetButtonEnabled => _resetProgressButton.IsEnabled;

    internal void ResetProgressForTest() => ResetProgress();

    internal void SaveForTest(string key, object? value) => Save(key, value);

    private void Reload()
    {
        _loading = true;
        _errorBar.IsOpen = false;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> configResult =
                _backend.GetConfigScope(ScopeName, _instanceIndex, _groupId);
            if (!configResult.Success || configResult.Value is null)
            {
                ShowError(configResult.Error ?? "情报板配置读取失败。");
                return;
            }

            ZzzBackendResult<ZzzConfigScopeValuesDto> teamResult =
                _backend.GetConfigScope("team", _instanceIndex);
            if (!teamResult.Success
                || teamResult.Value is null
                || !teamResult.Value.Values.TryGetValue("team_list", out object? rawTeams)
                || rawTeams is not List<PredefinedTeamInfo> teams)
            {
                ShowError(teamResult.Error ?? "预备编队配置读取失败。");
                return;
            }

            ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> catalogResult =
                _backend.GetBattleAssistantConfigCatalog();
            if (!catalogResult.Success || catalogResult.Value is null)
            {
                ShowError(catalogResult.Error ?? "自动战斗配置读取失败。");
                return;
            }

            _predefinedTeamCombo.ItemsSource = new[] { new ZzzIntelBoardSettingOption("游戏内配队", -1) }
                .Concat(teams.Select(team => new ZzzIntelBoardSettingOption(team.Name, team.Idx)))
                .ToArray();
            _autoBattleCombo.ItemsSource = catalogResult.Value.AutoBattle
                .Select(value => new ZzzIntelBoardSettingOption(value, value))
                .ToArray();

            IReadOnlyDictionary<string, object?> values = configResult.Value.Values;
            Select(_predefinedTeamCombo, RequiredInt(values, "predefined_team_idx"));
            Select(_autoBattleCombo, RequiredString(values, "auto_battle_config"));
            _expGrindToggle.IsChecked = RequiredBool(values, "exp_grind_mode");
            UpdateAutoBattleVisibility();
        }
        catch (InvalidOperationException exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnPredefinedTeamChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _predefinedTeamCombo.SelectedItem is ZzzIntelBoardSettingOption option)
        {
            Save("predefined_team_idx", option.Value);
        }

        UpdateAutoBattleVisibility();
    }

    private void OnAutoBattleChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _autoBattleCombo.SelectedItem is ZzzIntelBoardSettingOption option)
        {
            Save("auto_battle_config", option.Value);
        }
    }

    private void OnExpGrindChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("exp_grind_mode", _expGrindToggle.IsChecked == true);
        }
    }

    private void OnResetProgressClicked(object? sender, RoutedEventArgs args) => ResetProgress();

    private void ResetProgress()
    {
        ZzzBackendResult<bool> result = _progressBackend.ResetIntelBoardProgress(_instanceIndex);
        if (!result.Success)
        {
            ShowError(result.Error ?? "情报板进度重置失败。");
            return;
        }

        _errorBar.IsOpen = false;
        _resetProgressButton.Content = "已重置";
        _resetProgressButton.IsEnabled = false;
    }

    private void UpdateAutoBattleVisibility()
    {
        _autoBattleItem.IsVisible = _predefinedTeamCombo.SelectedItem is ZzzIntelBoardSettingOption { Value: int value }
            && value == -1;
    }

    private void Save(string key, object? value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            _groupId));
        if (!result.Success)
        {
            ShowError(result.Error ?? "情报板配置保存失败。");
        }
    }

    private void ShowError(string message)
    {
        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private static void Select(SelectingItemsControl combo, object value)
    {
        combo.SelectedItem = combo.ItemsSource?.OfType<ZzzIntelBoardSettingOption>()
            .FirstOrDefault(option => Equals(option.Value, value));
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"情报板配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"情报板配置缺少 {key}。");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"情报板配置缺少 {key}。");
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"情报板设置缺少 {name}。");
}
