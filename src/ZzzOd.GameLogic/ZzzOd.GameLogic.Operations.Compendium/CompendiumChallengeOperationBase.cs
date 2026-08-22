using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations.ChallengeMission;
using ZzzOd.GameLogic.ScreenArea;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 快捷手册进入后的挑战流程基类。
/// </summary>
public abstract class CompendiumChallengeOperationBase : ZOperation
{
	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>电量计划条目。</summary>
	protected ChargePlanItem Plan { get; }

	/// <summary>电量计划配置。</summary>
	protected ChargePlanConfig Config { get; }

	/// <summary>子流程集合。</summary>
	protected ChallengeMissionServices Services { get; }

	/// <summary>电量不足状态。</summary>
	protected virtual string ChargeNotEnoughStatus => "电量不足";

	/// <summary>战斗超时状态。</summary>
	protected virtual string FightTimeoutStatus => "战斗超时";

	/// <summary>超时退出后等待的画面。</summary>
	protected virtual string TimeoutExitWaitScreenName => "战斗-挑战结果-失败";

	/// <summary>超时退出后等待的区域。</summary>
	protected virtual string TimeoutExitWaitAreaName => "按钮-退出";

	/// <summary>超时退出后是否还需要点击挑战结果页退出。</summary>
	protected virtual bool ClickResultExitAfterTimeout => true;

	private IZzzControllerActions? ControllerActions => base.ZContext.Controller as IZzzControllerActions;

	/// <summary>
	/// 初始化挑战流程。
	/// </summary>
	protected CompendiumChallengeOperationBase(ZContext context, string operationName, ChargePlanItem plan, ChargePlanConfig? config = null, ChallengeMissionServices? services = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, operationName)
	{
		Plan = plan;
		Config = config ?? ChargePlanConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.RunContext.CurrentGroupId ?? "one_dragon");
		Services = services ?? new ChallengeMissionServices();
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	/// <inheritdoc />
	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		base.ZContext.AutoBattleContext.StopAutoBattle();
		return Task.CompletedTask;
	}

	/// <summary>
	/// 等待挑战入口加载出挑战等级区域。
	/// </summary>
	[OperationNode("等待入口加载", IsStartNode = true, NodeMaxRetryTimes = 60)]
	protected virtual OperationRoundResult WaitEntryLoad()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "实战模拟室", "挑战等级", _retryDelay, _retryDelay);
		return operationRoundResult.IsSuccess ? RoundSuccess(operationRoundResult.Status, null, _retryDelay) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	/// <summary>
	/// 点击下一步并识别恢复电量或出战入口。
	/// 注意：不在基类声明来自"等待入口加载"的无状态兜底边——各子类对该节点的兜底去向不同
	/// （实战模拟室去"选择副本"、区域巡防直接来本节点），由需要的子类在重写方法上自行声明，
	/// 避免一个节点出现两条无状态兜底边导致解析依赖反射枚举顺序。
	/// </summary>
	[NodeFrom("恢复电量", Status = "恢复电量成功")]
	[OperationNode("下一步", NodeMaxRetryTimes = 10)]
	protected virtual OperationRoundResult ClickNext()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恢复电量", "标题-恢复电量");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(ChargeNotEnoughStatus);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "实战模拟室", "出战");
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status);
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult3 = RoundByFindAndClickArea(lastScreenshot, "实战模拟室", "下一步", preDelay, null, retryDelay);
		if (operationRoundResult3.IsSuccess)
		{
			Thread.Sleep(TimeSpan.FromMilliseconds(500L));
			base.ZContext.Controller?.MouseMove(ScreenNormalWorldEnum.Uid.Center);
			return RoundWait(operationRoundResult3.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		return RoundRetry(operationRoundResult3.Status, null, _retryDelay);
	}

	/// <summary>
	/// 电量不足时按配置执行恢复电量流程。
	/// </summary>
	[NodeFrom("下一步", Status = "电量不足")]
	[OperationNode("恢复电量")]
	protected virtual async Task<OperationRoundResult> RestoreCharge()
	{
		if (!Config.IsRestoreChargeEnabled)
		{
			return RoundSuccess(ChargeNotEnoughStatus);
		}
		OperationResult operationResult;
		if (Services.RestoreChargeAsync != null)
		{
			operationResult = await Services.RestoreChargeAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			ZContext zContext = base.ZContext;
			ChargePlanConfig config = Config;
			TimeSpan? retryDelay = _retryDelay;
			TimeSpan? preClickDelay = _preClickDelay;
			operationResult = await new RestoreCharge(zContext, config, retryDelay, preClickDelay).ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		OperationResult result = operationResult;
		return RoundByOperationResult(result);
	}

	/// <summary>
	/// 根据电量计划选择预备编队。
	/// </summary>
	[NodeFrom("下一步", Status = "出战")]
	[OperationNode("选择预备编队")]
	protected virtual async Task<OperationRoundResult> ChoosePredefinedTeam()
	{
		if (Plan.PredefinedTeamIndex == -1)
		{
			return RoundSuccess("无需选择预备编队");
		}
		OperationResult operationResult = ((Services.ChoosePredefinedTeamAsync == null) ? (await new ChoosePredefinedTeam(base.ZContext, new int[] { Plan.PredefinedTeamIndex }, _retryDelay, _preClickDelay).ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false)) : (await Services.ChoosePredefinedTeamAsync(base.ZContext, Plan).ConfigureAwait(continueOnCapturedContext: false)));
		OperationResult result = operationResult;
		return RoundByOperationResult(result);
	}

	/// <summary>
	/// 执行出战节点。
	/// </summary>
	[NodeFrom("选择预备编队")]
	[OperationNode("出战")]
	protected virtual async Task<OperationRoundResult> Deploy()
	{
		OperationResult operationResult = ((Services.DeployAsync == null) ? (await new Deploy(base.ZContext, _retryDelay, _preClickDelay).ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false)) : (await Services.DeployAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false)));
		OperationResult result = operationResult;
		return RoundByOperationResult(result);
	}

	/// <summary>加载本次计划对应的自动战斗指令。</summary>
	[NodeFrom("出战")]
	[NodeFrom("重新开始-确认")]
	[OperationNode("加载自动战斗指令")]
	protected virtual OperationRoundResult InitAutoBattle()
	{
		string text = ResolveAutoBattleName();
		if (Services.InitializeAutoBattle != null)
		{
			return RoundByOperationResult(Services.InitializeAutoBattle(base.ZContext, Plan, text));
		}
		try
		{
			base.ZContext.AutoBattleContext.StopAutoBattle();
			base.ZContext.AutoBattleContext.LastCheckEndResult = null;
			base.ZContext.AutoBattleContext.InitAutoOp(text);
			return RoundSuccess();
		}
		catch (InvalidOperationException ex)
		{
			return RoundFail(ex.Message);
		}
	}

	/// <summary>等待战斗 UI 加载。</summary>
	[NodeFrom("加载自动战斗指令")]
	[OperationNode("等待战斗画面加载", NodeMaxRetryTimes = 60)]
	protected virtual OperationRoundResult WaitBattleScreen()
	{
			// 重试时把本轮总时长补足到 1 秒，不追加固定延时。
		return RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-普通攻击", null, null, cropFirst: true, null, _retryDelay);
	}

	/// <summary>进入战斗前向前移动。</summary>
	[NodeFrom("等待战斗画面加载")]
	[OperationNode("向前移动准备战斗")]
	protected virtual Task<OperationRoundResult> MoveToBattle()
	{
		ControllerActions?.MoveW(press: true, TimeSpan.FromSeconds(1L), release: true);
		base.ZContext.AutoBattleContext.StartAutoBattle();
		return Task.FromResult(RoundSuccess());
	}

	/// <summary>启动自动战斗。</summary>
	[NodeFrom("战斗失败", Status = "战斗结果-倒带")]
	[OperationNode("开始自动战斗")]
	protected virtual OperationRoundResult StartAutoBattle()
	{
		base.ZContext.AutoBattleContext.StartAutoBattle();
		return RoundSuccess();
	}

	/// <summary>持续检查自动战斗状态。</summary>
	[NodeFrom("向前移动准备战斗")]
	[NodeFrom("开始自动战斗")]
	[OperationNode("自动战斗", TimeoutSeconds = 600.0)]
	protected virtual OperationRoundResult AutoBattle()
	{
		OperationResult operationResult = Services.BattleFlow.CheckBattleState(base.ZContext, Plan, ResolveAutoBattleName(), base.LastScreenshot, base.LastScreenshotTimeUtc);
		if (operationResult.IsSuccess)
		{
			return RoundSuccess(operationResult.Status);
		}
		if (string.Equals(operationResult.Status, "节点超时", StringComparison.Ordinal) || string.Equals(operationResult.Status, FightTimeoutStatus, StringComparison.Ordinal))
		{
			return RoundFail(operationResult.Status);
		}
		return RoundWait(operationResult.Status, null, TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
	}

	/// <summary>记录一次成功完成的计划。</summary>
	[NodeFrom("自动战斗")]
	[OperationNode("战斗结束")]
	protected virtual OperationRoundResult AfterBattle()
	{
		Config.AddPlanRunTimes(Plan);
		return RoundSuccess();
	}

	/// <summary>判断是否继续下一次挑战。</summary>
	[NodeFrom("战斗结束")]
	[OperationNode("判断下一次")]
	protected virtual async Task<OperationRoundResult> CheckNext()
	{
		ChooseNextOrFinishAfterBattle operation = new ChooseNextOrFinishAfterBattle(base.ZContext, Plan.PlanTimes > Plan.RunTimes, Plan.IsAgentPlan, Config, _retryDelay, _preClickDelay);
		return RoundByOperationResult(await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>为下一次挑战做确认，普通挑战不需要额外动作。</summary>
	[NodeFrom("判断下一次", Status = "战斗结果-再来一次")]
	[OperationNode("重新开始-确认")]
	protected virtual OperationRoundResult RestartConfirm()
	{
		return RoundSuccess();
	}

	/// <summary>处理自动战斗超时。</summary>
	[NodeFrom("自动战斗", Success = false)]
	[OperationNode("战斗超时")]
	protected virtual async Task<OperationRoundResult> BattleTimeout()
	{
		base.ZContext.AutoBattleContext.StopAutoBattle();
		ExitInBattle operation = new ExitInBattle(base.ZContext, TimeoutExitWaitScreenName, TimeoutExitWaitAreaName, _retryDelay, _preClickDelay);
		OperationResult result = await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			return RoundRetry(result.Status, null, _retryDelay);
		}
		return ClickResultExitAfterTimeout ? RoundSuccess(result.Status) : RoundFail(FightTimeoutStatus);
	}

	/// <summary>点击挑战结果页退出。需要该节点的子类在重写方法上自行声明节点与连线。</summary>
	protected virtual OperationRoundResult ClickResultExit()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(null, "战斗-挑战结果-失败", "按钮-退出", _preClickDelay, _retryDelay, _retryDelay, cropFirst: true, centerX: false, null, new (string, string)[] { ("战斗-挑战结果-失败", "按钮-退出") });
		return operationRoundResult.IsSuccess ? RoundFail(FightTimeoutStatus) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	/// <summary>处理普通战斗撤退页。</summary>
	[NodeFrom("自动战斗", Status = "普通战斗-撤退")]
	[OperationNode("战斗失败")]
	protected virtual OperationRoundResult BattleFail()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "战斗画面", "战斗结果-撤退", preDelay, null, retryDelay);
		return operationRoundResult.IsSuccess ? RoundSuccess(operationRoundResult.Status, null, TimeSpan.FromSeconds(5L)) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	/// <summary>解析本次使用的自动战斗配置。</summary>
	protected string ResolveAutoBattleName()
	{
		if (Plan.PredefinedTeamIndex < 0 || Plan.PredefinedTeamIndex >= base.ZContext.TeamConfig.TeamList.Count)
		{
			return Plan.AutoBattleConfig;
		}
		string autoBattle = base.ZContext.TeamConfig.TeamList[Plan.PredefinedTeamIndex].AutoBattle;
		return string.IsNullOrWhiteSpace(autoBattle) ? Plan.AutoBattleConfig : autoBattle;
	}
}
