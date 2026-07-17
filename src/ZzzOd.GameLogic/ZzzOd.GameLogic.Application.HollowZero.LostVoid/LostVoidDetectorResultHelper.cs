using System;
using System.Linq;
using OneDragon.Core.Yolo;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

internal static class LostVoidDetectorResultHelper
{
	public static string DescribeDetectedClasses(YoloDetectFrameResult? frameResult)
	{
		return (frameResult == null || frameResult.Results.Count == 0) ? "无" : string.Join(", ", frameResult.Results.Select((YoloDetectObjectResult result) => result.DetectClass.ClassName));
	}

	public static (bool WithInteract, bool WithDistance, bool WithEntry) IsFrameWithAll(YoloDetectFrameResult? frameResult)
	{
		if (frameResult == null)
		{
			return (WithInteract: false, WithDistance: false, WithEntry: false);
		}
		bool item = false;
		bool item2 = false;
		bool item3 = false;
		foreach (YoloDetectObjectResult result in frameResult.Results)
		{
			if (string.Equals(result.DetectClass.ClassName, "0000-感叹号", StringComparison.Ordinal))
			{
				item = true;
			}
			else if (string.Equals(result.DetectClass.ClassName, "0001-距离", StringComparison.Ordinal))
			{
				item2 = true;
			}
			else
			{
				item3 = true;
			}
		}
		return (WithInteract: item, WithDistance: item2, WithEntry: item3);
	}
}
