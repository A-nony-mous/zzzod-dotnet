using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleCraftDispatch : SuibianTempleSubOperation
{
	private static readonly IReadOnlyList<IReadOnlyList<int>> ItemNameColorRange = new IReadOnlyList<int>[2]
	{
		new int[3] { 230, 230, 230 },
		new int[3] { 255, 255, 255 }
	};

	private readonly bool _fromCraft;

	private readonly List<string> _chosenItems;

	private int _currentBangbooIndex = 1;

	private bool _bangbooDispatchClicked;

	private bool _doneCraft;

	private int _dragTimes;

	private List<string> _lastItemList = new List<string>();

	private bool _scrollAfterChoose;

	public SuibianTempleCraftDispatch(ZContext context, SuibianTempleConfig config, bool fromCraft, List<string> chosenItemList)
		: base(context, config, "随便观 制造派驻")
	{
		_fromCraft = fromCraft;
		_chosenItems = chosenItemList;
	}

	[OperationNode("检查邦布", IsStartNode = true, NodeMaxRetryTimes = 1)]
	public OperationRoundResult CheckBangboo()
	{
		return ClickText("邦布电量不足", "未选择邦布", "请先选择邦布");
	}

	[NodeFrom("检查邦布")]
	[OperationNode("打开选择邦布")]
	public OperationRoundResult OpenChooseBangboo()
	{
		_currentBangbooIndex = 2;
		return ClickArea("随便观-制造坊", "区域-选择邦布");
	}

	[NodeFrom("打开选择邦布")]
	[NodeFrom("点击派驻", Status = "无法派驻")]
	[OperationNode("选择邦布")]
	public OperationRoundResult ChooseBangboo()
	{
		OneDragon.Core.Screen.ScreenArea area;
		while (true)
		{
			if (_currentBangbooIndex > 8)
			{
				return RoundSuccess("没有合适邦布");
			}
			area = base.ZContext.ScreenContext.GetArea("随便观-制造坊", $"区域-邦布-{_currentBangbooIndex}");
			if (area == null)
			{
				return RoundFail($"区域未配置 区域-邦布-{_currentBangbooIndex}");
			}
			if (base.LastScreenshot == null || !base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot).Any((OcrMatchResult result) => IsWorkingBangbooStatus(result.Text) && CalUtils.CalculateOverlapPercent(result.Rect, area.Rect, result.Rect) > 0.7))
			{
				break;
			}
			_currentBangbooIndex++;
		}
		base.ZContext.Controller?.Click(area.Center);
		return RoundSuccess("已选择邦布");
	}

	[NodeFrom("选择邦布", Status = "已选择邦布")]
	[OperationNode("点击派驻")]
	public OperationRoundResult ClickBangbooDispatch()
	{
		if (_bangbooDispatchClicked)
		{
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
			OperationRoundResult operationRoundResult = RoundByFindArea(lastScreenshot, "随便观-制造坊", "按钮-街区", null, retryDelay);
			if (operationRoundResult.IsSuccess)
			{
				_currentBangbooIndex++;
				return RoundSuccess("无法派驻");
			}
			return RoundSuccess("派驻完成");
		}
		OperationRoundResult operationRoundResult2 = ClickText("确认派驻");
		if (operationRoundResult2.IsSuccess)
		{
			_bangbooDispatchClicked = true;
			return RoundWait(operationRoundResult2.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		return RoundRetry(operationRoundResult2.Status, null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("检查邦布", Success = false)]
	[NodeFrom("选择邦布", Status = "没有合适邦布")]
	[NodeFrom("点击派驻", Status = "派驻完成")]
	[OperationNode("选择商品")]
	public OperationRoundResult ChooseItem()
	{
		OperationRoundResult operationRoundResult = ClickText("所需材料不足", "邦布电量不足", "未选择邦布", "请先选择邦布");
		if (!operationRoundResult.IsSuccess)
		{
			return RoundSuccess("材料充足");
		}
		string status = operationRoundResult.Status;
		if ((status == "邦布电量不足" || status == "未选择邦布") ? true : false)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		if (!_fromCraft)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-制造坊", "区域-商品列表");
		if (area == null)
		{
			return RoundFail("区域未配置 区域-商品列表");
		}
		IReadOnlyList<OcrMatchResult> areaOcrResults = GetAreaOcrResults("随便观-制造坊", "区域-商品列表", ItemNameColorRange);
		List<OcrMatchResult> source = areaOcrResults.Where((OcrMatchResult result) => RemoveDigits(result.Text).Length > 0).ToList();
		IReadOnlyList<OcrMatchResult> areaOcrResults2 = GetAreaOcrResults("随便观-制造坊", "区域-商品列表");
		List<OneDragon.Core.Abstractions.Geometry.Rect> craftablePositions = (from result in areaOcrResults2
			where RemoveDigits(result.Text).Length > 0
			where StringUtils.FindBestMatchByDifflib(RemoveDigits(result.Text), new string[] { base.ZContext.GameTextResolver("可制造") }).HasValue
			select result.Rect).ToList();
		List<OcrMatchResult> list = source.Where((OcrMatchResult item) => craftablePositions.Any((OneDragon.Core.Abstractions.Geometry.Rect position) => position.Center.X > item.Center.X && Math.Abs(position.Center.Y - item.Center.Y) < 20)).ToList();
		foreach (OcrMatchResult item in list)
		{
			if (_chosenItems.Contains<string>(item.Text, StringComparer.Ordinal))
			{
				continue;
			}
			_scrollAfterChoose = false;
			_chosenItems.Add(item.Text);
			base.ZContext.Controller?.Click(item.RightBottom + new OneDragon.Core.Abstractions.Geometry.Point(50, 0));
			return RoundWait("选择下一个商品", null, SuibianTempleSubOperation.OneSecond);
		}
		List<string> list2 = source.Select((OcrMatchResult result) => result.Text).ToList();
		bool flag = list2.Any((string newItem) => !StringUtils.FindBestMatchByDifflib(newItem, _lastItemList).HasValue);
		_lastItemList = list2;
		if (_dragTimes >= base.Config.CraftDragTimes)
		{
			return RoundSuccess("已滑动次数达到上限", null, SuibianTempleSubOperation.OneSecond);
		}
		if (!flag && _scrollAfterChoose)
		{
			return RoundSuccess("未发现新商品", null, SuibianTempleSubOperation.OneSecond);
		}
		_dragTimes++;
		_scrollAfterChoose = true;
		base.ZContext.Controller?.DragTo(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, -300), area.Center);
		return RoundWait("滑动找未选择过的商品", null, SuibianTempleSubOperation.OneSecond);
	}

	private bool IsWorkingBangbooStatus(string text)
	{
		return new string[3] { "制造中", "游历中", "售卖中" }.Select(base.ZContext.GameTextResolver).Any((string target) => StringUtils.FindByLcs(target, text, 0.5));
	}

	private static string RemoveDigits(string text)
	{
		return new string(text.Where((char character) => !char.IsDigit(character)).ToArray());
	}

	[NodeFrom("选择商品", Status = "材料充足")]
	[OperationNode("点击开始制造")]
	public OperationRoundResult ClickStartCrafting()
	{
		OperationRoundResult operationRoundResult = ClickText("开始制造", "调整计划");
		_doneCraft = operationRoundResult.IsSuccess;
		return operationRoundResult;
	}

	[NodeFrom("选择商品", Success = false)]
	[NodeFrom("选择商品", Status = "未发现新商品")]
	[NodeFrom("选择商品", Status = "已滑动次数达到上限")]
	[NodeFrom("选择商品", Status = "所需材料不足")]
	[NodeFrom("选择商品", Status = "邦布电量不足")]
	[NodeFrom("选择商品", Status = "未选择邦布")]
	[NodeFrom("点击开始制造")]
	[NodeFrom("点击开始制造", Success = false)]
	[OperationNode("完成后返回")]
	public OperationRoundResult BackAtLast()
	{
		if (_fromCraft && CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "随便观-制造坊" }) != null)
		{
			return RoundSuccess("随便观-制造坊", _doneCraft);
		}
		if (!_fromCraft && RoundByFindArea(base.LastScreenshot, "随便观-饮茶仙", "按钮-制造").IsSuccess)
		{
			return RoundSuccess("按钮-制造", _doneCraft);
		}
		OperationRoundResult operationRoundResult = ClickText("确认");
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		OperationRoundResult operationRoundResult2 = FindAndClickArea("菜单", "返回");
		return operationRoundResult2.IsSuccess ? RoundWait(operationRoundResult2.Status, null, SuibianTempleSubOperation.OneSecond) : RoundRetry(operationRoundResult2.Status, false, SuibianTempleSubOperation.OneSecond);
	}
}
