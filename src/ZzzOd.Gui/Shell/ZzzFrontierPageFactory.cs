using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;
using ZzzOd.AppHost.Resources;
using ZzzOd.AppHost.Notifications;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Home;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Notices;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Views;
using ZzzOd.Gui.Views.FrontierPages.Accounts;
using ZzzOd.Gui.Views.FrontierPages.DevTools;
using ZzzOd.Gui.Views.FrontierPages.OneDragon;
using FrontierGameAssistantVisual = ZzzOd.Gui.Views.FrontierPages.GameAssistant.FrontierGameAssistantPage;
using FrontierHomeVisual = ZzzOd.Gui.Views.FrontierPages.Home.FrontierHomePage;
using FrontierSettingsVisual = ZzzOd.Gui.Views.FrontierPages.Settings.FrontierSettingsPage;
using FrontierStandaloneVisual = ZzzOd.Gui.Views.FrontierPages.Standalone.FrontierStandalonePage;
using FrontierDevtoolsVisual = ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierDevtoolsPage;
using FrontierOneDragonVisual = ZzzOd.Gui.Views.FrontierPages.OneDragon.FrontierOneDragonPage;

namespace ZzzOd.Gui.Shell;

internal sealed record ZzzFrontierRoute(ZzzNavigationEntry Entry)
{
    public string Key => Entry.Key;
}

internal sealed class ZzzFrontierPageFactory : IFANavigationPageFactory
{
    private readonly IServiceProvider _services;
    private readonly IReadOnlyDictionary<string, ZzzFrontierRoute> _routes;
    private readonly Dictionary<string, Control> _pages = new(StringComparer.Ordinal);

    public ZzzFrontierPageFactory(IServiceProvider services, ZzzNavigationRegistry navigationRegistry)
    {
        _services = services;
        Routes = navigationRegistry.Entries
            .Select(entry => new ZzzFrontierRoute(entry))
            .ToArray();
        _routes = Routes.ToDictionary(route => route.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<ZzzFrontierRoute> Routes { get; }

    // 测试缝:允许 shell 测试为合成路由注入哑页面;生产路径不设置。
    internal Func<ZzzFrontierRoute, Control>? CreateRoutePageOverrideForTest { get; set; }

    public IReadOnlyCollection<Control> CreatedPages => _pages.Values;

    public Control? CurrentPage { get; private set; }

    public ZzzFrontierRoute? FindRoute(string key) =>
        _routes.TryGetValue(key, out ZzzFrontierRoute? route) ? route : null;

    public ZzzFrontierRoute? FindRoute(Control page) =>
        _pages.FirstOrDefault(pair => ReferenceEquals(pair.Value, page)) is { Key: { Length: > 0 } key }
            ? FindRoute(key)
            : null;

    public void MarkCurrent(Control page)
    {
        if (FindRoute(page) is not null)
        {
            CurrentPage = page;
        }
    }

    public Control GetPage(Type srcType)
    {
        // Frontier 导航使用稳定的 route object，类型导航没有对应的产品路由。
        _ = srcType;
        return null!;
    }

    public Control GetPageFromObject(object target)
    {
        if (target is not ZzzFrontierRoute route || !_routes.ContainsKey(route.Key))
        {
            return null!;
        }

        if (!_pages.TryGetValue(route.Key, out Control? page))
        {
            page = CreateRoutePage(route);
            _pages.Add(route.Key, page);
        }

        CurrentPage = page;
        return page;
    }

    public void DisposeCachedPages(Control? alreadyDisposed)
    {
        // 这条路径由窗口 OnClosed 触发，异常逃出去会落到 Win32 WndProc 上并跳过其余页面的释放。
        // 单个页面释放失败只记录，不中断整轮清理。
        ILogger<ZzzFrontierPageFactory>? logger = _services.GetService<ILogger<ZzzFrontierPageFactory>>();
        foreach (Control page in _pages.Values)
        {
            if (ReferenceEquals(page, alreadyDisposed) || page is not IZzzPageLifecycle lifecycle)
            {
                continue;
            }

            try
            {
                lifecycle.DisposePage();
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception, "释放缓存页面 {PageType} 失败。", page.GetType().Name);
            }
        }

        _pages.Clear();
        CurrentPage = null;
    }

    private Control CreateRoutePage(ZzzFrontierRoute route)
    {
        if (CreateRoutePageOverrideForTest is not null)
        {
            return CreateRoutePageOverrideForTest(route);
        }

        Control? dedicated = route.Key switch
        {
            "home" => new FrontierHomeVisual(
                _services.GetRequiredService<IZzzAppBackend>(),
                _services.GetRequiredService<ZzzLauncherMediaService>(),
                _services.GetRequiredService<ZzzNoticeService>(),
                _services.GetRequiredService<ZzzDashboardReadinessService>(),
                _services.GetRequiredService<ZzzShellNavigationService>(),
                _services.GetRequiredService<ZzzGuiRunIntentService>(),
                _services.GetRequiredService<IZzzDialogService>(),
                _services.GetService<ZzzGuiOperationTracker>()),
            "game-assistant" => new FrontierGameAssistantVisual(
                _services.GetRequiredService<IZzzAppBackend>(),
                _services.GetRequiredService<ZzzGuiRunIntentService>()),
            "accounts" => new ZzzFrontierAccountsPage(
                _services.GetRequiredService<IZzzAppBackend>(),
                _services.GetService<ZzzGuiOperationTracker>()),
            "one-dragon" => new FrontierOneDragonVisual(
                _services.GetRequiredService<IZzzAppBackend>(),
                _services.GetRequiredService<ZzzGuiRunIntentService>(),
                _services.GetService<ZzzGuiOperationTracker>()),
            "devtools" => new FrontierDevtoolsVisual(
                _services.GetRequiredService<IZzzAppBackend>(),
                _services.GetRequiredService<ZzzGuiRunIntentService>(),
                _services.GetRequiredService<IZzzScreenManageService>(),
                _services.GetRequiredService<IZzzImageAnalysisService>()),
            "standalone" => new FrontierStandaloneVisual(
                _services.GetRequiredService<IZzzAppBackend>(),
                _services.GetRequiredService<ZzzGuiRunIntentService>(),
                _services.GetService<ZzzGuiOperationTracker>()),
            "settings" => new FrontierSettingsVisual(
                _services.GetRequiredService<IZzzAppBackend>(),
                _services.GetRequiredService<ZzzOverlayController>(),
                _services.GetRequiredService<ZzzLauncherMediaService>(),
                _services.GetRequiredService<IZzzResourceDownloadService>(),
                _services.GetRequiredService<IZzzPushNotificationService>(),
                _services.GetService<ZzzGlobalInputMonitor>(),
                _services.GetService<IZzzEnvironmentRuntimeCoordinator>(),
                _services.GetService<ZzzGuiOperationTracker>()),
            _ => null,
        };

        return dedicated
            ?? throw new InvalidOperationException($"导航路由 {route.Key} 没有对应的前卫页面实现。");
    }
}
