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

	/// <summary>继续查找下一个计划。</summary>
	public const string StatusFindNextPlan = "继续查找下一个计划";

	private readonly ChargePlanConfig _config;

	private readonly ChargePlanRunRecord _runRecord;

	private readonly Func<ZContext, Task<OperationResult>> _backToWorldBeforeCompendiumAsync;

	private readonly Func<ZContext, Task<OperationResult>>? _openCompendiumAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _transportAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _combatSimulationAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _areaPatrolAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _expertChallengeAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _notoriousHuntAsync;

	private readonly Func<ZContext, Task<OperationResult>> _backToWorldAsync;

	private readonly Func<ZContext, ChargePlanResourceReading?> _resourceReader;

	private readonly Func<ZContext, int, Task<ChargePlanDoubleRewardResult>> _doubleRewardPlanAsync;

	private readonly Func<ZContext, Task<OperationResult>> _doubleRewardTransportAsync;

	private readonly Func<DateTimeOffset> _now;

	private int _batteryCharge;

	private int _backupBatteryCharge;

	private int _etherBattery;

	private bool _doubleRewardChecked;

	private ChargePlanItem? _tempPlan;

	private ChargePlanItem? _lastTriedPlan;

	private ChargePlanItem? _currentPlan;

	/// <summary>
	/// 初始化电量计划流程。
	/// </summary>
	public ChargePlanOperation(ZContext context, ChargePlanConfig config, ChargePlanRunRecord runRecord, Func<ZContext, Task<OperationResult>>? backToWorldBeforeCompendiumAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? transportAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? combatSimulationAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? areaPatrolAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? expertChallengeAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? notoriousHuntAsync = null, Func<ZContext, Task<OperationResult>>? backToWorldAsync = null, Func<ZContext, ChargePlanResourceReading?>? resourceReader = null, Func<ZContext, int, Task<ChargePlanDoubleRewardResult>>? doubleRewardPlanAsync = null, Func<ZContext, Task<OperationResult>>? doubleRewardTransportAsync = null, Func<DateTimeOffset>? now = null, Func<ZContext, Task<OperationResult>>? openCompendiumAsync = null)
		: base(context, "体力刷本")
	{
		_config = config;
		_runRecord = runRecord;
		_backToWorldBeforeCompendiumAsync = backToWorldBeforeCompendiumAsync ?? ((Func<ZContext, Task<OperationResult>>)((ZContext ctx) => new BackToNormalWorld(ctx, ensureNormalWorld: true).ExecuteAsync()));
		_openCompendiumAsync = openCompendiumAsync;
		_transportAsync = transportAsync ?? new Func<ZContext, ChargePlanItem, Task<OperationResult>>(DefaultTransportAsync);
		_combatSimulationAsync = combatSimulationAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new CombatSimulation(ctx, plan, _config).ExecuteAsync()));
		_areaPatrolAsync = areaPatrolAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new AreaPatrol(ctx, plan, _config).ExecuteAsync()));
		_expertChallengeAsync = expertChallengeAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new ExpertChallenge(ctx, plan, _config).ExecuteAsync()));
		_notoriousHuntAsync = notoriousHuntAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new ZzzOd.GameLogic.Operations.Compendium.NotoriousHunt(ctx, plan, _config, null, useChargePower: true).ExecuteAsync()));
		_backToWorldAsync = backToWorldAsync ?? ((Func<ZContext, Task<OperationResult>>)((ZContext ctx) => new BackToNormalWorld(ctx).ExecuteAsync()));
		_resourceReader = resourceReader ?? new Func<ZContext, ChargePlanResourceReading?>(ReadResourcesFromCompendium);
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
		_doubleRewardChecked = false;
		foreach (ChargePlanItem plan in _config.PlanList)
		{
			plan.Skipped = false;
		}
		_config.TryResetPlanTimesByDt(GetCurrentDt());
		return RoundSuccess();
	}

	/// <summary>
	/// 打开快捷手册前先回到大世界。
	/// </summary>
	[NodeFrom("挑战完成")]
	[NodeFrom("开始体力计划")]
	[NodeFrom("跳过或结束计划", Status = StatusFindNextPlan)]
	[OperationNode("前往大世界")]
	public async Task<OperationRoundResult> BackBeforeOpenCompendium()
	{
		return RoundByOperationResult(await _backToWorldBeforeCompendiumAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 打开快捷手册训练页。
	/// </summary>
	[NodeFrom("前往大世界")]
	[OperationNode("打开快捷手册")]
	public async Task<OperationRoundResult> OpenCompendium()
	{
		if (_openCompendiumAsync != null)
		{
			return RoundByOperationResult(await _openCompendiumAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
		}
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByGotoScreen(null, "快捷手册-训练", null, null, retryDelay);
	}

	/// <summary>
	/// 识别快捷手册资源栏中的电量、储蓄电量和以太电池。
	/// </summary>
	[NodeFrom("打开快捷手册")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentFail)]
	[OperationNode("识别电量")]
	public OperationRoundResult CheckBatteryCharge()
	{
		ChargePlanResourceReading? reading = _resourceReader(base.ZContext);
		if (reading == null)
		{
			return RoundRetry("未识别到电量", null, TimeSpan.FromSeconds(1L));
		}
		_batteryCharge = reading.BatteryCharge;
		_backupBatteryCharge = reading.BackupBatteryCharge;
		_etherBattery = reading.EtherBattery;
		_runRecord.RecordCurrentChargePower(_batteryCharge);
		base.ZContext.Logger.Information("剩余电量 {BatteryCharge} 储蓄电量 {BackupBatteryCharge} 以太电池 {EtherBattery}", _batteryCharge, _backupBatteryCharge, _etherBattery);
		if (_config.DoubleReward && !_doubleRewardChecked)
		{
			_doubleRewardChecked = true;
			return RoundSuccess("查看双倍活动");
		}
		return RoundSuccess("查找候选计划");
	}

	/// <summary>
	/// 检查实战模拟室双倍活动。
	/// </summary>
	[NodeFrom("识别电量", Status = "查看双倍活动")]
	[OperationNode("查看双倍活动")]
	public async Task<OperationRoundResult> CheckDoubleRewardEvent()
	{
		ChargePlanDoubleRewardResult result = await _doubleRewardPlanAsync(base.ZContext, _batteryCharge).ConfigureAwait(continueOnCapturedContext: false);
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
	/// 查找计划列表中的下一个候选计划；是否执行交给后续节点判断。
	/// </summary>
	[NodeFrom("识别电量", Status = "查找候选计划")]
	[NodeFrom("查看双倍活动")]
	[NodeFrom("查看双倍活动", Success = false)]
	[NodeFrom("判断是否执行", Status = StatusFindNextPlan)]
	[OperationNode("查找候选计划")]
	public OperationRoundResult FindNextPlan()
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
		ChargePlanItem nextPlan = _config.GetNextPlan(_lastTriedPlan);
		if (nextPlan == null)
		{
			return RoundFail("没有可运行的计划");
		}
		_currentPlan = nextPlan;
		return RoundSuccess();
	}

	/// <summary>
	/// 判断候选计划是否执行：电量足够或储蓄/以太可覆盖缺口才放行。
	/// </summary>
	[NodeFrom("查找候选计划")]
	[OperationNode("判断是否执行")]
	public OperationRoundResult CheckBeforeTransport()
	{
		if (_currentPlan == null)
		{
			return RoundFail("未选择计划");
		}
		if (_currentPlan == _tempPlan)
		{
			return RoundSuccess();
		}
		// 未知类型会返回 0，交给副本内流程继续判断真实消耗
		int needBatteryCharge = _currentPlan.EstimatedChargePower;
		if (needBatteryCharge <= 0 || _batteryCharge >= needBatteryCharge)
		{
			return RoundSuccess();
		}
		if (CanRestoreCharge(needBatteryCharge - _batteryCharge))
		{
			return RoundSuccess();
		}
		if (!_config.SkipPlan)
		{
			return RoundSuccess("已完成一轮计划");
		}
		_currentPlan.Skipped = true;
		_lastTriedPlan = _currentPlan;
		return RoundSuccess(StatusFindNextPlan);
	}

	private bool CanRestoreCharge(int requiredCharge)
	{
		if (!_config.IsRestoreChargeEnabled)
		{
			return false;
		}
		RestoreChargeMode mode = RestoreChargeMode.FromDisplayName(_config.RestoreCharge);
		bool backupCovers = (mode == RestoreChargeMode.BackupOnly || mode == RestoreChargeMode.Both) && _backupBatteryCharge >= requiredCharge;
		bool etherCovers = (mode == RestoreChargeMode.EtherOnly || mode == RestoreChargeMode.Both) && _etherBattery * 60 >= requiredCharge;
		return backupCovers || etherCovers;
	}

	/// <summary>
	/// 传送到计划副本。
	/// </summary>
	[NodeFrom("判断是否执行")]
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
			return RoundSuccess(StatusFindNextPlan);
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
	[NodeFrom("查找候选计划", Status = "已完成一轮计划")]
	[NodeFrom("查找候选计划", Success = false)]
	[NodeFrom("判断是否执行", Status = "已完成一轮计划")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _backToWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false), $"剩余电量 {_batteryCharge}");
	}

	private async Task<OperationRoundResult> RunChallengeAsync(Func<ZContext, ChargePlanItem, Task<OperationResult>> runAsync)
	{
		if (_currentPlan == null)
		{
			return RoundFail("未选择计划");
		}
		return RoundByOperationResult(await runAsync(base.ZContext, _currentPlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	private ChargePlanResourceReading? ReadResourcesFromCompendium(ZContext context)
	{
		if (base.LastScreenshot == null)
		{
			return null;
		}
		return ReadResourcesFromCompendium(context, base.LastScreenshot);
	}

	private static ChargePlanResourceReading? ReadResourcesFromCompendium(ZContext context, Mat screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("快捷手册", "资源栏");
		if (area == null)
		{
			return null;
		}
		using Mat part = CvImageUtils.Crop(screen, area.Rect);
		System.Collections.Generic.IReadOnlyList<int> lower = area.ColorRangeLower;
		System.Collections.Generic.IReadOnlyList<int> upper = area.ColorRangeUpper;
		using Mat mask = new Mat();
		Cv2.InRange(part, new Scalar(lower[0], lower[1], lower[2]), new Scalar(upper[0], upper[1], upper[2]), mask);
		using Mat colored = new Mat();
		Cv2.CvtColor(mask, colored, ColorConversionCodes.GRAY2RGB);
		// 整栏文字检测会被图标、分隔线和“/240”干扰，导致漏识或串位；
		// 资源栏右对齐、数字变长向左推移，三个字段按电量 3 位、储蓄电量 4 位、以太电池 3 位的上限预留，互不串入，
		// 因此按固定偏移切成三个互不相交的字段后分别做单行识别。
		(int X1, int Y1, int X2, int Y2)[] fieldOffsets = new (int, int, int, int)[3]
		{
			(75, 8, 225, 72),
			(275, 8, 410, 72),
			(425, 8, 535, 72),
		};
		int?[] values = new int?[3];
		for (int i = 0; i < fieldOffsets.Length; i++)
		{
			(int x1, int y1, int x2, int y2) = fieldOffsets[i];
			using Mat field = new Mat(colored, new OpenCvSharp.Rect(x1, y1, x2 - x1, y2 - y1));
			string text = context.OcrService.RunOcrSingleLine(field, null, strictOneLine: true);
			values[i] = StringUtils.GetPositiveDigits(text);
		}
		if (!values[0].HasValue || !values[1].HasValue || !values[2].HasValue)
		{
			return null;
		}
		return new ChargePlanResourceReading(values[0].Value, values[1].Value, values[2].Value);
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

	internal ChargePlanResourceReading? ReadResourcesForTesting(Mat screen)
	{
		return ReadResourcesFromCompendium(base.ZContext, screen);
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
