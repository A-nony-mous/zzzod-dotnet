using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.HollowZero.HollowMap;

public class HollowZeroMap
{
	public List<HollowZeroMapNode> Nodes { get; set; }

	public int? CurrentIdx { get; set; }

	public Dictionary<int, List<int>> Edges { get; set; }

	public float CheckTime { get; set; }

	public int NotCurrentMapTimes { get; set; }

	public bool IsValidMap => CurrentIdx.HasValue;

	public HollowZeroMap(List<HollowZeroMapNode> nodes, int? currentIdx, Dictionary<int, List<int>> edges, float? checkTime = null)
	{
		Nodes = nodes;
		CurrentIdx = currentIdx;
		Edges = edges;
		CheckTime = checkTime ?? ((float)DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000f);
		NotCurrentMapTimes = 0;
	}

	public bool ContainsEntry(string entryName)
	{
		foreach (HollowZeroMapNode node in Nodes)
		{
			if (node.Entry.EntryName == entryName)
			{
				return true;
			}
		}
		return false;
	}

	public bool SearchEntry(string entryName)
	{
		foreach (HollowZeroMapNode node in Nodes)
		{
			if (node.Entry.EntryName == entryName)
			{
				return true;
			}
		}
		return false;
	}

	public void InitPathRelated()
	{
		foreach (HollowZeroMapNode node in Nodes)
		{
			node.PathFirstNode = null;
			node.PathFirstNeedStepNode = null;
			node.PathLastNode = null;
			node.PathStepCnt = -1;
			node.PathNodeCnt = -1;
			node.PathGoWay = 1;
		}
		if (IsValidMap && CurrentIdx.HasValue)
		{
			HollowZeroMapNode hollowZeroMapNode = Nodes[CurrentIdx.Value];
			hollowZeroMapNode.PathStepCnt = 0;
			hollowZeroMapNode.PathNodeCnt = 0;
		}
	}
}
