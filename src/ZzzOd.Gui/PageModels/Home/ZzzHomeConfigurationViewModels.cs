using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.Home;

public sealed record ZzzHomeQuickLink(string Key, string Label, string Tooltip, string Uri);

internal sealed class ZzzHomeProjectSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField HomePageLinkField = new("home_page_link", typeof(string), string.Empty);
    private static readonly ZzzConfigField GithubHomepageField = new("github_homepage", typeof(string), string.Empty);
    private static readonly ZzzConfigField DocLinkField = new("doc_link", typeof(string), string.Empty);
    private static readonly ZzzConfigField QqLinkField = new("qq_link", typeof(string), string.Empty);
    private static readonly ZzzConfigField NoticeUrlField = new("notice_url", typeof(string), string.Empty);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        HomePageLinkField,
        GithubHomepageField,
        DocLinkField,
        QqLinkField,
        NoticeUrlField,
    ];

    public ZzzHomeProjectSettingsViewModel(IZzzAppBackend backend)
        : base(backend)
    {
    }

    protected override string ScopeName => "project";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public IReadOnlyList<ZzzHomeQuickLink> QuickLinks =>
    [
        new("home", "官网", "使用说明 · 功能介绍", HomePageLink),
        new("github", "GitHub", "源码 · 反馈 · Star⭐", GithubHomepage),
        new("docs", "帮助文档", "遇到问题？点这里找答案", DocLink),
        new("official-channel", "官方频道", "加入官方交流频道", QqLink),
    ];

    public string NoticeUrl => GetValue<string>(NoticeUrlField).Trim();

    private string HomePageLink => GetValue<string>(HomePageLinkField).Trim();

    private string GithubHomepage => GetValue<string>(GithubHomepageField).Trim();

    private string DocLink => GetValue<string>(DocLinkField).Trim();

    private string QqLink => GetValue<string>(QqLinkField).Trim();
}

internal sealed class ZzzHomeThemeSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField CustomThemeColorField = new("custom_theme_color", typeof(bool), false);
    private static readonly ZzzConfigField GlobalThemeColorField = new("global_theme_color", typeof(string), string.Empty);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList = [CustomThemeColorField, GlobalThemeColorField];

    public ZzzHomeThemeSettingsViewModel(IZzzAppBackend backend)
        : base(backend)
    {
    }

    protected override string ScopeName => "custom";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public bool CustomThemeColor => GetValue<bool>(CustomThemeColorField);

    public string GlobalThemeColor => GetValue<string>(GlobalThemeColorField).Trim();

    public void Reload() => OnPageShown();

    public void SaveExtractedThemeColor(string value)
    {
        if (!SetValue(GlobalThemeColorField, value, nameof(GlobalThemeColor)))
        {
            SaveValue(GlobalThemeColorField, value);
        }
    }
}
