using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZzzOd.Api;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Pages;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Home;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Notices;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        ZzzRunRootResolution runRootResolution = ZzzRunRootResolver.Resolve(args);
        string runRoot = runRootResolution.RunRoot.Path;
        using ZzzRuntimeLock? runtimeLock = ZzzRuntimeLock.TryAcquire(runRoot);
        if (runtimeLock is null)
        {
            if (ZzzGuiSingleInstanceSignal.TryShowExistingAsync(runRoot).GetAwaiter().GetResult())
            {
                Console.Error.WriteLine("已有 GUI 正在运行，已请求显示现有窗口。");
                return 0;
            }

            Console.Error.WriteLine("已有 GUI 或 API-only 宿主持有当前运行根目录。");
            return 2;
        }

        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging => logging.AddConsole())
            .ConfigureServices(services =>
            {
                services.AddZzzAppHost(runRoot, ZzzHostMode.Gui);
                services.AddZzzGuiApiServer();
                services.AddSingleton<ZzzOverlayController>();
                services.AddSingleton<ZzzWindowBackdropService>();
                services.AddSingleton<ZzzGuiShellPresetService>();
                services.AddSingleton<ZzzNavigationRegistry>();
                services.AddSingleton<ZzzGuiOperationTracker>();
                services.AddSingleton<ZzzPageLifecycleService>();
                services.AddSingleton<ZzzShellNavigationService>();
                services.AddSingleton<IZzzUiDispatcher, ZzzAvaloniaUiDispatcher>();
                services.AddSingleton<ZzzShellViewModel>();
                services.AddSingleton<ZzzGuiRunIntentService>();
                services.AddSingleton<ZzzDashboardReadinessService>();
                services.AddSingleton<ZzzLauncherMediaService>();
                services.AddSingleton<ZzzNoticeService>();
                services.AddSingleton<IZzzDialogService, ZzzDialogService>();
                services.AddSingleton<ZzzGlobalInputMonitor>();
                services.AddSingleton<IZzzImageClipboardService, ZzzImageClipboardService>();
                services.AddSingleton<ZzzEnvironmentRuntimeCoordinator>();
                services.AddSingleton<IZzzEnvironmentRuntimeCoordinator>(provider =>
                    provider.GetRequiredService<ZzzEnvironmentRuntimeCoordinator>());
                services.AddHostedService(provider =>
                    provider.GetRequiredService<ZzzEnvironmentRuntimeCoordinator>());
                services.AddTransient<ZzzPageFactory>();
            })
            .Build();

        App.Host = host;
        App.RunRoot = runRoot;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
