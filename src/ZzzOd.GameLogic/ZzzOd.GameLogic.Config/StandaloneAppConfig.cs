using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 独立应用运行配置。
/// </summary>
public sealed class StandaloneAppConfig
{
	[YamlMember(Alias = "app_list", ApplyNamingConventions = false)]
	public List<string> AppList { get; set; } = new List<string>();

	[YamlMember(Alias = "active_app_id", ApplyNamingConventions = false)]
	public string ActiveAppId { get; set; } = string.Empty;
}
