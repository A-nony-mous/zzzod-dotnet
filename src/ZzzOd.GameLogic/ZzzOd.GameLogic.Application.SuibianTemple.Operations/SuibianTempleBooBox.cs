using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleBooBox(ZContext context, SuibianTempleConfig config) : SuibianTempleSubOperation(context, config, "随便观 邦巢")
{
	private int _boughtCount;

	private int _refreshCount;

	private readonly List<OneDragon.Core.Abstractions.Geometry.Rect> _doneBangbooPositions = new List<OneDragon.Core.Abstractions.Geometry.Rect>();

	private OneDragon.Core.Abstractions.Geometry.Rect? _currentBangbooPosition;

	private string _currentBangbooPrice = string.Empty;

	[OperationNode("前往邦巢", IsStartNode = true, NodeMaxRetryTimes = 5)]
	public OperationRoundResult GoToBooBox()
	{
		return GoToScreenByText("随便观-邦巢", "邻里街坊", "邦巢");
	}

	[NodeFrom("前往邦巢")]
	[NodeFrom("检查邦布类型", Success = false)]
	[NodeFrom("检查邦布类型", Status = "不购买该类型邦布")]
	[NodeFrom("检查邦布类型", Status = "价格低于配置要求")]
	[NodeFrom("检查邦布", Status = "刷新邦布完成")]
	[NodeFrom("返回界面", Status = "继续检查邦布")]
	[NodeFrom("处理购买动画", Status = "确认后继续检查邦布")]
	[NodeFrom("处理购买动画", Status = "已返回邦巢界面")]
	[OperationNode("检查邦布")]
	public OperationRoundResult CheckBangboo()
	{
		if (!RoundByFindArea(base.LastScreenshot, "随便观-邦巢", "按钮-聘用").IsSuccess)
		{
			return RoundRetry("不在邦巢界面，等待加载", null, TimeSpan.FromSeconds(2L));
		}
		string[] array = (from item in SuibianTempleBangbooPrice.Options
			where !object.Equals(item.Value, "NONE")
			select item.Label).ToArray();
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-邦巢", "区域-邦布列表");
		if (area == null)
		{
			return RoundFail("区域未配置 区域-邦布列表");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, null, area.Rect, cropFirst: false);
		string[] array2 = array;
		foreach (string price in array2)
		{
			foreach (OcrMatchResult candidate in ocrResultList.Where((OcrMatchResult result) => result.Text.Contains(price, StringComparison.Ordinal)))
			{
				if (_doneBangbooPositions.Any((OneDragon.Core.Abstractions.Geometry.Rect done) => CalUtils.CalculateOverlapPercent(done, candidate.Rect) > 0.7))
				{
					continue;
				}
				_currentBangbooPosition = candidate.Rect;
				_currentBangbooPrice = candidate.Text;
				_doneBangbooPositions.Add(candidate.Rect);
				base.ZContext.Controller?.Click(candidate.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, -150));
				return RoundSuccess("点击S级邦布", null, TimeSpan.FromMilliseconds(1500L));
			}
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot, "次数用尽", null, 0.5, null, retryDelay).IsSuccess || _refreshCount >= 50)
		{
			return RoundSuccess("次数用尽", new { _boughtCount, _refreshCount });
		}
		_refreshCount++;
		ClickArea("随便观-邦巢", "按钮-刷新");
		_doneBangbooPositions.Clear();
		return RoundWait("刷新邦布完成", null, TimeSpan.FromMilliseconds(1500L));
	}

	[NodeFrom("检查邦布", Status = "点击S级邦布")]
	[OperationNode("检查邦布类型")]
	public OperationRoundResult CheckBangbooType()
	{
		string text = null;
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-邦巢", "标题-邦布名称");
		if (area == null)
		{
			return RoundFail("区域未配置 标题-邦布名称");
		}
		foreach (OcrMatchResult ocrResult in base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, null, area.Rect))
		{
			string[] array = new string[3] { "游历", "制造", "售卖" };
			foreach (string text2 in array)
			{
				if (ocrResult.Text.Contains(text2, StringComparison.Ordinal))
				{
					text = text2;
					break;
				}
			}
		}
		if (1 == 0)
		{
		}
		string text3 = text switch
		{
			"游历" => base.Config.BooBoxAdventurePrice, 
			"制造" => base.Config.BooBoxCraftPrice, 
			"售卖" => base.Config.BooBoxSellPrice, 
			_ => string.Empty, 
		};
		if (1 == 0)
		{
		}
		string text4 = text3;
		if (string.IsNullOrEmpty(text4))
		{
			return RoundRetry("未识别邦布类型", null, SuibianTempleSubOperation.ShortDelay);
		}
		if (text4 == "NONE")
		{
			return RoundSuccess("不购买该类型邦布");
		}
		string optionLabel = SuibianTempleSubOperation.GetOptionLabel(SuibianTempleBangbooPrice.Options, text4);
		int valueOrDefault = SuibianTempleSubOperation.ExtractPositiveDigits(_currentBangbooPrice).GetValueOrDefault();
		int valueOrDefault2 = SuibianTempleSubOperation.ExtractPositiveDigits(optionLabel).GetValueOrDefault();
		if (valueOrDefault < valueOrDefault2)
		{
			return RoundSuccess("价格低于配置要求");
		}
		return RoundSuccess("符合购买要求");
	}

	private OperationRoundResult ClickConfirmText()
	{
		OcrMatchResult ocrMatchResult = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot).FirstOrDefault((OcrMatchResult result) => string.Equals(result.Text, "确认", StringComparison.Ordinal));
		if (ocrMatchResult == null)
		{
			return ClickText("确认");
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(300L));
		ControllerBase? controller = base.ZContext.Controller;
		return (controller != null && controller.Click(ocrMatchResult.Center)) ? RoundSuccess("确认") : RoundRetry("点击确认失败");
	}

	[NodeFrom("检查邦布类型", Status = "符合购买要求")]
	[OperationNode("点击聘用")]
	public OperationRoundResult ClickHire()
	{
		OperationRoundResult operationRoundResult = FindAndClickArea("随便观-邦巢", "按钮-聘用");
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry("未找到聘用按钮", null, SuibianTempleSubOperation.OneSecond);
		}
		_boughtCount++;
		return RoundSuccess("点击聘用", null, TimeSpan.FromSeconds(2L));
	}

	[NodeFrom("点击聘用", Status = "点击聘用")]
	[OperationNode("处理购买动画")]
	public OperationRoundResult HandlePurchaseAnimation()
	{
		if (RoundByFindArea(base.LastScreenshot, "随便观-邦巢", "标题-无法聘用").IsSuccess)
		{
			OperationRoundResult operationRoundResult = FindAndClickArea("随便观-邦巢", "取消");
			return operationRoundResult.IsSuccess ? RoundSuccess("持有上限", null, SuibianTempleSubOperation.OneSecond) : RoundRetry(operationRoundResult.Status);
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot, "获得", null, 0.5, null, retryDelay).IsSuccess)
		{
			OperationRoundResult operationRoundResult2 = ClickConfirmText();
			return operationRoundResult2.IsSuccess ? RoundWait("确认后继续检查邦布", null, TimeSpan.FromSeconds(2L)) : RoundRetry(operationRoundResult2.Status);
		}
		Mat? lastScreenshot2 = base.LastScreenshot;
		retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot2, "聘用", null, 0.5, null, retryDelay).IsSuccess)
		{
			return RoundSuccess("已返回邦巢界面");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-邦巢", "按钮-跳过");
		if (area == null)
		{
			return RoundFail("区域未配置 按钮-跳过");
		}
		base.ZContext.Controller?.DragTo(area.Center, area.LeftTop, TimeSpan.FromMilliseconds(200L));
		base.ZContext.Controller?.Click();
		return RoundWait("点击跳过", null, TimeSpan.FromMilliseconds(500L));
	}

	[NodeFrom("处理购买动画", Status = "点击跳过")]
	[OperationNode("返回界面")]
	public OperationRoundResult ReturnInterface()
	{
		OperationRoundResult operationRoundResult = ClickText("返回");
		return operationRoundResult.IsSuccess ? RoundWait("继续检查邦布", null, TimeSpan.FromSeconds(2L)) : RoundRetry("未找到返回按钮", null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("检查邦布", Status = "次数用尽")]
	[NodeFrom("处理购买动画", Status = "持有上限")]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		return BackToEntry();
	}
}
