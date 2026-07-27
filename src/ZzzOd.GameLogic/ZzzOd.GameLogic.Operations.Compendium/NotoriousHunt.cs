using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
using ZzzOd.GameLogic.Application.NotoriousHunt;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations.ChallengeMission;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 恶名狩猎挑战流程。
/// </summary>
public sealed class NotoriousHunt : CompendiumChallengeOperationBase
{
	private const string DefaultLevel = "默认等级";

	private readonly bool _useChargePower;

	private readonly NotoriousHuntConfig _notoriousHuntConfig;

	private readonly NotoriousHuntRunRecord _runRecord;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private int _canRunTimes = -1;

	private int _leftTimesOcrRetries;

	/// <summary>周期挑战有剩余次数。</summary>
	public const string StatusWithLeftTimes = "周期挑战有剩余次数";

	/// <summary>周期挑战无剩余次数。</summary>
	public const string StatusNoLeftTimes = "周期挑战无剩余次数";

	/// <summary>周期挑战剩余次数阻塞深度追猎。</summary>
	public const string StatusBlockedByLeftTimes = "周期挑战有剩余次数，本次跳过深度追猎";

	/// <summary>电量不足。</summary>
	public const string StatusChargeNotEnough = "电量不足";

	/// <summary>战斗超时。</summary>
	public const string StatusFightTimeout = "战斗超时";

	/// <summary>
	/// 当前是否处于 BaselineParity `自动战斗` 节点。
	/// </summary>
	public bool IsAutoBattleNodeActive => string.Equals(base.CurrentNode.Name, "自动战斗", StringComparison.Ordinal);

	/// <summary>
	/// 仅在自动战斗节点恢复自动战斗。
	/// </summary>
	public void ResumeAutoBattle()
	{
		if (IsAutoBattleNodeActive)
		{
			base.ZContext.AutoBattleContext.ResumeAutoBattle();
		}
	}

	/// <summary>
	/// 初始化恶名狩猎挑战。
	/// </summary>
	public NotoriousHunt(ZContext context, ChargePlanItem plan, ChargePlanConfig? config = null, ChallengeMissionServices? services = null, bool useChargePower = false, NotoriousHuntConfig? notoriousHuntConfig = null, NotoriousHuntRunRecord? runRecord = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "恶名狩猎 " + plan.MissionTypeName, plan, config, services, retryDelay, preClickDelay)
	{
		_useChargePower = useChargePower;
		_notoriousHuntConfig = notoriousHuntConfig ?? NotoriousHuntConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = runRecord ?? NotoriousHuntRunRecord.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), _notoriousHuntConfig, context.GameAccountConfig.GameRefreshHourOffset);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	/// <summary>
	/// 加载迷失之地检测模型。
	/// </summary>
	[OperationNode("初始化加载", IsStartNode = true)]
	private OperationRoundResult InitForNotoriousHunt()
	{
		try
		{
			bool loaded = base.Services.LoadLostVoidDetectorModel?.Invoke(base.ZContext)
				?? base.ZContext.LostVoid.LoadLostVoidDetectorModel();
			return loaded ? RoundSuccess() : RoundFail("初始化失败");
		}
		catch (Exception)
		{
			return RoundFail("初始化失败");
		}
	}

	/// <inheritdoc />
	[NodeFrom("初始化加载")]
	[OperationNode("等待入口加载", NodeMaxRetryTimes = 60)]
	protected override OperationRoundResult WaitEntryLoad()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "当期剩余奖励次数", _retryDelay, _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status, null, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "按钮-街区", _retryDelay, _retryDelay);
		return operationRoundResult2.IsSuccess ? RoundSuccess(operationRoundResult2.Status, null, _retryDelay) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("等待入口加载", Status = "按钮-街区")]
	[OperationNode("判断副本名称")]
	private OperationRoundResult CheckMission()
	{
		if (base.Plan.IsAgentPlan)
		{
			return RoundSuccess();
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("恶名狩猎", "标题-副本名称");
		if (area == null)
		{
			return RoundFail("区域未配置 标题-副本名称");
		}
		if (base.LastScreenshot != null && base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect).Any((OcrMatchResult result) => MatchMissionType(base.Plan.MissionTypeName, result.Text)))
		{
			return RoundSuccess();
		}
		return RoundByClickArea("菜单", "返回", clickLeftTop: false, _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("等待入口加载", Status = "当期剩余奖励次数")]
	[NodeFrom("判断副本名称", Status = "返回")]
	[OperationNode("选择副本")]
	private OperationRoundResult ChooseMission()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("恶名狩猎", "副本名称列表");
		if (area == null)
		{
			return RoundFail("区域未配置 副本名称列表");
		}
		if (base.LastScreenshot != null)
		{
			IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect);
			OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult result) => MatchMissionType(base.Plan.MissionTypeName, result.Text));
			if (ocrMatchResult != null)
			{
				OneDragon.Core.Abstractions.Geometry.Point value = ocrMatchResult.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, 100);
				ControllerBase? controller = base.ZContext.Controller;
				if (controller != null && controller.Click(value))
				{
					return RoundSuccess(null, null, TimeSpan.FromSeconds(2L));
				}
			}
		}
		DragMissionList(area);
		// 对应 notorious_hunt.py:175 的 wait_round_time=2（补足制，非固定延时）。
		return RoundRetry("未能识别" + base.Plan.MissionTypeName, null, null, TimeSpan.FromSeconds(2L));
	}

	[NodeFrom("判断副本名称")]
	[NodeFrom("选择副本")]
	[OperationNode("抉择恶名狩猎", NodeMaxRetryTimes = 10)]
	private OperationRoundResult DecideNotoriousHunt()
	{
		if (_useChargePower)
		{
			return RoundSuccess("深度追猎");
		}
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "深度追猎-信息");
		if (operationRoundResult.IsSuccess)
		{
			_canRunTimes = 0;
			_runRecord.UpdateLeftTimes(0);
			return RoundSuccess("周期挑战无剩余次数");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("恶名狩猎", "剩余次数");
		string value = ((base.LastScreenshot == null || area == null) ? string.Empty : RunOcrSingleLineInArea(base.LastScreenshot, area));
		int? positiveDigits = StringUtils.GetPositiveDigits(value);
		if (!positiveDigits.HasValue)
		{
			if (_leftTimesOcrRetries++ < 10)
			{
				return RoundRetry("未识别到剩余次数", null, TimeSpan.FromMilliseconds(500L));
			}
			_canRunTimes = _runRecord.LeftTimes;
		}
		else
		{
			_leftTimesOcrRetries = 0;
			_runRecord.UpdateLeftTimes(positiveDigits.Value);
			_canRunTimes = positiveDigits.Value;
		}
		int val = Math.Max(0, base.Plan.PlanTimes - base.Plan.RunTimes);
		_canRunTimes = Math.Min(_canRunTimes, val);
		return RoundSuccess("周期挑战有剩余次数");
	}

	[NodeFrom("抉择恶名狩猎", Status = "深度追猎")]
	[OperationNode("抉择深度追猎")]
	private OperationRoundResult DecideByUsePower()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "恶名狩猎", "按钮-深度追猎-确认", _preClickDelay, _retryDelay, _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "深度追猎-信息");
		if (!operationRoundResult2.IsSuccess)
		{
			return RoundSuccess("周期挑战有剩余次数，本次跳过深度追猎");
		}
		OperationRoundResult operationRoundResult3 = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "按钮-深度追猎-ON");
		if (operationRoundResult3.IsSuccess)
		{
			return RoundSuccess("周期挑战无剩余次数");
		}
		OperationRoundResult operationRoundResult4 = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "按钮-无报酬模式");
		if (operationRoundResult4.IsSuccess)
		{
			RoundByClickArea("恶名狩猎", "按钮-深度追猎-ON", clickLeftTop: false, _preClickDelay);
			return RoundWait(operationRoundResult4.Status, null, _retryDelay);
		}
		return RoundRetry(operationRoundResult2.Status, null, _retryDelay);
	}

	[NodeFrom("抉择恶名狩猎", Status = "周期挑战有剩余次数")]
	[NodeFrom("抉择深度追猎", Status = "周期挑战无剩余次数")]
	[OperationNode("选择难度")]
	private OperationRoundResult ChooseLevel()
	{
		if (base.Plan.Level == "默认等级")
		{
			return RoundSuccess();
		}
		RoundByClickArea("恶名狩猎", "难度选择入口", clickLeftTop: false, _preClickDelay);
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("恶名狩猎", "难度选择区域");
		Mat? screen = Screenshot();
		string level = base.Plan.Level;
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByOcrAndClick(screen, level, area, 0.6, null, successDelay, retryDelay);
		Mat? screen2 = Screenshot();
		string level2 = base.Plan.Level;
		retryDelay = _retryDelay;
		RoundByOcrAndClick(screen2, level2, area, 0.6, null, retryDelay);
		return operationRoundResult.IsSuccess ? operationRoundResult : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	/// <inheritdoc />
	[NodeFrom("选择难度")]
	[NodeFrom("恢复电量", Status = "恢复电量成功")]
	[OperationNode("下一步", NodeMaxRetryTimes = 10)]
	protected override OperationRoundResult ClickNext()
	{
		return base.ClickNext();
	}

	/// <inheritdoc />
	[NodeFrom("选择预备编队")]
	[OperationNode("出战")]
	protected override Task<OperationRoundResult> Deploy()
	{
		// 对应 notorious_hunt.py:312-315 的 success_wait=1（固定）+ retry_wait_round=1（补足制）。
		OperationRoundResult result = RoundByFindAndClickArea(base.LastScreenshot, "实战模拟室", "出战", _preClickDelay, _retryDelay, null, retryDelayUntilRoundTime: _retryDelay);
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	[NodeFrom("加载自动战斗指令")]
	[OperationNode("等待战斗画面加载", NodeMaxRetryTimes = 60)]
	protected override OperationRoundResult WaitBattleScreen()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-普通攻击");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(base.Plan.MissionTypeName);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-交互");
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(base.Plan.MissionTypeName);
		}
		return RoundRetry(operationRoundResult2.Status, null, _retryDelay);
	}

	/// <inheritdoc />
	[NodeFrom("等待战斗画面加载")]
	[OperationNode("向前移动准备战斗", TimeoutSeconds = 120.0)]
	protected override async Task<OperationRoundResult> MoveToBattle()
	{
		OperationResult operationResult = ((base.Services.BeforeBattleMoveAsync == null) ? (await new NotoriousHuntMove(base.ZContext, base.Plan.NotoriousHuntBuffNum).ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false)) : (await base.Services.BeforeBattleMoveAsync(base.ZContext, base.Plan).ConfigureAwait(continueOnCapturedContext: false)));
		OperationResult moveResult = operationResult;
		return RoundByOperationResult(moveResult);
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

	/// <inheritdoc />
	[NodeFrom("自动战斗", Status = "普通战斗-撤退")]
	[OperationNode("战斗失败")]
	protected override OperationRoundResult BattleFail()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "战斗画面", "战斗结果-倒带", _preClickDelay, _retryDelay, _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			base.ZContext.AutoBattleContext.LastCheckEndResult = null;
			return RoundSuccess(operationRoundResult.Status, null, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(base.LastScreenshot, "战斗画面", "战斗结果-撤退", _preClickDelay, _retryDelay, _retryDelay);
		return operationRoundResult2.IsSuccess ? RoundSuccess(operationRoundResult2.Status, null, _retryDelay) : RoundRetry(operationRoundResult2.Status, null, _retryDelay);
	}

	[NodeFrom("战斗失败", Status = "战斗结果-撤退")]
	[OperationNode("战斗失败退出")]
	private OperationRoundResult BattleFailExit()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "战斗画面", "战斗结果-退出", _preClickDelay, TimeSpan.FromSeconds(10L), _retryDelay);
		return operationRoundResult.IsSuccess ? RoundFail(operationRoundResult.Status, null, TimeSpan.FromSeconds(10L)) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	/// <summary>战前移动失败或自动战斗超时后退出战斗。</summary>
	[NodeFrom("向前移动准备战斗", Success = false)]
	[NodeFrom("自动战斗", Success = false)]
	[OperationNode("退出战斗")]
	protected override async Task<OperationRoundResult> BattleTimeout()
	{
		base.ZContext.AutoBattleContext.StopContext();
		ExitInBattle operation = new ExitInBattle(base.ZContext, "战斗-挑战结果-失败", "按钮-退出", _retryDelay, _preClickDelay);
		OperationResult result = await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess(result.Status) : RoundRetry(result.Status, null, _retryDelay);
	}

	/// <inheritdoc />
	[NodeFrom("退出战斗")]
	[OperationNode("点击挑战结果退出")]
	protected override OperationRoundResult ClickResultExit()
	{
		return base.ClickResultExit();
	}

	/// <inheritdoc />
	[NodeFrom("自动战斗")]
	[OperationNode("战斗结束")]
	protected override OperationRoundResult AfterBattle()
	{
		_canRunTimes--;
		if (_useChargePower)
		{
			base.Config.AddPlanRunTimes(base.Plan);
		}
		else
		{
			_runRecord.UpdateLeftTimes(_runRecord.LeftTimes - 1);
			_notoriousHuntConfig.AddPlanRunTimes(base.Plan);
		}
		return RoundSuccess();
	}

	/// <inheritdoc />
	[NodeFrom("战斗结束")]
	[OperationNode("判断下一次")]
	protected override async Task<OperationRoundResult> CheckNext()
	{
		ChooseNextOrFinishAfterBattle operation = new ChooseNextOrFinishAfterBattle(tryNext: _useChargePower ? (base.Plan.PlanTimes > base.Plan.RunTimes) : (_canRunTimes > 0), context: base.ZContext, isAgentPlan: base.Plan.IsAgentPlan, config: base.Config, retryDelay: _retryDelay, preClickDelay: _preClickDelay);
		OperationResult result = await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (string.Equals(result.Status, "战斗结果-完成", StringComparison.Ordinal) && _canRunTimes > 0)
		{
			_runRecord.UpdateLeftTimes(0);
		}
		return RoundByOperationResult(result);
	}

	/// <inheritdoc />
	[NodeFrom("判断下一次", Status = "战斗结果-再来一次")]
	[OperationNode("重新开始-确认")]
	protected override OperationRoundResult RestartConfirm()
	{
		if (_useChargePower)
		{
			return RoundSuccess();
		}
		// 对应 notorious_hunt.py:448-449 的 success_wait=1（固定）+ retry_wait_round=1（补足制）。
		return RoundByFindAndClickArea(base.LastScreenshot, "恶名狩猎", "重新开始-确认", _preClickDelay, _retryDelay, null, retryDelayUntilRoundTime: _retryDelay);
	}

	[NodeFrom("判断下一次", Status = "战斗结果-完成")]
	[OperationNode("等待返回入口", NodeMaxRetryTimes = 60)]
	private OperationRoundResult WaitBackToEntry()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "剩余奖励次数");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(null, null, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "按钮-街区");
		return operationRoundResult2.IsSuccess ? RoundSuccess(null, null, _retryDelay) : RoundRetry(operationRoundResult2.Status, null, _retryDelay);
	}

	private bool MatchMissionType(string targetName, string ocrResult)
	{
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = targetName;
		List<string> list2 = list;
		foreach (CompendiumMissionType missionTypeListDatum in base.ZContext.CompendiumService.GetMissionTypeListData("训练", "恶名狩猎"))
		{
			if (missionTypeListDatum.MissionTypeName == targetName || missionTypeListDatum.AliasList.Contains<string>(targetName, StringComparer.Ordinal))
			{
				list2.Add(missionTypeListDatum.MissionTypeName);
				list2.AddRange(missionTypeListDatum.AliasList);
				break;
			}
		}
		return list2.Distinct<string>(StringComparer.Ordinal).Select(base.ZContext.GameTextResolver).Any((string name) => StringUtils.FindByLcs(name, ocrResult, 0.5));
	}

	private void DragMissionList(OneDragon.Core.Screen.ScreenArea area)
	{
		if (base.ZContext.Controller == null)
		{
			return;
		}
		IReadOnlyList<CompendiumMissionType> missionTypeListData = base.ZContext.CompendiumService.GetMissionTypeListData("训练", "恶名狩猎");
		bool flag = false;
		foreach (CompendiumMissionType missionType in missionTypeListData.AsEnumerable().Reverse())
		{
			if (MatchMissionType(base.Plan.MissionTypeName, missionType.MissionTypeName))
			{
				break;
			}
			if (base.LastScreenshot != null && base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect).Any((OcrMatchResult result) => StringUtils.FindByLcs(base.ZContext.GameTextResolver(missionType.MissionTypeName), result.Text, 0.5)))
			{
				flag = true;
			}
		}
		OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
		OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(flag ? (-500) : 500, 0);
		base.ZContext.Controller.DragTo(end, center);
	}

	private string RunOcrSingleLineInArea(Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		using Mat image = new Mat(screen, new OpenCvSharp.Rect(area.X1, area.Y1, area.Width, area.Height));
		return base.ZContext.OcrService.RunOcrSingleLineForCrop(
			image,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1);
	}
}
