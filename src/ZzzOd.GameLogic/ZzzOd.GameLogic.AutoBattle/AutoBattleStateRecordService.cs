using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using OneDragon.Core.Operation;
using Serilog;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

/// <summary>
/// 自动战斗状态记录服务
/// </summary>
public class AutoBattleStateRecordService : StateRecordService
{
	private readonly ConcurrentDictionary<string, StateRecorder> _recorders = new ConcurrentDictionary<string, StateRecorder>();

	private readonly HashSet<string> _allStateEventIds;

	private readonly Dictionary<string, List<string>> _mutexList;

	public AutoBattleStateRecordService()
	{
		_allStateEventIds = InitializeAllStateEventIds();
		_mutexList = InitializeMutexList();
	}

	public override StateRecorder? GetStateRecorder(string stateName)
	{
		if (IsValidState(stateName))
		{
			return _recorders.GetOrAdd(stateName, delegate(string key)
			{
				_mutexList.TryGetValue(key, out List<string> value);
				return new StateRecorder(key, value);
			});
		}
		Log.Error("使用了不合法的状态: " + stateName);
		return null;
	}

	public IReadOnlyDictionary<string, StateRecorderSnapshot> GetSnapshot()
	{
		Dictionary<string, StateRecorderSnapshot> dictionary = new Dictionary<string, StateRecorderSnapshot>();
		foreach (KeyValuePair<string, StateRecorder> recorder in _recorders)
		{
			dictionary[recorder.Key] = recorder.Value.GetSnapshot();
		}
		return dictionary;
	}

	public int ClearExpiredStates(double now, double maxAgeSeconds)
	{
		int num = 0;
		foreach (StateRecorder value in _recorders.Values)
		{
			StateRecorderSnapshot snapshot = value.GetSnapshot();
			if (!(snapshot.LastRecordTime <= 0.0) && now - snapshot.LastRecordTime > maxAgeSeconds)
			{
				value.ClearStateRecord();
				num++;
			}
		}
		return num;
	}

	private bool IsValidState(string stateName)
	{
		if (_allStateEventIds.Contains(stateName))
		{
			return true;
		}
		if (stateName.StartsWith("自定义-"))
		{
			return true;
		}
		return false;
	}

	private HashSet<string> InitializeAllStateEventIds()
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (YoloStateEventEnum value3 in Enum.GetValues(typeof(YoloStateEventEnum)))
		{
			hashSet.Add(value3.GetDescription());
		}
		foreach (BattleStateEnum value4 in Enum.GetValues(typeof(BattleStateEnum)))
		{
			string description = value4.GetDescription();
			hashSet.Add(description);
			hashSet.Add(description + "-松开");
			hashSet.Add(description + "-按下");
		}
		foreach (AgentEnum value5 in AgentEnum.Values)
		{
			string agentName = value5.Value.AgentName;
			hashSet.Add("前台-" + agentName);
			hashSet.Add("后台-" + agentName);
			hashSet.Add("后台-1-" + agentName);
			hashSet.Add("后台-2-" + agentName);
			hashSet.Add("连携技-1-" + agentName);
			hashSet.Add("连携技-2-" + agentName);
			hashSet.Add("快速支援-" + agentName);
			hashSet.Add("切换角色-" + agentName);
			hashSet.Add(agentName + "-能量");
			hashSet.Add(agentName + "-特殊技可用");
			hashSet.Add(agentName + "-终结技可用");
			if (value5.Value.StateList == null)
			{
				continue;
			}
			foreach (AgentStateDef state in value5.Value.StateList)
			{
				hashSet.Add(state.StateName);
			}
		}
		foreach (AgentTypeEnum value6 in Enum.GetValues(typeof(AgentTypeEnum)))
		{
			if (value6 != AgentTypeEnum.UNKNOWN)
			{
				string stringValue = value6.GetStringValue();
				hashSet.Add("前台-" + stringValue);
				hashSet.Add("后台-1-" + stringValue);
				hashSet.Add("后台-2-" + stringValue);
				hashSet.Add("连携技-1-" + stringValue);
				hashSet.Add("连携技-2-" + stringValue);
				hashSet.Add("快速支援-" + stringValue);
				hashSet.Add("切换角色-" + stringValue);
			}
		}
		foreach (CommonAgentStateEnum value7 in CommonAgentStateEnum.Values)
		{
			hashSet.Add(value7.Value.StateName);
		}
		for (int i = 1; i <= 2; i++)
		{
			hashSet.Add($"连携技-{i}-邦布");
		}
		hashSet.Add("连携技-准备");
		foreach (DetectionTask dETECTION_TASK in TargetState.DETECTION_TASKS)
		{
			if (!dETECTION_TASK.Enabled)
			{
				continue;
			}
			foreach (TargetStateDef stateDefinition in dETECTION_TASK.StateDefinitions)
			{
				hashSet.Add(stateDefinition.StateName);
			}
		}
		hashSet.Add("格挡-破碎");
		hashSet.Add("切人-冷却");
		return hashSet;
	}

	private Dictionary<string, List<string>> InitializeMutexList()
	{
		Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();
		foreach (AgentEnum value in AgentEnum.Values)
		{
			List<string> list = new List<string>();
			foreach (AgentEnum value2 in AgentEnum.Values)
			{
				if (value2 != value)
				{
					list.Add(value2.Value.AgentName);
				}
			}
			string agentName = value.Value.AgentName;
			dictionary["前台-" + agentName] = new List<string>();
			foreach (string item in list)
			{
				dictionary["前台-" + agentName].Add("前台-" + item);
			}
			dictionary["前台-" + agentName].Add("后台-1-" + agentName);
			dictionary["前台-" + agentName].Add("后台-2-" + agentName);
			dictionary["前台-" + agentName].Add("后台-" + agentName);
			dictionary["后台-" + agentName] = new List<string> { "前台-" + agentName };
			dictionary["后台-1-" + agentName] = new List<string>();
			foreach (string item2 in list)
			{
				dictionary["后台-1-" + agentName].Add("后台-1-" + item2);
			}
			dictionary["后台-1-" + agentName].Add("后台-2-" + agentName);
			dictionary["后台-1-" + agentName].Add("前台-" + agentName);
			dictionary["后台-2-" + agentName] = new List<string>();
			foreach (string item3 in list)
			{
				dictionary["后台-2-" + agentName].Add("后台-2-" + item3);
			}
			dictionary["后台-2-" + agentName].Add("后台-1-" + agentName);
			dictionary["后台-2-" + agentName].Add("前台-" + agentName);
			List<string> list2 = new List<string>(list) { "邦布" };
			dictionary["连携技-1-" + agentName] = new List<string>();
			foreach (string item4 in list2)
			{
				dictionary["连携技-1-" + agentName].Add("连携技-1-" + item4);
			}
			dictionary["连携技-2-" + agentName] = new List<string>();
			foreach (string item5 in list2)
			{
				dictionary["连携技-2-" + agentName].Add("连携技-2-" + item5);
			}
			dictionary["快速支援-" + agentName] = new List<string>();
			foreach (string item6 in list)
			{
				dictionary["快速支援-" + agentName].Add("快速支援-" + item6);
			}
			dictionary["切换角色-" + agentName] = new List<string>();
			foreach (string item7 in list)
			{
				dictionary["切换角色-" + agentName].Add("切换角色-" + item7);
			}
		}
		foreach (AgentTypeEnum value3 in Enum.GetValues(typeof(AgentTypeEnum)))
		{
			if (value3 == AgentTypeEnum.UNKNOWN)
			{
				continue;
			}
			List<string> list3 = new List<string>();
			foreach (AgentTypeEnum value4 in Enum.GetValues(typeof(AgentTypeEnum)))
			{
				if (value4 != AgentTypeEnum.UNKNOWN && value4 != value3)
				{
					list3.Add(value4.GetStringValue());
				}
			}
			string stringValue = value3.GetStringValue();
			dictionary["前台-" + stringValue] = new List<string>();
			foreach (string item8 in list3)
			{
				dictionary["前台-" + stringValue].Add("前台-" + item8);
			}
			dictionary["后台-1-" + stringValue] = new List<string>();
			foreach (string item9 in list3)
			{
				dictionary["后台-1-" + stringValue].Add("后台-1-" + item9);
			}
			dictionary["后台-2-" + stringValue] = new List<string>();
			foreach (string item10 in list3)
			{
				dictionary["后台-2-" + stringValue].Add("后台-2-" + item10);
			}
			dictionary["连携技-1-" + stringValue] = new List<string>();
			foreach (string item11 in list3)
			{
				dictionary["连携技-1-" + stringValue].Add("连携技-1-" + item11);
			}
			dictionary["连携技-2-" + stringValue] = new List<string>();
			foreach (string item12 in list3)
			{
				dictionary["连携技-2-" + stringValue].Add("连携技-2-" + item12);
			}
			dictionary["快速支援-" + stringValue] = new List<string>();
			foreach (string item13 in list3)
			{
				dictionary["快速支援-" + stringValue].Add("快速支援-" + item13);
			}
			dictionary["切换角色-" + stringValue] = new List<string>();
			foreach (string item14 in list3)
			{
				dictionary["切换角色-" + stringValue].Add("切换角色-" + item14);
			}
		}
		for (int i = 1; i <= 2; i++)
		{
			dictionary[$"连携技-{i}-邦布"] = new List<string>();
			foreach (AgentEnum value5 in AgentEnum.Values)
			{
				dictionary[$"连携技-{i}-邦布"].Add($"连携技-{i}-{value5.Value.AgentName}");
			}
		}
		return dictionary;
	}
}
