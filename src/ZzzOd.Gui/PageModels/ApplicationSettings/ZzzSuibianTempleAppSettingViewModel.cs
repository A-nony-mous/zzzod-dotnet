using OneDragon.Core.Configuration;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.SuibianTemple;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed class ZzzSuibianTempleAppSettingViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField AutoManageEnabledField =
        new("auto_manage_enabled", typeof(bool), true);
    private static readonly ZzzConfigField YumChaSinField =
        new("yum_cha_sin", typeof(bool), true);
    private static readonly ZzzConfigField YumChaRefreshField =
        new("yum_cha_sin_period_refresh", typeof(bool), true);
    private static readonly ZzzConfigField AdventureDurationField =
        new("adventure_duration", typeof(string), SuibianTempleAdventureDispatchDuration.Hour20.Name);
    private static readonly ZzzConfigField AdventureMission1Field =
        new("adventure_mission_1", typeof(string), SuibianTempleAdventureMission.Research34.Name);
    private static readonly ZzzConfigField AdventureMission2Field =
        new("adventure_mission_2", typeof(string), SuibianTempleAdventureMission.Research24.Name);
    private static readonly ZzzConfigField AdventureMission3Field =
        new("adventure_mission_3", typeof(string), SuibianTempleAdventureMission.Research14.Name);
    private static readonly ZzzConfigField AdventureMission4Field =
        new("adventure_mission_4", typeof(string), SuibianTempleAdventureMission.Community34.Name);
    private static readonly ZzzConfigField CraftDragTimesField =
        new("craft_drag_times", typeof(int), 10);
    private static readonly ZzzConfigField GoodGoodsPurchaseEnabledField =
        new("good_goods_purchase_enabled", typeof(bool), false);
    private static readonly ZzzConfigField BooBoxPurchaseEnabledField =
        new("boo_box_purchase_enabled", typeof(bool), false);
    private static readonly ZzzConfigField BooBoxAdventurePriceField =
        new("boo_box_adventure_price", typeof(string), SuibianTempleBangbooPrice.S4.Name);
    private static readonly ZzzConfigField BooBoxCraftPriceField =
        new("boo_box_craft_price", typeof(string), SuibianTempleBangbooPrice.S4.Name);
    private static readonly ZzzConfigField BooBoxSellPriceField =
        new("boo_box_sell_price", typeof(string), SuibianTempleBangbooPrice.S4.Name);

    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        AutoManageEnabledField,
        YumChaSinField,
        YumChaRefreshField,
        AdventureDurationField,
        AdventureMission1Field,
        AdventureMission2Field,
        AdventureMission3Field,
        AdventureMission4Field,
        CraftDragTimesField,
        GoodGoodsPurchaseEnabledField,
        BooBoxPurchaseEnabledField,
        BooBoxAdventurePriceField,
        BooBoxCraftPriceField,
        BooBoxSellPriceField,
    ];

    private readonly int _instanceIndex;
    private readonly string _groupId;

    public ZzzSuibianTempleAppSettingViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => "suibian-temple";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzSuibianOption> AdventureDurationOptions { get; } =
        Options(SuibianTempleAdventureDispatchDuration.Options);

    public IReadOnlyList<ZzzSuibianOption> AdventureMissionOptions { get; } =
        Options(SuibianTempleAdventureMission.Options);

    public IReadOnlyList<ZzzSuibianOption> BooBoxPriceOptions { get; } =
        Options(SuibianTempleBangbooPrice.Options);

    public bool AutoManageEnabled
    {
        get => GetValue<bool>(AutoManageEnabledField);
        set
        {
            if (SetValue(AutoManageEnabledField, value))
            {
                OnPropertyChanged(nameof(ManualSettingsVisible));
            }
        }
    }

    public bool ManualSettingsVisible => !AutoManageEnabled;

    public bool YumChaSin
    {
        get => GetValue<bool>(YumChaSinField);
        set => SetValue(YumChaSinField, value);
    }

    public bool YumChaRefresh
    {
        get => GetValue<bool>(YumChaRefreshField);
        set => SetValue(YumChaRefreshField, value);
    }

    public ZzzSuibianOption? SelectedAdventureDuration
    {
        get => Find(AdventureDurationOptions, GetValue<string>(AdventureDurationField));
        set => SetSelected(value, selected => SetValue(AdventureDurationField, selected.Value));
    }

    public ZzzSuibianOption? SelectedAdventureMission1
    {
        get => Find(AdventureMissionOptions, GetValue<string>(AdventureMission1Field));
        set => SetSelected(value, selected => SetValue(AdventureMission1Field, selected.Value));
    }

    public ZzzSuibianOption? SelectedAdventureMission2
    {
        get => Find(AdventureMissionOptions, GetValue<string>(AdventureMission2Field));
        set => SetSelected(value, selected => SetValue(AdventureMission2Field, selected.Value));
    }

    public ZzzSuibianOption? SelectedAdventureMission3
    {
        get => Find(AdventureMissionOptions, GetValue<string>(AdventureMission3Field));
        set => SetSelected(value, selected => SetValue(AdventureMission3Field, selected.Value));
    }

    public ZzzSuibianOption? SelectedAdventureMission4
    {
        get => Find(AdventureMissionOptions, GetValue<string>(AdventureMission4Field));
        set => SetSelected(value, selected => SetValue(AdventureMission4Field, selected.Value));
    }

    public double CraftDragTimes
    {
        get => GetValue<int>(CraftDragTimesField);
        set => SetValue(CraftDragTimesField, (int)value);
    }

    public bool GoodGoodsPurchaseEnabled
    {
        get => GetValue<bool>(GoodGoodsPurchaseEnabledField);
        set => SetValue(GoodGoodsPurchaseEnabledField, value);
    }

    public bool BooBoxPurchaseEnabled
    {
        get => GetValue<bool>(BooBoxPurchaseEnabledField);
        set => SetValue(BooBoxPurchaseEnabledField, value);
    }

    public ZzzSuibianOption? SelectedBooBoxAdventurePrice
    {
        get => Find(BooBoxPriceOptions, GetValue<string>(BooBoxAdventurePriceField));
        set => SetSelected(value, selected => SetValue(BooBoxAdventurePriceField, selected.Value));
    }

    public ZzzSuibianOption? SelectedBooBoxCraftPrice
    {
        get => Find(BooBoxPriceOptions, GetValue<string>(BooBoxCraftPriceField));
        set => SetSelected(value, selected => SetValue(BooBoxCraftPriceField, selected.Value));
    }

    public ZzzSuibianOption? SelectedBooBoxSellPrice
    {
        get => Find(BooBoxPriceOptions, GetValue<string>(BooBoxSellPriceField));
        set => SetSelected(value, selected => SetValue(BooBoxSellPriceField, selected.Value));
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        OnPropertyChanged(nameof(SelectedAdventureDuration));
        OnPropertyChanged(nameof(SelectedAdventureMission1));
        OnPropertyChanged(nameof(SelectedAdventureMission2));
        OnPropertyChanged(nameof(SelectedAdventureMission3));
        OnPropertyChanged(nameof(SelectedAdventureMission4));
        OnPropertyChanged(nameof(CraftDragTimes));
        OnPropertyChanged(nameof(SelectedBooBoxAdventurePrice));
        OnPropertyChanged(nameof(SelectedBooBoxCraftPrice));
        OnPropertyChanged(nameof(SelectedBooBoxSellPrice));
        OnPropertyChanged(nameof(ManualSettingsVisible));
    }

    internal void SaveForTest(string key, object? value)
    {
        ZzzConfigField field = Fields.SingleOrDefault(candidate => candidate.Key == key)
            ?? throw new ArgumentOutOfRangeException(nameof(key), key, "未知的随便观配置字段。");
        SaveValue(field, value);
    }

    private static IReadOnlyList<ZzzSuibianOption> Options(IReadOnlyList<ConfigItem> options) =>
        options.Select(option => new ZzzSuibianOption(option.Label, option.Value?.ToString() ?? string.Empty)).ToArray();

    private static ZzzSuibianOption? Find(
        IReadOnlyList<ZzzSuibianOption> options,
        string value) => options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private static void SetSelected(
        ZzzSuibianOption? selected,
        Action<ZzzSuibianOption> apply)
    {
        if (selected is not null)
        {
            apply(selected);
        }
    }
}
