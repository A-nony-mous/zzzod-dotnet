using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手业务服务。
/// </summary>
public interface ICommissionAssistantOperationServices
{
	/// <summary>是否需要因后台运行暂停。</summary>
	bool NeedPauseInBackground(ZContext context, CommissionAssistantConfig config);

	/// <summary>点击对话确认框。</summary>
	OperationResult ClickDialogConfirm(ZContext context, Mat? screen);

	/// <summary>战斗交互键是否可见。</summary>
	bool IsInteractVisible(ZContext context, Mat? screen);

	/// <summary>识别并返回当前大世界画面。</summary>
	string? CheckCurrentWorldScreen(ZContext context, Mat? screen);

	/// <summary>二级菜单是否可见。</summary>
	bool IsSecondaryMenuVisible(ZContext context, Mat? screen);

	/// <summary>处理空洞事件。</summary>
	OperationResult HandleHollow(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc);

	/// <summary>点击空洞通关完成。</summary>
	OperationResult ClickHollowFinished(ZContext context, Mat? screen);

	/// <summary>加载自动战斗指令。</summary>
	AutoBattleOperator LoadAutoOp(ZContext context, string subDir, string opName);

	/// <summary>发布指令已加载事件。</summary>
	void DispatchOpLoaded(ZContext context, AutoBattleOperator autoOp);

	/// <summary>启动自动战斗。</summary>
	void StartAutoBattle(ZContext context);

	/// <summary>恢复自动战斗。</summary>
	void ResumeAutoBattle(ZContext context);

	/// <summary>停止自动战斗。</summary>
	void StopAutoBattle(ZContext context);

	/// <summary>检查战斗状态。</summary>
	void CheckBattleState(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc);

	/// <summary>处理剧情模式。</summary>
	OperationResult HandleStoryMode(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, Mat? screen);

	/// <summary>使用跳过操作后的新截图确认对话框。</summary>
	OperationResult HandleSkipStoryConfirm(ZContext context, CommissionAssistantRuntimeState state, Mat? screen);

	/// <summary>等待二级菜单。</summary>
	OperationResult WaitSecondaryMenu(ZContext context, Mat? screen);

	/// <summary>检测勘域菜单是否打开（在勘域中避免自动点击）。</summary>
	OperationResult CheckExploreDomainMenu(ZContext context, Mat? screen);

	/// <summary>检测战斗菜单是否打开（在空洞自由行动场景中避免自动点击）。</summary>
	OperationResult CheckBattleMenu(ZContext context, Mat? screen);

	/// <summary>检查玩法引导。</summary>
	OperationResult CheckGameTutorial(ZContext context, Mat? screen);

	/// <summary>处理短信对话。</summary>
	OperationResult HandleKnockKnock(ZContext context, Mat? screen);

	/// <summary>检查是否进入钓鱼。</summary>
	OperationResult CheckFishing(ZContext context, Mat? screen, CommissionAssistantRuntimeState state);

	/// <summary>处理普通对话点击。</summary>
	OperationResult DoDialogClick(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, Mat? screen, bool checkCenterWords);

	/// <summary>处理钓鱼流程。</summary>
	OperationResult HandleFishing(ZContext context, Mat? screen, CommissionAssistantRuntimeState state);
}
