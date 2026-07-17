using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Coffee;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzCoffeeSettingOption(string Label, object Value, string Description = "")
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzCoffeeAppSettingPage : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "coffee";
    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private readonly InfoBar _errorBar;
    private readonly SettingsExpanderItem _chooseWayItem;
    private readonly SettingsExpanderItem _cardNumItem;
    private readonly SettingsExpanderItem _autoBattleItem;
    private readonly FAComboBox _transportPointCombo;
    private readonly FAComboBox _chooseWayCombo;
    private readonly FAComboBox _challengeWayCombo;
    private readonly FAComboBox _cardNumCombo;
    private readonly FAComboBox _predefinedTeamCombo;
    private readonly FAComboBox _autoBattleCombo;
    private readonly ToggleSwitch _runChargePlanAfterwardsToggle;
    private bool _loading;

    public ZzzCoffeeAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        AvaloniaXamlLoader.Load(this);

        _errorBar = Required<InfoBar>("ErrorBar");
        _chooseWayItem = Required<SettingsExpanderItem>("ChooseWayItem");
        _cardNumItem = Required<SettingsExpanderItem>("CardNumItem");
        _autoBattleItem = Required<SettingsExpanderItem>("AutoBattleItem");
        _transportPointCombo = Required<FAComboBox>("TransportPointCombo");
        _chooseWayCombo = Required<FAComboBox>("ChooseWayCombo");
        _challengeWayCombo = Required<FAComboBox>("ChallengeWayCombo");
        _cardNumCombo = Required<FAComboBox>("CardNumCombo");
        _predefinedTeamCombo = Required<FAComboBox>("PredefinedTeamCombo");
        _autoBattleCombo = Required<FAComboBox>("AutoBattleCombo");
        _runChargePlanAfterwardsToggle = Required<ToggleSwitch>("RunChargePlanAfterwardsToggle");

        _transportPointCombo.ItemsSource = new ZzzCoffeeSettingOption[]
        {
            new ZzzCoffeeSettingOption("六分街 - 咖啡店", "六分街 - 咖啡店"),
            new ZzzCoffeeSettingOption("澄辉坪 - 汀曼咖啡", "澄辉坪 - 汀曼咖啡"),
        };
        _chooseWayCombo.ItemsSource = new ZzzCoffeeSettingOption[]
        {
            new ZzzCoffeeSettingOption(
                CoffeeChooseWay.PlanPriority,
                CoffeeChooseWay.PlanPriority,
                "优先选择符合体力计划的咖啡，实战模拟室计划会选浓缩咖啡，没有匹配时选择汀曼特调"),
            new ZzzCoffeeSettingOption(CoffeeChooseWay.TinmanOnly, CoffeeChooseWay.TinmanOnly, "只选择汀曼特调"),
            new ZzzCoffeeSettingOption(CoffeeChooseWay.EspressoOnly, CoffeeChooseWay.EspressoOnly, "只选择浓缩咖啡"),
        };
        _challengeWayCombo.ItemsSource = CoffeeChallengeWay.Options
            .Select(option => new ZzzCoffeeSettingOption(option.Label, option.Value ?? option.Label))
            .ToArray();
        _cardNumCombo.ItemsSource = new ZzzCoffeeSettingOption[]
        {
            new ZzzCoffeeSettingOption(CoffeeCardNum.Default, CoffeeCardNum.Default, "挑战体力计划外的副本时，按游戏内设数量"),
            new ZzzCoffeeSettingOption(CoffeeCardNum.Num1, CoffeeCardNum.Num1, "挑战体力计划外的副本时，选择最少数量"),
        };

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

    internal void SaveForTest(string key, object? value) => Save(key, value);

    private void Reload()
    {
        _loading = true;
        _errorBar.IsOpen = false;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> coffeeResult =
                _backend.GetConfigScope(ScopeName, _instanceIndex, _groupId);
            if (!coffeeResult.Success || coffeeResult.Value is null)
            {
                ShowError(coffeeResult.Error ?? "咖啡计划配置读取失败。");
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

            _predefinedTeamCombo.ItemsSource = new[] { new ZzzCoffeeSettingOption("游戏内配队", -1) }
                .Concat(teams.Select(team => new ZzzCoffeeSettingOption(team.Name, team.Idx)))
                .ToArray();
            _autoBattleCombo.ItemsSource = catalogResult.Value.AutoBattle
                .Select(value => new ZzzCoffeeSettingOption(value, value))
                .ToArray();

            IReadOnlyDictionary<string, object?> values = coffeeResult.Value.Values;
            Select(_transportPointCombo, RequiredString(values, "transport_point"));
            Select(_chooseWayCombo, RequiredString(values, "choose_way"));
            Select(_challengeWayCombo, RequiredString(values, "challenge_way"));
            Select(_cardNumCombo, RequiredString(values, "card_num"));
            Select(_predefinedTeamCombo, RequiredInt(values, "predefined_team_idx"));
            Select(_autoBattleCombo, RequiredString(values, "auto_battle"));
            _runChargePlanAfterwardsToggle.IsChecked = RequiredBool(values, "run_charge_plan_afterwards");

            UpdateDescription(_chooseWayItem, _chooseWayCombo);
            UpdateDescription(_cardNumItem, _cardNumCombo);
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

    private void OnComboChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || sender is not FAComboBox { Tag: string key, SelectedItem: ZzzCoffeeSettingOption option } combo)
        {
            return;
        }

        Save(key, option.Value);
        if (ReferenceEquals(combo, _chooseWayCombo))
        {
            UpdateDescription(_chooseWayItem, combo);
        }
        else if (ReferenceEquals(combo, _cardNumCombo))
        {
            UpdateDescription(_cardNumItem, combo);
        }
    }

    private void OnPredefinedTeamChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _predefinedTeamCombo.SelectedItem is ZzzCoffeeSettingOption option)
        {
            Save("predefined_team_idx", option.Value);
        }

        UpdateAutoBattleVisibility();
    }

    private void OnRunChargePlanAfterwardsChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("run_charge_plan_afterwards", _runChargePlanAfterwardsToggle.IsChecked == true);
        }
    }

    private void UpdateAutoBattleVisibility()
    {
        _autoBattleItem.IsVisible = _predefinedTeamCombo.SelectedItem is ZzzCoffeeSettingOption { Value: int value }
            && value == -1;
    }

    private static void UpdateDescription(SettingsExpanderItem item, SelectingItemsControl combo)
    {
        item.Description = combo.SelectedItem is ZzzCoffeeSettingOption option
            ? option.Description
            : string.Empty;
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
            ShowError(result.Error ?? "咖啡计划配置保存失败。");
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
        combo.SelectedItem = combo.ItemsSource?.OfType<ZzzCoffeeSettingOption>()
            .FirstOrDefault(option => Equals(option.Value, value));
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"咖啡计划配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"咖啡计划配置缺少 {key}。");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"咖啡计划配置缺少 {key}。");
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"咖啡计划设置缺少 {name}。");
}
