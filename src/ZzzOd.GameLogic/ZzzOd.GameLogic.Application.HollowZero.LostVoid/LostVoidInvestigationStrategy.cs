using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地调查战略。
/// </summary>
public sealed class LostVoidInvestigationStrategy
{
	/// <summary>战略名称。</summary>
	[YamlMember(Alias = "strategy_name", ApplyNamingConventions = false)]
	public string StrategyName { get; set; } = string.Empty;
}
