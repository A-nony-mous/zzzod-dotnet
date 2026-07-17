using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用启动器初始化计划。
/// </summary>
public static class ZApplicationLauncherInitializationPlan
{
	/// <summary>
	/// 对齐 BaselineParity `ApplicationLauncher.init_context()`、`OneDragonContext.init()` 和 `ZContext` 钩子的初始化顺序。
	/// </summary>
	public static IReadOnlyList<ZApplicationLauncherInitializationStep> Steps { get; } = new ZApplicationLauncherInitializationStep[11]
	{
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.CreateContext, "zzz_application_launcher.py:create_context", "创建 ZContext。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.RegisterBuiltInApplications, "one_dragon_context.py:register_application_factory / ApplicationFactoryRegistry", "注册 C# 内置应用 factory。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.SetDefaultApplicationGroup, "one_dragon_context.py:app_group_manager.set_default_apps", "把 default_group 应用写入 RunContext.DefaultGroupApps。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.InitializeOcrProfile, "one_dragon_context.py:init_ocr / OneDragonContext.UseOcrProfile", "用 ModelConfig 的 OCR profile 替换 NullOcrMatcher。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.ReloadScreenDefinitions, "one_dragon_context.py:screen_loader.reload", "重新加载 screen_info，确保后续截图识别使用最新画面定义。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.ReloadInstanceConfig, "one_dragon_context.py:reload_instance_config / zzz_context.py:reload_instance_config", "刷新账号实例级配置。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.InitializeController, "zzz_context.py:init_controller", "按 game/env/project 配置创建 ZPcController 并设置窗口标题。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.InitializeForApplication, "zzz_context.py:init_for_application", "加载地图、快捷手册、世界巡逻和自动战斗运行前资源。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.CheckRunRecords, "one_dragon_context.py:check_and_update_all_run_record", "运行应用前刷新当前实例 run record。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.InitializePushNotifications, "one_dragon_context.py:push_service.init_push_channels", "初始化第三方通知推送渠道。"),
		new ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage.InitializeTelemetry, "zzz_context.py:init_others", "初始化 telemetry，保持与通知推送分离。")
	};
}
