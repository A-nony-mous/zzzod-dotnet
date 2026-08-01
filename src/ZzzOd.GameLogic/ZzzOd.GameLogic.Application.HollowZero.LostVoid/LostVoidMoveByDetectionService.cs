using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Screen;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidMoveByDetectionService
{
	public static LostVoidMoveByDetectionService Instance { get; } = new LostVoidMoveByDetectionService();

	private LostVoidMoveByDetectionService()
	{
	}

	public bool IsInNormalWorld(ZContext context, Mat screen)
	{
		return FindArea(context, screen, "战斗画面", "按键-普通攻击") || FindArea(context, screen, "战斗画面", "按键-交互") || FindArea(context, screen, "迷失之地-大世界", "按键-交互-不可用");
	}

	public bool IsChoosingRewardScreen(ZContext context, Mat screen)
	{
		return ScreenUtils.GetMatchScreenName(context, screen, new string[2] { "迷失之地-武备选择", "迷失之地-通用选择" }) != null;
	}

	public IReadOnlyList<string>? BuildLabelList(LostVoidDetector? detector, IReadOnlyList<string> ignoreList)
	{
		return detector == null
			? null
			: BuildLabelList(detector.CoreDetector.Classes.Values, ignoreList);
	}

	internal static IReadOnlyList<string>? BuildLabelList(IEnumerable<YoloDetectClass> classes, IReadOnlyList<string> ignoreList)
	{
		YoloDetectClass[] loadedClasses = classes.ToArray();
		if (ignoreList.Count == 0 || loadedClasses.Length == 0)
		{
			return null;
		}
		return (from item in loadedClasses
			select item.ClassName into label
			where label.Length <= 5 || !ignoreList.Contains<string>(label.Substring(5), StringComparer.Ordinal)
			select label).ToArray();
	}

	public LostVoidMoveTargetWrapper? GetMoveTarget(LostVoidContext context, string currentRegion, string targetType, YoloDetectFrameResult frameResult, LostVoidMoveTargetWrapper? lastTarget = null, IReadOnlyList<string>? ignoreEntryList = null)
	{
		bool flag = string.Equals(currentRegion, "入口", StringComparison.Ordinal);
		if (!string.Equals(targetType, "xxxx-入口", StringComparison.Ordinal))
		{
			YoloDetectObjectResult yoloDetectObjectResult = (flag ? frameResult.Results.Where((YoloDetectObjectResult item) => item.DetectClass.ClassName == targetType).MaxBy((YoloDetectObjectResult item) => item.Center.X) : frameResult.Results.Where((YoloDetectObjectResult item) => item.DetectClass.ClassName == targetType).MinBy((YoloDetectObjectResult item) => item.Center.X));
			return (yoloDetectObjectResult == null) ? null : new LostVoidMoveTargetWrapper(yoloDetectObjectResult);
		}
		return GetEntryTarget(context, frameResult, lastTarget, ignoreEntryList ?? Array.Empty<string>());
	}

	public LostVoidMoveTargetWrapper? GetEntryTarget(LostVoidContext context, YoloDetectFrameResult frameResult, LostVoidMoveTargetWrapper? lastTarget = null, IReadOnlyList<string>? ignoreEntryList = null)
	{
		List<LostVoidMoveTargetWrapper> list = (from result in frameResult.Results.Where(delegate(YoloDetectObjectResult result)
			{
				string className = result.DetectClass.ClassName;
				bool flag = ((className == "0000-感叹号" || className == "0001-距离") ? true : false);
				return !flag;
			})
			select new LostVoidMoveTargetWrapper(result)).ToList();
		foreach (LostVoidMoveTargetWrapper item in list)
		{
			foreach (LostVoidMoveTargetWrapper item2 in list)
			{
				item.MergeAnotherTarget(item2);
			}
		}
		list = list.Where((LostVoidMoveTargetWrapper item) => item.MergeParent == null).ToList();
		if (context.HadInteractedOpheliaOnCurrentLevel)
		{
			list = list.Where((LostVoidMoveTargetWrapper item) => !item.TargetNames.Contains<string>("战斗-道中危机", StringComparer.Ordinal)).ToList();
		}
		if (lastTarget != null)
		{
			LostVoidMoveTargetWrapper sameAsLastTarget = GetSameAsLastTarget(list, lastTarget);
			if (sameAsLastTarget != null)
			{
				return sameAsLastTarget;
			}
		}
		IReadOnlyList<string> ignoreEntryList2 = ignoreEntryList ?? Array.Empty<string>();
		LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper = SelectEntryByPriority(context, list.Where((LostVoidMoveTargetWrapper item) => !item.IsMixed).ToList(), ignoreEntryList2);
		return lostVoidMoveTargetWrapper ?? SelectEntryByPriority(context, list.Where((LostVoidMoveTargetWrapper item) => item.IsMixed).ToList(), ignoreEntryList2);
	}

	public LostVoidMoveTargetWrapper? SelectEntryByPriority(LostVoidContext context, IReadOnlyList<LostVoidMoveTargetWrapper> entryList, IReadOnlyList<string> ignoreEntryList)
	{
		if (entryList.Count == 0)
		{
			return null;
		}
		HashSet<string> ignored = new HashSet<string>(ignoreEntryList, StringComparer.Ordinal);
		if (context.HadInteractedOpheliaOnCurrentLevel)
		{
			ignored.Add("战斗-道中危机");
		}
		LostVoidChallengeConfig? challengeConfig = context.ChallengeConfig;
		IReadOnlyList<string> readOnlyList;
		if (challengeConfig == null || challengeConfig.RegionTypePriority.Count <= 0)
		{
			readOnlyList = LostVoidRegionType.All;
		}
		else
		{
			IReadOnlyList<string> regionTypePriority = context.ChallengeConfig.RegionTypePriority;
			readOnlyList = regionTypePriority;
		}
		IReadOnlyList<string> readOnlyList2 = readOnlyList;
		foreach (string priority in readOnlyList2)
		{
			if (ignored.Contains(priority))
			{
				continue;
			}
			LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper = entryList.Where((LostVoidMoveTargetWrapper entry) => entry.TargetNames.Any((string name) => !ignored.Contains(name) && string.Equals(name, priority, StringComparison.Ordinal))).MaxBy((LostVoidMoveTargetWrapper entry) => entry.EntireRect.X1);
			if (lostVoidMoveTargetWrapper == null)
			{
				continue;
			}
			return lostVoidMoveTargetWrapper;
		}
		return entryList.Where((LostVoidMoveTargetWrapper entry) => entry.TargetNames.All((string name) => !ignored.Contains(name))).MaxBy((LostVoidMoveTargetWrapper entry) => entry.EntireRect.X1);
	}

	public bool ShouldStopForInteraction(YoloDetectFrameResult frameResult, bool stopWhenInteract, bool interactButtonAvailable, bool allowArrivalByInteractButton = false)
	{
		if (!stopWhenInteract || !interactButtonAvailable)
		{
			return false;
		}
		if (allowArrivalByInteractButton)
		{
			return true;
		}
		foreach (YoloDetectObjectResult result in frameResult.Results)
		{
			if (!string.Equals(result.DetectClass.ClassName, "0001-距离", StringComparison.Ordinal))
			{
				int num = (string.Equals(result.DetectClass.ClassName, "0000-感叹号", StringComparison.Ordinal) ? 70 : 50);
				if (result.Width > num && result.Height > num)
				{
					return true;
				}
			}
		}
		return false;
	}

	public int CalculateTurnDistance(OneDragon.Core.Abstractions.Geometry.Point target, int standardWidth, bool isMoving)
	{
		int num = target.X - standardWidth / 2;
		int num2 = 50;
		if (Math.Abs(num) <= num2)
		{
			return 0;
		}
		int num3 = 5;
		int num4 = (isMoving ? 15 : 200);
		int value = (int)((double)num * 0.2);
		if (Math.Abs(value) < num3)
		{
			value = ((num > 0) ? num3 : (-num3));
		}
		return Math.Clamp(value, -num4, num4);
	}

	private static LostVoidMoveTargetWrapper? GetSameAsLastTarget(IReadOnlyList<LostVoidMoveTargetWrapper> entryList, LostVoidMoveTargetWrapper lastTarget)
	{
		return entryList.Where((LostVoidMoveTargetWrapper entry) => entry.TargetNames.Count == lastTarget.TargetNames.Count && string.Equals(entry.LeftestTargetName, lastTarget.LeftestTargetName, StringComparison.Ordinal)).MinBy((LostVoidMoveTargetWrapper entry) => Math.Abs(Distance(entry.EntireRect.Center, lastTarget.EntireRect.Center)));
	}

	private static bool FindArea(ZContext context, Mat screen, string screenName, string areaName)
	{
		return ScreenUtils.FindArea(context, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	private static double Distance(OneDragon.Core.Abstractions.Geometry.Point left, OneDragon.Core.Abstractions.Geometry.Point right)
	{
		int num = left.X - right.X;
		int num2 = left.Y - right.Y;
		return Math.Sqrt(num * num + num2 * num2);
	}
}
