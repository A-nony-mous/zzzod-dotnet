using System;
using System.Collections.Generic;
using System.Threading;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleSalesStall : SuibianTempleSubOperation
{
	public SuibianTempleSalesStall(ZContext context, SuibianTempleConfig config)
		: base(context, config, "随便观 售卖铺")
	{
	}

	[OperationNode("前往售卖铺", IsStartNode = true)]
	public OperationRoundResult GoToSalesStall()
	{
		return GoToScreenByText("随便观-售卖铺", "经营", "售卖");
	}

	[NodeFrom("前往售卖铺")]
	[OperationNode("更换邦布")]
	public OperationRoundResult ChooseAnotherBangboo()
	{
		// 三次点击的返回结果均被丢弃，仅依赖显式同步等待还原点击节奏；
		// 三个操作对象在游戏内需要依次生效，点击过快会导致后续点击落空。
		ClickArea("随便观-售卖铺", "区域-选择邦布");
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		ClickArea("随便观-售卖铺", "区域-第二只邦布");
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		ClickArea("随便观-售卖铺", "按钮-确认派驻");
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		return RoundSuccess();
	}

	[NodeFrom("更换邦布")]
	[NodeFrom("取消售卖后返回售卖铺")]
	[OperationNode("选择库存不足货架", NodeMaxRetryTimes = 2)]
	public OperationRoundResult ChooseShelfWithNotEnough()
	{
		return ClickText("库存不足");
	}

	[NodeFrom("选择库存不足货架")]
	[OperationNode("点击取消售卖")]
	public OperationRoundResult CancelSelling()
	{
		IReadOnlyList<string> texts = new string[] { "取消售卖" };
		TimeSpan? retryDelay = TimeSpan.FromMilliseconds(500L);
		return ClickTextByPriority(texts, null, null, null, null, retryDelay);
	}

	[NodeFrom("点击取消售卖")]
	[OperationNode("取消售卖后返回售卖铺")]
	public OperationRoundResult BackFromCancelSelling()
	{
		if (CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "随便观-售卖铺" }) != null)
		{
			return RoundSuccess("随便观-售卖铺");
		}
		OperationRoundResult operationRoundResult = ClickArea("随便观-售卖铺", "按钮-返回");
		return RoundRetry(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("选择库存不足货架", Success = false)]
	[NodeFrom("点击开始售卖")]
	[OperationNode("选择货架开始售卖", NodeMaxRetryTimes = 2)]
	public OperationRoundResult ClickChooseShelfSell()
	{
		IReadOnlyList<string> texts = new string[2] { "开始售卖", "售卖铺" };
		IReadOnlyList<string> ignoreTexts = new string[] { "售卖铺" };
		return ClickTextByPriority(texts, null, null, ignoreTexts);
	}

	[NodeFrom("选择货架开始售卖")]
	[OperationNode("选择商品")]
	public OperationRoundResult ChooseItem()
	{
		OperationRoundResult operationRoundResult = ClickText("库存不足");
		return operationRoundResult.IsSuccess ? RoundSuccess("库存不足") : RoundSuccess("库存充足");
	}

	[NodeFrom("选择商品")]
	[OperationNode("点击开始售卖")]
	public OperationRoundResult ClickStartSelling()
	{
		IReadOnlyList<string> texts = new string[] { "开始售卖" };
		TimeSpan? retryDelay = SuibianTempleSubOperation.OneSecond;
		return ClickTextByPriority(texts, null, null, null, null, retryDelay);
	}

	[NodeFrom("选择货架开始售卖", Success = false)]
	[NodeFrom("选择商品", Status = "库存不足")]
	[NodeFrom("选择商品", Success = false)]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		return BackToEntry();
	}
}
