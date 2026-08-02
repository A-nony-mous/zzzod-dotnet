using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战战斗流程。
/// </summary>
public sealed class ShiyuDefenseBattle : ZOperation
{
	/// <summary>需要移动。</summary>
	public const string StatusNeedSpecialMove = "需要移动";

	/// <summary>移动失败。</summary>
	public const string StatusFailToMove = "移动失败";

	/// <summary>战斗超时。</summary>
	public const string StatusBattleTimeout = "战斗超时";

	/// <summary>下一阶段。</summary>
	public const string StatusToNextPhase = "下一阶段";

	internal const string StatusWaitPrepare = "等待战斗准备";

	internal const string StatusWaitMove = "等待战斗后移动";

	internal const string StatusWaitInteract = "等待交互完成";

	internal const string StatusAutoBattleRunning = "自动战斗中";

	private readonly int _predefinedTeamIndex;

	private readonly IShiyuDefenseBattleServices _services;

	private string? _battleFail;

	/// <summary>
	/// 初始化战斗流程。
	/// </summary>
	public ShiyuDefenseBattle(ZContext context, int predefinedTeamIndex, IShiyuDefenseBattleServices? services = null)
		: base(context, "式舆防卫战 自动战斗")
	{
		_predefinedTeamIndex = predefinedTeamIndex;
		_services = services ?? new DefaultShiyuDefenseBattleServices();
	}

	/// <summary>
	/// 加载自动战斗指令。
	/// </summary>
	[OperationNode("加载自动战斗指令", IsStartNode = true)]
	public OperationRoundResult LoadAutoOperation()
	{
		_services.LoadAutoOperation(base.ZContext, _predefinedTeamIndex);
		return RoundSuccess();
	}

	/// <summary>
	/// 等待战斗画面加载。
	/// </summary>
	[NodeFrom("加载自动战斗指令")]
	[OperationNode("等待战斗画面加载", NodeMaxRetryTimes = 60)]
	public OperationRoundResult WaitBattleScreen()
	{
		// 对应 shiyu_defense_battle.py:58 的 retry_wait_round=1（补足制，非固定延时）。
		return _services.IsBattleScreenReady(base.ZContext, base.LastScreenshot) ? RoundSuccess("按键-普通攻击") : RoundRetry("未找到 按键-普通攻击", null, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 向前移动准备战斗。
	/// </summary>
	[NodeFrom("等待战斗画面加载")]
	[OperationNode("向前移动准备战斗")]
	public OperationRoundResult StartMove()
	{
		OperationResult operationResult = _services.PrepareBattle(base.ZContext, base.LastScreenshot);
		if (operationResult.IsSuccess)
		{
			return RoundSuccess(operationResult.Status);
		}
		if (string.Equals(operationResult.Status, "移动失败", StringComparison.Ordinal))
		{
			_battleFail = "移动失败";
			return RoundFail("移动失败");
		}
		TimeSpan value = (string.Equals(operationResult.Status, "等待战斗准备", StringComparison.Ordinal) ? TimeSpan.FromMilliseconds(20L) : TimeSpan.FromMilliseconds(500L));
		return RoundWait(operationResult.Status, null, value);
	}

	/// <summary>
	/// 自动战斗。
	/// </summary>
	[NodeFrom("向前移动准备战斗")]
	[NodeFrom("战斗后移动", Status = "返回战斗")]
	[OperationNode("自动战斗", TimeoutSeconds = 600.0, Mute = true)]
	public OperationRoundResult AutoBattle()
	{
		OperationResult operationResult = _services.RunAutoBattle(base.ZContext, base.LastScreenshot, base.LastScreenshotTimeUtc);
		if (!operationResult.IsSuccess && string.Equals(operationResult.Status, "自动战斗中", StringComparison.Ordinal))
		{
			return RoundWait(null, null, TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
		}
		if (!operationResult.IsSuccess && IsVisionPipelineFailure(operationResult.Status))
		{
			return RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		return RoundByOperationResult(operationResult);
	}

	/// <summary>
	/// 战斗后移动。
	/// </summary>
	[NodeFrom("自动战斗", Status = "需要移动")]
	[OperationNode("战斗后移动", NodeMaxRetryTimes = 5)]
	public OperationRoundResult MoveAfterBattle()
	{
		OperationResult operationResult = _services.MoveAfterBattle(base.ZContext, base.LastScreenshot);
		if (operationResult.IsSuccess)
		{
			return RoundSuccess(operationResult.Status);
		}
		if (string.Equals(operationResult.Status, "移动失败", StringComparison.Ordinal))
		{
			_battleFail = operationResult.Status;
			return RoundFail(operationResult.Status);
		}
		if (string.Equals(operationResult.Status, "等待战斗后移动", StringComparison.Ordinal) || string.Equals(operationResult.Status, "等待交互完成", StringComparison.Ordinal))
		{
			return RoundWait(operationResult.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		return RoundRetry(operationResult.Status);
	}

	/// <summary>
	/// 战斗超时。
	/// </summary>
	[NodeFrom("自动战斗", Success = false, Status = "执行超时")]
	[OperationNode("战斗超时")]
	public OperationRoundResult BattleTimeout()
	{
		_battleFail = "战斗超时";
		return RoundSuccess("战斗超时");
	}

	/// <summary>
	/// 主动退出。
	/// </summary>
	[NodeFrom("向前移动准备战斗", Success = false, Status = "移动失败")]
	[NodeFrom("战斗超时")]
	[NodeFrom("战斗后移动", Success = false)]
	[OperationNode("主动退出")]
	public OperationRoundResult VoluntaryExit()
	{
		if (_battleFail == null)
		{
			_battleFail = base.PreviousNode.Status ?? "移动失败";
		}
		_services.StopAutoBattle(base.ZContext);
		OperationResult operationResult = _services.PrepareVoluntaryExit(base.ZContext, base.LastScreenshot);
		if (!operationResult.IsSuccess)
		{
			return RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		TimeSpan value = (string.Equals(operationResult.Status, "退出战斗", StringComparison.Ordinal) ? TimeSpan.FromMilliseconds(500L) : TimeSpan.FromSeconds(1L));
		return RoundSuccess(operationResult.Status, null, value);
	}

	/// <summary>
	/// 点击退出。
	/// </summary>
	[NodeFrom("主动退出")]
	[OperationNode("点击退出")]
	public OperationRoundResult ClickExit()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "式舆防卫战", "退出战斗", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 点击退出确认。
	/// </summary>
	[NodeFrom("点击退出")]
	[OperationNode("点击退出确认")]
	public OperationRoundResult ClickExitConfirm()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "零号空洞-战斗", "退出战斗-确认", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 战斗失败撤退。
	/// </summary>
	[NodeFrom("自动战斗", Status = "战斗结束-撤退")]
	[OperationNode("战斗失败撤退")]
	public OperationRoundResult BattleFailExit()
	{
		_battleFail = "战斗结束-撤退";
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "式舆防卫战", "战斗结束-撤退", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 等待退出。
	/// </summary>
	[NodeFrom("点击退出确认")]
	[NodeFrom("战斗失败撤退")]
	[OperationNode("等待退出", NodeMaxRetryTimes = 60)]
	public OperationRoundResult WaitExit()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		OperationRoundResult operationRoundResult = RoundByFindArea(lastScreenshot, "式舆防卫战", "战报", null, retryDelay);
		return (operationRoundResult.IsSuccess && _battleFail != null) ? RoundFail(_battleFail) : operationRoundResult;
	}

	/// <summary>恢复暂停前正在执行的自动战斗。</summary>
	public void ResumeAutoBattle()
	{
		if (string.Equals(base.CurrentNode.Name, "自动战斗", StringComparison.Ordinal))
		{
			base.ZContext.AutoBattleContext.ResumeAutoBattle();
		}
	}

	private static bool IsVisionPipelineFailure(string? status)
	{
		return !string.IsNullOrWhiteSpace(status) && status.StartsWith("图像分析", StringComparison.Ordinal);
	}
}
