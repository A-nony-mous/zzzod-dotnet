using System;

namespace ZzzOd.GameLogic.HollowZero.HollowMap;

public static class HollowMapUtils
{
	public static bool IsSameNodePos(HollowZeroMapNode? x, HollowZeroMapNode? y)
	{
		if (x == null || y == null)
		{
			return false;
		}
		return Math.Abs(x.Pos.Center.X - y.Pos.Center.X) < 10 && Math.Abs(x.Pos.Center.Y - y.Pos.Center.Y) < 10;
	}

	public static bool IsSameNode(HollowZeroMapNode? x, HollowZeroMapNode? y)
	{
		if (x == null || y == null)
		{
			return false;
		}
		return x.Entry.EntryId == y.Entry.EntryId && IsSameNodePos(x, y);
	}
}
