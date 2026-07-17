using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 一条龙应用组配置项。
/// </summary>
public sealed class OneDragonApplicationConfigItem
{
	[YamlMember(Alias = "app_id", ApplyNamingConventions = false)]
	public string AppId { get; set; } = string.Empty;

	[YamlMember(Alias = "enabled", ApplyNamingConventions = false)]
	public bool Enabled { get; set; }

	public OneDragonApplicationConfigItem()
	{
	}

	public OneDragonApplicationConfigItem(string appId, bool enabled)
	{
		AppId = appId;
		Enabled = enabled;
	}
}
