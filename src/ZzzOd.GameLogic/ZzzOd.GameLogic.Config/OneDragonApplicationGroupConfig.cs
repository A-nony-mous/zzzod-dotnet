using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 一条龙应用组配置。
/// </summary>
public sealed class OneDragonApplicationGroupConfig
{
	[YamlMember(Alias = "app_list", ApplyNamingConventions = false)]
	public List<OneDragonApplicationConfigItem> AppList { get; set; } = new List<OneDragonApplicationConfigItem>();
}
