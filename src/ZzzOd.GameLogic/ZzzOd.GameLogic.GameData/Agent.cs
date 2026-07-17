using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.GameData;

public sealed class Agent
{
	public string AgentId { get; }

	public string TemplateId { get; }

	public string AgentName { get; }

	public RareTypeEnum RareType { get; }

	public AgentTypeEnum AgentType { get; }

	public DmgTypeEnum DmgType { get; }

	public IReadOnlyList<string> TemplateIdList { get; }

	public IReadOnlyList<AgentStateDef> StateList { get; }

	public string AgentTypeStr => AgentType.GetStringValue();

	public Agent(string agentId, string agentName, RareTypeEnum rareType, AgentTypeEnum agentType, DmgTypeEnum dmgType, IReadOnlyList<string> templateIdList, IReadOnlyList<AgentStateDef>? stateList = null)
	{
		AgentId = agentId;
		TemplateId = agentId;
		AgentName = agentName;
		RareType = rareType;
		AgentType = agentType;
		DmgType = dmgType;
		TemplateIdList = templateIdList.ToList();
		StateList = stateList?.ToList() ?? new List<AgentStateDef>();
	}
}
