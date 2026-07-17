using System;
using System.Collections.Generic;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleAdventureDispatch : SuibianTempleSubOperation
{
	public const string StatusCantDispatch = "无法完成派遣";

	private readonly string _duration;

	private bool _chosenDuration;

	public SuibianTempleAdventureDispatch(ZContext context, SuibianTempleConfig config, string duration)
		: base(context, config, "随便观-游历派遣")
	{
		_duration = duration;
	}

	[OperationNode("检查画面", IsStartNode = true)]
	public OperationRoundResult CheckScreen()
	{
		return EnsureScreen("随便观-游历");
	}

	[NodeFrom("检查画面")]
	[OperationNode("选择游历时间")]
	public OperationRoundResult ChoosePeriod()
	{
		string optionLabel = SuibianTempleSubOperation.GetOptionLabel(SuibianTempleAdventureDispatchDuration.Options, _duration);
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		if (!_chosenDuration)
		{
			list.Add(optionLabel);
			foreach (ConfigItem option in SuibianTempleAdventureDispatchDuration.Options)
			{
				if (!string.Equals(option.Label, optionLabel, StringComparison.Ordinal))
				{
					list.Add(option.Label);
					list2.Add(option.Label);
				}
			}
		}
		list.Add("确认");
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-游历", "弹窗-游历时间选择");
		IReadOnlyList<string> ignoreTexts = list2;
		OperationRoundResult operationRoundResult = ClickTextByPriority(list, area, null, ignoreTexts);
		if (operationRoundResult.IsSuccess)
		{
			if (operationRoundResult.Status == "确认")
			{
				return RoundSuccess("确认", null, SuibianTempleSubOperation.OneSecond);
			}
			_chosenDuration = true;
			return RoundWait(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		OperationRoundResult operationRoundResult2 = RoundByOcr(lastScreenshot, "提前收获", null, 0.5, null, retryDelay);
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess("提前收获", null, SuibianTempleSubOperation.OneSecond);
		}
		ClickArea("随便观-游历", "按钮-选择时间");
		return RoundRetry("未识别弹窗", null, TimeSpan.FromSeconds(2L));
	}

	[NodeFrom("选择游历时间", Status = "确认")]
	[OperationNode("游历时间弹窗确认", NodeMaxRetryTimes = 1)]
	public OperationRoundResult ChoosePeriodConfirmDialog()
	{
		return ClickText("确认");
	}

	[NodeFrom("游历时间弹窗确认")]
	[NodeFrom("游历时间弹窗确认", Success = false)]
	[OperationNode("点击自动选择邦布")]
	public OperationRoundResult ClickAutoChoose()
	{
		return ClickArea("随便观-游历", "按钮-自动选择邦布");
	}

	[NodeFrom("点击自动选择邦布")]
	[OperationNode("点击派遣")]
	public OperationRoundResult ClickDispatch()
	{
		IReadOnlyList<string> texts = new string[3] { "邦布电量不足", "派遣", "可派遣小队" };
		IReadOnlyList<string> ignoreTexts = new string[] { "可派遣小队" };
		TimeSpan? retryDelay = SuibianTempleSubOperation.OneSecond;
		return ClickTextByPriority(texts, null, null, ignoreTexts, null, retryDelay);
	}

	[NodeFrom("点击派遣", Status = "派遣")]
	[OperationNode("点击派遣弹窗确认", NodeMaxRetryTimes = 1)]
	public OperationRoundResult ClickDispatchConfirmDialog()
	{
		return ClickText("确认");
	}

	[NodeFrom("选择游历时间", Status = "提前收获")]
	[OperationNode("已派遣")]
	public OperationRoundResult AlreadyDispatch()
	{
		return RoundSuccess("已派遣");
	}

	[NodeFrom("点击派遣", Status = "邦布电量不足")]
	[NodeFrom("点击派遣弹窗确认", Status = "确认")]
	[OperationNode("无法派遣")]
	public OperationRoundResult CantDispatch()
	{
		return RoundSuccess("无法完成派遣");
	}

	[NodeFrom("点击派遣弹窗确认", Success = false)]
	[OperationNode("派遣成功")]
	public OperationRoundResult DispatchSuccess()
	{
		return RoundSuccess("派遣成功");
	}
}
