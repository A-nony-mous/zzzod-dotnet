namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用启动器初始化阶段。
/// </summary>
public enum ZApplicationLauncherInitializationStage
{
	/// <summary>创建 ZContext。</summary>
	CreateContext,
	/// <summary>注册内置应用 factory。</summary>
	RegisterBuiltInApplications,
	/// <summary>设置默认应用组。</summary>
	SetDefaultApplicationGroup,
	/// <summary>初始化 OCR profile。</summary>
	InitializeOcrProfile,
	/// <summary>重新加载画面定义。</summary>
	ReloadScreenDefinitions,
	/// <summary>重新加载实例配置。</summary>
	ReloadInstanceConfig,
	/// <summary>初始化控制器。</summary>
	InitializeController,
	/// <summary>初始化应用运行前资源。</summary>
	InitializeForApplication,
	/// <summary>检查并更新运行记录。</summary>
	CheckRunRecords,
	/// <summary>初始化通知推送。</summary>
	InitializePushNotifications,
	/// <summary>初始化遥测。</summary>
	InitializeTelemetry
}
