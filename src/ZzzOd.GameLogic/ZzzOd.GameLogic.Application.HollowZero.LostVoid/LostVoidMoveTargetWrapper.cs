using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Yolo;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidMoveTargetWrapper
{
	public bool IsMixed { get; private set; }

	public List<string> TargetNames { get; } = new List<string>();

	public List<Rect> TargetRects { get; } = new List<Rect>();

	public string LeftestTargetName { get; private set; }

	public Rect EntireRect { get; private set; }

	public LostVoidMoveTargetWrapper? MergeParent { get; private set; }

	public LostVoidMoveTargetWrapper(YoloDetectObjectResult detectResult)
	{
		string className = detectResult.DetectClass.ClassName;
		string text = ((className.Length > 5) ? className.Substring(5) : className);
		TargetNames.Add(text);
		TargetRects.Add(new Rect(detectResult.X1, detectResult.Y1, detectResult.X2, detectResult.Y2));
		LeftestTargetName = text;
		EntireRect = TargetRects[0];
	}

	public bool MergeAnotherTarget(LostVoidMoveTargetWrapper other)
	{
		LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper = MergeParent ?? this;
		LostVoidMoveTargetWrapper target = other.MergeParent ?? other;
		if (lostVoidMoveTargetWrapper == target)
		{
			return false;
		}
		if (!lostVoidMoveTargetWrapper.TargetRects.Any((Rect left) => target.TargetRects.Any((Rect right) => Distance(left.Center, right.Center) < (double)left.Width * 2.0)))
		{
			return false;
		}
		lostVoidMoveTargetWrapper.IsMixed = true;
		target.IsMixed = true;
		target.MergeParent = lostVoidMoveTargetWrapper;
		lostVoidMoveTargetWrapper.TargetNames.AddRange(target.TargetNames);
		lostVoidMoveTargetWrapper.TargetRects.AddRange(target.TargetRects);
		lostVoidMoveTargetWrapper.RecalculateBounds();
		return true;
	}

	public LostVoidMoveTarget ToMoveTarget()
	{
		return new LostVoidMoveTarget(TargetNames, EntireRect);
	}

	private void RecalculateBounds()
	{
		int x = TargetRects.Min((Rect rect) => rect.X1);
		int y = TargetRects.Min((Rect rect) => rect.Y1);
		int x2 = TargetRects.Max((Rect rect) => rect.X2);
		int y2 = TargetRects.Max((Rect rect) => rect.Y2);
		EntireRect = new Rect(x, y, x2, y2);
		int item = TargetRects.Select((Rect rect, int index) => (rect: rect, index: index)).MinBy(((Rect rect, int index) tuple) => tuple.rect.X1).index;
		LeftestTargetName = TargetNames[item];
	}

	private static double Distance(Point left, Point right)
	{
		int num = left.X - right.X;
		int num2 = left.Y - right.Y;
		return Math.Sqrt(num * num + num2 * num2);
	}
}
