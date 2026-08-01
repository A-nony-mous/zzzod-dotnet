using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 实战模拟室挑战流程。
/// </summary>
public sealed class CombatSimulation : CompendiumChallengeOperationBase
{
	private const double MissionNameCutoff = 0.8;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private int _scrollCount;

	/// <summary>需要选择类型。</summary>
	public const string StatusNeedType = "需选择类型";

	/// <summary>选择成功。</summary>
	public const string StatusChooseSuccess = "选择成功";

	/// <summary>选择失败。</summary>
	public const string StatusChooseFail = "选择失败";

	/// <summary>电量不足。</summary>
	public const string StatusChargeNotEnough = "电量不足";

	/// <summary>战斗超时。</summary>
	public const string StatusFightTimeout = "战斗超时";

	/// <inheritdoc />
	protected override string TimeoutExitWaitScreenName => "画面-通用";

	/// <inheritdoc />
	protected override string TimeoutExitWaitAreaName => "左上角-区域";

	/// <inheritdoc />
	protected override bool ClickResultExitAfterTimeout => false;

	/// <summary>
	/// 初始化实战模拟室挑战。
	/// </summary>
	public CombatSimulation(ZContext context, ChargePlanItem plan, ChargePlanConfig? config = null, ChallengeMissionServices? services = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "实战模拟室 " + (plan.MissionName ?? plan.MissionTypeName), plan, config, services, retryDelay, preClickDelay)
	{
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	/// <inheritdoc />
	protected override OperationRoundResult WaitEntryLoad()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "实战模拟室", "挑战等级", _retryDelay, _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(base.Plan.MissionTypeName, null, _retryDelay);
		}
		return IsInCategoryScreen() ? RoundSuccess("需选择类型") : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("等待入口加载", Status = "自定义模板")]
	[OperationNode("自定义模版的返回")]
	private OperationRoundResult BackForDiv()
	{
		if (IsInCategoryScreen())
		{
			return RoundSuccess();
		}
		OperationRoundResult operationRoundResult = RoundByClickArea("菜单", "返回", clickLeftTop: false, _preClickDelay, _retryDelay, _retryDelay);
		return operationRoundResult.IsSuccess ? RoundRetry("尝试返回副本类型列表", null, _retryDelay) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("等待入口加载", Status = "需选择类型")]
	[NodeFrom("自定义模版的返回")]
	[OperationNode("选择类型")]
	private OperationRoundResult ChooseMissionType()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("实战模拟室", "副本类型列表");
		Mat? lastScreenshot = base.LastScreenshot;
		string missionTypeName = base.Plan.MissionTypeName;
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByOcrAndClick(lastScreenshot, missionTypeName, area, 0.5, null, successDelay, retryDelay);
	}

	[NodeFrom("等待入口加载")]
	[NodeFrom("选择类型")]
	[OperationNode("选择副本")]
	private OperationRoundResult ChooseMission()
	{
		if (_scrollCount > 10)
		{
			_scrollCount = 0;
			return RoundFail("选择失败");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("实战模拟室", base.Plan.IsAgentPlan ? "副本名称列表顶部" : "副本名称列表");
		if (area == null)
		{
			return RoundFail("区域未配置 副本名称列表");
		}
		if (base.Plan.IsAgentPlan)
		{
			OneDragon.Core.Abstractions.Geometry.Point? point = ((base.LastScreenshot == null) ? ((OneDragon.Core.Abstractions.Geometry.Point?)null) : CompendiumChooseMissionType.ResolveAgentPlanTargetByImage(base.LastScreenshot, area, new OneDragon.Core.Abstractions.Geometry.Point(0, 80)));
			if (point.HasValue)
			{
				ControllerBase? controller = base.ZContext.Controller;
				if (controller != null && controller.Click(point.Value))
				{
					return RoundSuccess("选择成功", null, _retryDelay);
				}
			}
			OneDragon.Core.Abstractions.Geometry.Point point2 = area.Center + new OneDragon.Core.Abstractions.Geometry.Point(-100, 0);
			OneDragon.Core.Abstractions.Geometry.Point end = point2 + new OneDragon.Core.Abstractions.Geometry.Point(-400, 0);
			base.ZContext.Controller?.DragTo(end, point2);
			_scrollCount++;
			return RoundRetry("找不到 " + base.Plan.MissionName, null, _retryDelay);
		}
		if (string.IsNullOrWhiteSpace(base.Plan.MissionName))
		{
			return RoundSuccess("选择成功");
		}
		if (base.LastScreenshot != null)
		{
			IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect);
			List<string> targetWords = ocrResultList.Select((OcrMatchResult result) => result.Text).ToList();
			string word = base.ZContext.GameTextResolver(base.Plan.MissionName);
			int? num = StringUtils.FindBestMatchByDifflib(word, targetWords, 0.8);
			if (num.HasValue)
			{
				OcrMatchResult ocrMatchResult = ocrResultList[num.Value];
				OneDragon.Core.Abstractions.Geometry.Point value = ocrMatchResult.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, 50);
				ControllerBase? controller2 = base.ZContext.Controller;
				if (controller2 != null && controller2.Click(value))
				{
					return RoundSuccess("选择成功", null, _retryDelay);
				}
			}
		}
		DragMissionList(area);
		_scrollCount++;
		return RoundRetry("找不到 " + base.Plan.MissionName, null, _retryDelay);
	}

	[NodeFrom("选择副本", Status = "选择成功")]
	[OperationNode("进入选择数量")]
	private OperationRoundResult ClickCard()
	{
		if (base.Plan.CardNum == "默认数量")
		{
			return RoundSuccess(base.Plan.CardNum);
		}
		return RoundByClickArea("实战模拟室", "外层-卡片1", clickLeftTop: false, _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("进入选择数量")]
	[OperationNode("选择数量")]
	private OperationRoundResult ChooseCardNum()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "实战模拟室", "保存方案", _retryDelay, _retryDelay);
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry(operationRoundResult.Status, null, _retryDelay);
		}
		for (int i = 0; i < 5; i++)
		{
			RoundByClickArea("实战模拟室", "内层-已选择卡片1");
			Thread.Sleep(TimeSpan.FromMilliseconds(500L));
		}
		int result;
		int num = (int.TryParse(base.Plan.CardNum, out result) ? Math.Clamp(result, 0, 5) : 0);
		for (int j = 0; j < num; j++)
		{
			RoundByClickArea("实战模拟室", "内层-卡片1");
			Thread.Sleep(TimeSpan.FromMilliseconds(500L));
		}
		return RoundByFindAndClickArea(base.LastScreenshot, "实战模拟室", "保存方案", _preClickDelay, TimeSpan.FromSeconds(2L), _retryDelay);
	}

	/// <inheritdoc />
	[NodeFrom("进入选择数量", Status = "默认数量")]
	[NodeFrom("选择数量")]
	[NodeFrom("恢复电量", Status = "恢复电量成功")]
	[OperationNode("下一步", NodeMaxRetryTimes = 10)]
	protected override OperationRoundResult ClickNext()
	{
		return base.ClickNext();
	}

	/// <inheritdoc />
	[NodeFrom("等待战斗画面加载")]
	[OperationNode("向前移动准备战斗")]
	protected override Task<OperationRoundResult> MoveToBattle()
	{
		if (base.ZContext.Controller is IZzzControllerActions zzzControllerActions)
		{
			zzzControllerActions.MoveW(press: true, TimeSpan.FromSeconds(1L), release: true);
		}
		return Task.FromResult(RoundSuccess());
	}

	/// <inheritdoc />
	[NodeFrom("向前移动准备战斗")]
	[NodeFrom("战斗失败", Status = "战斗结果-倒带")]
	[OperationNode("开始自动战斗")]
	protected override OperationRoundResult StartAutoBattle()
	{
		return base.StartAutoBattle();
	}

	/// <inheritdoc />
	[NodeFrom("开始自动战斗")]
	[OperationNode("自动战斗", TimeoutSeconds = 600.0)]
	protected override OperationRoundResult AutoBattle()
	{
		return base.AutoBattle();
	}

	private bool IsInCategoryScreen()
	{
		if (base.LastScreenshot == null)
		{
			return false;
		}
		List<string> targetWords = (from item in base.ZContext.CompendiumService.GetMissionTypeListData("训练", "实战模拟室")
			select base.ZContext.GameTextResolver(item.MissionTypeName)).ToList();
		if (targetWords.Count == 0)
		{
			return false;
		}
		int num = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot).Count((OcrMatchResult result) => StringUtils.FindBestMatchByDifflib(result.Text, targetWords).HasValue);
		return num >= 3;
	}

	private void DragMissionList(OneDragon.Core.Screen.ScreenArea area)
	{
		if (base.ZContext.Controller != null)
		{
			List<string> orderedNames = (from item in base.ZContext.CompendiumService.GetMissionListData(base.Plan.TabName, base.Plan.CategoryName, base.Plan.MissionTypeName)
				select base.ZContext.GameTextResolver(item.MissionName)).ToList();
			List<string> ocrWords = ((base.LastScreenshot == null) ? new List<string>() : (from item in base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect)
				select item.Text).ToList());
			bool flag = IsTargetAfterOcrList(base.ZContext.GameTextResolver(base.Plan.MissionName ?? string.Empty), orderedNames, ocrWords);
			OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
			OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(flag ? (-400) : 400, 0);
			base.ZContext.Controller.DragTo(end, center);
		}
	}

	private static bool IsTargetAfterOcrList(string? target, IReadOnlyList<string> orderedNames, IReadOnlyList<string> ocrWords)
	{
		if (string.IsNullOrWhiteSpace(target) || orderedNames.Count == 0)
		{
			return false;
		}
		bool flag = false;
		bool flag2 = false;
		foreach (string orderedName in orderedNames)
		{
			if (string.Equals(orderedName, target, StringComparison.Ordinal))
			{
				flag = true;
				break;
			}
			if (StringUtils.FindBestMatchByDifflib(orderedName, ocrWords, 0.8).HasValue)
			{
				flag2 = true;
			}
		}
		return flag && flag2;
	}
}
