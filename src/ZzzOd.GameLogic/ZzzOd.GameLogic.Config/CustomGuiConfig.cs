using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// GUI 自定义外观配置。
/// </summary>
public sealed class CustomGuiConfig
{
	[YamlMember(Alias = "ui_language", ApplyNamingConventions = false)]
	public string UiLanguage { get; set; } = "auto";

	[YamlMember(Alias = "theme", ApplyNamingConventions = false)]
	public string Theme { get; set; } = "Auto";

	[YamlMember(Alias = "gui_shell_preset", ApplyNamingConventions = false)]
	public string GuiShellPreset { get; set; } = "classic";

	[YamlMember(Alias = "custom_theme_color", ApplyNamingConventions = false)]
	public bool CustomThemeColor { get; set; }

	[YamlMember(Alias = "global_theme_color", ApplyNamingConventions = false)]
	public string GlobalThemeColor { get; set; } = "0,120,215";

	[YamlMember(Alias = "background_type", ApplyNamingConventions = false)]
	public string BackgroundType { get; set; } = "static_background";

	[YamlMember(Alias = "custom_banner", ApplyNamingConventions = false)]
	public bool CustomBanner { get; set; }

	[YamlMember(Alias = "last_version_poster_fetch_time", ApplyNamingConventions = false)]
	public string LastVersionPosterFetchTime { get; set; } = string.Empty;

	[YamlMember(Alias = "last_static_background_fetch_time", ApplyNamingConventions = false)]
	public string LastStaticBackgroundFetchTime { get; set; } = string.Empty;

	[YamlMember(Alias = "last_dynamic_background_fetch_time", ApplyNamingConventions = false)]
	public string LastDynamicBackgroundFetchTime { get; set; } = string.Empty;
}
