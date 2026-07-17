using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 队伍配置。
/// </summary>
public sealed class TeamConfig
{
	private List<PredefinedTeamInfo> _teamList = new List<PredefinedTeamInfo>();

	[YamlMember(Alias = "team_list", ApplyNamingConventions = false)]
	public List<PredefinedTeamInfo> TeamList
	{
		get
		{
			return _teamList;
		}
		set
		{
			_teamList = NormalizeTeamList(value ?? new List<PredefinedTeamInfo>());
		}
	}

	public TeamConfig()
	{
		_teamList = NormalizeTeamList(new List<PredefinedTeamInfo>());
	}

	public void EnsureDefaultTeams()
	{
		_teamList = NormalizeTeamList(_teamList);
	}

	/// <summary>
	/// 按队伍名称更新代理人列表。
	/// </summary>
	/// <param name="teamName">队伍名称。</param>
	/// <param name="agentIds">识别到的代理人 ID。</param>
	/// <returns>找到并更新队伍时返回 <c>true</c>。</returns>
	public bool UpdateTeamMembers(string teamName, IEnumerable<string> agentIds)
	{
		PredefinedTeamInfo predefinedTeamInfo = _teamList.FirstOrDefault((PredefinedTeamInfo item) => string.Equals(item.Name, teamName, StringComparison.Ordinal));
		if (predefinedTeamInfo == null)
		{
			return false;
		}
		predefinedTeamInfo.AgentIdList = agentIds.ToList();
		predefinedTeamInfo.EnsureThreeAgents();
		return true;
	}

	private static List<PredefinedTeamInfo> NormalizeTeamList(List<PredefinedTeamInfo> input)
	{
		List<PredefinedTeamInfo> list = input ?? new List<PredefinedTeamInfo>();
		for (int i = 0; i < list.Count; i++)
		{
			PredefinedTeamInfo predefinedTeamInfo = list[i];
			predefinedTeamInfo.Idx = i;
			if (string.IsNullOrWhiteSpace(predefinedTeamInfo.Name))
			{
				predefinedTeamInfo.Name = $"编队{i + 1}";
			}
			if (string.IsNullOrWhiteSpace(predefinedTeamInfo.AutoBattle))
			{
				predefinedTeamInfo.AutoBattle = "全配队通用";
			}
			predefinedTeamInfo.EnsureThreeAgents();
		}
		for (int j = list.Count; j < 20; j++)
		{
			list.Add(new PredefinedTeamInfo(j, $"编队{j + 1}", "全配队通用", new List<string>()));
		}
		return list;
	}
}
