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

	/// <summary>
	/// 是否已进入用户保存的应用顺序；运行期状态，不落盘。
	/// 新注册应用以未持久化的临时项置顶展示，用户交互后才写入保存顺序。
	/// </summary>
	[YamlIgnore]
	public bool IsPersisted { get; set; } = true;

	public OneDragonApplicationConfigItem()
	{
	}

	public OneDragonApplicationConfigItem(string appId, bool enabled)
	{
		AppId = appId;
		Enabled = enabled;
	}
}
