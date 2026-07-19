using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleGoodGoods : SuibianTempleSubOperation
{
	private int _purchasedCount;

	public SuibianTempleGoodGoods(ZContext context, SuibianTempleConfig config)
		: base(context, config, "随便观 好物铺")
	{
	}

	[OperationNode("前往邻里街坊", IsStartNode = true, NodeMaxRetryTimes = 5)]
	public OperationRoundResult GoToLinliJiefang()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot, "好物铺", null, 0.5, null, retryDelay).IsSuccess)
		{
			return RoundSuccess("已在邻里街坊-进入好物铺");
		}
		OperationRoundResult operationRoundResult = ClickText("邻里街坊");
		return operationRoundResult.IsSuccess ? RoundWait(operationRoundResult.Status, null, TimeSpan.FromMilliseconds(1500L)) : RoundRetry("未找到邻里街坊", null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("前往邻里街坊")]
	[OperationNode("已在邻里街坊-进入好物铺", NodeMaxRetryTimes = 5)]
	public OperationRoundResult GoToGoodGoods()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot, "经营购置", null, 0.5, null, retryDelay).IsSuccess)
		{
			return RoundSuccess("已在好物铺-购买");
		}
		OperationRoundResult operationRoundResult = ClickText("好物铺");
		return operationRoundResult.IsSuccess ? RoundWait(operationRoundResult.Status, null, TimeSpan.FromSeconds(2L)) : RoundRetry("未找到好物铺", null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("已在邻里街坊-进入好物铺")]
	[NodeFrom("已在好物铺-购买", Status = "已确认兑换")]
	[OperationNode("已在好物铺-购买")]
	public OperationRoundResult ProcessGoodGoods()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot, "获得", null, 0.5, null, retryDelay).IsSuccess)
		{
			OperationRoundResult operationRoundResult = ClickConfirmText();
			if (!operationRoundResult.IsSuccess)
			{
				return RoundRetry("点击获得确认失败");
			}
			_purchasedCount++;
			return RoundSuccess("购买成功", _purchasedCount);
		}
		Mat? lastScreenshot2 = base.LastScreenshot;
		retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot2, "兑换确认", null, 0.75, null, retryDelay).IsSuccess)
		{
			base.ZContext.Controller?.DragTo(new OneDragon.Core.Abstractions.Geometry.Point(1300, 672), new OneDragon.Core.Abstractions.Geometry.Point(755, 672), TimeSpan.FromSeconds(2L));
			OperationRoundResult operationRoundResult2 = ClickConfirmText();
			return operationRoundResult2.IsSuccess ? RoundWait("已确认兑换", null, TimeSpan.FromSeconds(2L)) : RoundRetry("点击兑换确认失败");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot);
		OcrMatchResult ocrMatchResult = (from result in ocrResultList
			where result.Text.Contains("邦布能源插件", StringComparison.Ordinal)
			orderby result.Rect.X1, result.Rect.Y2 descending
			select result).FirstOrDefault();
		if (ocrMatchResult != null)
		{
			IReadOnlyList<OcrMatchResult> ocrResultList2 = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, null, ocrMatchResult.Rect);
			if (ocrResultList2.Any((OcrMatchResult result) => result.Text.Contains("已售罄", StringComparison.Ordinal) || result.Text.Contains("售罄", StringComparison.Ordinal)))
			{
				return RoundSuccess("跳过购买-已售罄");
			}
			OneDragon.Core.Abstractions.Geometry.Rect value = new OneDragon.Core.Abstractions.Geometry.Rect(ocrMatchResult.Rect.X1 - 20, ocrMatchResult.Rect.Y2, ocrMatchResult.Rect.X2 + 20, base.LastScreenshot.Height);
			IReadOnlyList<OcrMatchResult> ocrResultList3 = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, null, value);
			if (!ocrResultList3.Any((OcrMatchResult result) => HasPurchasablePluginPrice(result.Text)))
			{
				return RoundSuccess("跳过购买-已售罄");
			}
			ControllerBase? controller = base.ZContext.Controller;
			if (controller == null || !controller.Click(ocrMatchResult.Center))
			{
				return RoundRetry("点击邦布能源插件失败");
			}
			return RoundWait("已点击邦布能源插件", null, TimeSpan.FromMilliseconds(1500L));
		}
		OperationRoundResult operationRoundResult3 = ClickText("经营购置");
		return operationRoundResult3.IsSuccess ? RoundWait("切换到经营购置", null, SuibianTempleSubOperation.OneSecond) : RoundSuccess("找不到商品或已完成");
	}

	private static bool HasPurchasablePluginPrice(string text)
	{
		if (new string[6] { "500", "5OO", "50O", "S00", "soo", "5oo" }.Any((string pattern) => text.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		if (!text.Any(char.IsDigit) || text.Contains("lv", StringComparison.OrdinalIgnoreCase) || text.Contains("等级", StringComparison.OrdinalIgnoreCase) || text.Contains("level", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		int num = 0;
		string text2 = text;
		foreach (char c in text2)
		{
			num = (char.IsDigit(c) ? (num + 1) : 0);
			if (num >= 3)
			{
				return true;
			}
		}
		return false;
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

	[NodeFrom("已在好物铺-购买", Status = "跳过购买-已售罄")]
	[NodeFrom("已在好物铺-购买", Status = "购买成功")]
	[NodeFrom("已在好物铺-购买", Status = "找不到商品或已完成")]
	[OperationNode("好物铺-返回邻里")]
	public OperationRoundResult ExitGoodGoods()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		if (RoundByOcr(lastScreenshot, "邻里街坊", null, 0.5, null, retryDelay).IsSuccess)
		{
			return RoundSuccess("已返回邻里街坊");
		}
		OperationRoundResult operationRoundResult = FindAndClickArea("菜单", "返回");
		return operationRoundResult.IsSuccess ? RoundWait("点击左上角返回", null, SuibianTempleSubOperation.OneSecond) : RoundRetry("无法从好物铺返回", null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("好物铺-返回邻里")]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		return BackToEntry();
	}
}
