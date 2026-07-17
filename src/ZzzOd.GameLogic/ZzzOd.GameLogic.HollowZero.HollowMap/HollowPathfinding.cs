using System.Collections.Generic;
using ZzzOd.GameLogic.HollowZero.GameData;

namespace ZzzOd.GameLogic.HollowZero.HollowMap;

public static class HollowPathfinding
{
	public static void SearchMap(HollowZeroMap currentMap, HashSet<string>? avoidEntryList, List<HollowZeroMapNode>? visitedNodes)
	{
		if (currentMap == null)
		{
			return;
		}
		currentMap.InitPathRelated();
		if (!currentMap.IsValidMap || !currentMap.CurrentIdx.HasValue)
		{
			return;
		}
		BfsSearchMap(currentMap, new List<int> { currentMap.CurrentIdx.Value }, avoidEntryList, visitedNodes);
		List<int> list = new List<int>();
		for (int i = 0; i < currentMap.Nodes.Count; i++)
		{
			if (currentMap.Nodes[i].PathStepCnt >= 0)
			{
				list.Add(i);
			}
		}
		BfsSearchMap(currentMap, list, null, visitedNodes);
	}

	private static void BfsSearchMap(HollowZeroMap currentMap, List<int> startIdxList, HashSet<string>? avoidEntryList = null, List<HollowZeroMapNode>? visitedNodes = null)
	{
		List<int> list = new List<int>(startIdxList);
		HashSet<int> hashSet = new HashSet<int>(startIdxList);
		while (list.Count > 0)
		{
			List<int> list2 = new List<int>();
			int num = 0;
			while (num < list.Count)
			{
				int num2 = list[num];
				HollowZeroMapNode hollowZeroMapNode = currentMap.Nodes[num2];
				hashSet.Add(num2);
				num++;
				if (!currentMap.Edges.TryGetValue(num2, out List<int> value))
				{
					continue;
				}
				foreach (int item in value)
				{
					if (hashSet.Contains(item))
					{
						continue;
					}
					HollowZeroMapNode hollowZeroMapNode2 = currentMap.Nodes[item];
					HollowZeroEntry entry = hollowZeroMapNode2.Entry;
					if (!entry.CanGo || (avoidEntryList != null && avoidEntryList.Contains(entry.EntryName)))
					{
						continue;
					}
					int needStep = entry.NeedStep;
					int num3 = hollowZeroMapNode.PathStepCnt + needStep;
					HollowZeroMapNode pathFirstNeedStepNode = ((num3 > 1 || entry.NeedStep <= 0) ? hollowZeroMapNode.PathFirstNeedStepNode : hollowZeroMapNode2);
					hollowZeroMapNode2.PathFirstNode = hollowZeroMapNode.PathFirstNode ?? hollowZeroMapNode2;
					hollowZeroMapNode2.PathFirstNeedStepNode = pathFirstNeedStepNode;
					hollowZeroMapNode2.PathLastNode = hollowZeroMapNode;
					hollowZeroMapNode2.PathStepCnt = num3;
					hollowZeroMapNode2.PathNodeCnt = hollowZeroMapNode.PathNodeCnt + 1;
					if (num3 == hollowZeroMapNode.PathStepCnt)
					{
						if (!list.Contains(item))
						{
							if (list2.Contains(item))
							{
								list2.Remove(item);
								list.Add(item);
							}
							else
							{
								list.Add(item);
							}
						}
					}
					else if (!list2.Contains(item))
					{
						list2.Add(item);
					}
				}
			}
			list = list2;
		}
	}

	public static HollowZeroMapNode? GetRouteIn1Step(HollowZeroMap currentMap, List<HollowZeroMapNode> visitedNodes, List<string>? targetEntryList = null)
	{
		HollowZeroMapNode hollowZeroMapNode = null;
		foreach (HollowZeroMapNode node in currentMap.Nodes)
		{
			if (node.PathStepCnt == 1 && (targetEntryList == null || targetEntryList.Contains(node.Entry.EntryName)) && !HadBeenVisited(node, visitedNodes) && (hollowZeroMapNode == null || hollowZeroMapNode.PathNodeCnt > node.PathNodeCnt))
			{
				hollowZeroMapNode = node;
			}
		}
		return hollowZeroMapNode;
	}

	public static HollowZeroMapNode? GetRouteByEntry(HollowZeroMap currentMap, string entryName, List<HollowZeroMapNode> visitedNodes)
	{
		HollowZeroMapNode hollowZeroMapNode = null;
		foreach (HollowZeroMapNode node in currentMap.Nodes)
		{
			if (node.PathStepCnt != -1 && node.Entry != null && !(node.Entry.EntryName != entryName) && !HadBeenVisited(node, visitedNodes) && (hollowZeroMapNode == null || hollowZeroMapNode.PathStepCnt > node.PathStepCnt || (hollowZeroMapNode.PathStepCnt == node.PathStepCnt && hollowZeroMapNode.PathNodeCnt > node.PathNodeCnt)))
			{
				hollowZeroMapNode = node;
			}
		}
		return hollowZeroMapNode;
	}

	public static HollowZeroMapNode? GetRouteByDirection(HollowZeroMap currentMap, string direction)
	{
		HollowZeroMapNode hollowZeroMapNode = null;
		foreach (HollowZeroMapNode node in currentMap.Nodes)
		{
			if (node.PathStepCnt == -1)
			{
				continue;
			}
			string entryName = node.Entry.EntryName;
			if (!(entryName == "当前") && !(entryName == "空白已通行") && !(entryName == "空白未通行"))
			{
				if (hollowZeroMapNode == null)
				{
					hollowZeroMapNode = node;
				}
				else if (direction == "w" && hollowZeroMapNode.Pos.Y1 > node.Pos.Y1)
				{
					hollowZeroMapNode = node;
				}
				else if (direction == "s" && hollowZeroMapNode.Pos.Y2 < node.Pos.Y2)
				{
					hollowZeroMapNode = node;
				}
				else if (direction == "a" && hollowZeroMapNode.Pos.X1 > node.Pos.X1)
				{
					hollowZeroMapNode = node;
				}
				else if (direction == "d" && hollowZeroMapNode.Pos.X2 < node.Pos.X2)
				{
					hollowZeroMapNode = node;
				}
			}
		}
		return hollowZeroMapNode;
	}

	public static bool HadBeenVisited(HollowZeroMapNode current, List<HollowZeroMapNode>? visitedNodes)
	{
		if (visitedNodes == null)
		{
			return false;
		}
		foreach (HollowZeroMapNode visitedNode in visitedNodes)
		{
			if (visitedNode.GtMaxVisitedTimes && HollowMapUtils.IsSameNode(current, visitedNode))
			{
				return true;
			}
		}
		return false;
	}
}
