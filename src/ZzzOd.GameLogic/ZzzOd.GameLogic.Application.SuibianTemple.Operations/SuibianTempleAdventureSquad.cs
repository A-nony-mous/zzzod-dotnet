using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleAdventureSquad : SuibianTempleSubOperation
{
	private readonly bool _claim;

	private readonly bool _dispatch;

	private readonly List<string> _missionList;

	private int _currentMissionIndex;

	public SuibianTempleAdventureSquad(ZContext context, SuibianTempleConfig config, bool claim = true, bool dispatch = true)
		: base(context, config, "随便观 游历")
	{
		_claim = claim;
		_dispatch = dispatch;
		int num = 5;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = string.Empty;
		span[1] = SuibianTempleSubOperation.GetOptionLabel(SuibianTempleAdventureMission.Options, config.AdventureMission1);
		span[2] = SuibianTempleSubOperation.GetOptionLabel(SuibianTempleAdventureMission.Options, config.AdventureMission2);
		span[3] = SuibianTempleSubOperation.GetOptionLabel(SuibianTempleAdventureMission.Options, config.AdventureMission3);
		span[4] = SuibianTempleSubOperation.GetOptionLabel(SuibianTempleAdventureMission.Options, config.AdventureMission4);
		_missionList = list;
	}

	[OperationNode("前往游历", IsStartNode = true)]
	public OperationRoundResult GoToAdventure()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = SuibianTempleSubOperation.OneSecond;
		TimeSpan? retryDelay = SuibianTempleSubOperation.OneSecond;
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("随便观-入口", "按钮-游历") };
		return RoundByFindAndClickArea(lastScreenshot, "随便观-入口", "按钮-游历", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
	}

	[NodeFrom("前往游历")]
	[NodeFrom("收获后重新派遣")]
	[NodeFrom("收获后重新派遣", Success = false)]
	[OperationNode("点击游历小队")]
	public OperationRoundResult ClickSquadTeam()
	{
		return _claim ? ClickText(SuibianTempleSubOperation.OneSecond, "游历小队") : RoundSuccess("跳过收获");
	}

	[NodeFrom("点击游历小队", Status = "游历小队")]
	[OperationNode("点击游历完成")]
	public OperationRoundResult ClickFinish()
	{
		return ClickText(SuibianTempleSubOperation.OneSecond, "游历完成", "游历小队");
	}

	[NodeFrom("点击游历完成", Status = "游历完成")]
	[OperationNode("点击可收获")]
	public OperationRoundResult ClickClaim()
	{
		return ClickText(SuibianTempleSubOperation.OneSecond, "可收获");
	}

	[NodeFrom("点击可收获", Status = "可收获")]
	[OperationNode("点击确认")]
	public OperationRoundResult ClickConfirm()
	{
		return ClickText(SuibianTempleSubOperation.OneSecond, "确认");
	}

	[NodeFrom("点击确认", Status = "确认")]
	[OperationNode("收获后重新派遣")]
	public OperationRoundResult ExecuteDispatchAfterClaim()
	{
		return _dispatch ? RunChild(new SuibianTempleAdventureDispatch(base.ZContext, base.Config, base.Config.AdventureDuration)) : RoundSuccess("跳过派遣");
	}

	[NodeFrom("点击游历小队", Status = "跳过收获")]
	[NodeFrom("点击游历完成", Success = false)]
	[NodeFrom("点击游历完成", Status = "游历小队")]
	[NodeFrom("选择新派遣", Status = "派遣成功")]
	[OperationNode("准备选择副本")]
	public OperationRoundResult PrepareToChooseMission()
	{
		if (!_dispatch)
		{
			return RoundSuccess("跳过派遣");
		}
		_currentMissionIndex++;
		return (_currentMissionIndex >= _missionList.Count) ? RoundSuccess("已完成所有副本选择") : RoundSuccess();
	}

	[NodeFrom("准备选择副本")]
	[OperationNode("选择副本")]
	public OperationRoundResult ChooseMission()
	{
		string text = _missionList[_currentMissionIndex];
		string text2;
		if (text.Length <= 2)
		{
			text2 = text;
		}
		else
		{
			string text3 = text;
			text2 = text3.Substring(0, text3.Length - 2);
		}
		string text4 = text2;
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = text4;
		List<string> list2 = list;
		List<string> list3 = new List<string>();
		foreach (ConfigItem option in SuibianTempleAdventureMission.Options)
		{
			string label = option.Label;
			string text5;
			if (label.Length <= 2)
			{
				text5 = label;
			}
			else
			{
				string text3 = label;
				text5 = text3.Substring(0, text3.Length - 2);
			}
			string text6 = text5;
			if (!string.Equals(text6, text4, StringComparison.Ordinal) && !list2.Contains<string>(text6, StringComparer.Ordinal))
			{
				list2.Add(text6);
				list3.Add(text6);
			}
		}
		// lcsPercent 使用框架默认值 0.6（按优先级列表匹配的场景不应沿用单目标匹配的 0.5 阈值）。
		OperationRoundResult operationRoundResult = RoundByOcrAndClickByPriority(base.LastScreenshot, list2, null, offset: new OneDragon.Core.Abstractions.Geometry.Point(0, -100), successDelay: SuibianTempleSubOperation.OneSecond, retryDelay: SuibianTempleSubOperation.OneSecond, colorRange: null, cropFirst: true, ignoreTextList: list3);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		Mat? lastScreenshot = base.LastScreenshot;
		int x = ((lastScreenshot != null) ? (lastScreenshot.Cols / 2) : 960);
		Mat? lastScreenshot2 = base.LastScreenshot;
		OneDragon.Core.Abstractions.Geometry.Point point = new OneDragon.Core.Abstractions.Geometry.Point(x, (lastScreenshot2 != null) ? (lastScreenshot2.Rows / 2) : 540);
		OneDragon.Core.Abstractions.Geometry.Point end = ((base.NodeRetryTimes % 2 == 0) ? (point + new OneDragon.Core.Abstractions.Geometry.Point(-800, 0)) : (point + new OneDragon.Core.Abstractions.Geometry.Point(800, 0)));
		base.ZContext.Controller?.DragTo(end, point);
		return RoundRetry("未识别到副本", null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("选择副本")]
	[OperationNode("选择子副本")]
	public OperationRoundResult ChooseSubMission()
	{
		string text = _missionList[_currentMissionIndex];
		object obj;
		if (text.Length <= 0)
		{
			obj = "1";
		}
		else
		{
			obj = text[text.Length - 1].ToString();
		}
		string text2 = (string)obj;
		return ClickArea("随便观-游历", "标题-子副本-" + text2, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("选择子副本")]
	[OperationNode("选择新派遣")]
	public OperationRoundResult ExecuteNewDispatch()
	{
		return RunChild(new SuibianTempleAdventureDispatch(base.ZContext, base.Config, base.Config.AdventureDuration));
	}

	[NodeFrom("选择新派遣")]
	[NodeFrom("准备选择副本", Status = "跳过派遣")]
	[NodeFrom("准备选择副本", Status = "已完成所有副本选择")]
	[NodeFrom("准备选择副本", Success = false)]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		return BackToEntry();
	}
}
