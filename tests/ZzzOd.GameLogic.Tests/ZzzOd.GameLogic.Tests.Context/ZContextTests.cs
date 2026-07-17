using System;
using System.IO;
using System.Reflection;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OneDragon.Core.Windows.Controller;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.DebugData;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.Telemetry;
using ZzzOd.GameLogic.Yolo;

namespace ZzzOd.GameLogic.Tests.Context;

public class ZContextTests
{
	[Fact]
	public void GetWindowTitle_UsesChineseTitleForCnRegion()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config", "00"));
		File.WriteAllText(Path.Combine(text, "config", "00", "game_account.yml"), "game_region: cn_b\n");
		OneDragonEnvironment environment = new OneDragonEnvironment(text);
		using ZContext context = new ZContext(environment);
		string actual = InvokeWindowTitle(context);
		Assert.Equal("绝区零", actual);
		Directory.Delete(text, recursive: true);
	}

	[Fact]
	public void GetWindowTitle_UsesCustomTitleWhenConfigured()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config", "00"));
		File.WriteAllText(Path.Combine(text, "config", "00", "game_account.yml"), "use_custom_win_title: true\ncustom_win_title: My Window\n");
		OneDragonEnvironment environment = new OneDragonEnvironment(text);
		using ZContext context = new ZContext(environment);
		string actual = InvokeWindowTitle(context);
		Assert.Equal("My Window", actual);
		Directory.Delete(text, recursive: true);
	}

	[Fact]
	public void ReloadInstanceConfig_UpdatesConfigReference()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(AppContext.BaseDirectory);
		using ZContext zContext = new ZContext(environment);
		ZzzOd.GameLogic.Config.ModelConfig modelConfig = zContext.ModelConfig;
		GameConfig gameConfig = zContext.GameConfig;
		BattleAssistantConfig battleAssistantConfig = zContext.BattleAssistantConfig;
		AutoBattleContext autoBattleContext = zContext.AutoBattleContext;
		LostVoidContext lostVoid = zContext.LostVoid;
		WitheredDomainContext witheredDomain = zContext.WitheredDomain;
		MapAreaService mapService = zContext.MapService;
		CompendiumService compendiumService = zContext.CompendiumService;
		TelemetryManager telemetry = zContext.Telemetry;
		ZzzBackendContext backend = zContext.Backend;
		FlashClassifier flashClassifier = zContext.FlashClassifier;
		HollowEventDetector hollowEventDetector = zContext.HollowEventDetector;
		ZzzOcrService zzzOcrService = zContext.ZzzOcrService;
		ZzzDebugDataPublisher debugDataPublisher = zContext.DebugDataPublisher;
		ApplicationFactoryRegistry applicationFactoryRegistry = zContext.ApplicationFactoryRegistry;
		zContext.ReloadInstanceConfig();
		ZzzOd.GameLogic.Config.ModelConfig modelConfig2 = zContext.ModelConfig;
		GameConfig gameConfig2 = zContext.GameConfig;
		BattleAssistantConfig battleAssistantConfig2 = zContext.BattleAssistantConfig;
		AutoBattleContext autoBattleContext2 = zContext.AutoBattleContext;
		LostVoidContext lostVoid2 = zContext.LostVoid;
		WitheredDomainContext witheredDomain2 = zContext.WitheredDomain;
		MapAreaService mapService2 = zContext.MapService;
		CompendiumService compendiumService2 = zContext.CompendiumService;
		TelemetryManager telemetry2 = zContext.Telemetry;
		ZzzBackendContext backend2 = zContext.Backend;
		FlashClassifier flashClassifier2 = zContext.FlashClassifier;
		HollowEventDetector hollowEventDetector2 = zContext.HollowEventDetector;
		ZzzOcrService zzzOcrService2 = zContext.ZzzOcrService;
		ZzzDebugDataPublisher debugDataPublisher2 = zContext.DebugDataPublisher;
		ApplicationFactoryRegistry applicationFactoryRegistry2 = zContext.ApplicationFactoryRegistry;
		Assert.NotSame(modelConfig, modelConfig2);
		Assert.NotSame(gameConfig, gameConfig2);
		Assert.NotSame(battleAssistantConfig, battleAssistantConfig2);
		Assert.NotSame(autoBattleContext, autoBattleContext2);
		Assert.NotSame(lostVoid, lostVoid2);
		Assert.NotSame(witheredDomain, witheredDomain2);
		Assert.NotSame(mapService, mapService2);
		Assert.NotSame(compendiumService, compendiumService2);
		Assert.NotSame(telemetry, telemetry2);
		Assert.NotSame(backend, backend2);
		Assert.NotSame(flashClassifier, flashClassifier2);
		Assert.NotSame(hollowEventDetector, hollowEventDetector2);
		Assert.NotSame(zzzOcrService, zzzOcrService2);
		Assert.NotSame(debugDataPublisher, debugDataPublisher2);
		Assert.NotSame(applicationFactoryRegistry, applicationFactoryRegistry2);
	}

	[Fact]
	public void Constructor_LoadsInstanceScopedConfigsByRequestedIndex()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(Path.Combine(text, "config", "00"));
			Directory.CreateDirectory(Path.Combine(text, "config", "01"));
			File.WriteAllText(Path.Combine(text, "config", "00", "game_account.yml"), "game_path: C:\\Games\\Zero\\ZenlessZoneZero.exe\naccount: zero\n");
			File.WriteAllText(Path.Combine(text, "config", "01", "game_account.yml"), "game_path: D:\\Games\\One\\ZenlessZoneZero.exe\naccount: one\n");
			File.WriteAllText(Path.Combine(text, "config", "00", "game.yml"), "control_method: keyboard\n");
			File.WriteAllText(Path.Combine(text, "config", "01", "game.yml"), "control_method: xbox\n");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using ZContext zContext = new ZContext(environment, null, 1);
			Assert.Equal(1, zContext.InstanceIndex);
			Assert.Equal("one", zContext.GameAccountConfig.Account);
			Assert.Equal("D:\\Games\\One\\ZenlessZoneZero.exe", zContext.GameAccountConfig.GamePath);
			Assert.Equal("xbox", zContext.GameConfig.ControlMethod);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void InitController_UsesEnvAndProjectConfig()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config"));
		Directory.CreateDirectory(Path.Combine(text, "config", "00"));
		File.WriteAllText(Path.Combine(text, "config", "env.yml"), "screenshot_method: bitblt\n");
		File.WriteAllText(Path.Combine(text, "config", "project.yml"), "screen_standard_width: 1600\nscreen_standard_height: 900\n");
		OneDragonEnvironment environment = new OneDragonEnvironment(text);
		using ZContext zContext = new ZContext(environment);
		zContext.InitController();
		WindowsGameController windowsGameController = Assert.IsAssignableFrom<WindowsGameController>(zContext.Controller);
		Assert.Equal(1600, windowsGameController.StandardWidth);
		Assert.Equal(900, windowsGameController.StandardHeight);
		Directory.Delete(text, recursive: true);
	}

	[Fact]
	public void ReloadInstanceConfig_RebuildsYoloAndOcrBusinessServicesFromUpdatedModelConfig()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config"));
		File.WriteAllText(Path.Combine(text, "config", "model.yml"), "flash_classifier: yolov8n-640-flash-20250906\nhollow_zero_event: yolov8s-736-hollow-zero-event-1130\nocr_profile: v5-server\n");
		OneDragonEnvironment environment = new OneDragonEnvironment(text);
		using ZContext zContext = new ZContext(environment);
		Assert.Equal("yolov8n-640-flash-20250906", zContext.FlashClassifier.ModelName);
		Assert.Equal("yolov8s-736-hollow-zero-event-1130", zContext.HollowEventDetector.ModelName);
		Assert.Equal("v5-server", zContext.ZzzOcrService.ProfileId);
		File.WriteAllText(Path.Combine(text, "config", "model.yml"), "flash_classifier: yolov8n-640-flash-20250921\nhollow_zero_event: yolov8s-736-hollow-zero-event-0126\nocr_profile: v6-small\n");
		zContext.ReloadInstanceConfig();
		Assert.Equal("yolov8n-640-flash-20250921", zContext.FlashClassifier.ModelName);
		Assert.Equal("yolov8s-736-hollow-zero-event-0126", zContext.HollowEventDetector.ModelName);
		Assert.Equal("v6-small", zContext.ZzzOcrService.ProfileId);
		Directory.Delete(text, recursive: true);
	}

	[Fact]
	public void ReloadInstanceConfig_ReloadsBattleAssistantConfig()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config", "00"));
		string text2 = Path.Combine(text, "config", "00", "battle_assistant.yml");
		File.WriteAllText(text2, "auto_battle_config: 初始配置\n");
		OneDragonEnvironment environment = new OneDragonEnvironment(text);
		using ZContext zContext = new ZContext(environment);
		Assert.Equal("初始配置", zContext.BattleAssistantConfig.AutoBattleConfig);
		File.WriteAllText(text2, "auto_battle_config: 刷新配置\n");
		YamlOperator.InvalidateCache(text2);
		zContext.ReloadInstanceConfig();
		Assert.Equal("刷新配置", zContext.BattleAssistantConfig.AutoBattleConfig);
		Directory.Delete(text, recursive: true);
	}

	[Fact]
	public void AfterAppShutdown_StopsBusinessServices()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(AppContext.BaseDirectory);
		using ZContext zContext = new ZContext(environment);
		zContext.Telemetry.Initialize();
		zContext.Backend.Start();
		_ = zContext.FlashClassifier;
		_ = zContext.HollowEventDetector;
		_ = zContext.ZzzOcrService;
		_ = zContext.LostVoid;
		_ = zContext.WitheredDomain;
		zContext.AutoBattleContext.StartContextAsync();
		zContext.AfterAppShutdown();
		Assert.False(zContext.Telemetry.IsInitialized);
		Assert.False(zContext.Backend.IsStarted);
		Assert.True(zContext.FlashClassifier.IsShutdown);
		Assert.True(zContext.HollowEventDetector.IsShutdown);
		Assert.True(zContext.ZzzOcrService.IsShutdown);
		Assert.True(zContext.LostVoid.AfterAppShutdownCalled);
		Assert.True(zContext.WitheredDomain.AfterAppShutdownCalled);
		Assert.True(zContext.AutoBattleContext.AfterAppShutdownCalled);
	}

	[Fact]
	public void Shutdown_StopsBusinessServicesBeforeDisposal()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(AppContext.BaseDirectory);
		ZContext zContext = new ZContext(environment);
		zContext.Telemetry.Initialize();
		zContext.Backend.Start();
		FlashClassifier flashClassifier = zContext.FlashClassifier;
		HollowEventDetector hollowEventDetector = zContext.HollowEventDetector;
		ZzzOcrService zzzOcrService = zContext.ZzzOcrService;
		LostVoidContext lostVoid = zContext.LostVoid;
		WitheredDomainContext witheredDomain = zContext.WitheredDomain;
		AutoBattleContext autoBattleContext = zContext.AutoBattleContext;
		zContext.Shutdown();
		Assert.False(zContext.Telemetry.IsInitialized);
		Assert.False(zContext.Backend.IsStarted);
		Assert.True(flashClassifier.IsShutdown);
		Assert.True(hollowEventDetector.IsShutdown);
		Assert.True(zzzOcrService.IsShutdown);
		Assert.True(lostVoid.AfterAppShutdownCalled);
		Assert.True(witheredDomain.AfterAppShutdownCalled);
		Assert.True(autoBattleContext.AfterAppShutdownCalled);
	}

	private static string InvokeWindowTitle(ZContext context)
	{
		MethodInfo method = typeof(ZContext).GetMethod("GetWindowTitle", BindingFlags.Instance | BindingFlags.NonPublic);
		return (string)method.Invoke(context, null);
	}
}
