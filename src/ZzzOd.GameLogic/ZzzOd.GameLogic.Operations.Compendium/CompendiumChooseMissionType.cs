using System;
using System.Collections.Generic;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// Chooses a mission type and clicks the corresponding go button.
/// </summary>
public sealed class CompendiumChooseMissionType : ZOperation
{
	private readonly CompendiumMissionType _missionType;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _successDelay;

	private readonly Func<Mat, OneDragon.Core.Screen.ScreenArea, OneDragon.Core.Abstractions.Geometry.Point?> _agentPlanTargetResolver;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public CompendiumChooseMissionType(ZContext context, CompendiumMissionType missionType, Func<Mat, OneDragon.Core.Screen.ScreenArea, OneDragon.Core.Abstractions.Geometry.Point?>? agentPlanTargetResolver = null, TimeSpan? retryDelay = null, TimeSpan? successDelay = null)
		: base(context, "快捷手册 选择副本类型 " + missionType.MissionTypeName)
	{
		_missionType = missionType;
		_agentPlanTargetResolver = agentPlanTargetResolver ?? new Func<Mat, OneDragon.Core.Screen.ScreenArea, OneDragon.Core.Abstractions.Geometry.Point?>(ResolveAgentPlanTargetByImage);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_successDelay = successDelay ?? TimeSpan.FromSeconds(1L);
	}

	[OperationNode("选择副本", IsStartNode = true, NodeMaxRetryTimes = 20)]
	private OperationRoundResult ChooseMissionType()
	{
		if (_missionType.IsAgentPlan)
		{
			return RoundSuccess("代理人方案培养");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("快捷手册", "副本列表");
		if (area == null || base.LastScreenshot == null)
		{
			return RoundFail("非法的副本分类 " + _missionType.MissionTypeName);
		}
		List<CompendiumMissionType> sameCategoryMissionTypeList = base.ZContext.CompendiumService.GetSameCategoryMissionTypeList(_missionType.MissionTypeName);
		if (sameCategoryMissionTypeList == null)
		{
			return RoundFail("非法的副本分类 " + _missionType.MissionTypeName);
		}
		int num = sameCategoryMissionTypeList.FindIndex((CompendiumMissionType compendiumMissionType) => string.Equals(compendiumMissionType.MissionTypeName, _missionType.MissionTypeName, StringComparison.Ordinal));
		if (num < 0)
		{
			return RoundFail("非法的副本分类 " + _missionType.MissionTypeName);
		}
		(List<string> TargetWords, Dictionary<string, int> NameToIndex) tuple = BuildMissionTypeMatchIndex(sameCategoryMissionTypeList);
		List<string> item = tuple.TargetWords;
		Dictionary<string, int> item2 = tuple.NameToIndex;
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect);
		OneDragon.Core.Abstractions.Geometry.Point? point = null;
		foreach (OcrMatchResult item4 in ocrResultList)
		{
			int? num2 = StringUtils.FindBestMatchByDifflib(item4.Text, item);
			if (!num2.HasValue || !item2.TryGetValue(item[num2.Value], out var value) || value != num)
			{
				continue;
			}
			point = item4.Center;
			break;
		}
		return (!point.HasValue) ? HandleScroll(area.Rect) : HandleGoButton(base.LastScreenshot, point.Value);
	}

	[NodeFrom("选择副本", Status = "代理人方案培养")]
	[OperationNode("选择代理人方案", NodeMaxRetryTimes = 10)]
	private OperationRoundResult ChooseMissionTypeByAgent()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("找不到 代理人方案培养", null, _retryDelay);
		}
		string text = _missionType.Category?.Tab?.TabName ?? string.Empty;
		string text2 = ((_missionType.Category?.CategoryName == "恶名狩猎") ? "目标列表-训练-恶名狩猎" : ("目标列表-" + text));
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("快捷手册", text2);
		if (area == null)
		{
			return RoundFail("区域未配置 " + text2);
		}
		OneDragon.Core.Abstractions.Geometry.Point? point = _agentPlanTargetResolver(base.LastScreenshot, area);
		return (!point.HasValue) ? HandleScroll(area.Rect) : HandleGoButton(base.LastScreenshot, point.Value);
	}

	[NodeFrom("选择副本")]
	[NodeFrom("选择代理人方案")]
	[OperationNode("确认")]
	private OperationRoundResult Confirm()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(5L);
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindAndClickArea(lastScreenshot, "快捷手册", "传送确认", null, successDelay, retryDelay);
	}

	private OperationRoundResult HandleScroll(OneDragon.Core.Abstractions.Geometry.Rect area)
	{
		if (base.ZContext.Controller == null)
		{
			return RoundRetry("找不到 " + _missionType.MissionTypeName, null, _retryDelay);
		}
		OneDragon.Core.Abstractions.Geometry.Point point = area.Center + new OneDragon.Core.Abstractions.Geometry.Point(-100, 0);
		OneDragon.Core.Abstractions.Geometry.Point end = point + new OneDragon.Core.Abstractions.Geometry.Point(0, -300);
		base.ZContext.Controller.DragTo(end, point);
		return RoundRetry("找不到 " + _missionType.MissionTypeName, null, _retryDelay);
	}

	private OperationRoundResult HandleGoButton(Mat screen, OneDragon.Core.Abstractions.Geometry.Point targetPoint)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("快捷手册", "前往列表");
		if (area == null)
		{
			return RoundFail("区域未配置 前往列表");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		OneDragon.Core.Abstractions.Geometry.Point? point = null;
		foreach (OcrMatchResult item in ocrResultList)
		{
			if (StringUtils.FindByLcs("前往", item.Text, 0.5))
			{
				OneDragon.Core.Abstractions.Geometry.Point center = item.Center;
				if (center.Y > targetPoint.Y && (!point.HasValue || center.Y < point.Value.Y))
				{
					point = center;
				}
			}
		}
		if (!point.HasValue)
		{
			if (base.ZContext.Controller == null)
			{
				return RoundRetry("找不到 前往", null, _retryDelay);
			}
			OneDragon.Core.Abstractions.Geometry.Point center2 = area.Rect.Center;
			OneDragon.Core.Abstractions.Geometry.Point end = center2 + new OneDragon.Core.Abstractions.Geometry.Point(0, -200);
			base.ZContext.Controller.DragTo(end, center2);
			return RoundRetry("找不到 前往", null, _retryDelay);
		}
		if (base.ZContext.Controller == null)
		{
			return RoundRetry("点击失败 前往", null, _retryDelay);
		}
		base.ZContext.Controller.Click(point.Value);
		return RoundSuccess(null, null, _successDelay);
	}

	private static (List<string> TargetWords, Dictionary<string, int> NameToIndex) BuildMissionTypeMatchIndex(IReadOnlyList<CompendiumMissionType> missionTypes)
	{
		List<string> targetWords = new List<string>();
		Dictionary<string, int> nameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
		for (int i = 0; i < missionTypes.Count; i++)
		{
			CompendiumMissionType compendiumMissionType = missionTypes[i];
			AddName(compendiumMissionType.MissionTypeName, i);
			if (!string.IsNullOrWhiteSpace(compendiumMissionType.MissionTypeNameDisplay))
			{
				AddName(compendiumMissionType.MissionTypeNameDisplay, i);
			}
			foreach (string alias in compendiumMissionType.AliasList)
			{
				AddName(alias, i);
			}
		}
		return (TargetWords: targetWords, NameToIndex: nameToIndex);
		void AddName(string? name, int index)
		{
			if (!string.IsNullOrWhiteSpace(name) && !nameToIndex.ContainsKey(name))
			{
				targetWords.Add(name);
				nameToIndex[name] = index;
			}
		}
	}

	internal static OneDragon.Core.Abstractions.Geometry.Point? ResolveAgentPlanTargetByImage(Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		return ResolveAgentPlanTargetByImage(screen, area, new OneDragon.Core.Abstractions.Geometry.Point(0, -80));
	}

	internal static OneDragon.Core.Abstractions.Geometry.Point? ResolveAgentPlanTargetByImage(Mat screen, OneDragon.Core.Screen.ScreenArea area, OneDragon.Core.Abstractions.Geometry.Point clickOffset)
	{
		if (screen.Empty())
		{
			return null;
		}
		OneDragon.Core.Abstractions.Geometry.Rect rect = area.Rect;
		using Mat mat = new Mat(screen, new OpenCvSharp.Rect(rect.X1, rect.Y1, rect.Width, rect.Height));
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.BGR2HSV);
		using Mat mat3 = new Mat();
		Cv2.InRange(mat2, new Scalar(0.0, 0.0, 0.0), new Scalar(10.0, 10.0, 255.0), mat3);
		using Mat mat4 = new Mat();
		Cv2.BitwiseNot(mat3, mat4);
		Cv2.FindContours(mat4, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
		OpenCvSharp.Point[][] array = contours;
		foreach (OpenCvSharp.Point[] array2 in array)
		{
			double num = Cv2.ContourArea(array2);
			if (!(num < 800.0))
			{
				OpenCvSharp.Rect rect2 = Cv2.BoundingRect(array2);
				return new OneDragon.Core.Abstractions.Geometry.Point(rect.X1 + rect2.X + rect2.Width / 2 + clickOffset.X, rect.Y1 + rect2.Y + rect2.Height / 2 + clickOffset.Y);
			}
		}
		return null;
	}
}
