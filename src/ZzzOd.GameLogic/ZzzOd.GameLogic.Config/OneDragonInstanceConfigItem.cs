using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 一条龙实例设置项。
/// </summary>
public sealed class OneDragonInstanceConfigItem
{
	[YamlMember(Alias = "idx", ApplyNamingConventions = false)]
	public int Idx { get; set; }

	[YamlMember(Alias = "name", ApplyNamingConventions = false)]
	public string Name { get; set; } = string.Empty;

	[YamlMember(Alias = "active", ApplyNamingConventions = false)]
	public bool Active { get; set; }

	[YamlMember(Alias = "active_in_od", ApplyNamingConventions = false)]
	public bool ActiveInOneDragon { get; set; } = true;

	[YamlMember(Alias = "force_login_before_run", ApplyNamingConventions = false)]
	public bool ForceLoginBeforeRun { get; set; }
}
