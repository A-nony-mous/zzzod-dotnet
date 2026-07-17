using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 项目级配置。
/// </summary>
public sealed class ProjectConfig
{
	[YamlMember(Alias = "project_name", ApplyNamingConventions = false)]
	public string ProjectName { get; set; } = string.Empty;

	[YamlMember(Alias = "github_homepage", ApplyNamingConventions = false)]
	public string GithubHomepage { get; set; } = string.Empty;

	[YamlMember(Alias = "notice_url", ApplyNamingConventions = false)]
	public string NoticeUrl { get; set; } = string.Empty;

	[YamlMember(Alias = "qq_link", ApplyNamingConventions = false)]
	public string QqLink { get; set; } = string.Empty;

	[YamlMember(Alias = "home_page_link", ApplyNamingConventions = false)]
	public string HomePageLink { get; set; } = string.Empty;

	[YamlMember(Alias = "doc_link", ApplyNamingConventions = false)]
	public string DocLink { get; set; } = string.Empty;

	[YamlMember(Alias = "screen_standard_width", ApplyNamingConventions = false)]
	public int ScreenStandardWidth { get; set; } = 1920;

	[YamlMember(Alias = "screen_standard_height", ApplyNamingConventions = false)]
	public int ScreenStandardHeight { get; set; } = 1080;
}
