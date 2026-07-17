using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

internal sealed class WitheredDomainResonium
{
	[YamlMember(Alias = "category", ApplyNamingConventions = false)]
	public string Category { get; set; } = string.Empty;

	[YamlMember(Alias = "name", ApplyNamingConventions = false)]
	public string Name { get; set; } = string.Empty;

	[YamlMember(Alias = "level", ApplyNamingConventions = false)]
	public string Level { get; set; } = string.Empty;
}
