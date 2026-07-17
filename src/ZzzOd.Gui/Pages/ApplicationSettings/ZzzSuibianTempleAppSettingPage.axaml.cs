using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.SuibianTemple;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzSuibianOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzSuibianTempleAppSettingPage : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "suibian-temple";
    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private readonly InfoBar _errorBar;
    private readonly ToggleSwitch _autoManageToggle;
    private readonly NumberBox _craftDragNumber;
    private readonly IReadOnlyDictionary<string, ToggleSwitch> _boolEditors;
    private readonly IReadOnlyDictionary<string, FAComboBox> _comboEditors;
    private readonly SettingsExpanderItem[] _manualItems;
    private bool _loading;

    public ZzzSuibianTempleAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<InfoBar>("ErrorBar");
        _autoManageToggle = Required<ToggleSwitch>("AutoManageToggle");
        _craftDragNumber = Required<NumberBox>("CraftDragNumber");
        _boolEditors = new Dictionary<string, ToggleSwitch>(StringComparer.Ordinal)
        {
            ["yum_cha_sin"] = Required<ToggleSwitch>("YumChaToggle"),
            ["yum_cha_sin_period_refresh"] = Required<ToggleSwitch>("YumChaRefreshToggle"),
            ["good_goods_purchase_enabled"] = Required<ToggleSwitch>("GoodGoodsToggle"),
            ["boo_box_purchase_enabled"] = Required<ToggleSwitch>("BooBoxToggle"),
        };
        _comboEditors = new Dictionary<string, FAComboBox>(StringComparer.Ordinal)
        {
            ["adventure_duration"] = Required<FAComboBox>("AdventureDurationCombo"),
            ["adventure_mission_1"] = Required<FAComboBox>("AdventureMission1Combo"),
            ["adventure_mission_2"] = Required<FAComboBox>("AdventureMission2Combo"),
            ["adventure_mission_3"] = Required<FAComboBox>("AdventureMission3Combo"),
            ["adventure_mission_4"] = Required<FAComboBox>("AdventureMission4Combo"),
            ["boo_box_adventure_price"] = Required<FAComboBox>("BooBoxAdventureCombo"),
            ["boo_box_craft_price"] = Required<FAComboBox>("BooBoxCraftCombo"),
            ["boo_box_sell_price"] = Required<FAComboBox>("BooBoxSellCombo"),
        };
        _manualItems =
        [
            Required<SettingsExpanderItem>("YumChaItem"),
            Required<SettingsExpanderItem>("YumChaRefreshItem"),
            Required<SettingsExpanderItem>("AdventureDurationItem"),
            Required<SettingsExpanderItem>("AdventureMissionItem"),
            Required<SettingsExpanderItem>("CraftDragItem"),
        ];

        _comboEditors["adventure_duration"].ItemsSource = Options(SuibianTempleAdventureDispatchDuration.Options);
        foreach (string key in new[] { "adventure_mission_1", "adventure_mission_2", "adventure_mission_3", "adventure_mission_4" })
        {
            _comboEditors[key].ItemsSource = Options(SuibianTempleAdventureMission.Options);
        }

        foreach (string key in new[] { "boo_box_adventure_price", "boo_box_craft_price", "boo_box_sell_price" })
        {
            _comboEditors[key].ItemsSource = Options(SuibianTempleBangbooPrice.Options);
        }

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

    internal bool ManualSettingsVisible => _manualItems.All(item => item.IsVisible);

    internal void SaveForTest(string key, object? value) => Save(key, value);

    private void Reload()
    {
        _loading = true;
        _errorBar.IsOpen = false;
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(ScopeName, _instanceIndex, _groupId);
        if (!result.Success || result.Value is null)
        {
            ShowError(result.Error ?? "随便观配置读取失败。");
            _loading = false;
            return;
        }

        IReadOnlyDictionary<string, object?> values = result.Value.Values;
        bool autoManage = RequiredBool(values, "auto_manage_enabled");
        _autoManageToggle.IsChecked = autoManage;
        foreach ((string key, ToggleSwitch editor) in _boolEditors)
        {
            editor.IsChecked = RequiredBool(values, key);
        }

        foreach ((string key, FAComboBox editor) in _comboEditors)
        {
            Select(editor, RequiredString(values, key));
        }

        _craftDragNumber.Value = RequiredInt(values, "craft_drag_times");
        UpdateManualVisibility(autoManage);
        _loading = false;
    }

    private void OnAutoManageChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        bool enabled = _autoManageToggle.IsChecked == true;
        Save("auto_manage_enabled", enabled);
        UpdateManualVisibility(enabled);
    }

    private void OnBoolChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && sender is ToggleSwitch { Tag: string key } editor)
        {
            Save(key, editor.IsChecked == true);
        }
    }

    private void OnComboChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && sender is FAComboBox { Tag: string key, SelectedItem: ZzzSuibianOption option })
        {
            Save(key, option.Value);
        }
    }

    private void OnCraftDragChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_loading)
        {
            Save("craft_drag_times", (int)sender.Value);
        }
    }

    private void UpdateManualVisibility(bool autoManage)
    {
        foreach (SettingsExpanderItem item in _manualItems)
        {
            item.IsVisible = !autoManage;
        }
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
            ShowError(result.Error ?? "随便观配置保存失败。");
        }
    }

    private void ShowError(string message)
    {
        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private static IReadOnlyList<ZzzSuibianOption> Options(IReadOnlyList<global::OneDragon.Core.Configuration.ConfigItem> options) =>
        options.Select(option => new ZzzSuibianOption(option.Label, option.Value?.ToString() ?? string.Empty)).ToArray();

    private static void Select(SelectingItemsControl combo, string value)
    {
        combo.SelectedItem = combo.ItemsSource?.OfType<ZzzSuibianOption>()
            .FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"随便观配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"随便观配置缺少 {key}。");
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"随便观配置缺少 {key}。");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"随便观设置缺少 {name}。");
}
