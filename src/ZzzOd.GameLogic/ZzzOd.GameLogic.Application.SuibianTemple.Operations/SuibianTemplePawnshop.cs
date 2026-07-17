using System;
using System.Collections.Generic;
using System.Linq;
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

public sealed class SuibianTemplePawnshop(ZContext context, SuibianTempleConfig config) : SuibianTempleSubOperation(context, config, "随便观 德丰大押")
{
	private sealed class GoodsPosition(string name, OcrMatchResult position)
	{
		public string Name { get; } = name;

		public OcrMatchResult Position { get; } = position;

		public bool SoldOut { get; set; }

		public bool Unlimited { get; set; }
	}

	private readonly List<string> _chosenGoods = new List<string>();

	private readonly List<string> _chosenUnlimitedGoods = new List<string>();

	[OperationNode("前往德丰大押", IsStartNode = true)]
	public OperationRoundResult GoToPawnshop()
	{
		return GoToScreenByText("随便观-德丰大押", "邻里街坊", "德丰大押");
	}

	[NodeFrom("前往德丰大押")]
	[OperationNode("切换到百通宝-周期")]
	public OperationRoundResult GoToOmnicoin()
	{
		OperationRoundResult result;
		if (!base.Config.PawnshopOmnicoinEnabled)
		{
			result = RoundSuccess("未开启");
		}
		else
		{
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? successDelay = SuibianTempleSubOperation.OneSecond;
			TimeSpan? retryDelay = SuibianTempleSubOperation.OneSecond;
			result = RoundByFindAndClickArea(lastScreenshot, "随便观-德丰大押", "按钮-百通宝-周期", null, successDelay, retryDelay);
		}
		return result;
	}

	[NodeFrom("切换到百通宝-周期")]
	[NodeFrom("购买百通宝商品后处理")]
	[OperationNode("选择百通宝商品", NodeMaxRetryTimes = 2)]
	public OperationRoundResult ChooseOmnicoinGoods()
	{
		return ChooseGoods(base.Config.PawnshopOmnicoinPriority, SuibianTemplePawnshopOmnicoinGoods.Options, _chosenGoods, skipUnlimitedInPriority: false, chooseAnyUnlimited: false);
	}

	[NodeFrom("选择百通宝商品")]
	[OperationNode("购买百通宝商品")]
	public OperationRoundResult BuyOmnicoinGoods()
	{
		return BuyGoods();
	}

	[NodeFrom("购买百通宝商品")]
	[OperationNode("购买百通宝商品后处理")]
	public OperationRoundResult AfterBuyOmnicoinGoods()
	{
		return AfterBuy();
	}

	[NodeFrom("切换到百通宝-周期", Status = "未开启")]
	[NodeFrom("选择百通宝商品", Success = false)]
	[OperationNode("切换到云纹徽-周期")]
	public OperationRoundResult GoToCrest()
	{
		OperationRoundResult result;
		if (!base.Config.PawnshopCrestEnabled)
		{
			result = RoundSuccess("未开启");
		}
		else
		{
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? successDelay = SuibianTempleSubOperation.OneSecond;
			TimeSpan? retryDelay = SuibianTempleSubOperation.OneSecond;
			result = RoundByFindAndClickArea(lastScreenshot, "随便观-德丰大押", "按钮-云纹徽-周期", null, successDelay, retryDelay);
		}
		return result;
	}

	[NodeFrom("切换到云纹徽-周期")]
	[NodeFrom("购买云纹徽商品后处理")]
	[OperationNode("选择云纹徽商品", NodeMaxRetryTimes = 2)]
	public OperationRoundResult ChooseCrestGoods()
	{
		OperationRoundResult operationRoundResult = ChooseGoods(base.Config.PawnshopCrestPriority, SuibianTemplePawnshopCrestGoods.Options, _chosenGoods, skipUnlimitedInPriority: true, chooseAnyUnlimited: false);
		if (operationRoundResult.IsSuccess || !base.Config.PawnshopCrestUnlimitedDennyEnabled)
		{
			return operationRoundResult;
		}
		return ChooseGoods(Array.Empty<string>(), SuibianTemplePawnshopCrestGoods.Options, _chosenUnlimitedGoods, skipUnlimitedInPriority: false, chooseAnyUnlimited: true);
	}

	[NodeFrom("选择云纹徽商品")]
	[OperationNode("购买云纹徽商品")]
	public OperationRoundResult BuyCrestGoods()
	{
		return BuyGoods();
	}

	[NodeFrom("购买云纹徽商品")]
	[OperationNode("购买云纹徽商品后处理")]
	public OperationRoundResult AfterBuyCrestGoods()
	{
		return AfterBuy();
	}

	[NodeFrom("切换到云纹徽-周期", Status = "未开启")]
	[NodeFrom("购买百通宝商品", Success = false)]
	[NodeFrom("购买百通宝商品后处理", Success = false)]
	[NodeFrom("选择云纹徽商品", Success = false)]
	[NodeFrom("购买云纹徽商品", Success = false)]
	[NodeFrom("购买云纹徽商品后处理", Success = false)]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		return BackToEntry();
	}

	private OperationRoundResult ChooseGoods(IReadOnlyList<string> priority, IReadOnlyList<ConfigItem> options, List<string> chosen, bool skipUnlimitedInPriority, bool chooseAnyUnlimited)
	{
		IReadOnlyList<GoodsPosition> goodsPositions = GetGoodsPositions(options);
		foreach (string item in priority)
		{
			string optionLabel = SuibianTempleSubOperation.GetOptionLabel(options, item);
			foreach (GoodsPosition item2 in goodsPositions)
			{
				if (!string.Equals(item2.Name, optionLabel, StringComparison.Ordinal) || chosen.Contains<string>(item2.Name, StringComparer.Ordinal) || (skipUnlimitedInPriority && item2.Unlimited))
				{
					continue;
				}
				chosen.Add(item2.Name);
				ControllerBase? controller = base.ZContext.Controller;
				return (controller != null && controller.Click(item2.Position.Center)) ? RoundSuccess(item2.Name, null, SuibianTempleSubOperation.OneSecond) : RoundRetry("点击失败 " + item2.Name, null, SuibianTempleSubOperation.ShortDelay);
			}
		}
		if (chooseAnyUnlimited)
		{
			foreach (GoodsPosition item3 in goodsPositions)
			{
				if (!item3.Unlimited || chosen.Contains<string>(item3.Name, StringComparer.Ordinal))
				{
					continue;
				}
				chosen.Add(item3.Name);
				ControllerBase? controller2 = base.ZContext.Controller;
				return (controller2 != null && controller2.Click(item3.Position.Center)) ? RoundSuccess(item3.Name, null, SuibianTempleSubOperation.OneSecond) : RoundRetry("点击失败 " + item3.Name, null, SuibianTempleSubOperation.ShortDelay);
			}
		}
		return RoundRetry("未找到可购买商品", null, SuibianTempleSubOperation.ShortDelay);
	}

	private OperationRoundResult BuyGoods()
	{
		OperationRoundResult operationRoundResult = ClickText("[百通宝]数量不足", "[云纹徽]数量不足", "已达背包容量上限");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-德丰大押", "按钮-购买件数-最小");
		OneDragon.Core.Screen.ScreenArea area2 = base.ZContext.ScreenContext.GetArea("随便观-德丰大押", "按钮-购买件数-最大");
		if (area == null || area2 == null)
		{
			return RoundFail("区域未配置 按钮-购买件数");
		}
		base.ZContext.Controller?.DragTo(area2.Center + new OneDragon.Core.Abstractions.Geometry.Point(50, 0), area.Center, TimeSpan.FromSeconds(2L));
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = SuibianTempleSubOperation.OneSecond;
		TimeSpan? retryDelay = SuibianTempleSubOperation.OneSecond;
		return RoundByFindAndClickArea(lastScreenshot, "随便观-德丰大押", "按钮-确认", null, successDelay, retryDelay);
	}

	private OperationRoundResult AfterBuy()
	{
		if (CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "随便观-德丰大押" }) != null)
		{
			return RoundSuccess("随便观-德丰大押");
		}
		IReadOnlyList<IReadOnlyList<int>> colorRange = new IReadOnlyList<int>[2]
		{
			new int[3] { 170, 50, 40 },
			new int[3] { 200, 65, 50 }
		};
		if (GetAreaOcrResults("随便观-德丰大押", "区域-购买货币", colorRange).Any((OcrMatchResult result) => SuibianTempleSubOperation.ExtractPositiveDigits(result.Text).HasValue))
		{
			OperationRoundResult operationRoundResult = ClickArea("随便观-德丰大押", "按钮-兑换关闭");
			return RoundWait(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		OperationRoundResult operationRoundResult2 = ClickText("[百通宝]数量不足", "[云纹徽]数量不足", "已达背包容量上限", "确认");
		if (operationRoundResult2.IsSuccess && operationRoundResult2.Status == "确认")
		{
			return RoundWait(operationRoundResult2.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		ClickArea("随便观-德丰大押", "按钮-兑换关闭");
		return RoundRetry("未识别当前画面", null, SuibianTempleSubOperation.OneSecond);
	}

	private IReadOnlyList<GoodsPosition> GetGoodsPositions(IReadOnlyList<ConfigItem> options)
	{
		IReadOnlyList<OcrMatchResult> areaOcrResults = GetAreaOcrResults("随便观-德丰大押", "区域-商品列表");
		string[] array = options.Select((ConfigItem item) => item.Label).ToArray();
		List<GoodsPosition> list = new List<GoodsPosition>();
		foreach (OcrMatchResult item in areaOcrResults)
		{
			int? num = StringUtils.FindBestMatchByDifflib(item.Text, array);
			if (num.HasValue)
			{
				list.Add(new GoodsPosition(array[num.Value], item));
			}
		}
		foreach (OcrMatchResult item2 in areaOcrResults)
		{
			int? num2 = StringUtils.FindBestMatchByDifflib(item2.Text, new string[3] { "已售罄", "不限购", "限购x" });
			if (!num2.HasValue)
			{
				continue;
			}
			GoodsPosition goodsPosition = null;
			foreach (GoodsPosition item3 in list)
			{
				if (item2.Center.Y > item3.Position.Center.Y && (goodsPosition == null || CalUtils.DistanceBetween(item3.Position.Center, item2.Center) < CalUtils.DistanceBetween(goodsPosition.Position.Center, item2.Center)))
				{
					goodsPosition = item3;
				}
			}
			if (goodsPosition != null)
			{
				if (num2 == 0)
				{
					goodsPosition.SoldOut = true;
				}
				else if (num2 == 1)
				{
					goodsPosition.Unlimited = true;
				}
			}
		}
		return list.Where((GoodsPosition goods) => !goods.SoldOut).ToArray();
	}
}
