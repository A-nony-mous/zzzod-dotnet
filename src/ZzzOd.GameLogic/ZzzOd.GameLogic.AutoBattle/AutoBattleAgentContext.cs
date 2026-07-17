using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Matcher;
using OneDragon.Core.Operation;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using Serilog;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public class AutoBattleAgentContext
{
	private static readonly DedicatedTaskScheduler AgentCheckScheduler = new DedicatedTaskScheduler("zzz-agent-check", 16);

	private static readonly TaskFactory AgentCheckExecutor = new TaskFactory(AgentCheckScheduler);

	private readonly ZContext _ctx;

	private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

	private readonly object _checkAgentLock = new object();

	private double _lastSwitchAgentTime;

	private double _lastUltimateTime;

	private string? _lastLoggedTeamState;

	private long _lastTeamStateLogAtMilliseconds;

	public TeamInfo Team { get; private set; }

	public AutoBattleInterval CheckAgentInterval { get; private set; } = new AutoBattleInterval(0.5f, 0.5f);

	public double LastCheckAgentTime { get; private set; }

	public AutoBattleAgentContext(ZContext ctx)
	{
		_ctx = ctx;
		Team = new TeamInfo();
	}

	public void InitAutoOp(AutoBattleOperator autoOp)
	{
		CheckAgentInterval = autoOp.CheckAgentInterval;
	}

	public void InitBattleAgentContext(List<string>? agentNames = null)
	{
		Team = new TeamInfo(agentNames);
		LastCheckAgentTime = 0.0;
		_lastSwitchAgentTime = 0.0;
	}

	/// <summary>
	/// 让下一次队伍识别跳过时间间隔。迷失之地在追加代理人类型优先级前需要按 BaselineParity
	/// `agent_context._last_check_agent_time = 0` 的语义，用刚获取的新截图刷新队伍。
	/// </summary>
	public void ResetCheckAgentTime()
	{
		lock (_checkAgentLock)
		{
			LastCheckAgentTime = 0.0;
		}
	}

	public IReadOnlyList<(Agent Agent, string? MatchedTemplateId)> GetPossibleAgentList()
	{
		if (Team.ShouldCheckAllAgents || Team.Agents.Count == 0 || Team.Agents.Any((AgentInfo info) => info.Agent == null))
		{
			return AgentEnum.Values.Select((AgentEnum agentEnum) => ((Agent Value, string))(Value: agentEnum.Value, null)).ToList();
		}
		return (from info in Team.Snapshot()
			where info.Agent != null
			select (info.Agent, MatchedTemplateId: info.MatchedTemplateId)).ToList();
	}

	public IReadOnlyList<StateRecord> CheckAgentRelated(Mat screen, double screenshotTime, bool updateState = true, string source = "unknown", double queueDelayMilliseconds = 0.0)
	{
		if (!Monitor.TryEnter(_checkAgentLock))
		{
			return Array.Empty<StateRecord>();
		}
		try
		{
			if (screenshotTime - LastCheckAgentTime < (double)CheckAgentInterval.NextValue())
			{
				return Array.Empty<StateRecord>();
			}
			LastCheckAgentTime = screenshotTime;
			IReadOnlyList<(Agent, string)> readOnlyList = CheckAgentInScreen(screen);
			if (ShouldForceCheckAllAgents(readOnlyList))
			{
				Team.RequestCheckAllAgents();
				LastCheckAgentTime = 0.0;
			}
			(List<StateRecord> Energy, List<StateRecord> Special, List<StateRecord> Ultimate, List<StateRecord> Other) tuple = CheckAllAgentState(screen, screenshotTime, readOnlyList);
			List<StateRecord> item = tuple.Energy;
			List<StateRecord> item2 = tuple.Special;
			List<StateRecord> item3 = tuple.Ultimate;
			List<StateRecord> item4 = tuple.Other;
			List<StateRecord> list = new List<StateRecord>();
			if (Team.UpdateAgentList(readOnlyList, item.Select((StateRecord record) => record.Value.GetValueOrDefault()).ToList(), item2.Select((StateRecord record) => record.Value.GetValueOrDefault()).ToList(), item3.Select((StateRecord record) => record.Value.GetValueOrDefault()).ToList(), screenshotTime))
			{
				list.AddRange(GetAgentStateRecords(screenshotTime));
				list.AddRange(item4);
				LogTeamState(screenshotTime);
			}
			if (updateState && list.Count > 0)
			{
				_ctx.AutoBattleContext.StateRecordService.BatchUpdateStates(list);
			}
			return list;
		}
		catch (Exception exception)
		{
			ILogger logger = _ctx.Logger;
			double? queueDelayMilliseconds2 = queueDelayMilliseconds;
			AutoBattleDiagnosticLogger.LogFailure(logger, exception, "识别画面角色失败", "Agent", source, screenshotTime, null, queueDelayMilliseconds2);
			return Array.Empty<StateRecord>();
		}
		finally
		{
			Monitor.Exit(_checkAgentLock);
		}
	}

	public List<StateRecord> UpdateAgentList(IReadOnlyList<(Agent? Agent, string? MatchedTemplateId)> currentAgentList, IReadOnlyList<int> energyList, IReadOnlyList<int> specialList, IReadOnlyList<int> ultimateList, double updateTime, bool updateState = true)
	{
		if (!Team.UpdateAgentList(currentAgentList, energyList, specialList, ultimateList, updateTime))
		{
			return new List<StateRecord>();
		}
		List<StateRecord> agentStateRecords = GetAgentStateRecords(updateTime);
		if (updateState)
		{
			_ctx.AutoBattleContext.StateRecordService.BatchUpdateStates(agentStateRecords);
		}
		return agentStateRecords;
	}

	public List<StateRecord> SwitchNextAgent(double updateTime, bool updateState = true, bool checkAgent = false)
	{
		if (!Team.SwitchNextAgent(updateTime))
		{
			return new List<StateRecord>();
		}
		if (checkAgent)
		{
			LastCheckAgentTime = 0.0;
		}
		List<StateRecord> agentStateRecords = GetAgentStateRecords(updateTime, @switch: true);
		if (updateState)
		{
			_ctx.AutoBattleContext.StateRecordService.BatchUpdateStates(agentStateRecords);
		}
		return agentStateRecords;
	}

	public List<StateRecord> SwitchPrevAgent(double updateTime, bool updateState = true, bool checkAgent = false)
	{
		if (!Team.SwitchPrevAgent(updateTime))
		{
			return new List<StateRecord>();
		}
		if (checkAgent)
		{
			LastCheckAgentTime = 0.0;
		}
		List<StateRecord> agentStateRecords = GetAgentStateRecords(updateTime, @switch: true);
		if (updateState)
		{
			_ctx.AutoBattleContext.StateRecordService.BatchUpdateStates(agentStateRecords);
		}
		return agentStateRecords;
	}

	public (int Position, List<StateRecord> Records) SwitchQuickAssist(double updateTime, bool updateState = true)
	{
		Agent agent = null;
		StateRecorder stateRecorder = null;
		foreach (AgentEnum value2 in AgentEnum.Values)
		{
			Agent value = value2.Value;
			StateRecorder stateRecorder2 = _ctx.AutoBattleContext.StateRecordService.GetStateRecorder("快速支援-" + value.AgentName);
			if (stateRecorder2 != null && !(stateRecorder2.LastRecordTime <= 0.0) && (stateRecorder == null || stateRecorder2.LastRecordTime > stateRecorder.LastRecordTime))
			{
				stateRecorder = stateRecorder2;
				agent = value;
			}
		}
		if (agent == null)
		{
			return (Position: 0, Records: new List<StateRecord>());
		}
		int agentPos = Team.GetAgentPos(agent);
		return agentPos switch
		{
			2 => (Position: agentPos, Records: SwitchNextAgent(updateTime, updateState)), 
			3 => (Position: agentPos, Records: SwitchPrevAgent(updateTime, updateState)), 
			_ => (Position: 0, Records: new List<StateRecord>()), 
		};
	}

	public List<StateRecord> ChainLeft(double updateTime, bool updateState = true)
	{
		List<string> chainName = GetChainName();
		string text = ((chainName.Count < 1 || chainName[0] != "邦布") ? chainName.ElementAtOrDefault(0) : chainName.ElementAtOrDefault(1));
		if (string.IsNullOrEmpty(text))
		{
			return new List<StateRecord>();
		}
		return ForceReconstructAgentStates(text, updateTime, updateState);
	}

	public List<StateRecord> ChainRight(double updateTime, bool updateState = true)
	{
		List<string> chainName = GetChainName();
		string text = ((chainName.Count < 2 || chainName[1] == "邦布") ? chainName.ElementAtOrDefault(0) : chainName.ElementAtOrDefault(1));
		if (string.IsNullOrEmpty(text))
		{
			return new List<StateRecord>();
		}
		return ForceReconstructAgentStates(text, updateTime, updateState);
	}

	public List<string?> GetChainName()
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string> { "邦布" };
		list2.AddRange(AgentEnum.Values.Select((AgentEnum agentEnum) => agentEnum.Value.AgentName));
		for (int num = 1; num <= 2; num++)
		{
			string item = null;
			StateRecorder stateRecorder = null;
			foreach (string item2 in list2)
			{
				StateRecorder stateRecorder2 = _ctx.AutoBattleContext.StateRecordService.GetStateRecorder($"连携技-{num}-{item2}");
				if (stateRecorder2 != null && !(stateRecorder2.LastRecordTime <= 0.0) && (stateRecorder == null || stateRecorder2.LastRecordTime > stateRecorder.LastRecordTime))
				{
					stateRecorder = stateRecorder2;
					item = item2;
				}
			}
			list.Add(item);
		}
		return list;
	}

	public (int Position, List<StateRecord> Records) SwitchByAgentName(string agentName, double updateTime, bool updateState = true)
	{
		int agentPosByName = Team.GetAgentPosByName(agentName);
		return agentPosByName switch
		{
			2 => (Position: agentPosByName, Records: SwitchNextAgent(updateTime, updateState, checkAgent: true)), 
			3 => (Position: agentPosByName, Records: SwitchPrevAgent(updateTime, updateState, checkAgent: true)), 
			_ => (Position: 0, Records: new List<StateRecord>()), 
		};
	}

	public StateRecord? CheckAgentRelatedState(Mat image, AgentStateDef stateDef, double screenshotTime, Mat? mask = null)
	{
		AgentStateDef agentStateDef = AgentStateChecker.ResolveStateDef(stateDef);
		int num = AgentStateChecker.CheckStateValue(image, agentStateDef, mask);
		if (num < 0 || num < agentStateDef.MinValueTriggerState)
		{
			return null;
		}
		bool flag = num == 0 && (agentStateDef.ClearOnZero || stateDef.StateName == CommonAgentStateEnum.SWITCH_BAN.Value.StateName || stateDef.StateName == CommonAgentStateEnum.GUARD_BREAK.Value.StateName);
		string stateName = stateDef.StateName;
		int? value = num;
		bool isClear = flag;
		return new StateRecord(stateName, screenshotTime, value, null, null, isClear);
	}

	public StateRecord? CheckAgentRelatedState(Mat screen, AgentStateDef stateDef, double screenshotTime, int? total, int? pos)
	{
		AgentStateDef agentStateDef = AgentStateChecker.ResolveStateDef(stateDef, total, pos);
		int num = AgentStateChecker.CheckStateValue(_ctx, screen, agentStateDef, total, pos);
		if (num < 0 || num < agentStateDef.MinValueTriggerState)
		{
			return null;
		}
		bool flag = num == 0 && (agentStateDef.ClearOnZero || stateDef.StateName == CommonAgentStateEnum.SWITCH_BAN.Value.StateName || stateDef.StateName == CommonAgentStateEnum.GUARD_BREAK.Value.StateName);
		string stateName = stateDef.StateName;
		int? value = num;
		bool isClear = flag;
		return new StateRecord(stateName, screenshotTime, value, null, null, isClear);
	}

	public List<StateRecord> GetAgentStateRecords(double updateTime, bool @switch = false)
	{
		List<StateRecord> list = new List<StateRecord>();
		IReadOnlyList<AgentInfo> readOnlyList = Team.Snapshot();
		if (readOnlyList.Count == 0)
		{
			return list;
		}
		for (int i = 0; i < readOnlyList.Count; i++)
		{
			string text = ((i == 0) ? "前台-" : $"后台-{i}-");
			AgentInfo agentInfo = readOnlyList[i];
			Agent agent = agentInfo.Agent;
			bool isClear;
			if (agent != null)
			{
				list.Add(new StateRecord(text + agent.AgentName, updateTime));
				list.Add(new StateRecord(text + agent.AgentType.GetStringValue(), updateTime));
				if (i > 0)
				{
					list.Add(new StateRecord("后台-" + agent.AgentName, updateTime));
				}
				if (i == 0 && @switch)
				{
					list.Add(new StateRecord("切换角色-" + agent.AgentName, updateTime));
					list.Add(new StateRecord("切换角色-" + agent.AgentType.GetStringValue(), updateTime));
					_lastSwitchAgentTime = updateTime;
				}
				list.Add(new StateRecord(agent.AgentName + "-能量", updateTime, agentInfo.Energy));
				if (updateTime - _lastSwitchAgentTime >= 0.10000000149011612)
				{
					string stateName = agent.AgentName + "-终结技可用";
					isClear = !agentInfo.UltimateReady;
					list.Add(new StateRecord(stateName, updateTime, null, null, null, isClear));
					string stateName2 = agent.AgentName + "-特殊技可用";
					isClear = !agentInfo.SpecialReady;
					list.Add(new StateRecord(stateName2, updateTime, null, null, null, isClear));
				}
			}
			if (i == 0)
			{
				string description = BattleStateEnum.StatusSpecialReady.GetDescription();
				isClear = !agentInfo.SpecialReady;
				list.Add(new StateRecord(description, updateTime, null, null, null, isClear));
				string description2 = BattleStateEnum.StatusUltimateReady.GetDescription();
				isClear = !agentInfo.UltimateReady;
				list.Add(new StateRecord(description2, updateTime, null, null, null, isClear));
				if (agentInfo.UltimateReady && _ctx.AutoBattleContext.AutoUltimateEnabled)
				{
					AutoBattleOperator? autoOp = _ctx.AutoBattleContext.AutoOp;
					if (autoOp != null && autoOp.IsRunning && updateTime - _lastUltimateTime > 2.0)
					{
						list.Add(new StateRecord("自定义-终结技被强制释放", updateTime));
						_lastUltimateTime = updateTime;
					}
				}
			}
			list.Add(new StateRecord(text + "能量", updateTime, agentInfo.Energy));
			string stateName3 = text + "特殊技可用";
			isClear = !agentInfo.SpecialReady;
			list.Add(new StateRecord(stateName3, updateTime, null, null, null, isClear));
			string stateName4 = text + "终结技可用";
			isClear = !agentInfo.UltimateReady;
			list.Add(new StateRecord(stateName4, updateTime, null, null, null, isClear));
		}
		return list;
	}

	private List<StateRecord> ForceReconstructAgentStates(string agentName, double updateTime, bool updateState)
	{
		if (!Team.ForceFrontAgent(agentName, updateTime))
		{
			return new List<StateRecord>();
		}
		List<StateRecord> agentStateRecords = GetAgentStateRecords(updateTime, @switch: true);
		if (updateState)
		{
			_ctx.AutoBattleContext.StateRecordService.BatchUpdateStates(agentStateRecords);
		}
		return agentStateRecords;
	}

	private IReadOnlyList<(Agent? Agent, string? MatchedTemplateId)> CheckAgentInScreen(Mat screen)
	{
		string[] array = new string[4] { "头像-3-1", "头像-3-2", "头像-3-3", "头像-2-2" };
		(Agent, string)[] array2 = new(Agent, string)[array.Length];
		IReadOnlyList<(Agent Agent, string? MatchedTemplateId)> possibleAgents = GetPossibleAgentList();
		bool[] array3 = new bool[4] { true, false, false, false };
		if (!Team.ShouldCheckAllAgents)
		{
			if (Team.Agents.Count == 3)
			{
				array3[1] = true;
				array3[2] = true;
			}
			else if (Team.Agents.Count == 2)
			{
				array3[3] = true;
			}
		}
		else
		{
			array3 = new bool[4] { true, true, true, true };
		}
		Task<(int, Agent, string)>[] array4 = new Task<(int, Agent, string)>[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			if (!array3[i])
			{
				continue;
			}
			int index = i;
			string areaName = array[index];
			array4[index] = AgentCheckExecutor.StartNew(delegate
			{
				OneDragon.Core.Screen.ScreenArea area = _ctx.ScreenContext.GetArea("战斗画面", areaName);
				if (area != null)
				{
					using Mat image = CvImageUtils.Crop(screen, area.Rect);
					var (item3, item4) = MatchAgentIn(image, index == 0, possibleAgents);
					return (index: index, item3, item4);
				}
				return ((int index, Agent, string))(index: index, null, null);
			}, _shutdownCts.Token);
		}
		Task<(int, Agent, string)>[] array5 = array4;
		foreach (Task<(int, Agent, string)> task in array5)
		{
			if (task != null)
			{
				try
				{
					var (num2, item, item2) = task.GetAwaiter().GetResult();
					array2[num2] = (item, item2);
				}
				catch (Exception exception)
				{
					Log.Error(exception, "识别角色头像失败");
				}
			}
		}
		if (array2.Length >= 3 && array2[1].Item1 != null && array2[2].Item1 != null)
		{
			return array2.Take(3).ToList();
		}
		if (array2.Length >= 4 && array2[3].Item1 != null)
		{
			return new(Agent, string)[2]
			{
				array2[0],
				array2[3]
			};
		}
		return new (Agent, string)[] { array2[0] };
	}

	private (Agent? Agent, string? MatchedTemplateId) MatchAgentIn(Mat image, bool isFront, IReadOnlyList<(Agent Agent, string? MatchedTemplateId)> possibleAgents)
	{
		Agent agent = null;
		string item = null;
		double num = 0.0;
		string text = (isFront ? "avatar_1_" : "avatar_2_");
		List<(Agent, string)>[] array = new List<(Agent, string)>[2]
		{
			new List<(Agent, string)>(),
			new List<(Agent, string)>()
		};
		foreach (var (agent2, text2) in possibleAgents)
		{
			foreach (string templateId in agent2.TemplateIdList)
			{
				if (text2 != null && string.Equals(templateId, text2, StringComparison.Ordinal))
				{
					array[0].Add((agent2, templateId));
				}
				else
				{
					array[1].Add((agent2, templateId));
				}
			}
		}
		List<(Agent, string)>[] array2 = array;
		foreach (List<(Agent, string)> list in array2)
		{
			foreach (var item4 in list)
			{
				Agent item2 = item4.Item1;
				string item3 = item4.Item2;
				MatchResultList matchResultList = _ctx.TemplateMatcher.MatchTemplate(image, "battle", text + item3, "raw", 0.8);
				if (matchResultList.Max != null && !(matchResultList.Max.Confidence < num))
				{
					agent = item2;
					item = item3;
					num = matchResultList.Max.Confidence;
				}
			}
			if (agent != null)
			{
				return (Agent: agent, MatchedTemplateId: item);
			}
		}
		return (Agent: null, MatchedTemplateId: null);
	}

	private bool ShouldForceCheckAllAgents(IReadOnlyList<(Agent? Agent, string? MatchedTemplateId)> screenAgentList)
	{
		return !Team.ShouldCheckAllAgents && screenAgentList.All<(Agent, string)>(((Agent Agent, string MatchedTemplateId) item) => item.Agent == null);
	}

	private (List<StateRecord> Energy, List<StateRecord> Special, List<StateRecord> Ultimate, List<StateRecord> Other) CheckAllAgentState(Mat screen, double screenshotTime, IReadOnlyList<(Agent? Agent, string? MatchedTemplateId)> screenAgentList)
	{
		if (screenAgentList.Count == 0)
		{
			return (Energy: new List<StateRecord>(), Special: new List<StateRecord>(), Ultimate: new List<StateRecord>(), Other: new List<StateRecord>());
		}
		int count = screenAgentList.Count;
		AgentStateDef[] source;
		AgentStateDef[] source2;
		AgentStateDef[] source3;
		switch (count)
		{
		case 3:
			source = new AgentStateDef[3]
			{
				CommonAgentStateEnum.ENERGY_31.Value,
				CommonAgentStateEnum.ENERGY_32.Value,
				CommonAgentStateEnum.ENERGY_33.Value
			};
			source2 = new AgentStateDef[3]
			{
				CommonAgentStateEnum.SPECIAL_31.Value,
				CommonAgentStateEnum.SPECIAL_32.Value,
				CommonAgentStateEnum.SPECIAL_33.Value
			};
			source3 = new AgentStateDef[3]
			{
				CommonAgentStateEnum.ULTIMATE_31.Value,
				CommonAgentStateEnum.ULTIMATE_32.Value,
				CommonAgentStateEnum.ULTIMATE_33.Value
			};
			break;
		case 2:
			source = new AgentStateDef[2]
			{
				CommonAgentStateEnum.ENERGY_21.Value,
				CommonAgentStateEnum.ENERGY_22.Value
			};
			source2 = new AgentStateDef[2]
			{
				CommonAgentStateEnum.SPECIAL_21.Value,
				CommonAgentStateEnum.SPECIAL_22.Value
			};
			source3 = new AgentStateDef[2]
			{
				CommonAgentStateEnum.ULTIMATE_21.Value,
				CommonAgentStateEnum.ULTIMATE_22.Value
			};
			break;
		default:
			source = new AgentStateDef[1] { CommonAgentStateEnum.ENERGY_21.Value };
			source2 = new AgentStateDef[1] { CommonAgentStateEnum.SPECIAL_31.Value };
			source3 = new AgentStateDef[1] { CommonAgentStateEnum.ULTIMATE_31.Value };
			break;
		}
		List<(AgentStateDef, int?, int?, int)> list = new List<(AgentStateDef, int?, int?, int)>();
		list.AddRange(source.Select((AgentStateDef state) => ((AgentStateDef state, int?, int?, int))(state: state, null, null, 0)));
		list.AddRange(source2.Select((AgentStateDef state) => ((AgentStateDef state, int?, int?, int))(state: state, null, null, 1)));
		list.AddRange(source3.Select((AgentStateDef state) => ((AgentStateDef state, int?, int?, int))(state: state, null, null, 2)));
		for (int num = 0; num < screenAgentList.Count; num++)
		{
			Agent item = screenAgentList[num].Agent;
			if (item == null)
			{
				continue;
			}
			foreach (AgentStateDef state in item.StateList)
			{
				list.Add((state, count, num + 1, 3));
			}
		}
		AgentStateDef[] array = new AgentStateDef[3]
		{
			CommonAgentStateEnum.GUARD_BREAK.Value,
			CommonAgentStateEnum.SWITCH_BAN.Value,
			(count == 3) ? CommonAgentStateEnum.LIFE_DEDUCTION_31.Value : CommonAgentStateEnum.LIFE_DEDUCTION_21.Value
		};
		foreach (AgentStateDef item2 in array)
		{
			list.Add((item2, null, null, 3));
		}
		List<StateRecord> list2 = new List<StateRecord>();
		List<StateRecord> list3 = new List<StateRecord>();
		List<StateRecord> list4 = new List<StateRecord>();
		List<StateRecord> list5 = new List<StateRecord>();
		foreach (var (num3, stateRecord) in RunAgentStateChecksInParallel(screen, screenshotTime, list))
		{
			if (stateRecord != null)
			{
				switch (num3)
				{
				case 0:
					list2.Add(stateRecord);
					break;
				case 1:
					list3.Add(stateRecord);
					break;
				case 2:
					list4.Add(stateRecord);
					break;
				default:
					list5.Add(stateRecord);
					break;
				}
			}
		}
		return (Energy: list2, Special: list3, Ultimate: list4, Other: list5);
	}

	private IReadOnlyList<(int Group, StateRecord? Record)> RunAgentStateChecksInParallel(Mat screen, double screenshotTime, IReadOnlyList<(AgentStateDef State, int? Total, int? Position, int Group)> checks)
	{
		Task<(int, StateRecord)>[] array = checks.Select<(AgentStateDef, int?, int?, int), Task<(int, StateRecord)>>(((AgentStateDef State, int? Total, int? Position, int Group) check) => AgentCheckExecutor.StartNew(() => (Group: check.Group, CheckAgentRelatedState(screen, check.State, screenshotTime, check.Total, check.Position)), _shutdownCts.Token)).ToArray();
		List<(int, StateRecord)> list = new List<(int, StateRecord)>();
		Task<(int, StateRecord)>[] array2 = array;
		foreach (Task<(int, StateRecord)> task in array2)
		{
			try
			{
				list.Add(task.GetAwaiter().GetResult());
			}
			catch (Exception exception)
			{
				Log.Error(exception, "识别角色状态失败");
			}
		}
		return list;
	}

	public void AfterAppShutdown()
	{
		_shutdownCts.Cancel();
	}

	private void LogTeamState(double screenshotTime)
	{
		IReadOnlyList<AgentInfo> source = Team.Snapshot();
		string text = string.Join("; ", source.Select((AgentInfo agent, int index) => $"{index + 1}:{agent.Agent?.AgentName ?? "未知"},能量={agent.Energy},特殊={agent.SpecialReady},终结={agent.UltimateReady}"));
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		if (!string.Equals(text, _lastLoggedTeamState, StringComparison.Ordinal) || num - Interlocked.Read(in _lastTeamStateLogAtMilliseconds) >= 1000)
		{
			_lastLoggedTeamState = text;
			Interlocked.Exchange(ref _lastTeamStateLogAtMilliseconds, num);
			_ctx.Logger.Information("自动战斗角色状态: ScreenshotTime={ScreenshotTime:F3}, Team={Team}", screenshotTime, text);
		}
	}
}
