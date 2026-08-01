using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidChooseCommonOperation : ZOperation
{
	private readonly LostVoidInteractService _service;

	private int _fallbackClickCount;

	private int _lastChooseTargetNum;

	public LostVoidChooseCommonOperation(ZContext context, LostVoidInteractService? service = null)
		: base(context, "迷失之地-通用选择")
	{
		_service = service ?? LostVoidInteractService.Instance;
	}

	[OperationNode("选择", IsStartNode = true)]
	public OperationRoundResult ChooseArtifact()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-通用选择", "文本-详情");
		if (area == null || base.ZContext.Controller == null)
		{
			return RoundRetry("选择界面未就绪", null, TimeSpan.FromSeconds(1L));
		}
		base.ZContext.Controller.MouseMove(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, 100));
		Thread.Sleep(TimeSpan.FromMilliseconds(100L));
		IReadOnlyList<string> titleWords = ReadOcrTexts("迷失之地-通用选择", "区域-标题");
		LostVoidChooseTitleState lostVoidChooseTitleState = ResolveChooseTitle(
			_service,
			titleWords,
			() => ScreenUtils.FindArea(base.ZContext, base.LastScreenshot, "迷失之地-通用选择", "区域-武备标识") == FindAreaResultEnum.True);
		if (lostVoidChooseTitleState.ToChooseNum <= 0)
		{
			_fallbackClickCount = 0;
			_lastChooseTargetNum = 0;
			return ClickConfirm();
		}
		if (_lastChooseTargetNum != lostVoidChooseTitleState.ToChooseNum)
		{
			_fallbackClickCount = 0;
			_lastChooseTargetNum = lostVoidChooseTitleState.ToChooseNum;
		}
		if (TrySelectByLayers(lostVoidChooseTitleState) || TryFillByAnswerFallback(lostVoidChooseTitleState.ToChooseNum, lostVoidChooseTitleState.ToChooseGearBranch) || TryFillByCanChoose(lostVoidChooseTitleState.ToChooseNum, lostVoidChooseTitleState.ToChooseGearBranch))
		{
			return ClickConfirm();
		}
		Mat mat = Screenshot();
		if (mat == null)
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromMilliseconds(500L));
		}
		int effectiveChosenCount = GetEffectiveChosenCount(mat, lostVoidChooseTitleState.ToChooseNum);
		return (effectiveChosenCount >= lostVoidChooseTitleState.ToChooseNum) ? ClickConfirm() : RoundRetry($"未选满 目标={lostVoidChooseTitleState.ToChooseNum} 当前={effectiveChosenCount}", null, TimeSpan.FromMilliseconds(500L));
	}

	internal static LostVoidChooseTitleState ResolveChooseTitle(LostVoidInteractService service, IReadOnlyList<string> titleWords, Func<bool> gearMarkerDetector)
	{
		LostVoidChooseTitleState state = service.ParseChooseTitle(titleWords);
		return string.Equals(state.RuleId, "fallback:none", StringComparison.Ordinal)
			? service.ParseChooseTitle(titleWords, gearMarkerDetector())
			: state;
	}

	private bool TrySelectByLayers(LostVoidChooseTitleState state)
	{
		for (int i = 0; i < 12; i++)
		{
			Mat mat = Screenshot();
			if (mat == null)
			{
				return false;
			}
			IReadOnlyList<LostVoidArtifactPos> artifactPos = base.ZContext.LostVoid.GetArtifactPos(mat, state.ToChooseGearBranch);
			int effectiveChosenCount = GetEffectiveChosenCount(mat, state.ToChooseNum);
			if (effectiveChosenCount >= state.ToChooseNum)
			{
				return true;
			}
			List<LostVoidArtifactPos> list = artifactPos.Where((LostVoidArtifactPos item) => item.CanChoose).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			List<LostVoidArtifactPos> list2 = new List<LostVoidArtifactPos>();
			LostVoidChallengeConfig? challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
			if (challengeConfig != null && challengeConfig.ArtifactPriorityNew)
			{
				list2.AddRange(_service.SortCandidates(list.Where((LostVoidArtifactPos item) => item.IsNew)));
			}
			if (HasPriorityRule())
			{
				list2.AddRange(base.ZContext.LostVoid.GetArtifactByPriority(list, list.Count, considerPriority1: true, considerPriority2: true, considerNotInPriority: false));
			}
			LostVoidArtifactPos lostVoidArtifactPos = (from item in list2
				group item by (X: item.Rect.Center.X, Y: item.Rect.Center.Y) into @group
				select @group.First()).FirstOrDefault();
			if (lostVoidArtifactPos == null)
			{
				return false;
			}
			if (!base.ZContext.Controller.Click(lostVoidArtifactPos.Rect.Center))
			{
				return false;
			}
			_fallbackClickCount++;
			Thread.Sleep(TimeSpan.FromMilliseconds(300L));
			if (ReachedTargetChooseNum(state.ToChooseNum))
			{
				return true;
			}
		}
		Mat mat2 = Screenshot();
		return mat2 != null && GetEffectiveChosenCount(mat2, state.ToChooseNum) >= state.ToChooseNum;
	}

	private bool TryFillByAnswerFallback(int targetNum, bool toChooseGearBranch)
	{
		return TryFillByCandidates(targetNum, toChooseGearBranch, (LostVoidArtifactPos item) => item.CanChoose && item.Artifact.Category == "无详情");
	}

	private bool TryFillByCanChoose(int targetNum, bool toChooseGearBranch)
	{
		return TryFillByCandidates(targetNum, toChooseGearBranch, (LostVoidArtifactPos item) => item.CanChoose);
	}

	private bool TryFillByCandidates(int targetNum, bool toChooseGearBranch, Func<LostVoidArtifactPos, bool> predicate)
	{
		List<OneDragon.Core.Abstractions.Geometry.Point> tried = new List<OneDragon.Core.Abstractions.Geometry.Point>();
		for (int i = 0; i < 12; i++)
		{
			Mat mat = Screenshot();
			if (mat == null)
			{
				return false;
			}
			if (GetEffectiveChosenCount(mat, targetNum) >= targetNum)
			{
				return true;
			}
			LostVoidArtifactPos lostVoidArtifactPos = _service.SortCandidates(base.ZContext.LostVoid.GetArtifactPos(mat, toChooseGearBranch).Where(predicate)).FirstOrDefault((LostVoidArtifactPos item) => tried.All((OneDragon.Core.Abstractions.Geometry.Point point) => Math.Abs(item.Rect.Center.X - point.X) >= 40 || Math.Abs(item.Rect.Center.Y - point.Y) >= 40));
			if (lostVoidArtifactPos == null || !base.ZContext.Controller.Click(lostVoidArtifactPos.Rect.Center))
			{
				break;
			}
			tried.Add(lostVoidArtifactPos.Rect.Center);
			_fallbackClickCount++;
			Thread.Sleep(TimeSpan.FromMilliseconds(300L));
		}
		Mat mat2 = Screenshot();
		return mat2 != null && GetEffectiveChosenCount(mat2, targetNum) >= targetNum;
	}

	private bool ReachedTargetChooseNum(int targetNum)
	{
		for (int i = 0; i < 3; i++)
		{
			Mat mat = Screenshot();
			if (mat != null && GetEffectiveChosenCount(mat, targetNum) >= targetNum)
			{
				return true;
			}
			Thread.Sleep(TimeSpan.FromMilliseconds(200L));
		}
		return false;
	}

	private int GetEffectiveChosenCount(Mat screen, int targetNum)
	{
		int? num = _service.ParseConfirmChosenCount(ReadOcrTexts(screen, "迷失之地-通用选择", "按钮-确定"), targetNum);
		if (num.HasValue)
		{
			_fallbackClickCount = num.Value;
			return num.Value;
		}
		return _fallbackClickCount;
	}

	private bool HasPriorityRule()
	{
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		return challengeConfig != null && (base.ZContext.LostVoid.DynamicPriorityList.Count > 0 || challengeConfig.ArtifactPriority.Count > 0 || challengeConfig.ArtifactPriority2.Count > 0);
	}

	private OperationRoundResult ClickConfirm()
	{
		Mat mat = Screenshot();
		if (mat == null)
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromSeconds(1L));
		}
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(mat, "迷失之地-通用选择", "按钮-确定", null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			base.ZContext.LostVoid.PriorityUpdated = false;
			return RoundSuccess(operationRoundResult.Status);
		}
		return RoundRetry(operationRoundResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	private IReadOnlyList<string> ReadOcrTexts(string screenName, string areaName)
	{
		IReadOnlyList<string> result;
		if (base.LastScreenshot != null)
		{
			result = ReadOcrTexts(base.LastScreenshot, screenName, areaName);
		}
		else
		{
			IReadOnlyList<string> readOnlyList = Array.Empty<string>();
			result = readOnlyList;
		}
		return result;
	}

	private IReadOnlyList<string> ReadOcrTexts(Mat screen, string screenName, string areaName)
	{
		if (screen == null)
		{
			return Array.Empty<string>();
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea(screenName, areaName);
		return (from result in base.ZContext.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect)
			select result.Text).ToArray();
	}
}
