using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Matcher;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public class WitheredDomainContext
{
	private readonly ZContext _ctx;

	private readonly List<HollowZeroMap> _mapResults = new List<HollowZeroMap>();

	private readonly List<HollowZeroMapNode> _visitedNodes = new List<HollowZeroMapNode>();

	private WitheredDomainChallengeConfigStore? _challengeConfigStore;

	public HollowLevelInfo LevelInfo { get; }

	public string ChallengeConfigName { get; private set; } = string.Empty;

	public WitheredDomainChallengeConfig? ChallengeConfig { get; private set; }

	public IReadOnlyList<HollowZeroMap> MapResults => _mapResults;

	public IReadOnlyList<HollowZeroMapNode> VisitedNodes => _visitedNodes;

	public HollowZeroMapNode? LastTargetNode { get; private set; }

	public HollowZeroMapNode? LastCurrentNode { get; private set; }

	public bool SpeedUpClicked { get; set; }

	public int InvalidMapTimes { get; set; }

	public bool AfterAppShutdownCalled { get; private set; }

	public IReadOnlyList<Agent?>? AgentList { get; private set; }

	public WitheredDomainContext(ZContext ctx)
	{
		_ctx = ctx;
		LevelInfo = new HollowLevelInfo();
		SpeedUpClicked = false;
		InvalidMapTimes = 0;
	}

	public void InitBeforeRun(string? challengeConfigName = null)
	{
		if (string.IsNullOrWhiteSpace(challengeConfigName))
		{
			throw new InvalidOperationException("枯萎之都未选择挑战配置。");
		}
		ChallengeConfigName = challengeConfigName.Trim();
		_challengeConfigStore = new WitheredDomainChallengeConfigStore(_ctx.Environment);
		ChallengeConfig = _challengeConfigStore.LoadSelected(ChallengeConfigName);
		LastTargetNode = null;
		LastCurrentNode = null;
		SpeedUpClicked = false;
		InvalidMapTimes = 0;
		_mapResults.Clear();
		_visitedNodes.Clear();
		AgentList = null;
	}

	public void InitBeforeHollowStart(string missionTypeName, string missionName, int level = 1, int phase = 1)
	{
		InitLevelInfo(missionTypeName, missionName, level, phase);
		LastTargetNode = null;
		LastCurrentNode = null;
		SpeedUpClicked = false;
		InvalidMapTimes = 0;
		_mapResults.Clear();
		_visitedNodes.Clear();
		AgentList = null;
	}

	public void InitLevelInfo(string missionTypeName, string missionName, int level = 1, int phase = 1)
	{
		LevelInfo.MissionTypeName = missionTypeName;
		LevelInfo.MissionName = missionName;
		LevelInfo.Level = level;
		LevelInfo.Phase = phase;
	}

	public HollowZeroMapNode? GetNextToMove(HollowZeroMap currentMap)
	{
		if (!currentMap.IsValidMap)
		{
			InvalidMapTimes++;
			if (InvalidMapTimes >= 5)
			{
				HollowZeroMapNode hollowZeroMapNode = currentMap.Nodes.FirstOrDefault((HollowZeroMapNode node) => node.Entry.EntryName == "空白已通行");
				if (hollowZeroMapNode != null)
				{
					hollowZeroMapNode.PathLastNode = hollowZeroMapNode;
					hollowZeroMapNode.PathFirstNode = hollowZeroMapNode;
					hollowZeroMapNode.PathFirstNeedStepNode = hollowZeroMapNode;
					hollowZeroMapNode.PathStepCnt = 999;
					hollowZeroMapNode.PathNodeCnt = 1;
					InvalidMapTimes = 0;
					return hollowZeroMapNode;
				}
			}
			return null;
		}
		InvalidMapTimes = 0;
		HollowPathfinding.SearchMap(currentMap, new HashSet<string>(GetAvoid(), StringComparer.Ordinal), _visitedNodes);
		HollowZeroMapNode hollowZeroMapNode2 = TryTargetNode(HollowPathfinding.GetRouteIn1Step(currentMap, _visitedNodes, GetGoInOneStep().ToList()));
		if (hollowZeroMapNode2 != null)
		{
			return hollowZeroMapNode2;
		}
		foreach (string item in GetWaypoint())
		{
			hollowZeroMapNode2 = TryTargetNode(HollowPathfinding.GetRouteByEntry(currentMap, item, _visitedNodes));
			if (hollowZeroMapNode2 != null)
			{
				return hollowZeroMapNode2;
			}
		}
		string[] array = new string[3] { "守门人", "传送点", "不宜久留" };
		foreach (string entryName in array)
		{
			hollowZeroMapNode2 = TryTargetNode(HollowPathfinding.GetRouteByEntry(currentMap, entryName, _visitedNodes));
			if (hollowZeroMapNode2 != null)
			{
				return hollowZeroMapNode2;
			}
			hollowZeroMapNode2 = TryTargetNode(HollowPathfinding.GetRouteByEntry(currentMap, entryName, new List<HollowZeroMapNode>()));
			if (hollowZeroMapNode2 != null)
			{
				hollowZeroMapNode2.PathGoWay = 0;
				return hollowZeroMapNode2;
			}
		}
		hollowZeroMapNode2 = TryTargetNode(HollowPathfinding.GetRouteIn1Step(currentMap, _visitedNodes, _challengeConfigStore.GetNoBattle().ToList()));
		if (hollowZeroMapNode2 != null)
		{
			return hollowZeroMapNode2;
		}
		hollowZeroMapNode2 = TryTargetNode(HollowPathfinding.GetRouteByDirection(currentMap, ResolveFallbackDirection()));
		if (hollowZeroMapNode2 != null)
		{
			return hollowZeroMapNode2;
		}
		return TryTargetNode(HollowPathfinding.GetRouteIn1Step(currentMap, _visitedNodes));
	}

	public HollowZeroMapNode? TryTargetNode(HollowZeroMapNode? target)
	{
		if (target == null)
		{
			return null;
		}
		if (HollowMapUtils.IsSameNode(LastTargetNode?.NextNodeToMove, target.NextNodeToMove))
		{
			target.PathGoWay = 0;
		}
		LastTargetNode = target;
		return target;
	}

	public void UpdateContextAfterMove(HollowZeroMap currentMap, HollowZeroMapNode node, bool updateCurrent = true)
	{
		HollowZeroMapNode hollowZeroMapNode = _visitedNodes.FirstOrDefault((HollowZeroMapNode item) => HollowMapUtils.IsSameNode(item, node));
		if (hollowZeroMapNode == null)
		{
			hollowZeroMapNode = new HollowZeroMapNode(node.Pos, node.Entry)
			{
				VisitedTimes = 1
			};
			_visitedNodes.Add(hollowZeroMapNode);
		}
		else
		{
			hollowZeroMapNode.VisitedTimes++;
		}
		if (updateCurrent)
		{
			UpdateMapCurrentNode(currentMap, node);
		}
		if (hollowZeroMapNode.Entry.IsTp)
		{
			_visitedNodes.Clear();
			if (hollowZeroMapNode.Entry.EntryName == "传送点")
			{
				LevelInfo.ToNextPhase();
				_mapResults.Clear();
			}
		}
	}

	public void UpdateMapCurrentNode(HollowZeroMap currentMap, HollowZeroMapNode node)
	{
		int num = currentMap.Nodes.FindIndex((HollowZeroMapNode item) => HollowMapUtils.IsSameNode(item, node));
		if (num < 0)
		{
			return;
		}
		int? currentIdx = currentMap.CurrentIdx;
		if (currentIdx.HasValue)
		{
			int valueOrDefault = currentIdx.GetValueOrDefault();
			if (valueOrDefault >= 0 && valueOrDefault < currentMap.Nodes.Count)
			{
				currentMap.Nodes[valueOrDefault].Entry = new HollowZeroEntry("0002-空白已通行");
				currentMap.Nodes[valueOrDefault].Confidence = 0.6f;
			}
		}
		HollowZeroMapNode hollowZeroMapNode = currentMap.Nodes[num];
		hollowZeroMapNode.Entry = new HollowZeroEntry("0000-当前");
		hollowZeroMapNode.Confidence = 0.6f;
		currentMap.CurrentIdx = num;
		LastCurrentNode = hollowZeroMapNode;
	}

	public void UpdateToNextLevel()
	{
		_visitedNodes.Clear();
		LevelInfo.ToNextLevel();
		LastTargetNode = null;
		LastCurrentNode = null;
		_mapResults.Clear();
	}

	public bool HadBeenEntry(string entryName)
	{
		return _visitedNodes.Any((HollowZeroMapNode node) => node.Entry.EntryName == entryName && node.GtMaxVisitedTimes);
	}

	public string GetAutoBattleName()
	{
		return GetChallengeConfig().AutoBattle;
	}

	public IReadOnlyList<string?> GetTargetAgents()
	{
		return GetChallengeConfig().TargetAgents.ToArray();
	}

	public IReadOnlyList<Agent?>? CheckAgentList(Mat screen, bool skipIfChecked = false)
	{
		if (skipIfChecked && AgentList != null)
		{
			return AgentList;
		}
		IReadOnlyList<Agent> readOnlyList = TryMatchAgentList(screen, AgentList);
		if (readOnlyList == null)
		{
			readOnlyList = TryMatchAgentList(screen, null);
		}
		if (readOnlyList != null)
		{
			AgentList = readOnlyList;
		}
		return AgentList;
	}

	public void UpdateAgentListAfterSupport(Agent newAgent, int position)
	{
		bool flag = AgentList == null;
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((position < 1 || position > 3) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			return;
		}
		Agent[] array = AgentList.ToArray();
		int num = position - 1;
		if (array[num] == null)
		{
			array[num] = newAgent;
		}
		else
		{
			int num2 = Array.FindIndex(array, (Agent agent) => agent == null);
			if (num2 >= 0)
			{
				array[num2] = array[num];
			}
			array[num] = newAgent;
		}
		AgentList = array;
	}

	public IReadOnlyList<string> GetGoInOneStep()
	{
		WitheredDomainChallengeConfig challengeConfig = GetChallengeConfig();
		string pathFinding = challengeConfig.PathFinding;
		if (1 == 0)
		{
		}
		IReadOnlyList<string> result = pathFinding switch
		{
			"默认" => _challengeConfigStore.GetDefaultGoInOneStep(), 
			"速通" => Array.Empty<string>(), 
			"自定义" => challengeConfig.GoInOneStep.ToArray(), 
			_ => Array.Empty<string>(), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public IReadOnlyList<string> GetWaypoint()
	{
		WitheredDomainChallengeConfig challengeConfig = GetChallengeConfig();
		string pathFinding = challengeConfig.PathFinding;
		if (1 == 0)
		{
		}
		IReadOnlyList<string> result = pathFinding switch
		{
			"默认" => _challengeConfigStore.GetDefaultWaypoint(), 
			"速通" => _challengeConfigStore.GetOnlyBossWaypoint(), 
			"自定义" => challengeConfig.Waypoint.ToArray(), 
			_ => Array.Empty<string>(), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public IReadOnlyList<string> GetAvoid()
	{
		WitheredDomainChallengeConfig challengeConfig = GetChallengeConfig();
		string pathFinding = challengeConfig.PathFinding;
		if (1 == 0)
		{
		}
		IReadOnlyList<string> result;
		switch (pathFinding)
		{
		case "默认":
		case "速通":
			result = _challengeConfigStore.GetDefaultAvoid();
			break;
		case "自定义":
			result = challengeConfig.Avoid.ToArray();
			break;
		default:
			result = Array.Empty<string>();
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	public void AfterAppShutdown()
	{
		AfterAppShutdownCalled = true;
	}

	private string ResolveFallbackDirection()
	{
		if (LevelInfo.Level >= 2 && LevelInfo.Phase == 1)
		{
			return "w";
		}
		string missionTypeName = LevelInfo.MissionTypeName;
		bool flag = ((missionTypeName == "施工废墟" || missionTypeName == "巨厦遗骸") ? true : false);
		return flag ? "d" : "w";
	}

	private IReadOnlyList<Agent?>? TryMatchAgentList(Mat screen, IReadOnlyList<Agent?>? possibleAgents)
	{
		List<Agent> list = new List<Agent>();
		bool flag = false;
		HashSet<string> hashSet = (from agent in possibleAgents?.Where((Agent agent) => agent != null)
			select agent.AgentId).ToHashSet<string>(StringComparer.Ordinal);
		for (int num = 1; num <= 3; num++)
		{
			OneDragon.Core.Screen.ScreenArea area = _ctx.ScreenContext.GetArea("零号空洞-事件", $"角色-{num}");
			if (area == null)
			{
				return null;
			}
			Mat image = CvImageUtils.Crop(screen, area.Rect);
			TemplateMatchVisionContext visionContext = TemplateMatchVisionContext.ForCrop(screen.Width, screen.Height, area.X1, area.Y1);
			try
			{
				Agent item = null;
				foreach (Agent item2 in AgentEnum.Values.Select((AgentEnum agentEnum) => agentEnum.Value))
				{
					if ((hashSet == null || hashSet.Contains(item2.AgentId)) && item2.TemplateIdList.Any((string templateId) => _ctx.TemplateMatcher.MatchOneByFeature(
						image,
						"hollow",
						"avatar_" + templateId,
						null,
						0.8,
						visionContext: visionContext) != null))
					{
						item = item2;
						flag = true;
						break;
					}
				}
				list.Add(item);
			}
			finally
			{
				if (image != null)
				{
					((IDisposable)image).Dispose();
				}
			}
		}
		return flag ? list : null;
	}

	private WitheredDomainChallengeConfig GetChallengeConfig()
	{
		return ChallengeConfig ?? throw new InvalidOperationException("枯萎之都挑战配置尚未加载。");
	}
}
