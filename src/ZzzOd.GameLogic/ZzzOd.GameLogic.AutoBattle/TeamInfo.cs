using System.Collections.Generic;
using System.Linq;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public class TeamInfo
{
	private readonly object _updateAgentLock = new object();

	public List<AgentInfo> Agents { get; } = new List<AgentInfo>();

	public bool ShouldCheckAllAgents { get; private set; }

	public int CheckAgentSameTimes { get; private set; }

	public int CheckAgentDiffTimes { get; private set; }

	public double AgentUpdateTime { get; private set; }

	public TeamInfo(List<string>? agentNames = null)
	{
		ShouldCheckAllAgents = agentNames == null;
		if (agentNames == null)
		{
			return;
		}
		foreach (string agentName in agentNames)
		{
			Agent agent = FindAgentByName(agentName);
			if (agent != null)
			{
				Agents.Add(new AgentInfo(agent));
			}
		}
	}

	public bool UpdateAgentList(IReadOnlyList<(Agent? Agent, string? MatchedTemplateId)> currentAgentList, IReadOnlyList<int> energyList, IReadOnlyList<int> specialList, IReadOnlyList<int> ultimateList, double updateTime)
	{
		lock (_updateAgentLock)
		{
			if (ShouldCheckAllAgents)
			{
				if (IsSameAgentList(currentAgentList))
				{
					CheckAgentDiffTimes = 0;
					CheckAgentSameTimes++;
					if (CheckAgentSameTimes >= 5)
					{
						ShouldCheckAllAgents = false;
					}
				}
				else
				{
					CheckAgentDiffTimes++;
					CheckAgentSameTimes = 0;
				}
			}
			else if (!IsSameAgentList(currentAgentList))
			{
				CheckAgentDiffTimes++;
				CheckAgentSameTimes = 0;
				if (CheckAgentDiffTimes >= 250)
				{
					ShouldCheckAllAgents = true;
				}
			}
			else
			{
				CheckAgentDiffTimes = 0;
			}
			if (!ShouldCheckAllAgents && !IsSameAgentList(currentAgentList))
			{
				return false;
			}
			if (currentAgentList.All<(Agent, string)>(((Agent Agent, string MatchedTemplateId) tuple2) => tuple2.Agent == null))
			{
				return false;
			}
			if (updateTime < AgentUpdateTime)
			{
				return false;
			}
			AgentUpdateTime = updateTime;
			Agents.Clear();
			for (int num = 0; num < currentAgentList.Count; num++)
			{
				(Agent? Agent, string? MatchedTemplateId) tuple = currentAgentList[num];
				Agent item = tuple.Agent;
				string item2 = tuple.MatchedTemplateId;
				int energy = ((num < energyList.Count) ? energyList[num] : 0);
				bool specialReady = num < specialList.Count && specialList[num] > 0;
				bool ultimateReady = num < ultimateList.Count && ultimateList[num] > 0;
				Agents.Add(new AgentInfo(item, 100, energy, specialReady, ultimateReady, item2));
			}
			return true;
		}
	}

	public bool IsSameAgentList(IReadOnlyList<(Agent? Agent, string? MatchedTemplateId)> currentAgentList)
	{
		lock (_updateAgentLock)
		{
			if (Agents.Count != currentAgentList.Count)
			{
				return false;
			}
			HashSet<string> hashSet = (from info in Agents
				where info.Agent != null
				select info.Agent.AgentId).ToHashSet();
			HashSet<string> hashSet2 = (from item in currentAgentList
				where item.Agent != null
				select item.Agent.AgentId).ToHashSet();
			return hashSet.SetEquals(hashSet2);
		}
	}

	public void RequestCheckAllAgents()
	{
		lock (_updateAgentLock)
		{
			ShouldCheckAllAgents = true;
			CheckAgentSameTimes = 0;
			CheckAgentDiffTimes = 0;
		}
	}

	public bool SwitchNextAgent(double updateTime)
	{
		lock (_updateAgentLock)
		{
			if (updateTime < AgentUpdateTime || Agents.Count == 0)
			{
				return false;
			}
			AgentUpdateTime = updateTime;
			List<AgentInfo> list = Agents.Where((AgentInfo info) => info.Agent != null).ToList();
			int num = Agents.Count - list.Count;
			if (list.Count > 0)
			{
				AgentInfo item = list[0];
				list.RemoveAt(0);
				list.Add(item);
			}
			Agents.Clear();
			Agents.AddRange(list);
			for (int num2 = 0; num2 < num; num2++)
			{
				Agents.Add(new AgentInfo(null));
			}
			return true;
		}
	}

	public bool SwitchPrevAgent(double updateTime)
	{
		lock (_updateAgentLock)
		{
			if (updateTime < AgentUpdateTime || Agents.Count == 0)
			{
				return false;
			}
			AgentUpdateTime = updateTime;
			List<AgentInfo> list = Agents.Where((AgentInfo info) => info.Agent != null).ToList();
			int num = Agents.Count - list.Count;
			if (list.Count > 0)
			{
				AgentInfo item = list[list.Count - 1];
				list.RemoveAt(list.Count - 1);
				list.Insert(0, item);
			}
			Agents.Clear();
			Agents.AddRange(list);
			for (int num2 = 0; num2 < num; num2++)
			{
				Agents.Add(new AgentInfo(null));
			}
			return true;
		}
	}

	public int GetAgentPos(Agent agent)
	{
		lock (_updateAgentLock)
		{
			for (int i = 0; i < Agents.Count; i++)
			{
				if (Agents[i].Agent?.AgentId == agent.AgentId)
				{
					return i + 1;
				}
			}
			return 0;
		}
	}

	public int GetAgentPosByName(string agentName)
	{
		Agent agent = FindAgentByName(agentName);
		return (agent != null) ? GetAgentPos(agent) : 0;
	}

	public IReadOnlyList<AgentInfo> Snapshot()
	{
		lock (_updateAgentLock)
		{
			return Agents.Select((AgentInfo info) => new AgentInfo(info.Agent, info.Hp, info.Energy, info.SpecialReady, info.UltimateReady, info.MatchedTemplateId)).ToList();
		}
	}

	public bool ForceFrontAgent(string agentName, double updateTime)
	{
		lock (_updateAgentLock)
		{
			if (Agents.Count == 0)
			{
				return false;
			}
			if (Agents[0].Agent?.AgentName == agentName)
			{
				return false;
			}
			AgentInfo agentInfo = Agents.FirstOrDefault((AgentInfo info) => info.Agent?.AgentName == agentName);
			if (agentInfo == null)
			{
				return false;
			}
			Agents.Remove(agentInfo);
			Agents.Insert(0, agentInfo);
			AgentUpdateTime = updateTime;
			return true;
		}
	}

	private static Agent? FindAgentByName(string name)
	{
		foreach (AgentEnum value in AgentEnum.Values)
		{
			if (value.Value.AgentName == name)
			{
				return value.Value;
			}
		}
		return null;
	}
}
