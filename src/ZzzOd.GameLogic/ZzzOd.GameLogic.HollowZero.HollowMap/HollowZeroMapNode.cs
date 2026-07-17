using System;
using OneDragon.Core.Abstractions.Geometry;
using ZzzOd.GameLogic.HollowZero.GameData;

namespace ZzzOd.GameLogic.HollowZero.HollowMap;

public class HollowZeroMapNode
{
	public Rect Pos { get; set; }

	public HollowZeroEntry Entry { get; set; }

	public float CheckTime { get; set; }

	public float Confidence { get; set; }

	public int VisitedTimes { get; set; }

	public HollowZeroMapNode? PathFirstNode { get; set; }

	public HollowZeroMapNode? PathFirstNeedStepNode { get; set; }

	public HollowZeroMapNode? PathLastNode { get; set; }

	public int PathStepCnt { get; set; }

	public int PathNodeCnt { get; set; }

	public int PathGoWay { get; set; }

	public bool GtMaxVisitedTimes => VisitedTimes >= Entry.CanVisitedTimes;

	public HollowZeroMapNode? NextNodeToMove => (PathGoWay == 1) ? PathFirstNeedStepNode : PathFirstNode;

	public HollowZeroMapNode(Rect pos, HollowZeroEntry entry, float? checkTime = null, float confidence = 0f)
	{
		Pos = pos;
		Entry = entry;
		CheckTime = checkTime ?? ((float)DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000f);
		Confidence = confidence;
		VisitedTimes = 0;
		PathFirstNode = null;
		PathFirstNeedStepNode = null;
		PathLastNode = null;
		PathStepCnt = -1;
		PathNodeCnt = -1;
		PathGoWay = 1;
	}
}
