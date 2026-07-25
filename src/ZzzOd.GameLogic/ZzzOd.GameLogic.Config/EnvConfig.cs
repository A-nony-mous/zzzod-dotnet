using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 运行环境配置。
/// </summary>
public sealed class EnvConfig
{
	[YamlMember(Alias = "screenshot_method", ApplyNamingConventions = false)]
	public string ScreenshotMethod { get; set; } = "print_window";

	[YamlMember(Alias = "is_debug", ApplyNamingConventions = false)]
	public bool IsDebug { get; set; }

	[YamlMember(Alias = "copy_screenshot", ApplyNamingConventions = false)]
	public bool CopyScreenshot { get; set; } = true;

	[YamlMember(Alias = "proxy_type", ApplyNamingConventions = false)]
	public string ProxyType { get; set; } = "None";

	[YamlMember(Alias = "personal_proxy", ApplyNamingConventions = false)]
	public string PersonalProxy { get; set; } = string.Empty;

	[YamlMember(Alias = "key_start_running", ApplyNamingConventions = false)]
	public string KeyStartRunning { get; set; } = "f9";

	[YamlMember(Alias = "key_stop_running", ApplyNamingConventions = false)]
	public string KeyStopRunning { get; set; } = "f10";

	[YamlMember(Alias = "key_screenshot", ApplyNamingConventions = false)]
	public string KeyScreenshot { get; set; } = "f11";

	[YamlMember(Alias = "key_debug", ApplyNamingConventions = false)]
	public string KeyDebug { get; set; } = "f12";
}
