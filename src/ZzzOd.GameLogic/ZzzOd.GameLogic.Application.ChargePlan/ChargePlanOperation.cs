using System;
using System.Globalization;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Operations;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Compendium;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划主流程。
/// </summary>
public sealed class ChargePlanOperation : ZOperation
{
	/// <summary>没有可运行的计划。</summary>
	public const string StatusNoPlan = "没有可运行的计划";

	/// <summary>已完成一轮计划。</summary>
	public const string StatusRoundFinished = "已完成一轮计划";

	private readonly ChargePlanConfig _config;

	private readonly ChargePlanRunRecord _runRecord;

	private readonly Func<ZContext, Task<OperationResult>> _gotoMenuAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _transportAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _combatSimulationAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _areaPatrolAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _expertChallengeAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _notoriousHuntAsync;

	private readonly Func<ZContext, Task<OperationResult>> _backToWorldAsync;

	private readonly Func<ZContext, int?> _chargePowerReader;

	private readonly Func<ZContext, int, Task<ChargePlanDoubleRewardResult>> _doubleRewardPlanAsync;

	private readonly Func<ZContext, Task<OperationResult>> _doubleRewardTransportAsync;

	private readonly Func<DateTimeOffset> _now;

	private int _chargePower;

	private ChargePlanItem? _tempPlan;

	private ChargePlanItem? _lastTriedPlan;

	private ChargePlanItem? _currentPlan;

	/// <summary>
	/// 初始化电量计划流程。
	/// </summary>
	public ChargePlanOperation(ZContext context, ChargePlanConfig config, ChargePlanRunRecord runRecord, Func<ZContext, Task<OperationResult>>? gotoMenuAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? transportAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? combatSimulationAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? areaPatrolAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? expertChallengeAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? notoriousHuntAsync = null, Func<ZContext, Task<OperationResult>>? backToWorldAsync = null, Func<ZContext, int?>? chargePowerReader = null, Func<ZContext, int, Task<ChargePlanDoubleRewardResult>>? doubleRewardPlanAsync = null, Func<ZContext, Task<OperationResult>>? doubleRewardTransportAsync = null, Func<DateTimeOffset>? now = null)
		: base(context, "体力刷本")
	{
		_config = config;
		_runRecord = runRecord;
		_gotoMenuAsync = gotoMenuAsync ?? ((Func<ZContext, Task<OperationResult>>)((ZContext ctx) => new GotoMenu(ctx).ExecuteAsync()));
		_transportAsync = transportAsync ?? new Func<ZContext, ChargePlanItem, Task<OperationResult>>(DefaultTransportAsync);
		_combatSimulationAsync = combatSimulationAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new CombatSimulation(ctx, plan, _config).ExecuteAsync()));
		_areaPatrolAsync = areaPatrolAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new AreaPatrol(ctx, plan, _config).ExecuteAsync()));
		_expertChallengeAsync = expertChallengeAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new ExpertChallenge(ctx, plan, _config).ExecuteAsync()));
		_notoriousHuntAsync = notoriousHuntAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new ZzzOd.GameLogic.Operations.Compendium.NotoriousHunt(ctx, plan, _config, null, useChargePower: true).ExecuteAsync()));
		_backToWorldAsync = backToWorldAsync ?? ((Func<ZContext, Task<OperationResult>>)((ZContext ctx) => new BackToNormalWorld(ctx).ExecuteAsync()));
		_chargePowerReader = chargePowerReader ?? new Func<ZContext, int?>(ReadChargePowerFromMenu);
		_doubleRewardPlanAsync = doubleRewardPlanAsync ?? new Func<ZContext, int, Task<ChargePlanDoubleRewardResult>>(DefaultDoubleRewardPlanAsync);
		_doubleRewardTransportAsync = doubleRewardTransportAsync ?? ((Func<ZContext, Task<OperationResult>>)((ZContext ctx) => new TransportByCompendium(ctx, "训练", "实战模拟室").ExecuteAsync()));
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
	}

	/// <summary>
	/// 开始体力计划。
	/// </summary>
	[OperationNode("开始体力计划", IsStartNode = true)]
	public OperationRoundResult StartChargePlan()
	{
		_tempPlan = null;
		_lastTriedPlan = null;
		foreach (ChargePlanItem plan in _config.PlanList)
		{
			plan.Skipped = false;
		}
		_config.TryResetPlanTimesByDt(GetCurrentDt());
		return RoundSuccess();
	}

	/// <summary>
	/// 打开菜单。
	/// </summary>
	[NodeFrom("挑战完成")]
	[NodeFrom("开始体力计划")]
	[NodeFrom("跳过或结束计划")]
	[OperationNode("打开菜单")]
	public async Task<OperationRoundResult> GotoMenu()
	{
		return RoundByOperationResult(await _gotoMenuAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 识别当前电量。
	/// </summary>
	[NodeFrom("打开菜单")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentFail)]
	[OperationNode("识别电量")]
	public OperationRoundResult CheckChargePower()
	{
		int? num = _chargePowerReader(base.ZContext);
		if (!num.HasValue)
		{
			return RoundRetry("未识别到电量", null, TimeSpan.FromSeconds(1L));
		}
		_chargePower = num.Value;
		_runRecord.RecordCurrentChargePower(_chargePower);
		return _config.DoubleReward ? RoundSuccess("查看双倍活动") : RoundSuccess($"剩余电量 {_chargePower}");
	}

	/// <summary>
	/// 检查实战模拟室双倍活动。
	/// </summary>
	[NodeFrom("识别电量", Status = "查看双倍活动")]
	[OperationNode("查看双倍活动")]
	public async Task<OperationRoundResult> CheckDoubleRewardEvent()
	{
		ChargePlanDoubleRewardResult result = await _doubleRewardPlanAsync(base.ZContext, _chargePower).ConfigureAwait(continueOnCapturedContext: false);
		_tempPlan = result.Plan;
		ChargePlanDoubleRewardResultKind kind = result.Kind;
		if (1 == 0)
		{
		}
		OperationRoundResult result2 = kind switch
		{
			ChargePlanDoubleRewardResultKind.Success => RoundSuccess(result.Status), 
			ChargePlanDoubleRewardResultKind.Retry => RoundRetry(result.Status, null, result.Delay), 
			ChargePlanDoubleRewardResultKind.Fail => RoundFail(result.Status), 
			_ => RoundFail(result.Status), 
		};
		if (1 == 0)
		{
		}
		return result2;
	}

	/// <summary>
	/// 查找下一个可执行计划。
	/// </summary>
	[NodeFrom("识别电量")]
	[NodeFrom("查看双倍活动")]
	[NodeFrom("查看双倍活动", Success = false)]
	[OperationNode("查找并选择下一个可执行任务")]
	public OperationRoundResult FindAndSelectNextPlan()
	{
		if (_tempPlan != null)
		{
			_currentPlan = _tempPlan;
			return RoundSuccess();
		}
		if (_config.AllPlanFinished())
		{
			if (!_config.Loop)
			{
				return RoundSuccess("已完成一轮计划");
			}
			_lastTriedPlan = null;
			_config.ResetPlans();
		}
		ChargePlanItem nextPlan;
		while (true)
		{
			nextPlan = _config.GetNextPlan(_lastTriedPlan);
			if (nextPlan == null)
			{
				return RoundFail("没有可运行的计划");
			}
			int estimatedChargePower = nextPlan.EstimatedChargePower;
			if (estimatedChargePower <= 0 || _chargePower >= estimatedChargePower)
			{
				break;
			}
			if (!_config.SkipPlan)
			{
				return RoundSuccess("已完成一轮计划");
			}
			_lastTriedPlan = nextPlan;
		}
		_currentPlan = nextPlan;
		return RoundSuccess();
	}

	/// <summary>
	/// 传送到计划副本。
	/// </summary>
	[NodeFrom("查找并选择下一个可执行任务")]
	[OperationNode("传送")]
	public async Task<OperationRoundResult> Transport()
	{
		if (_currentPlan == null)
		{
			return RoundFail("未选择计划");
		}
		return RoundByOperationResult(await _transportAsync(base.ZContext, _currentPlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 识别副本分类。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("识别副本分类")]
	public OperationRoundResult CheckMissionType()
	{
		return (_currentPlan == null) ? RoundFail("未选择计划") : RoundSuccess(_currentPlan.CategoryName);
	}

	/// <summary>
	/// 执行实战模拟室。
	/// </summary>
	[NodeFrom("识别副本分类", Status = "实战模拟室")]
	[OperationNode("实战模拟室")]
	public Task<OperationRoundResult> CombatSimulation()
	{
		return RunChallengeAsync(_combatSimulationAsync);
	}

	/// <summary>
	/// 执行区域巡防。
	/// </summary>
	[NodeFrom("识别副本分类", Status = "区域巡防")]
	[OperationNode("区域巡防")]
	public Task<OperationRoundResult> AreaPatrol()
	{
		return RunChallengeAsync(_areaPatrolAsync);
	}

	/// <summary>
	/// 执行专业挑战室。
	/// </summary>
	[NodeFrom("识别副本分类", Status = "专业挑战室")]
	[OperationNode("专业挑战室")]
	public Task<OperationRoundResult> ExpertChallenge()
	{
		return RunChallengeAsync(_expertChallengeAsync);
	}

	/// <summary>
	/// 执行恶名狩猎。
	/// </summary>
	[NodeFrom("识别副本分类", Status = "恶名狩猎")]
	[OperationNode("恶名狩猎")]
	public Task<OperationRoundResult> NotoriousHunt()
	{
		return RunChallengeAsync(_notoriousHuntAsync);
	}

	/// <summary>
	/// 挑战完成后更新本轮状态。
	/// </summary>
	[NodeFrom("实战模拟室", Success = true)]
	[NodeFrom("实战模拟室", Success = false)]
	[NodeFrom("区域巡防", Success = true)]
	[NodeFrom("区域巡防", Success = false)]
	[NodeFrom("专业挑战室", Success = true)]
	[NodeFrom("专业挑战室", Success = false)]
	[NodeFrom("恶名狩猎", Success = true)]
	[NodeFrom("恶名狩猎", Success = false)]
	[OperationNode("挑战完成")]
	public OperationRoundResult ChallengeComplete()
	{
		if (_currentPlan == null)
		{
			return RoundFail("未选择计划");
		}
		if (base.PreviousNode.IsSuccess)
		{
			_lastTriedPlan = null;
		}
		else
		{
			_currentPlan.Skipped = true;
			_lastTriedPlan = _currentPlan;
		}
		if (_currentPlan == _tempPlan)
		{
			_tempPlan = null;
		}
		return RoundSuccess();
	}

	/// <summary>
	/// 电量或次数不足时跳过当前计划或结束。
	/// </summary>
	[NodeFrom("实战模拟室", Status = "电量不足")]
	[NodeFrom("区域巡防", Status = "电量不足")]
	[NodeFrom("专业挑战室", Status = "电量不足")]
	[NodeFrom("恶名狩猎", Status = "电量不足")]
	[NodeFrom("恶名狩猎", Status = "周期挑战有剩余次数，本次跳过深度追猎")]
	[NodeFrom("实战模拟室", Status = "特训目标已达成")]
	[NodeFrom("区域巡防", Status = "特训目标已达成")]
	[NodeFrom("专业挑战室", Status = "特训目标已达成")]
	[NodeFrom("恶名狩猎", Status = "特训目标已达成")]
	[NodeFrom("传送", Success = false, Status = "找不到 代理人方案培养")]
	[OperationNode("跳过或结束计划")]
	public OperationRoundResult SkipPlanOrFinish()
	{
		if (_currentPlan == null)
		{
			return RoundSuccess("已完成一轮计划");
		}
		bool flag = base.PreviousNode.Status == "周期挑战有剩余次数，本次跳过深度追猎";
		if (_config.SkipPlan || _currentPlan.IsAgentPlan || flag)
		{
			_currentPlan.Skipped = true;
			_lastTriedPlan = _currentPlan;
			if (_currentPlan == _tempPlan)
			{
				_tempPlan = null;
			}
			return RoundSuccess();
		}
		_lastTriedPlan = null;
		if (_currentPlan == _tempPlan)
		{
			_tempPlan = null;
		}
		return RoundSuccess("已完成一轮计划");
	}

	/// <summary>
	/// 回到大世界。
	/// </summary>
	[NodeFrom("跳过或结束计划", Status = "已完成一轮计划")]
	[NodeFrom("查找并选择下一个可执行任务", Status = "已完成一轮计划")]
	[NodeFrom("查找并选择下一个可执行任务", Success = false)]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _backToWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false), $"剩余电量 {_chargePower}");
	}

	private async Task<OperationRoundResult> RunChallengeAsync(Func<ZContext, ChargePlanItem, Task<OperationResult>> runAsync)
	{
		if (_currentPlan == null)
		{
			return RoundFail("未选择计划");
		}
		return RoundByOperationResult(await runAsync(base.ZContext, _currentPlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	private int? ReadChargePowerFromMenu(ZContext context)
	{
		if (base.LastScreenshot == null)
		{
			return null;
		}
		return ReadPositiveDigitsFromArea(context, base.LastScreenshot, "菜单", "文本-电量");
	}

	private async Task<ChargePlanDoubleRewardResult> DefaultDoubleRewardPlanAsync(ZContext context, int chargePower)
	{
		OperationResult transport = await _doubleRewardTransportAsync(context).ConfigureAwait(continueOnCapturedContext: false);
		if (!transport.IsSuccess)
		{
			return ChargePlanDoubleRewardResult.Fail(transport.Status ?? "传送失败");
		}
		OperationRoundResult doubleEvent = RoundByFindArea(Screenshot(), "快捷手册", "每日怪物卡双倍掉落次数");
		if (!doubleEvent.IsSuccess)
		{
			return ChargePlanDoubleRewardResult.NoActivity();
		}
		ChargePlanDoubleRewardOcrResult ocr = ReadDoubleRewardTimesLeft(context, base.LastScreenshot);
		if (ocr.Kind == ChargePlanDoubleRewardOcrResultKind.Retry)
		{
			return ChargePlanDoubleRewardResult.Retry(ocr.Status);
		}
		if (ocr.TimesLeft <= 0)
		{
			return ChargePlanDoubleRewardResult.NoActivity();
		}
		int cardNum = Math.Min(chargePower / 20, ocr.TimesLeft);
		if (cardNum <= 0)
		{
			return ChargePlanDoubleRewardResult.NoActivity();
		}
		ChargePlanItem tempPlan = _config.CombatSimulationDoubleRewardConfig.Clone();
		tempPlan.Skipped = false;
		tempPlan.RunTimes = 1;
		tempPlan.PlanTimes = 1;
		tempPlan.CardNum = cardNum.ToString(CultureInfo.InvariantCulture);
		return ChargePlanDoubleRewardResult.WithPlan(tempPlan);
	}

	internal int? ReadChargePowerFromMenuForTesting(Mat screen)
	{
		return ReadPositiveDigitsFromArea(base.ZContext, screen, "菜单", "文本-电量");
	}

	internal ChargePlanDoubleRewardOcrResult ReadDoubleRewardTimesLeftForTesting(Mat screen)
	{
		return ReadDoubleRewardTimesLeft(base.ZContext, screen);
	}

	private static int? ReadPositiveDigitsFromArea(ZContext context, Mat screen, string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return null;
		}
		using Mat image = CvImageUtils.Crop(screen, area.Rect);
		string value = context.OcrService.RunOcrSingleLineForCrop(
			image,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1);
		return StringUtils.GetPositiveDigits(value);
	}

	private static ChargePlanDoubleRewardOcrResult ReadDoubleRewardTimesLeft(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return ChargePlanDoubleRewardOcrResult.Retry("双倍活动识别出错");
		}
		int? num = ReadPositiveDigitsFromArea(context, screen, "快捷手册", "怪物卡双倍剩余次数");
		if (!num.HasValue)
		{
			return ChargePlanDoubleRewardOcrResult.Retry("双倍活动识别出错");
		}
		int num2 = num.Value / 10;
		if (num2 == 0 || num.Value % 10 != 5)
		{
			return ChargePlanDoubleRewardOcrResult.NoActivity();
		}
		return (num2 > 5) ? ChargePlanDoubleRewardOcrResult.Retry("双倍活动识别出错") : ChargePlanDoubleRewardOcrResult.Activity(num2);
	}

	private string GetCurrentDt()
	{
		return _now().ToUniversalTime().ToOffset(TimeSpan.FromHours(base.ZContext.GameAccountConfig.GameRefreshHourOffset)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
	}

	private static Task<OperationResult> DefaultTransportAsync(ZContext context, ChargePlanItem plan)
	{
		return new TransportByCompendium(context, plan.TabName, plan.CategoryName, plan.MissionTypeName).ExecuteAsync();
	}
}
