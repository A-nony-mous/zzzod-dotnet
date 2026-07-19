using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
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
using FrontierDevtoolsHost = ZzzOd.Gui.Views.FrontierDevtoolsPage;
using FrontierOneDragonHost = ZzzOd.Gui.Views.FrontierOneDragonPage;

namespace ZzzOd.Gui.Shell;

internal sealed record ZzzFrontierRoute(ZzzNavigationEntry Entry, ZzzFrontierPageLayout Layout)
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
            .Select(entry => new ZzzFrontierRoute(entry, ResolveLayout(entry.Key)))
            .ToArray();
        _routes = Routes.ToDictionary(route => route.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<ZzzFrontierRoute> Routes { get; }

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
        foreach (Control page in _pages.Values)
        {
            if (!ReferenceEquals(page, alreadyDisposed) && page is IZzzPageLifecycle lifecycle)
            {
                lifecycle.DisposePage();
            }
        }

        _pages.Clear();
        CurrentPage = null;
    }

    private static ZzzFrontierPageLayout ResolveLayout(string routeKey) => routeKey switch
    {
        // 首页保留真实壁纸和快捷入口的全尺寸表面。
        "home" => ZzzFrontierPageLayout.Surface,
        // 这些页面自身维护 Tab、固定操作区或二级 Frame，避免再套一层滚动。
        "game-assistant" or "one-dragon" or "standalone" or "devtools" => ZzzFrontierPageLayout.Surface,
        // Accounts and settings already own their sample scroll/tab boundary.
        // Keeping them as surface pages avoids wrapping a second ScrollViewer around
        // the page's real SettingsExpander/TabView tree.
        "accounts" or "settings" => ZzzFrontierPageLayout.Surface,
        _ => ZzzFrontierPageLayout.Standard,
    };

    private Control CreateRoutePage(ZzzFrontierRoute route)
    {
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

        if (dedicated is not null)
        {
            return dedicated;
        }

        Control content = route.Entry.CreatePage(_services);
        return CreateFrontierPage(route, content);
    }

    private static FrontierPageHost CreateFrontierPage(ZzzFrontierRoute route, Control content) => route.Key switch
    {
        "home" => new FrontierHomePage(route.Entry.Text, content),
        "game-assistant" => new FrontierGameAssistantPage(route.Entry.Text, content),
        "one-dragon" => new FrontierOneDragonHost(route.Entry.Text, content),
        "standalone" => new FrontierStandalonePage(route.Entry.Text, content),
        "devtools" => new FrontierDevtoolsHost(route.Entry.Text, content),
        "accounts" => new FrontierAccountsPage(route.Entry.Text, content),
        "settings" => new FrontierSettingsPage(route.Entry.Text, content),
        "diagnostics" => new FrontierDiagnosticsPage(route.Entry.Text, content),
        _ => new FrontierPageHost(route.Key, route.Entry.Text, content, route.Layout),
    };
}
