using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 预定义编队信息。
/// </summary>
public sealed class PredefinedTeamInfo
{
	[YamlIgnore]
	public int Idx { get; set; }

	[YamlMember(Alias = "name", ApplyNamingConventions = false)]
	public string Name { get; set; } = string.Empty;

	[YamlMember(Alias = "auto_battle", ApplyNamingConventions = false)]
	public string AutoBattle { get; set; } = "全配队通用";

	[YamlMember(Alias = "agent_id_list", ApplyNamingConventions = false)]
	public List<string> AgentIdList { get; set; } = new List<string>();

	public PredefinedTeamInfo()
	{
	}

	public PredefinedTeamInfo(int idx, string name, string autoBattle, List<string> agentIdList)
	{
		Idx = idx;
		Name = name;
		AutoBattle = autoBattle;
		AgentIdList = agentIdList;
		EnsureThreeAgents();
	}

	public void EnsureThreeAgents()
	{
		while (AgentIdList.Count < 3)
		{
			AgentIdList.Add("unknown");
		}
	}
}
