using System;
using OneDragon.Core.Utils;

namespace ZzzOd.GameLogic.HollowZero.HollowMap;

public static class HollowMapUtils
{
	/// <summary>
	/// 判断两个节点的坐标是否一致。
	/// 阈值按两节点宽高的最小值动态计算，而非固定像素差，避免不同分辨率或格子尺寸下判定失真。
	/// </summary>
	public static bool IsSameNodePos(HollowZeroMapNode? x, HollowZeroMapNode? y)
	{
		if (x == null || y == null)
		{
			return false;
		}
		int minDis = Math.Min(Math.Min(x.Pos.Height, x.Pos.Width), Math.Min(y.Pos.Height, y.Pos.Width)) / 2;
		return CalUtils.DistanceBetween(x.Pos.Center, y.Pos.Center) < minDis;
	}

	public static bool IsSameNode(HollowZeroMapNode? x, HollowZeroMapNode? y)
	{
		if (x == null || y == null)
		{
			return false;
		}
		return x.Entry.EntryName == y.Entry.EntryName && IsSameNodePos(x, y);
	}

	/// <summary>
	/// 获取某个节点在地图节点列表中的下标；找不到时返回 -1。
	/// </summary>
	public static int GetNodeIndex(HollowZeroMap currentMap, HollowZeroMapNode? node)
	{
		for (int i = 0; i < currentMap.Nodes.Count; i++)
		{
			if (IsSameNode(currentMap.Nodes[i], node))
			{
				return i;
			}
		}
		return -1;
	}
}
