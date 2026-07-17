using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public class AgentInfo
{
	public Agent? Agent { get; set; }

	public string? MatchedTemplateId { get; set; }

	public int Hp { get; set; }

	public int Energy { get; set; }

	public bool SpecialReady { get; set; }

	public bool UltimateReady { get; set; }

	public AgentInfo(Agent? agent, int hp = 100, int energy = 0, bool specialReady = false, bool ultimateReady = false, string? matchedTemplateId = null)
	{
		Agent = agent;
		MatchedTemplateId = matchedTemplateId;
		Hp = hp;
		Energy = energy;
		SpecialReady = specialReady;
		UltimateReady = ultimateReady;
	}
}
