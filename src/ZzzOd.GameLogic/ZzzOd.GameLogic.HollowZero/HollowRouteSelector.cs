using System;
using System.Linq;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public sealed class HollowRouteSelector : IHollowRouteSelector
{
	public HollowZeroMapNode? SelectNextNode(HollowZeroMap map)
	{
		ArgumentNullException.ThrowIfNull(map, "map");
		HollowPathfinding.SearchMap(map, null, null);
		HollowZeroMapNode hollowZeroMapNode = (from node in map.Nodes
			where node.PathStepCnt == 1 && !node.GtMaxVisitedTimes
			orderby node.PathNodeCnt
			select node).FirstOrDefault();
		if (hollowZeroMapNode != null)
		{
			return hollowZeroMapNode.NextNodeToMove ?? hollowZeroMapNode;
		}
		HollowZeroMapNode hollowZeroMapNode2 = (from node in map.Nodes
			where node.PathStepCnt > 0 && !node.GtMaxVisitedTimes
			orderby (!node.Entry.IsBenefit) ? 1 : 0, node.PathStepCnt, node.PathNodeCnt
			select node).FirstOrDefault();
		return hollowZeroMapNode2?.NextNodeToMove ?? hollowZeroMapNode2;
	}
}
