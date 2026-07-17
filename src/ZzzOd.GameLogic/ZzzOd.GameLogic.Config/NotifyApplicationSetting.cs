using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

public sealed class NotifyApplicationSetting
{
	[YamlMember(Alias = "lifecycle", ApplyNamingConventions = false)]
	public string Lifecycle { get; set; } = "start_and_finish";

	[YamlMember(Alias = "detail", ApplyNamingConventions = false)]
	public string Detail { get; set; } = "all";
}
