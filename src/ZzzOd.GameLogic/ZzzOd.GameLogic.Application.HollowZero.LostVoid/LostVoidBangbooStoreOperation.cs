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

public sealed class LostVoidBangbooStoreOperation(ZContext context) : ZOperation(context, "迷失之地-邦布商店")
{
	public const string GoldStore = "标识-金币";

	public const string BloodStore = "标识-血量";

	private string _storeType = "标识-金币";

	private int _refreshTimes;

	private bool _slidToRight;

	[OperationNode("识别商店类型", IsStartNode = true)]
	public OperationRoundResult CheckStoreType()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图");
		}
		string[] array = new string[2] { "标识-金币", "标识-血量" };
		foreach (string text in array)
		{
			OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "迷失之地-邦布商店", text);
			if (operationRoundResult.IsSuccess)
			{
				_storeType = text;
				return RoundSuccess(text);
			}
		}
		return RoundRetry("未识别商店类型", null, TimeSpan.FromSeconds(1L));
	}

	[NodeFrom("识别商店类型")]
	[NodeFrom("识别商店类型", Success = false)]
	[NodeFrom("确认后处理")]
	[OperationNode("购买藏品")]
	public OperationRoundResult BuyArtifact()
	{
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		if (base.LastScreenshot == null || challengeConfig == null || base.ZContext.Controller == null)
		{
			return RoundRetry("商店未就绪", null, TimeSpan.FromSeconds(1L));
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-邦布商店", "文本-详情");
		if (area == null)
		{
			return RoundFail("区域未配置 文本-详情");
		}
		base.ZContext.Controller.MouseMove(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, 100));
		Thread.Sleep(TimeSpan.FromMilliseconds(100L));
		if (_storeType == "标识-血量" && !challengeConfig.StoreBlood)
		{
			return RoundFail("不使用血量购买");
		}
		if (_storeType == "标识-血量" && !IsMinBloodValid(base.LastScreenshot, challengeConfig.StoreBloodMin))
		{
			return RoundFail("血量低于设定最小值");
		}
		if (_storeType == "标识-金币" && !challengeConfig.StoreGold)
		{
			return RoundFail("不使用金币购买");
		}
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(null, "迷失之地-邦布商店", "按钮-刷新-确认", null, null);
		if (operationRoundResult.IsSuccess)
		{
			_refreshTimes++;
			return RoundWait(operationRoundResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[2] { "迷失之地-邦布商店", "迷失之地-通用选择" });
		if (text == "迷失之地-通用选择")
		{
			OperationResult result = new LostVoidChooseCommonOperation(base.ZContext).ExecuteAsync().GetAwaiter().GetResult();
			return result.IsSuccess ? RoundWait("武备升级", null, TimeSpan.FromSeconds(1L)) : RoundRetry("武备升级失败", null, TimeSpan.FromSeconds(1L));
		}
		if (text != "迷失之地-邦布商店")
		{
			return RoundRetry("当前画面 " + text, null, TimeSpan.FromSeconds(1L));
		}
		var (readOnlyList, readOnlyList2) = GetStoreArtifactPositions(base.LastScreenshot);
		if (readOnlyList2.Count == 0)
		{
			if (readOnlyList.Count >= 4)
			{
				return RoundFail("已识别藏品但无可购买项");
			}
			return SlideOrRetry("未识别可购买藏品");
		}
		IReadOnlyList<LostVoidArtifactPos> artifactByPriority = base.ZContext.LostVoid.GetArtifactByPriority(readOnlyList2, 1, considerPriority1: true, _refreshTimes > challengeConfig.BuyOnlyPriority1, _refreshTimes > challengeConfig.BuyOnlyPriority2, null, challengeConfig.ArtifactPriorityNew);
		if (artifactByPriority.Count == 0)
		{
			if (!_slidToRight)
			{
				return SlideOrRetry("向右滑动");
			}
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
			OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(lastScreenshot, "迷失之地-邦布商店", "按钮-刷新-可用", null, null);
			if (operationRoundResult2.IsSuccess)
			{
				_slidToRight = false;
				return RoundWait(operationRoundResult2.Status, null, TimeSpan.FromSeconds(1L));
			}
			artifactByPriority = base.ZContext.LostVoid.GetArtifactByPriority(readOnlyList2, readOnlyList2.Count);
		}
		LostVoidArtifactPos lostVoidArtifactPos = artifactByPriority.FirstOrDefault((LostVoidArtifactPos item) => item.StoreBuyRect.HasValue);
		if (lostVoidArtifactPos != null)
		{
			OneDragon.Core.Abstractions.Geometry.Rect? storeBuyRect = lostVoidArtifactPos.StoreBuyRect;
			if (storeBuyRect.HasValue)
			{
				OneDragon.Core.Abstractions.Geometry.Rect valueOrDefault = storeBuyRect.GetValueOrDefault();
				if (base.ZContext.Controller.Click(valueOrDefault.Center))
				{
					return RoundSuccess(lostVoidArtifactPos.Artifact.Name, null, TimeSpan.FromSeconds(1L));
				}
			}
		}
		return RoundRetry("按优先级选择藏品失败", null, TimeSpan.FromSeconds(1L));
	}

	private OperationRoundResult SlideOrRetry(string status)
	{
		if (_slidToRight || base.ZContext.Controller == null)
		{
			return RoundRetry(status, null, TimeSpan.FromSeconds(1L));
		}
		OneDragon.Core.Abstractions.Geometry.Point point = new OneDragon.Core.Abstractions.Geometry.Point(base.ZContext.Controller.StandardWidth / 2, base.ZContext.Controller.StandardHeight / 2);
		base.ZContext.Controller.DragTo(point + new OneDragon.Core.Abstractions.Geometry.Point(-400, 0), point);
		_slidToRight = true;
		return RoundWait("向右滑动");
	}

	private (IReadOnlyList<LostVoidArtifactPos> All, IReadOnlyList<LostVoidArtifactPos> Purchasable) GetStoreArtifactPositions(Mat screen)
	{
		List<LostVoidArtifactPos> list = base.ZContext.LostVoid.GetArtifactPos(screen, toChooseGearBranch: false, "迷失之地-邦布商店").ToList();
		AssociatePriceOrBuyText(screen, list, "区域-价格", (OcrMatchResult artifact) => StringUtils.GetPositiveDigits(artifact.Text, -1) ?? (-1), (LostVoidArtifactPos position, int value, OneDragon.Core.Abstractions.Geometry.Rect rect) => value >= 0 && position.AddPrice(value, rect));
		AssociatePriceOrBuyText(screen, list, "区域-购买按钮", (OcrMatchResult artifact) => StringUtils.FindByLcs(base.ZContext.GameTextResolver("购买"), artifact.Text) ? 1 : (-1), (LostVoidArtifactPos position, int value, OneDragon.Core.Abstractions.Geometry.Rect rect) => value == 1 && position.AddBuy(rect));
		return (All: list, Purchasable: list.Where((LostVoidArtifactPos item) => item.StoreBuyRect.HasValue).ToArray());
	}

	private void AssociatePriceOrBuyText(Mat screen, IEnumerable<LostVoidArtifactPos> artifacts, string areaName, Func<OcrMatchResult, int> valueSelector, Func<LostVoidArtifactPos, int, OneDragon.Core.Abstractions.Geometry.Rect, bool> associate)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-邦布商店", areaName);
		if (area == null)
		{
			return;
		}
		using Mat mat = CvImageUtils.Crop(screen, area.Rect);
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		Cv2.InRange(mat, new Scalar(200.0, 200.0, 200.0), new Scalar(255.0, 255.0, 255.0), mat2);
		using Mat mat4 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
		Cv2.Dilate(mat2, mat3, mat4);
		using Mat mat5 = new Mat();
		Cv2.BitwiseAnd(mat, mat, mat5, mat3);
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultListForCrop(
			mat5,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1);
		foreach (OcrMatchResult item in ocrResultList)
		{
			int num = valueSelector(item);
			if (num < 0)
			{
				continue;
			}
			OneDragon.Core.Abstractions.Geometry.Rect arg = new OneDragon.Core.Abstractions.Geometry.Rect(item.Rect.X1 + area.Rect.X1, item.Rect.Y1 + area.Rect.Y1, item.Rect.X2 + area.Rect.X1, item.Rect.Y2 + area.Rect.Y1);
			foreach (LostVoidArtifactPos artifact in artifacts)
			{
				associate(artifact, num, arg);
			}
		}
	}

	private bool IsMinBloodValid(Mat screen, int minBlood)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-邦布商店", "区域-角色头像");
		if (area == null)
		{
			return false;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		foreach (OcrMatchResult item in ocrResultList)
		{
			int num = StringUtils.GetPositiveDigits(item.Text, -1) ?? (-1);
			if (num >= 0 && num < minBlood)
			{
				return false;
			}
		}
		return true;
	}

	[NodeFrom("购买藏品")]
	[OperationNode("点击确认")]
	public OperationRoundResult ClickConfirm()
	{
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-邦布商店", "按钮-购买-确认") };
		return RoundByFindAndClickArea(null, "迷失之地-邦布商店", "按钮-购买-确认", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
	}

	[NodeFrom("点击确认")]
	[OperationNode("确认后处理")]
	public OperationRoundResult AfterConfirm()
	{
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[2] { "迷失之地-通用选择", "迷失之地-邦布商店" });
		if (text == "迷失之地-通用选择")
		{
			OperationResult result = new LostVoidChooseCommonOperation(base.ZContext).ExecuteAsync().GetAwaiter().GetResult();
			return result.IsSuccess ? RoundWait("武备升级", null, TimeSpan.FromSeconds(1L)) : RoundRetry("武备升级失败", null, TimeSpan.FromSeconds(1L));
		}
		return (text == "迷失之地-邦布商店") ? RoundSuccess(_storeType, null, TimeSpan.FromSeconds(1L)) : RoundRetry("未知画面 " + text, null, TimeSpan.FromSeconds(1L));
	}

	[NodeFrom("购买藏品", Success = false)]
	[OperationNode("购买结束")]
	public OperationRoundResult Finish()
	{
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-邦布商店", "按钮-返回") };
		return RoundByFindAndClickArea(null, "迷失之地-邦布商店", "按钮-返回", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
	}
}
