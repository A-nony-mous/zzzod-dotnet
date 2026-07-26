using System.Collections.Generic;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// 非前端业务自动化 E2E 覆盖矩阵。
/// </summary>
public static class E2ECoverageMatrix
{
	private const string LiveCombatBlockedReason = "当前环境未确认真实游戏窗口、账号可进入目标玩法、允许真实输入操作和账号消耗风险，不能安全执行实机 E2E。";

	/// <summary>
	/// 全部覆盖条目。
	/// </summary>
	public static IReadOnlyList<E2ECoverageMatrixItem> Items { get; } = new E2ECoverageMatrixItem[59]
	{
		Application("auto_battle", "自动战斗", E2EVerificationMode.RealGameE2E, "AutoBattleAppOperation", "AutoBattleContext"),
		Application("charge_plan", "体力刷本", E2EVerificationMode.RealGameE2E, "ChargePlanOperation", "CompendiumChallengeOperationBase"),
		Application("city_fund", "丽都城募", E2EVerificationMode.RealGameE2E, "CityFundOperation"),
		Application("coffee", "咖啡店", E2EVerificationMode.RealGameE2E, "CoffeeOperation", "CoffeeSelectionService"),
		BlockedApplication("commission_assistant", "委托助手", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实 OCR 结果、真实输入、账号当前处于可安全自动对话/剧情/短信/钓鱼/空洞/自动战斗状态，不能安全接管全部委托助手分支。非 E2E 回归 evidence：CommissionAssistantAppTests 覆盖对话确认、交互键、大世界画面、二级菜单、空洞背包检测、通关完成点击、剧情按钮、玩法引导、短信关闭、钓鱼入口、钓鱼按键动作和 unsupported 分支；ProductionPlaceholderAuditTests 覆盖固定成功和短延迟缺口未回归。", "CommissionAssistantOperation", "DefaultCommissionAssistantOperationServices", "AutoBattleContext"),
		Application("daily_signin", "每日签到", E2EVerificationMode.PureLogic, "DailySignInApp"),
		Application("dodge_assistant", "闪避助手", E2EVerificationMode.RealGameE2E, "DodgeAssistantOperation", "AutoBattleDodgeContext"),
		BlockedApplication("drive_disc_dismantle", "驱动盘拆解", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实 OCR/模板检测、真实输入、账号当前驱动盘仓库状态、筛选条件和拆解物品可安全操作，不能安全执行真实拆解。非 E2E 回归 evidence：DriveDiscDismantleAppTests 覆盖导航到仓库驱动盘拆解页、区域点击、未知画面失败、区域未命中失败、返回大世界流程和应用 run record；ProductionPlaceholderAuditTests 覆盖固定成功和短延迟缺口未回归。", "DriveDiscDismantleOperation", "DefaultDriveDiscDismantleOperationServices"),
		Application("email", "邮件", E2EVerificationMode.RealGameE2E, "EmailOperation"),
		Application("engagement_reward", "活跃度奖励", E2EVerificationMode.RealGameE2E, "EngagementRewardOperation"),
		Application("hou_hou_bakery", "吼吼饼铺", E2EVerificationMode.RealGameE2E, "HouHouBakeryOperation"),
		BlockedApplication("intel_board", "情报板", "实机 E2E blocked evidence：未确认真实游戏窗口、账号已解锁情报板、当前委托可接取、允许真实输入接取/出战。非 E2E 回归 evidence：IntelBoardAppTests 覆盖委托流程、进度读取、AutoBattle end result、结算画面、失败画面、回到列表和超时；ProductionPlaceholderAuditTests 覆盖固定成功和短延迟缺口未回归。", "IntelBoardOperation", "AutoBattleContext"),
		Application("life_on_line", "真拿命验收", E2EVerificationMode.RealGameE2E, "LifeOnLineOperation"),
		BlockedApplication("lost_void", "迷失之地", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实 OCR 结果、LostVoid YOLO 检测、AutoBattle 接管、账号已解锁迷失之地、当前副本/周计划/悬赏状态可安全操作、允许真实输入进入空洞和战斗。非 E2E 回归 evidence：LostVoidAppTests 覆盖应用工厂、配置、run record 和 app flow；LostVoidContextTests 覆盖挑战确认、结果完成、寻路重试、失败退出、战斗失败撤退、失败退出完成、YOLO 目标选择和错误通知；ProductionPlaceholderAuditTests 覆盖固定成功和短延迟缺口未回归。", "LostVoidApp", "LostVoidRunLevel"),
		Application("mouse_sensitivity_checker", "鼠标灵敏度检查", E2EVerificationMode.ScreenshotOrFixedAssetRegression, "MouseSensitivityCheckerOperation"),
		Application("notorious_hunt", "恶名狩猎", E2EVerificationMode.RealGameE2E, "NotoriousHuntOperation"),
		Application("notify", "通知推送", E2EVerificationMode.PureLogic, "NotifyApp", "DefaultPushNotificationService"),
		Application("one_dragon", "一条龙", E2EVerificationMode.RealGameE2E, "ZOneDragonApp", "ApplicationRunContext"),
		Application("operation_debug", "Operation 调试", E2EVerificationMode.PureLogic, "OperationDebugOperation", "OperationDebugTemplateLoader"),
		Application("predefined_team_checker", "预备编队检查", E2EVerificationMode.ScreenshotOrFixedAssetRegression, "PredefinedTeamCheckerOperation"),
		Application("random_play", "录像店营业", E2EVerificationMode.RealGameE2E, "RandomPlayOperation"),
		Application("redemption_code", "兑换码", E2EVerificationMode.RealGameE2E, "RedemptionCodeOperation"),
		Application("ridu_weekly", "丽都周纪", E2EVerificationMode.RealGameE2E, "RiduWeeklyOperation"),
		Application("scratch_card", "刮刮卡", E2EVerificationMode.RealGameE2E, "ScratchCardOperation"),
		Application("screenshot_helper", "截图助手", E2EVerificationMode.ScreenshotOrFixedAssetRegression, "ScreenshotHelperService", "ScreenshotHelperCaptureSource"),
		BlockedApplication("shiyu_defense", "式舆防卫战", "实机 E2E blocked evidence：未确认真实游戏窗口、账号已解锁式舆防卫战、当前节点/奖励状态可安全操作、允许真实输入进入战斗。非 E2E 回归 evidence：ShiyuDefenseAppTests 覆盖主界面、节点选择、三间房间选择、战斗结束、失败退出、战后移动和领奖；ProductionPlaceholderAuditTests 覆盖固定成功和短延迟缺口未回归。", "ShiyuDefenseOperation", "ShiyuDefenseBattle", "AutoBattleContext"),
		Application("suibian_temple", "随便观", E2EVerificationMode.RealGameE2E, "SuibianTempleOperation", "SuibianTempleOperations"),
		Application("trigrams_collection", "卦象集录", E2EVerificationMode.RealGameE2E, "TrigramsCollectionOperation"),
		BlockedApplication("withered_domain", "枯萎之都", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、HollowEvent YOLO 检测、AutoBattle 战斗事件接管、账号已解锁枯萎之都、当前副本奖励/计划次数状态可安全操作、允许真实输入进行地图移动和事件选择；该路径不直接依赖 OCR 推进，OCR readiness 由入口和 profile evidence 覆盖。非 E2E 回归 evidence：WitheredDomainAppTests 覆盖应用工厂、配置、run record、app flow、真实 ColorCodedHollowMapSource、空截图地图失败和取消；HollowRunnerTests 覆盖地图移动、默认 EmptyHollowMapSource 失败、事件分发和停止清理；ProductionPlaceholderAuditTests 覆盖固定成功和短延迟缺口未回归。", "WitheredDomainApp", "HollowRunnerWitheredDomainRunner", "HollowRunner"),
		Application("world_patrol", "锄大地", E2EVerificationMode.RealGameE2E, "WorldPatrolRunRoute", "TransportBy3dMap"),
		Operation("operation.runtime", "Operation 状态机执行", E2EVerificationMode.PureLogic, "Operation", "OperationExecutor", "ApplicationRunContext"),
		Operation("operation.launcher", "默认 launcher 入口装配", E2EVerificationMode.PureLogic, "ZApplicationLauncher", "ZContext", "ApplicationFactoryRegistry"),
		BlockedOperation("operation.shiyu_battle_completion", "式舆战斗完成", "实机 E2E blocked evidence：未确认可安全进入式舆战斗并允许真实输入接管账号。非 E2E 回归 evidence：DefaultShiyuDefenseBattleServices_RunAutoBattle_UsesPreviousAutoBattleEndResult、LeavesCurrentFrameResultForNextRound、UsesSuppliedFrameWithoutCapturingAgain、UsesCurrentFrameTimestampForCountdown、MoveAfterBattle_* 和 ShiyuDefenseBattle_FailureExitFlowUsesScreenshotsAndReturnsFailure。", "ShiyuDefenseBattle", "AutoBattleContext"),
		BlockedOperation("operation.intel_board_battle_completion", "情报板战斗完成", "实机 E2E blocked evidence：未确认可安全接取真实情报板委托并允许真实输入进入战斗。非 E2E 回归 evidence：DefaultIntelBoardOperationServices_RunBattleAsync_UsesAutoBattleEndResult、DetectsSettlementScreen、DetectsBattleFailureScreen、DetectsBackToList 和 TimesOutWhenBattleKeepsRunning。", "IntelBoardOperation", "AutoBattleContext"),
		BlockedOperation("operation.lost_void_failure_recovery", "LostVoid 失败恢复", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实 OCR 结果、LostVoid YOLO 检测、AutoBattle 接管、账号已解锁迷失之地、当前账号可安全进入真实迷失之地空洞、触发挑战结果/失败画面并允许真实输入撤退或退出。非 E2E 回归 evidence：ScreenRunLevelRuntime_ConfirmChallengeResult_ClicksConfirmAndWaitsUntilHidden、ScreenRunLevelRuntime_FinishChallengeResult_ClicksCompleteRecordsRewardsAndWaitsUntilHidden、ScreenRunLevelRuntime_RestartForRetryAsync_RunsRestartInBattle、ScreenRunLevelRuntime_FailExitAsync_RunsExitInBattleUntilChallengeCompleteVisible、RunLevel_ExecuteAsync_TraversesBattleFailureExitGraphWithRecordedRuntimeCalls。", "LostVoidRunLevel", "LostVoidInteractOperations"),
		BlockedOperation("operation.withered_domain_hollow_runner", "枯萎之都地图运行", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、HollowEvent YOLO 检测、AutoBattle 战斗事件接管、账号已解锁枯萎之都、当前账号可安全进入真实枯萎之都空洞、真实截图可识别地图、允许真实输入点击下一节点和处理事件；该路径不直接依赖 OCR 推进。非 E2E 回归 evidence：HollowRunnerWitheredDomainRunner_UsesScreenshotMapSourceUntilMissionComplete、HollowRunnerWitheredDomainRunner_ReturnsFailureWhenDefaultScreenshotMapCannotDetect、HollowRunnerWitheredDomainRunner_ReturnsCanceledStatusWhenStopped、CheckScreenOnceAsync_RecordsFailureWhenMapMovementUsesDefaultEmptySource 和 Dispose_StopsPeriodicRunner。", "HollowRunnerWitheredDomainRunner", "ColorCodedHollowMapSource", "HollowRunner"),
		BlockedOperation("operation.commission_assistant_detection", "委托助手画面检测", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实 OCR 结果、真实输入、账号当前可安全触发委托助手对话、剧情、短信、钓鱼、空洞和自动战斗检查。非 E2E 回归 evidence：DefaultServices_ClickDialogConfirm_ClicksMatchedConfirmText、DefaultServices_HandleHollow_UsesHollowBackpackArea、DefaultServices_ClickHollowFinished_ClicksMatchedCompleteText、DefaultServices_HandleStoryMode_ClicksSkipButton、DefaultServices_CheckGameTutorial_UsesTutorialTextArea、DefaultServices_HandleKnockKnock_ClosesLatestMessageScreen、DefaultServices_CheckFishing_DetectsFishingCommand 和 DefaultServices_HandleFishing_ReturnsUnsupportedWithoutZzzControllerActions。", "CommissionAssistantOperation", "ScreenUtils", "AutoBattleContext"),
		BlockedOperation("operation.drive_disc_dismantle_input", "驱动盘拆解真实输入", "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实 OCR/模板检测、真实输入、账号当前驱动盘仓库状态、筛选条件和拆解物品可安全操作。非 E2E 回归 evidence：DefaultServices_GotoSalvage_NavigatesFromDriveDiscStorageToSalvageScreen、DefaultServices_GotoSalvage_ReturnsFailureWhenScreenIsUnknown、DefaultServices_ClickArea_ClicksConfiguredSalvageArea 和 DefaultServices_ClickArea_ReturnsFailureWhenAreaTextIsMissing。", "DriveDiscDismantleOperation", "ScreenUtils"),
		BlockedRuntime("autobattle.context", E2ECoverageArea.AutoBattle, "自动战斗运行上下文", "需要真实战斗画面、真实截图捕获、真实输入、AutoBattle 接管和账号战斗资源。非 E2E 回归 evidence：AutoBattleContextsTests、AutoBattleOperatorTests、AtomicOpFactoryTests、AutoBattleAppTests 和 ProductionPlaceholderAuditTests。", "AutoBattleContext", "AutoBattleOperator", "AutoBattleStateRecordService"),
		new E2ECoverageMatrixItem("autobattle.agent_state", E2ECoverageArea.AutoBattle, "角色状态识别", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[3] { "AutoBattleAgentContext", "AgentStateChecker", "assets/template/agent_state" }, "固定截图和模板回归优先，实机战斗 evidence 追加。"),
		new E2ECoverageMatrixItem("autobattle.target_state", E2ECoverageArea.AutoBattle, "目标状态识别", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[3] { "AutoBattleTargetContext", "TargetStateChecker", "assets/template/target_state" }, "固定截图和模板回归优先，实机战斗 evidence 追加。"),
		BlockedRuntime("autobattle.dodge", E2ECoverageArea.AutoBattle, "闪避状态检测", "需要真实战斗音画、真实截图捕获、真实输入、音频 loopback 和账号战斗资源。非 E2E 回归 evidence：DodgeAssistantAppTests、AutoBattleContextsTests、AudioRecorderTests 和 E2EEvidenceWriterTests。", "AutoBattleDodgeContext", "DodgeAssistantOperation"),
		new E2ECoverageMatrixItem("hollow.runner", E2ECoverageArea.HollowZero, "HollowRunner 地图运行", E2EVerificationMode.Blocked, new string[3] { "HollowRunner", "ColorCodedHollowMapSource", "HollowMapNavigation" }, "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实空洞地图截图、HollowEvent YOLO 检测、账号空洞状态和允许真实输入点击地图节点。非 E2E 回归 evidence：HollowRunnerTests 覆盖地图移动、空地图失败、默认 EmptyHollowMapSource 失败、事件分发和停止清理；WitheredDomainAppTests 覆盖 ColorCodedHollowMapSource 驱动的枯萎之都 runner。", "当前环境未确认真实游戏窗口、账号可进入目标玩法、允许真实输入操作和账号消耗风险，不能安全执行实机 E2E。"),
		new E2ECoverageMatrixItem("hollow.lost_void_detector", E2ECoverageArea.HollowZero, "LostVoid YOLO 检测", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[3] { "LostVoidDetector", "LostVoidMoveByDetection", "lost_void_det" }, "固定截图和模型回归优先，实机 LostVoid evidence 追加。"),
		new E2ECoverageMatrixItem("hollow.event_dispatch", E2ECoverageArea.HollowZero, "空洞事件分发", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[2] { "HollowEventDispatch", "HollowEventSource" }, "固定事件截图回归优先，实机 WitheredDomain evidence 追加。"),
		BlockedRuntime("input.foreground_keyboard_mouse", E2ECoverageArea.Input, "前台键鼠输入", "需要真实游戏窗口处于前台并允许真实键鼠输入。非 E2E 回归 evidence：ZPcControllerTests、KeySimRunnerTests、AtomicOpFactoryTests 和默认 Category!=E2E 输入测试。", "WindowsForegroundInputController", "ForegroundKeyboardMouseController"),
		BlockedRuntime("input.background_keyboard_mouse", E2ECoverageArea.Input, "后台键鼠输入", "需要真实游戏窗口句柄、后台输入权限和允许真实键鼠输入。非 E2E 回归 evidence：ZPcControllerTests、KeySimRunnerTests、AtomicOpFactoryTests 和默认 Category!=E2E 输入测试。", "WindowsBackgroundInputController", "WindowsBackgroundKeyboardMouseController"),
		BlockedRuntime("input.virtual_gamepad", E2ECoverageArea.Input, "虚拟手柄输入", "需要真实 ViGEm/虚拟手柄依赖、真实游戏窗口和允许真实手柄输入。非 E2E 回归 evidence：AutoBattleAppTests、DodgeAssistantAppTests、OperationDebugTests 和 ProductionPlaceholderAuditTests。", "VirtualXboxController", "VirtualDualShock4Controller", "ViGEmClientWrapper"),
		BlockedRuntime("capture.wgc", E2ECoverageArea.Capture, "Windows Graphics Capture", "需要真实游戏窗口句柄、桌面捕获权限和首帧截图。非 E2E 回归 evidence：E2ECaptureReadinessProbeTests 覆盖无窗口失败和首帧摘要记录，7.3 已验证 readiness 支撑测试。", "WindowsGraphicsCaptureScreenCapturer", "WindowsGraphicsCaptureInterop"),
		BlockedRuntime("capture.print_window", E2ECoverageArea.Capture, "PrintWindow 截图", "需要真实游戏窗口句柄和 PrintWindow 权限。非 E2E 回归 evidence：E2ECaptureReadinessProbeTests 覆盖无窗口失败和 fallback evidence 结构。", "PrintWindowScreenCapturer", "WindowsGameWindow"),
		new E2ECoverageMatrixItem("capture.screenshot_controller", E2ECoverageArea.Capture, "截图控制器选择", E2EVerificationMode.PureLogic, new string[2] { "ScreenshotController", "ScreenshotMethodCompatibility" }, "无窗口配置选择测试。"),
		new E2ECoverageMatrixItem("ocr.profile", E2ECoverageArea.Ocr, "OCR profile 装载", E2EVerificationMode.PureLogic, new string[3] { "ModelConfig", "OcrModelResolver", "ZzzOcrService" }, "默认测试验证 profile 选择，实机 evidence 记录 profile。"),
		new E2ECoverageMatrixItem("ocr.screen_text", E2ECoverageArea.Ocr, "游戏画面 OCR", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[3] { "OcrService", "ScreenUtils", "assets/game_data/screen_info" }, "固定截图 OCR 回归优先，实机 evidence 追加。"),
		new E2ECoverageMatrixItem("yolo.flash", E2ECoverageArea.Yolo, "闪光 YOLO 分类", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[3] { "ZzzYoloModelConfig", "AutoBattleAgentContext", "flash_classifier" }, "固定截图和模型回归。"),
		new E2ECoverageMatrixItem("yolo.lost_void", E2ECoverageArea.Yolo, "LostVoid YOLO 检测", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[2] { "LostVoidDetector", "lost_void_det" }, "固定截图和模型回归。"),
		new E2ECoverageMatrixItem("yolo.hollow_event", E2ECoverageArea.Yolo, "HollowZero 事件 YOLO", E2EVerificationMode.ScreenshotOrFixedAssetRegression, new string[2] { "HollowEventDetector", "hollow_zero_event" }, "固定截图和模型回归。"),
		BlockedRuntime("audio.dodge_recorder", E2ECoverageArea.Audio, "闪避音频采集", "需要真实系统 loopback 音频、真实战斗音效和允许 AutoBattle 接管。非 E2E 回归 evidence：AudioRecorderTests 覆盖多声道、32000Hz、非 32000Hz 重采样和 rolling buffer；E2EEvidenceWriterTests 记录采样率、通道数、目标采样率、重采样模式和 buffer 时长。", "AudioRecorder", "AutoBattleDodgeContext"),
		new E2ECoverageMatrixItem("audio.feature_math", E2ECoverageArea.Audio, "音频特征处理", E2EVerificationMode.PureLogic, new string[2] { "AudioMathUtils", "AudioFilterUtils" }, "无窗口音频单元测试。"),
		new E2ECoverageMatrixItem("notification.push_service", E2ECoverageArea.NotificationPush, "第三方通知推送", E2EVerificationMode.PureLogic, new string[3] { "NotifyApp", "DefaultNotifyAppFlow", "DefaultPushNotificationService" }, "与 TelemetryManager 分开验证。")
	};

	private static E2ECoverageMatrixItem Application(string id, string displayName, E2EVerificationMode mode, params string[] components)
	{
		if (mode == E2EVerificationMode.RealGameE2E)
		{
			return new E2ECoverageMatrixItem("application." + id, E2ECoverageArea.Application, displayName, E2EVerificationMode.Blocked, components, BuildGenericBlockedEvidence(displayName, components), "当前环境未确认真实游戏窗口、账号可进入目标玩法、允许真实输入操作和账号消耗风险，不能安全执行实机 E2E。");
		}
		return new E2ECoverageMatrixItem("application." + id, E2ECoverageArea.Application, displayName, mode, components, "默认测试或固定资产回归 evidence。");
	}

	private static E2ECoverageMatrixItem Operation(string id, string displayName, E2EVerificationMode mode, params string[] components)
	{
		if (mode == E2EVerificationMode.RealGameE2E)
		{
			return new E2ECoverageMatrixItem(id, E2ECoverageArea.Operation, displayName, E2EVerificationMode.Blocked, components, BuildGenericBlockedEvidence(displayName, components), "当前环境未确认真实游戏窗口、账号可进入目标玩法、允许真实输入操作和账号消耗风险，不能安全执行实机 E2E。");
		}
		return new E2ECoverageMatrixItem(id, E2ECoverageArea.Operation, displayName, mode, components, "默认测试或固定资产回归 evidence。");
	}

	private static E2ECoverageMatrixItem BlockedApplication(string id, string displayName, string evidence, params string[] components)
	{
		return new E2ECoverageMatrixItem("application." + id, E2ECoverageArea.Application, displayName, E2EVerificationMode.Blocked, components, evidence, "当前环境未确认真实游戏窗口、账号可进入目标玩法、允许真实输入操作和账号消耗风险，不能安全执行实机 E2E。");
	}

	private static E2ECoverageMatrixItem BlockedOperation(string id, string displayName, string evidence, params string[] components)
	{
		return new E2ECoverageMatrixItem(id, E2ECoverageArea.Operation, displayName, E2EVerificationMode.Blocked, components, evidence, "当前环境未确认真实游戏窗口、账号可进入目标玩法、允许真实输入操作和账号消耗风险，不能安全执行实机 E2E。");
	}

	private static E2ECoverageMatrixItem BlockedRuntime(string id, E2ECoverageArea area, string displayName, string evidence, params string[] components)
	{
		return new E2ECoverageMatrixItem(id, area, displayName, E2EVerificationMode.Blocked, components, "实机 E2E blocked evidence：未确认真实游戏窗口、真实截图捕获、真实输入授权、账号状态和账号消耗风险，当前不安全执行实机 E2E。" + evidence, "当前环境未确认真实游戏窗口、账号可进入目标玩法、允许真实输入操作和账号消耗风险，不能安全执行实机 E2E。");
	}

	private static string BuildGenericBlockedEvidence(string displayName, IReadOnlyList<string> components)
	{
		string value = string.Join("、", components);
		return $"实机 E2E blocked evidence：{displayName} 未确认真实游戏窗口、真实截图捕获、真实输入授权、账号状态和账号消耗风险，当前不安全执行实机 E2E。非 E2E 回归 evidence：{value} 已纳入默认 Category!=E2E 测试、E2ECoverageMatrixTests 和 ProductionPlaceholderAuditTests。";
	}
}
