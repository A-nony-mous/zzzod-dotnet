using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidChooseGearOperation : ZOperation
{
	private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1L);

	public LostVoidChooseGearOperation(ZContext context)
		: base(context, "迷失之地-武备选择")
	{
	}

	[OperationNode("选择武备", IsStartNode = true)]
	public OperationRoundResult ChooseGear()
	{
		if (base.LastScreenshot == null || base.ZContext.Controller == null)
		{
			return RoundRetry("未获取截图", null, OneSecond);
		}
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "迷失之地-武备选择" });
		if (!string.Equals(text, "迷失之地-武备选择", StringComparison.Ordinal))
		{
			return RoundRetry("当前画面 " + text, null, OneSecond);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-通用选择", "文本-详情");
		OneDragon.Core.Screen.ScreenArea area2 = base.ZContext.ScreenContext.GetArea("迷失之地-武备选择", "武备名称");
		if (area == null || area2 == null)
		{
			return RoundFail("武备选择区域未配置");
		}
		base.ZContext.Controller.MouseMove(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, 100));
		Thread.Sleep(TimeSpan.FromMilliseconds(100L));
		Thread.Sleep(OneSecond);
		IReadOnlyList<(LostVoidArtifactPos, bool)> readOnlyList = ReadGearCandidates(base.LastScreenshot, area2);
		if (readOnlyList.Count == 0)
		{
			return RoundRetry("无法识别武备槽位或名称", null, OneSecond);
		}
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		if (challengeConfig == null)
		{
			return RoundFail("挑战配置未加载");
		}
		LostVoidArtifactPos lostVoidArtifactPos = null;
		bool flag = false;
		if (challengeConfig.ChaseNewMode)
		{
			IReadOnlyList<LostVoidArtifactPos> readOnlyList2 = (from item in readOnlyList
				where !item.Item2
				select item.Item1).ToArray();
			if (readOnlyList2.Count > 0)
			{
				base.ZContext.Logger.Information("【武备追新】当前可选未获取武备 {Gears}", string.Join(",", readOnlyList2.Select((LostVoidArtifactPos item) => item.Artifact.DisplayName)));
				lostVoidArtifactPos = base.ZContext.LostVoid.GetArtifactByPriority(readOnlyList2, 1).FirstOrDefault() ?? readOnlyList2[0];
				flag = true;
			}
			else
			{
				base.ZContext.Logger.Information("【武备追新】所有武备都已获取，回退至优先级");
			}
		}
		if (lostVoidArtifactPos == null)
		{
			lostVoidArtifactPos = base.ZContext.LostVoid.GetArtifactByPriority(readOnlyList.Select<(LostVoidArtifactPos, bool), LostVoidArtifactPos>(((LostVoidArtifactPos Position, bool HasLevel) item) => item.Position), 1).FirstOrDefault() ?? readOnlyList[0].Item1;
		}
		if (!base.ZContext.Controller.Click(lostVoidArtifactPos.Rect.Center))
		{
			return RoundRetry("点击武备失败", null, OneSecond);
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(500L));
		if (flag && !challengeConfig.ArtifactPriorityInBattle.Contains<string>(lostVoidArtifactPos.Artifact.Category, StringComparer.Ordinal))
		{
			challengeConfig.ArtifactPriorityInBattle.Add(lostVoidArtifactPos.Artifact.Category);
			base.ZContext.Logger.Information("【武备追新】添加开局选择的武备属性至第一优先级: [{Category}]", lostVoidArtifactPos.Artifact.Category);
		}
		return RoundSuccess(null, null, TimeSpan.FromMilliseconds(500L));
	}

	private IReadOnlyList<(LostVoidArtifactPos Position, bool HasLevel)> ReadGearCandidates(Mat screen, OneDragon.Core.Screen.ScreenArea nameArea)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-武备选择", "武备列表");
		OneDragon.Core.Screen.ScreenArea area2 = base.ZContext.ScreenContext.GetArea("迷失之地-武备选择", "等级列表");
		if (area == null || area2 == null || base.ZContext.Controller == null)
		{
			return Array.Empty<(LostVoidArtifactPos, bool)>();
		}
		IReadOnlyList<OneDragon.Core.Abstractions.Geometry.Rect> gearRects = FindPipelineRects(screen, area, new Scalar(0.0, 0.0, 75.0), new Scalar(180.0, 255.0, 255.0), 2, 2000.0, 100000.0);
		IReadOnlyList<OneDragon.Core.Abstractions.Geometry.Rect> source = FindPipelineRects(screen, area2, new Scalar(128.0, 0.0, 0.0), new Scalar(138.0, 255.0, 255.0), 0, 300.0, 10000.0);
		if (gearRects.Count == 0)
		{
			return Array.Empty<(LostVoidArtifactPos, bool)>();
		}
		List<Mat> list = new List<Mat>();
		try
		{
			foreach (OneDragon.Core.Abstractions.Geometry.Rect item2 in gearRects)
			{
				base.ZContext.Controller.Click(item2.Center);
				Thread.Sleep(OneSecond);
				Mat mat = Screenshot();
				if (mat == null)
				{
					return Array.Empty<(LostVoidArtifactPos, bool)>();
				}
				list.Add(CvImageUtils.Crop(mat, nameArea.Rect));
			}
			using Mat mat2 = new Mat();
			Cv2.VConcat(list, mat2);
			IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultListWithoutOverlayVision(mat2);
			IReadOnlyList<string> readOnlyList = LostVoidInteractService.Instance.ExtractNamesFromStitchedOcr(ocrResultList, gearRects.Count, nameArea.Height);
			List<(LostVoidArtifactPos, bool)> list2 = new List<(LostVoidArtifactPos, bool)>();
			int index;
			for (index = 0; index < Math.Min(gearRects.Count, readOnlyList.Count); index++)
			{
				LostVoidArtifactNameResult lostVoidArtifactNameResult = LostVoidInteractService.Instance.BuildArtifactFromOcrName(readOnlyList[index]);
				if (lostVoidArtifactNameResult.Artifact != null)
				{
					bool item = source.Any((OneDragon.Core.Abstractions.Geometry.Rect levelRect) => Overlaps(gearRects[index], levelRect));
					list2.Add((new LostVoidArtifactPos(lostVoidArtifactNameResult.Artifact, gearRects[index], "", lostVoidArtifactNameResult.IsPrimaryName), item));
				}
			}
			base.ZContext.Logger.Information("当前识别武备 {Gears}", string.Join(" ", list2.Select<(LostVoidArtifactPos, bool), string>(((LostVoidArtifactPos Position, bool HasLevel) tuple) => tuple.Position.Artifact.DisplayName + "(" + (tuple.HasLevel ? "已获取" : "未获取") + ")")));
			return list2;
		}
		finally
		{
			foreach (Mat item3 in list)
			{
				item3.Dispose();
			}
		}
	}

	private static IReadOnlyList<OneDragon.Core.Abstractions.Geometry.Rect> FindPipelineRects(Mat screen, OneDragon.Core.Screen.ScreenArea area, Scalar lower, Scalar upper, int dilateIterations, double minArea, double maxArea)
	{
		using Mat mat = CvImageUtils.Crop(screen, area.Rect);
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.BGR2HSV);
		using Mat mat3 = new Mat();
		Cv2.InRange(mat2, lower, upper, mat3);
		using Mat mat4 = mat3.Clone();
		if (dilateIterations > 0)
		{
			using Mat mat5 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
			Cv2.Dilate(mat3, mat4, mat5, null, dilateIterations);
		}
		Cv2.FindContours(mat4, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
		return (from rect in contours.Select(Cv2.BoundingRect)
			where (double)(rect.Width * rect.Height) >= minArea && (double)(rect.Width * rect.Height) <= maxArea
			select new OneDragon.Core.Abstractions.Geometry.Rect(rect.X + area.Rect.X1, rect.Y + area.Rect.Y1, rect.Right + area.Rect.X1, rect.Bottom + area.Rect.Y1) into rect
			orderby rect.X1
			select rect).ToArray();
	}

	private static bool Overlaps(OneDragon.Core.Abstractions.Geometry.Rect left, OneDragon.Core.Abstractions.Geometry.Rect right)
	{
		return left.X1 <= right.X2 && right.X1 <= left.X2 && left.Y1 <= right.Y2 && right.Y1 <= left.Y2;
	}

	[NodeFrom("选择武备")]
	[OperationNode("点击携带")]
	public OperationRoundResult ClickEquip()
	{
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(null, "迷失之地-武备选择", "按钮-携带", null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			base.ZContext.LostVoid.PriorityUpdated = false;
			base.ZContext.Logger.Information("武备选择成功，已设置优先级更新标志");
		}
		return operationRoundResult;
	}

	[NodeFrom("选择武备", Success = false)]
	[NodeFrom("点击携带")]
	[OperationNode("点击返回")]
	public OperationRoundResult ClickBack()
	{
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(null, "迷失之地-武备选择", "按钮-返回", null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return operationRoundResult;
		}
		retryDelay = TimeSpan.FromMilliseconds(500L);
		successDelay = TimeSpan.FromMilliseconds(500L);
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(null, "迷失之地-武备选择", "不再提示", null, retryDelay, successDelay);
		if (!operationRoundResult2.IsSuccess)
		{
			return RoundRetry(null, null, TimeSpan.FromSeconds(1L));
		}
		RoundByFindAndClickArea(null, "迷失之地-武备选择", "确认");
		successDelay = TimeSpan.FromSeconds(1L);
		retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(null, "迷失之地-武备选择", "按钮-返回", null, successDelay, retryDelay);
	}
}
