using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;
using ZzzOd.AppHost.Notifications;
using ZzzOd.AppHost.Overlay;
using ZzzOd.AppHost.Resources;

namespace ZzzOd.AppHost;

/// <summary>
/// ZZZ 共享宿主服务注册扩展。
/// </summary>
public static class ZzzAppHostServiceCollectionExtensions
{
	/// <summary>
	/// 注册 ZZZ 共享宿主服务。
	/// </summary>
	/// <param name="services">服务集合。</param>
	/// <param name="runRoot">运行根目录。</param>
	/// <param name="mode">宿主模式。</param>
	/// <returns>服务集合。</returns>
	public static IServiceCollection AddZzzAppHost(this IServiceCollection services, string runRoot, ZzzHostMode mode)
	{
		string fullRoot = Path.GetFullPath(runRoot);
		ZzzApiOptions implementationInstance = ZzzApiOptions.LoadOrCreate(fullRoot);
		services.AddSingleton(implementationInstance);
		services.AddSingleton(new ZzzHostModeOptions(mode));
		services.AddSingleton(new ZzzRunRoot(fullRoot));
		services.AddSingleton<ZzzBackendEventBus>();
		services.AddSingleton<ZzzBattleAssistantRuntimeSource>();
		services.AddSingleton<IZzzOverlayService, ZzzOverlayService>();
		services.AddSingleton<IZzzPushNotificationService, ZzzPushNotificationService>();
		services.AddSingleton<IZzzResourceDownloadService, ZzzResourceDownloadService>();
		services.AddSingleton<IZzzScreenManageService, ZzzScreenManageService>();
		services.AddSingleton<IZzzImageAnalysisService, ZzzImageAnalysisService>();
		services.AddSingleton<ZzzLogFanOutLoggerProvider>();
		services.AddSingleton((Func<IServiceProvider, ILoggerProvider>)((IServiceProvider sp) => sp.GetRequiredService<ZzzLogFanOutLoggerProvider>()));
		services.AddSingleton((IServiceProvider sp) => new ZzzRuntimeManager(
			fullRoot,
			sp.GetRequiredService<ILogger<ZzzRuntimeManager>>(),
			sp.GetRequiredService<ZzzLogFanOutLoggerProvider>(),
			sp.GetRequiredService<IZzzPushNotificationService>()));
		services.AddSingleton<IZzzAppBackend, ZzzAppBackend>();
		services.AddHostedService<ZzzOperationTraceBridge>();
		services.AddHostedService<ZzzAppHostLifetimeService>();
		return services;
	}
}
