using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzDailySignInShopOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzDailySignInSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField SelectedSignField =
        new("selected_sign", typeof(string), "hou_hou_bakery");

    private static readonly IReadOnlyList<ZzzConfigField> FieldList = [SelectedSignField];

    private readonly int _instanceIndex;
    private readonly string _groupId;

    public ZzzDailySignInSettingsViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => "daily-signin";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzDailySignInShopOption> ShopOptions { get; } =
    [
        new("吼吼饼铺", "hou_hou_bakery"),
        new("卦象集录", "trigrams_collection"),
        new("刮刮卡", "scratch_card"),
    ];

    public ZzzDailySignInShopOption? SelectedShop
    {
        get => ShopOptions.FirstOrDefault(option => option.Value == SelectedSign);
        set
        {
            if (value is not null)
            {
                SelectedSign = value.Value;
            }
        }
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        OnPropertyChanged(nameof(SelectedShop));
    }

    internal bool SaveForTest(string key, string value)
    {
        if (key != SelectedSignField.Key)
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "未知的每日签到配置字段。");
        }

        return SaveValue(SelectedSignField, value);
    }

    private string SelectedSign
    {
        get => GetValue<string>(SelectedSignField);
        set => SetValue(SelectedSignField, value);
    }
}

internal sealed record ZzzDriveDiscOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzDriveDiscDismantleSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField DismantleLevelField =
        new("dismantle_level", typeof(string), "A及以下");
    private static readonly ZzzConfigField DismantleAbandonField =
        new("dismantle_abandon", typeof(bool), false);

    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        DismantleLevelField,
        DismantleAbandonField,
    ];

    private readonly int _instanceIndex;
    private readonly string _groupId;

    public ZzzDriveDiscDismantleSettingsViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => "drive-disc-dismantle";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzDriveDiscOption> LevelOptions { get; } =
    [
        new("B", "B"),
        new("A及以下", "A及以下"),
        new("S及以下", "S及以下"),
    ];

    public ZzzDriveDiscOption? SelectedLevel
    {
        get => LevelOptions.FirstOrDefault(option => option.Value == DismantleLevel);
        set
        {
            if (value is not null)
            {
                DismantleLevel = value.Value;
            }
        }
    }

    public bool DismantleAbandon
    {
        get => GetValue<bool>(DismantleAbandonField);
        set => SetValue(DismantleAbandonField, value);
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        OnPropertyChanged(nameof(SelectedLevel));
        OnPropertyChanged(nameof(DismantleAbandon));
    }

    internal void SaveForTest(string key, object? value)
    {
        ZzzConfigField field = Fields.SingleOrDefault(candidate => candidate.Key == key)
            ?? throw new ArgumentOutOfRangeException(nameof(key), key, "未知的驱动盘拆解配置字段。");
        SaveValue(field, value);
    }

    private string DismantleLevel
    {
        get => GetValue<string>(DismantleLevelField);
        set => SetValue(DismantleLevelField, value);
    }
}
