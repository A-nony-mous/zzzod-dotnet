using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手节点图。
/// </summary>
public sealed class CommissionAssistantOperation : ZOperation
{
	private const string StatusFishingDone = "钓鱼结束";

	/// <summary>自动战斗模式状态。</summary>
	public const string StatusAutoBattleMode = "自动战斗模式";

	/// <summary>检测剧情模式状态。</summary>
	public const string StatusStoryMode = "检测剧情模式";

	/// <summary>钓鱼状态。</summary>
	public const string StatusFishing = "钓鱼";

	/// <summary>切换自动对话模式状态。</summary>
	public const string StatusSwitchDialogMode = "切换自动对话模式";

	/// <summary>未知画面等待状态。</summary>
	public const string StatusDialogAfterUnknown = "等待重新检测";

	private readonly CommissionAssistantConfig _config;

	private readonly CommissionAssistantRuntimeState _state;

	private readonly ICommissionAssistantOperationServices _services;

	/// <summary>
	/// 初始化委托助手节点图。
	/// </summary>
	public CommissionAssistantOperation(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, ICommissionAssistantOperationServices? services = null)
		: base(context, "委托助手")
	{
		_config = config;
		_state = state;
		_services = services ?? new DefaultCommissionAssistantOperationServices();
	}

	/// <summary>
	/// 委托助手主节点。
	/// </summary>
	[NodeFrom("委托助手")]
	[NodeFrom("自动战斗模式")]
	[NodeFrom("剧情模式")]
	[NodeFrom("未知画面")]
	[NodeFrom("钓鱼")]
	[NodeFrom("钓鱼", Success = false)]
	[OperationNode("委托助手", IsStartNode = true)]
	public OperationRoundResult DialogMode()
	{
		if (_services.NeedPauseInBackground(base.ZContext, _config))
		{
			return RoundWait("等待游戏切换至前台", null, TimeSpan.FromSeconds(1L));
		}
		int runMode = _state.RunMode;
		if ((uint)(runMode - 1) <= 1u)
		{
			LoadAutoOp();
			return RoundSuccess("自动战斗模式");
		}
		Mat screen = Screenshot();
		OperationResult operationResult = _services.ClickDialogConfirm(base.ZContext, screen);
		if (operationResult.IsSuccess)
		{
			return RoundWait(operationResult.Status, null, TimeSpan.FromMilliseconds(100L));
		}
		if (_services.IsInteractVisible(base.ZContext, base.LastScreenshot))
		{
			return RoundWait("战斗画面-按键-交互", null, TimeSpan.FromSeconds(1L));
		}
		string text = _services.CheckCurrentWorldScreen(base.ZContext, base.LastScreenshot);
		if (text != null)
		{
			return RoundWait(text, null, TimeSpan.FromSeconds(1L));
		}
		if (_services.IsSecondaryMenuVisible(base.ZContext, base.LastScreenshot))
		{
			return RoundWait("处于二级界面, 等待用户操作", null, TimeSpan.FromSeconds(1L));
		}
		OperationResult operationResult2 = _services.HandleHollow(base.ZContext, base.LastScreenshot, base.LastScreenshotTimeUtc);
		if (operationResult2.IsSuccess)
		{
			return RoundWait(operationResult2.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		if (!string.Equals(operationResult2.Status, "未在空洞中", StringComparison.Ordinal))
		{
			return RoundWait(operationResult2.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		OperationResult operationResult3 = _services.ClickHollowFinished(base.ZContext, base.LastScreenshot);
		if (operationResult3.IsSuccess)
		{
			return RoundWait(operationResult3.Status, null, TimeSpan.FromSeconds(1L));
		}
		return RoundSuccess("检测剧情模式");
	}

	/// <summary>
	/// 自动战斗模式。
	/// </summary>
	[NodeFrom("委托助手", Status = "自动战斗模式")]
	[OperationNode("自动战斗模式")]
	public OperationRoundResult AutoMode()
	{
		if (_state.RunMode == 0)
		{
			_services.StopAutoBattle(base.ZContext);
			return RoundSuccess();
		}
		if (base.LastScreenshot == null || base.LastScreenshot.Empty())
		{
			return RoundRetry("未获取截图");
		}
		_services.CheckBattleState(base.ZContext, base.LastScreenshot, base.LastScreenshotTimeUtc);
		return RoundWaitForScreenshotRound(TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
	}

	/// <summary>
	/// 剧情模式。
	/// </summary>
	[NodeFrom("委托助手", Status = "检测剧情模式")]
	[OperationNode("剧情模式", NodeMaxRetryTimes = 5)]
	public OperationRoundResult StoryMode()
	{
		if (_services.NeedPauseInBackground(base.ZContext, _config))
		{
			return RoundWait("等待游戏切换至前台", null, TimeSpan.FromSeconds(1L));
		}
		if (_state.RunMode != 0)
		{
			return RoundSuccess("切换自动对话模式");
		}
		OperationResult operationResult = _services.HandleStoryMode(base.ZContext, _config, _state, base.LastScreenshot);
		if (!operationResult.IsSuccess && string.Equals(operationResult.Status, "需要重截图确认", StringComparison.Ordinal))
		{
			Mat screen = Screenshot();
			operationResult = _services.HandleSkipStoryConfirm(base.ZContext, _state, screen);
		}
		if (operationResult.IsSuccess)
		{
			return RoundWait(operationResult.Status, null, (operationResult.Data as TimeSpan?) ?? TimeSpan.FromMilliseconds(100L));
		}
		OperationResult operationResult2 = _services.WaitSecondaryMenu(base.ZContext, base.LastScreenshot);
		if (operationResult2.IsSuccess)
		{
			return RoundWait(operationResult2.Status, null, TimeSpan.FromSeconds(1L));
		}
		OperationResult operationResult3 = _services.CheckGameTutorial(base.ZContext, base.LastScreenshot);
		if (operationResult3.IsSuccess)
		{
			return RoundWait(operationResult3.Status, null, TimeSpan.FromSeconds(1L));
		}
		OperationResult operationResult4 = _services.HandleKnockKnock(base.ZContext, base.LastScreenshot);
		if (operationResult4.IsSuccess)
		{
			return RoundWait(operationResult4.Status, null, TimeSpan.FromMilliseconds(300L));
		}
		OperationResult operationResult5 = _services.CheckFishing(base.ZContext, base.LastScreenshot, _state);
		if (operationResult5.IsSuccess)
		{
			return RoundSuccess("钓鱼", null, TimeSpan.FromMilliseconds(100L));
		}
		OperationResult operationResult6 = _services.DoDialogClick(base.ZContext, _config, _state, base.LastScreenshot, !string.Equals(_config.StoryMode, CommissionAssistantStoryMode.Skip.Value, StringComparison.Ordinal));
		if (operationResult6.IsSuccess)
		{
			return RoundWait(operationResult6.Status, null, (operationResult6.Data as TimeSpan?) ?? TimeSpan.FromSeconds(_config.DialogClickInterval));
		}
		return RoundRetry(operationResult6.Status ?? "未知画面", null, TimeSpan.FromMilliseconds(200L));
	}

	/// <summary>
	/// 未知画面等待。
	/// </summary>
	[NodeFrom("剧情模式", Success = false)]
	[OperationNode("未知画面", ScreenshotBeforeRound = false)]
	public OperationRoundResult SleepAfterEmptyScreen()
	{
		_state.DialogClicked = false;
		return RoundSuccess("等待重新检测", null, TimeSpan.FromSeconds(_config.SleepAfterEmptyScreen));
	}

	/// <summary>
	/// 钓鱼流程。
	/// </summary>
	[NodeFrom("剧情模式", Status = "钓鱼")]
	[OperationNode("钓鱼", NodeMaxRetryTimes = 50)]
	public OperationRoundResult OnFishing()
	{
		OperationResult operationResult = _services.HandleFishing(base.ZContext, base.LastScreenshot, _state);
		if (operationResult.IsSuccess && string.Equals(operationResult.Status, "钓鱼结束", StringComparison.Ordinal))
		{
			return RoundSuccess(operationResult.Status, null, TimeSpan.FromMilliseconds(100L));
		}
		if (!operationResult.IsSuccess)
		{
			// 对应 commission_assistant_app.py:588 的 wait=0.1（固定）。
			return RoundRetry(operationResult.Status ?? "未识别到指令", null, TimeSpan.FromMilliseconds(100L));
		}
		// 带 FishingRoundPacing 的分支对应 Python 的 wait_round_time=0.1（补足制），其余分支是 wait=0.1（固定）。
		return operationResult.Data is FishingRoundPacing pacing
			? RoundWait(operationResult.Status, null, null, pacing.Duration)
			: RoundWait(operationResult.Status, null, TimeSpan.FromMilliseconds(100L));
	}

	private void LoadAutoOp()
	{
		string subDir = ((_state.RunMode == 2) ? "auto_battle" : "dodge");
		string opName = ((_state.RunMode == 2) ? _config.AutoBattle : _config.DodgeConfig);
		AutoBattleOperator autoOp = _services.LoadAutoOp(base.ZContext, subDir, opName);
		_services.DispatchOpLoaded(base.ZContext, autoOp);
		_services.StartAutoBattle(base.ZContext);
	}
}
