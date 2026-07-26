using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OneDragon.Core.Windows.Controller;
using OneDragon.Core.Windows.Screening;
using Serilog;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.Notify;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.DebugData;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.Telemetry;
using ZzzOd.GameLogic.Yolo;

namespace ZzzOd.GameLogic.Context;

/// <summary>
/// ZZZ 运行时上下文
/// </summary>
public class ZContext : OneDragonContext
{
	private Lazy<ZzzOd.GameLogic.Config.ModelConfig> _modelConfig;

	private Lazy<GameConfig> _gameConfig;

	private Lazy<YamlConfig<TeamConfig>> _teamConfig;

	private Lazy<BattleAssistantConfig> _battleAssistantConfig;

	private Lazy<GameAccountConfig> _gameAccountConfig;

	private Lazy<EnvConfig> _envConfig;

	private Lazy<ProjectConfig> _projectConfig;

	private Lazy<PushConfig> _pushConfig;

	private Lazy<AutoBattleContext> _autoBattleContext;

	private Lazy<LostVoidContext> _lostVoidContext;

	private Lazy<WitheredDomainContext> _witheredDomainContext;

	private Lazy<MapAreaService> _mapService;

	private Lazy<CompendiumService> _compendiumService;

	private Lazy<WorldPatrolService> _worldPatrolService;

	private Lazy<TelemetryManager> _telemetry;

	private Lazy<ZzzBackendContext> _backend;

	private Lazy<FlashClassifier> _flashClassifier;

	private Lazy<HollowEventDetector> _hollowEventDetector;

	private Lazy<ZzzOcrService> _zzzOcrService;

	private Lazy<ZzzDebugDataPublisher> _debugDataPublisher;

	private Lazy<ApplicationFactoryRegistry> _applicationFactoryRegistry;

	private Lazy<OperationNotificationService> _operationNotificationService;

	private int _instanceIndex;

	/// <summary>模型配置</summary>
	public ZzzOd.GameLogic.Config.ModelConfig ModelConfig => _modelConfig.Value;

	/// <summary>游戏配置</summary>
	public GameConfig GameConfig => _gameConfig.Value;

	/// <summary>队伍配置</summary>
	public TeamConfig TeamConfig => _teamConfig.Value.Current;

	/// <summary>战斗助手配置</summary>
	public BattleAssistantConfig BattleAssistantConfig => _battleAssistantConfig.Value;

	/// <summary>账号配置</summary>
	public GameAccountConfig GameAccountConfig => _gameAccountConfig.Value;

	/// <summary>环境配置</summary>
	public EnvConfig EnvConfig => _envConfig.Value;

	/// <summary>项目配置</summary>
	public ProjectConfig ProjectConfig => _projectConfig.Value;

	/// <summary>通知推送配置。</summary>
	public PushConfig PushConfig => _pushConfig.Value;

	/// <summary>自动战斗上下文</summary>
	public AutoBattleContext AutoBattleContext => _autoBattleContext.Value;

	/// <summary>
	/// 获取已初始化自动战斗上下文的 Overlay 状态，不会触发上下文初始化。
	/// </summary>
	public AutoBattleOverlayStatusSnapshot? TryGetAutoBattleOverlayStatus()
	{
		Lazy<AutoBattleContext> autoBattleContext = _autoBattleContext;
		return autoBattleContext.IsValueCreated
			? autoBattleContext.Value.GetOverlayStatusSnapshot()
			: null;
	}

	/// <summary>迷失之地上下文</summary>
	public LostVoidContext LostVoid => _lostVoidContext.Value;

	/// <summary>枯萎之都上下文</summary>
	public WitheredDomainContext WitheredDomain => _witheredDomainContext.Value;

	/// <summary>地图服务</summary>
	public MapAreaService MapService => _mapService.Value;

	/// <summary>快捷手册服务</summary>
	public CompendiumService CompendiumService => _compendiumService.Value;

	/// <summary>锄大地路线和地图服务</summary>
	public WorldPatrolService WorldPatrolService => _worldPatrolService.Value;

	/// <summary>遥测服务</summary>
	public TelemetryManager Telemetry => _telemetry.Value;

	/// <summary>业务后端</summary>
	public ZzzBackendContext Backend => _backend.Value;

	/// <summary>闪光分类器</summary>
	public FlashClassifier FlashClassifier => _flashClassifier.Value;

	/// <summary>空洞事件检测器</summary>
	public HollowEventDetector HollowEventDetector => _hollowEventDetector.Value;

	/// <summary>业务 OCR 封装</summary>
	public ZzzOcrService ZzzOcrService => _zzzOcrService.Value;

	/// <summary>业务调试数据发布器</summary>
	public ZzzDebugDataPublisher DebugDataPublisher => _debugDataPublisher.Value;

	/// <summary>应用 factory 注册表</summary>
	public ApplicationFactoryRegistry ApplicationFactoryRegistry => _applicationFactoryRegistry.Value;

	/// <summary>节点和应用生命周期通知服务。</summary>
	public OperationNotificationService OperationNotificationService => _operationNotificationService.Value;

	/// <summary>
	/// 当前运行时使用的推送通道服务。未配置外部通道时保留真实失败结果。
	/// </summary>
	public IPushNotificationService PushNotificationService { get; set; } = new DefaultPushNotificationService();

	/// <summary>当前实例编号。</summary>
	public int InstanceIndex => _instanceIndex;

	/// <summary>当前实例是否在运行前强制重新登录。</summary>
	public bool ForceLoginBeforeRun
	{
		get
		{
			YamlConfig<OneDragonConfig> config = new YamlConfig<OneDragonConfig>(base.Environment, "one_dragon", null, null, Array.Empty<string>());
			return config.Current.InstanceList.Any((OneDragonInstanceConfigItem item) => item.Idx == _instanceIndex && item.ForceLoginBeforeRun);
		}
	}

	/// <summary>
	/// 当前实例实际使用的游戏窗口标题。
	/// </summary>
	public string GameWindowTitle => GetWindowTitle();

	/// <summary>
	/// 初始化上下文
	/// </summary>
	/// <param name="environment">运行环境</param>
	/// <param name="logger">日志</param>
	/// <param name="instanceIndex">实例编号</param>
	public ZContext(OneDragonEnvironment environment, ILogger? logger = null, int instanceIndex = 0)
		: base(environment, logger)
	{
		if (instanceIndex < 0)
		{
			throw new ArgumentOutOfRangeException("instanceIndex", "实例编号不能小于 0。");
		}
		_instanceIndex = instanceIndex;
		_modelConfig = new Lazy<ZzzOd.GameLogic.Config.ModelConfig>(() => LoadSharedConfig<ZzzOd.GameLogic.Config.ModelConfig>("model"));
		_gameConfig = new Lazy<GameConfig>(() => LoadInstanceConfig<GameConfig>("game"));
		_teamConfig = new Lazy<YamlConfig<TeamConfig>>(() => new YamlConfig<TeamConfig>(base.Environment, "team", null, _instanceIndex));
		_battleAssistantConfig = new Lazy<BattleAssistantConfig>(() => LoadInstanceConfig<BattleAssistantConfig>("battle_assistant"));
		_gameAccountConfig = new Lazy<GameAccountConfig>(() => LoadInstanceConfig<GameAccountConfig>("game_account"));
		_envConfig = new Lazy<EnvConfig>(() => LoadSharedConfig<EnvConfig>("env"));
		_projectConfig = new Lazy<ProjectConfig>(() => LoadSharedConfig<ProjectConfig>("project"));
		_pushConfig = new Lazy<PushConfig>(() => LoadSharedConfig<PushConfig>("push"));
		_autoBattleContext = new Lazy<AutoBattleContext>(() => new AutoBattleContext(this));
		_lostVoidContext = new Lazy<LostVoidContext>(() => new LostVoidContext(this));
		_witheredDomainContext = new Lazy<WitheredDomainContext>(() => new WitheredDomainContext(this));
		_mapService = new Lazy<MapAreaService>(() => new MapAreaService(base.Environment));
		_compendiumService = new Lazy<CompendiumService>(() => new CompendiumService(base.Environment));
		_worldPatrolService = new Lazy<WorldPatrolService>(() => new WorldPatrolService(base.Environment));
		_telemetry = new Lazy<TelemetryManager>(() => new TelemetryManager(this));
		_backend = new Lazy<ZzzBackendContext>(() => new ZzzBackendContext(this));
		_flashClassifier = new Lazy<FlashClassifier>(() => new FlashClassifier(this));
		_hollowEventDetector = new Lazy<HollowEventDetector>(() => new HollowEventDetector(this));
		_zzzOcrService = new Lazy<ZzzOcrService>(() => new ZzzOcrService(this));
		_debugDataPublisher = new Lazy<ZzzDebugDataPublisher>(() => new ZzzDebugDataPublisher(base.EventBus, base.OverlayDebugBus));
		_applicationFactoryRegistry = new Lazy<ApplicationFactoryRegistry>(() => new ApplicationFactoryRegistry(this));
		_operationNotificationService = new Lazy<OperationNotificationService>(() => new OperationNotificationService(this));
		base.GameTextResolver = (string sourceText) => GameTextTranslator.Translate(base.Environment, GameAccountConfig.GameLanguage, sourceText);
	}

	/// <summary>
	/// 更新预定义队伍成员并写回当前实例的 team.yml。
	/// </summary>
	/// <param name="teamName">队伍名称。</param>
	/// <param name="agentIds">识别到的代理人 ID。</param>
	/// <returns>找到并更新队伍时返回 <c>true</c>。</returns>
	public bool UpdateTeamMembers(string teamName, IReadOnlyList<string> agentIds)
	{
		if (!TeamConfig.UpdateTeamMembers(teamName, agentIds))
		{
			return false;
		}
		_teamConfig.Value.Save();
		return true;
	}

	/// <summary>
	/// 切换当前实例并重新加载实例级配置。
	/// </summary>
	/// <param name="instanceIndex">目标实例编号。</param>
	/// <exception cref="T:System.InvalidOperationException">目标实例没有登记在 one_dragon.yml 中。</exception>
	public void SwitchInstance(int instanceIndex)
	{
		if (instanceIndex < 0)
		{
			throw new ArgumentOutOfRangeException("instanceIndex", "实例编号不能小于 0。");
		}
		OneDragonEnvironment environment = base.Environment;
		IReadOnlyList<string> subDirectories = Array.Empty<string>();
		YamlConfig<OneDragonConfig> yamlConfig = new YamlConfig<OneDragonConfig>(environment, "one_dragon", null, null, subDirectories);
		OneDragonInstanceConfigItem oneDragonInstanceConfigItem = yamlConfig.Current.InstanceList.FirstOrDefault((OneDragonInstanceConfigItem item) => item.Idx == instanceIndex);
		if (oneDragonInstanceConfigItem == null)
		{
			throw new InvalidOperationException($"实例不存在 {instanceIndex:00}");
		}
		foreach (OneDragonInstanceConfigItem instance in yamlConfig.Current.InstanceList)
		{
			instance.Active = instance.Idx == instanceIndex;
		}
		yamlConfig.Save();
		ApplicationFactoryRegistry registeredApplications = (_applicationFactoryRegistry.IsValueCreated ? _applicationFactoryRegistry.Value : null);
		_instanceIndex = instanceIndex;
		ReloadInstanceConfig();
		if (registeredApplications != null)
		{
			_applicationFactoryRegistry = new Lazy<ApplicationFactoryRegistry>(() => registeredApplications);
		}
		OnSwitchInstance();
		base.EventBus.Publish("instance_active", instanceIndex);
	}

	/// <summary>
	/// 重载实例级配置。
	/// </summary>
	public void ReloadInstanceConfig()
	{
		_modelConfig = new Lazy<ZzzOd.GameLogic.Config.ModelConfig>(() => LoadSharedConfig<ZzzOd.GameLogic.Config.ModelConfig>("model"));
		_gameConfig = new Lazy<GameConfig>(() => LoadInstanceConfig<GameConfig>("game"));
		_teamConfig = new Lazy<YamlConfig<TeamConfig>>(() => new YamlConfig<TeamConfig>(base.Environment, "team", null, _instanceIndex));
		_battleAssistantConfig = new Lazy<BattleAssistantConfig>(() => LoadInstanceConfig<BattleAssistantConfig>("battle_assistant"));
		_gameAccountConfig = new Lazy<GameAccountConfig>(() => LoadInstanceConfig<GameAccountConfig>("game_account"));
		_envConfig = new Lazy<EnvConfig>(() => LoadSharedConfig<EnvConfig>("env"));
		_projectConfig = new Lazy<ProjectConfig>(() => LoadSharedConfig<ProjectConfig>("project"));
		_pushConfig = new Lazy<PushConfig>(() => LoadSharedConfig<PushConfig>("push"));
		_autoBattleContext = new Lazy<AutoBattleContext>(() => new AutoBattleContext(this));
		_lostVoidContext = new Lazy<LostVoidContext>(() => new LostVoidContext(this));
		_witheredDomainContext = new Lazy<WitheredDomainContext>(() => new WitheredDomainContext(this));
		_mapService = new Lazy<MapAreaService>(() => new MapAreaService(base.Environment));
		_compendiumService = new Lazy<CompendiumService>(() => new CompendiumService(base.Environment));
		_worldPatrolService = new Lazy<WorldPatrolService>(() => new WorldPatrolService(base.Environment));
		_telemetry = new Lazy<TelemetryManager>(() => new TelemetryManager(this));
		_backend = new Lazy<ZzzBackendContext>(() => new ZzzBackendContext(this));
		_flashClassifier = new Lazy<FlashClassifier>(() => new FlashClassifier(this));
		_hollowEventDetector = new Lazy<HollowEventDetector>(() => new HollowEventDetector(this));
		_zzzOcrService = new Lazy<ZzzOcrService>(() => new ZzzOcrService(this));
		_debugDataPublisher = new Lazy<ZzzDebugDataPublisher>(() => new ZzzDebugDataPublisher(base.EventBus, base.OverlayDebugBus));
		_applicationFactoryRegistry = new Lazy<ApplicationFactoryRegistry>(() => new ApplicationFactoryRegistry(this));
		_operationNotificationService = new Lazy<OperationNotificationService>(() => new OperationNotificationService(this));
	}

	/// <summary>
	/// 获取窗口标题
	/// </summary>
	/// <returns>窗口标题</returns>
	private string GetWindowTitle()
	{
		return GameConst.ResolveWindowTitle(ParseGameRegion(GameAccountConfig.GameRegion), GameAccountConfig.UseCustomWinTitle ? GameAccountConfig.CustomWinTitle : null);
	}

	/// <summary>
	/// 获取窗口匹配条件
	/// </summary>
	/// <returns>启用自定义窗口标题时返回 null（退回纯标题查找），否则返回进程名与类名条件</returns>
	private WindowsGameWindowMatchOptions? GetWindowMatchOptions()
	{
		// 自定义标题是用户显式覆盖，可能指向云游戏等非 Unity 窗口，收紧会破坏该逃生通道。
		if (GameAccountConfig.UseCustomWinTitle)
		{
			return null;
		}
		return new WindowsGameWindowMatchOptions([GameConst.ProcessName], GameConst.WindowClassName);
	}

	/// <summary>
	/// 切换实例后更新控制器的窗口标题和账号配置
	/// </summary>
	public void OnSwitchInstance()
	{
		if (base.Controller is ZPcController zPcController)
		{
			zPcController.SetWindowTitle(GetWindowTitle());
			zPcController.SetMatchOptions(GetWindowMatchOptions());
			zPcController.SyncGameConfig(GameConfig);
		}
	}

	/// <summary>
	/// 初始化控制器
	/// </summary>
	public void InitController()
	{
		base.Controller?.CleanupAfterAppShutdown();
		// 必须传入上下文的日志器。省略时控制器会自建一个写向同一基路径的日志器，
		// Serilog 会给后建者的文件名追加序号后缀，导致同一次运行的日志被劈成两个文件。
		ZPcController zPcController = new ZPcController(GameConfig, EnvConfig.ScreenshotMethod, ProjectConfig.ScreenStandardWidth, ProjectConfig.ScreenStandardHeight, null, null, null, null, null, null, skipForegroundActivation: false, null, base.Logger, GetWindowMatchOptions());
		zPcController.SetWindowTitle(GetWindowTitle());
		AttachController(zPcController);
	}

	/// <summary>
	/// 运行 Application 前的初始化
	/// </summary>
	public void InitForApplication()
	{
		MapService.Reload();
		CompendiumService.Reload();
		WorldPatrolService.LoadData();
	}

	/// <summary>
	/// App 关闭后的操作
	/// </summary>
	public void AfterAppShutdown()
	{
		if (_telemetry.IsValueCreated)
		{
			_telemetry.Value.Shutdown();
		}
		if (_backend.IsValueCreated)
		{
			_backend.Value.Shutdown();
		}
		if (_flashClassifier.IsValueCreated)
		{
			_flashClassifier.Value.Shutdown();
		}
		if (_hollowEventDetector.IsValueCreated)
		{
			_hollowEventDetector.Value.Shutdown();
		}
		if (_zzzOcrService.IsValueCreated)
		{
			_zzzOcrService.Value.Shutdown();
		}
		if (_lostVoidContext.IsValueCreated)
		{
			_lostVoidContext.Value.AfterAppShutdown();
		}
		if (_witheredDomainContext.IsValueCreated)
		{
			_witheredDomainContext.Value.AfterAppShutdown();
		}
		if (_autoBattleContext.IsValueCreated)
		{
			_autoBattleContext.Value.AfterAppShutdown();
		}
		if (_operationNotificationService.IsValueCreated)
		{
			_operationNotificationService.Value.Dispose();
		}
	}

	/// <summary>
	/// 按 ZZZ 业务顺序执行关闭。
	/// </summary>
	public new void Shutdown()
	{
		AfterAppShutdown();
		base.Shutdown();
	}

	/// <summary>
	/// 释放上下文资源。
	/// </summary>
	public new void Dispose()
	{
		Shutdown();
		GC.SuppressFinalize(this);
	}

	private T LoadInstanceConfig<T>(string moduleName) where T : class, new()
	{
		YamlConfig<T> yamlConfig = new YamlConfig<T>(base.Environment, moduleName, null, _instanceIndex);
		return yamlConfig.Current;
	}

	private T LoadSharedConfig<T>(string moduleName) where T : class, new()
	{
		YamlConfig<T> yamlConfig = new YamlConfig<T>(base.Environment, moduleName);
		return yamlConfig.Current;
	}

	private static GameRegionEnum ParseGameRegion(string? region)
	{
		string text = region?.Trim().ToLowerInvariant();
		if (1 == 0)
		{
		}
		GameRegionEnum result = text switch
		{
			"cn" => GameRegionEnum.CN, 
			"cn_b" => GameRegionEnum.CNB, 
			"us" => GameRegionEnum.AMERICA, 
			"eu" => GameRegionEnum.EUROPE, 
			"asia" => GameRegionEnum.ASIA, 
			"twhkmo" => GameRegionEnum.TWHKMO, 
			_ => GameRegionEnum.CN, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
