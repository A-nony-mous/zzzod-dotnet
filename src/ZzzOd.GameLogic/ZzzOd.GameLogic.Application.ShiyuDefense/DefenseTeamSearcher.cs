using System;
using System.Collections.Generic;
using System.Linq;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战队伍搜索器。
/// </summary>
public sealed class DefenseTeamSearcher
{
	private readonly ShiyuDefenseConfig _config;

	private readonly IReadOnlyList<PredefinedTeamInfo> _predefinedTeamList;

	private readonly List<DefensePhaseTeamInfo> _teamList;

	private readonly Dictionary<int, ShiyuDefenseTeamConfig> _defenseTeamConfig = new Dictionary<int, ShiyuDefenseTeamConfig>();

	private readonly HashSet<int> _chosenIndexSet = new HashSet<int>();

	private List<DefensePhaseTeamInfo> _bestTeamList = new List<DefensePhaseTeamInfo>();

	/// <summary>
	/// 初始化队伍搜索器。
	/// </summary>
	public DefenseTeamSearcher(ShiyuDefenseConfig config, IReadOnlyList<PredefinedTeamInfo> predefinedTeamList, IReadOnlyList<DefensePhaseTeamInfo> teamList)
	{
		_config = config;
		_predefinedTeamList = predefinedTeamList;
		_teamList = teamList.Select((DefensePhaseTeamInfo team) => team.Clone()).ToList();
		foreach (PredefinedTeamInfo predefinedTeam in _predefinedTeamList)
		{
			_defenseTeamConfig[predefinedTeam.Idx] = _config.GetConfigByTeamIndex(predefinedTeam.Idx);
		}
	}

	/// <summary>
	/// 搜索最佳队伍。
	/// </summary>
	public IReadOnlyList<DefensePhaseTeamInfo> Search()
	{
		_chosenIndexSet.Clear();
		_bestTeamList = new List<DefensePhaseTeamInfo>();
		SearchDfs(0);
		return _bestTeamList;
	}

	private void SearchDfs(int phaseIndex)
	{
		if (phaseIndex >= _teamList.Count)
		{
			CompareAndSaveBest();
		}
		else
		{
			if (NoWayBetter(phaseIndex))
			{
				return;
			}
			DefensePhaseTeamInfo defensePhaseTeamInfo = _teamList[phaseIndex];
			List<int> list = new List<int>();
			List<int> list2 = new List<int>();
			List<int> list3 = new List<int>();
			foreach (PredefinedTeamInfo predefinedTeam in _predefinedTeamList)
			{
				if (!_chosenIndexSet.Contains(predefinedTeam.Idx) && _defenseTeamConfig.TryGetValue(predefinedTeam.Idx, out ShiyuDefenseTeamConfig value) && value.ForCritical)
				{
					bool flag = value.WeaknessList.Any(defensePhaseTeamInfo.PhaseWeakness.Contains);
					bool flag2 = value.WeaknessList.Any(defensePhaseTeamInfo.PhaseResistance.Contains);
					if (flag)
					{
						list.Add(predefinedTeam.Idx);
					}
					else if (flag2)
					{
						list2.Add(predefinedTeam.Idx);
					}
					else
					{
						list3.Add(predefinedTeam.Idx);
					}
				}
			}
			foreach (int item in list.Concat(list3).Concat(list2))
			{
				PredefinedTeamInfo newTeam = _predefinedTeamList[item];
				if (!_chosenIndexSet.Any((int oldIndex) => IsTeamConflict(newTeam, _predefinedTeamList[oldIndex])))
				{
					_chosenIndexSet.Add(item);
					defensePhaseTeamInfo.TeamIndex = item;
					defensePhaseTeamInfo.CalculateScore(_defenseTeamConfig.GetValueOrDefault(item));
					SearchDfs(phaseIndex + 1);
					_chosenIndexSet.Remove(item);
					defensePhaseTeamInfo.TeamIndex = -1;
					defensePhaseTeamInfo.SameAsWeakness = 0;
					defensePhaseTeamInfo.SameAsResistance = 0;
				}
			}
		}
	}

	private void CompareAndSaveBest()
	{
		int num = _teamList.Sum((DefensePhaseTeamInfo team) => team.Score);
		int num2 = _bestTeamList.Sum((DefensePhaseTeamInfo team) => team.Score);
		if (_bestTeamList.Count <= 0 || num > num2)
		{
			_bestTeamList = _teamList.Select((DefensePhaseTeamInfo team) => team.Clone()).ToList();
		}
	}

	private bool NoWayBetter(int nextPhaseIndex)
	{
		int num = _teamList.Count - nextPhaseIndex;
		int num2 = _teamList.Sum((DefensePhaseTeamInfo team) => team.Score);
		int num3 = _bestTeamList.Sum((DefensePhaseTeamInfo team) => team.Score);
		return num2 + num <= num3;
	}

	private static bool IsTeamConflict(PredefinedTeamInfo x, PredefinedTeamInfo y)
	{
		HashSet<string> hashSet = x.AgentIdList.Where((string agent) => agent != "unknown").ToHashSet<string>(StringComparer.Ordinal);
		HashSet<string> other = y.AgentIdList.Where((string agent) => agent != "unknown").ToHashSet<string>(StringComparer.Ordinal);
		return hashSet.Overlaps(other);
	}
}
