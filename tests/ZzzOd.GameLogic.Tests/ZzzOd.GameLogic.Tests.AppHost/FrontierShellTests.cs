using System.Reflection;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Home;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Notices;
using ZzzOd.Gui.Views;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Views.FrontierPages.Accounts;
using ZzzOd.Gui.Views.FrontierPages.Settings;
using ZzzOd.Gui.Views.FrontierPages.WorldPatrol;
using FrontierHomeVisual = ZzzOd.Gui.Views.FrontierPages.Home.FrontierHomePage;
using FrontierStandaloneVisual = ZzzOd.Gui.Views.FrontierPages.Standalone.FrontierStandalonePage;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class FrontierShellTests
{
    private sealed class ImmediateDispatcher : IZzzUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private sealed class RecordingWindowRuntime : IZzzShellWindowRuntime
    {
        public Window? Window { get; private set; }

        public FAInfoBar? ToastBar { get; private set; }

        public void Attach(Window window, FAInfoBar toastBar)
        {
            Window = window;
            ToastBar = toastBar;
        }

        public void ShowToast(string title, string message, TimeSpan duration, FAInfoBarSeverity severity)
        {
            if (ToastBar is null)
            {
                return;
            }

            ToastBar.Title = title;
            ToastBar.Message = message;
            ToastBar.Severity = severity;
            ToastBar.IsOpen = true;
        }

        public void Dispose()
        {
            Window = null;
            ToastBar = null;
        }
    }

    private sealed class LifecyclePage : Control, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
    {
        public int Shown { get; private set; }

        public int Hidden { get; private set; }

        public int Left { get; private set; }

        public int Disposed { get; private set; }

        public bool CanGoBack { get; private set; }

        public string? SelectedHeader { get; private set; }

        public event EventHandler? BackNavigationStateChanged;

        public void EnterSecondary()
        {
            CanGoBack = true;
            BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void GoBack()
        {
            CanGoBack = false;
            BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool SelectByHeader(string header)
        {
            SelectedHeader = header;
            return true;
        }

        public void OnPageShown() => Shown++;

        public void OnPageHidden() => Hidden++;

        public void OnPageLeave() => Left++;

        public void DisposePage() => Disposed++;
    }

    [Fact]
    public void ShellRunToastUsesRealStateAndTerminalDetails()
    {
        Assert.Null(ZzzShellWindowRuntime.CreateRunToast(new ZzzRunStatusDto(ZzzRunState.Idle)));

        ZzzRunToast paused = Assert.IsType<ZzzRunToast>(ZzzShellWindowRuntime.CreateRunToast(new ZzzRunStatusDto(
            ZzzRunState.Paused,
            "coffee",
            "咖啡店")));
        Assert.Equal("已暂停 咖啡店", paused.Title);
        Assert.Equal(string.Empty, paused.Message);
        Assert.Equal(TimeSpan.FromSeconds(3), paused.Duration);
        Assert.Equal(FAInfoBarSeverity.Warning, paused.Severity);

        ZzzRunToast failed = Assert.IsType<ZzzRunToast>(ZzzShellWindowRuntime.CreateRunToast(new ZzzRunStatusDto(
            ZzzRunState.Failed,
            "coffee",
            "咖啡店",
            LastStatus: "执行异常",
            Error: "真实失败")));
        Assert.Equal("运行异常 咖啡店", failed.Title);
        Assert.Equal("真实失败", failed.Message);
        Assert.Equal(FAInfoBarSeverity.Error, failed.Severity);
    }

    public class ShellBackendProxy : DispatchProxy
    {
        private readonly Channel<ZzzBackendEvent> _events = Channel.CreateUnbounded<ZzzBackendEvent>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name switch
            {
                nameof(IZzzAppBackend.GetCurrentInstance) => ZzzBackendResult<ZzzInstanceDto>.Ok(
                    new ZzzInstanceDto(0, "测试实例", true, "config/00")),
                nameof(IZzzAppBackend.GetCurrentRun) => ZzzBackendResult<ZzzRunStatusDto>.Ok(
                    new ZzzRunStatusDto(ZzzRunState.Idle)),
                nameof(IZzzAppBackend.GetConfigScope) => GetScope((string)args![0]!),
                nameof(IZzzAppBackend.SubscribeEvents) => _events.Reader,
                nameof(IZzzAppBackend.UnsubscribeEvents) => null,
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }

        private static ZzzBackendResult<ZzzConfigScopeValuesDto> GetScope(string scope)
        {
            Dictionary<string, object?> values = scope switch
            {
                "project" => new Dictionary<string, object?>
                {
                    ["project_name"] = "ZenlessZoneZero-OneDragon",
                    ["github_homepage"] = "https://github.com/OneDragon-Anything/ZenlessZoneZero-OneDragon",
                },
                _ => new Dictionary<string, object?>(),
            };
            ZzzConfigScopeDescriptorDto descriptor = new(scope, scope, false, false, true, []);
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(descriptor, null, null, values));
        }
    }

    [Fact]
    public void FrontierFactoryCreatesDedicatedRootRouteTypes()
    {
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            EnsureFluentTheme();
            ZzzNavigationRegistry registry = new(
            [
                new ZzzNavigationEntry("home", "仪表盘", "H", "h", "主页仪表盘", ZzzNavigationPlacement.Primary),
                new ZzzNavigationEntry("standalone", "应用运行", "A", "a", "独立应用", ZzzNavigationPlacement.Primary),
            ]);
            IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, ShellBackendProxy>();
            using ServiceProvider services = CreateFrontierTestServices(backend);
            ZzzFrontierPageFactory factory = new(services, registry);

            Assert.IsType<FrontierHomeVisual>(factory.GetPageFromObject(factory.FindRoute("home")!));
            Assert.IsType<FrontierStandaloneVisual>(factory.GetPageFromObject(factory.FindRoute("standalone")!));

            Assert.Equal(2, factory.CreatedPages.Count);
            factory.DisposeCachedPages(null);
        });
    }

    [Fact]
    public void FrontierSettingsPagesLoadSettingsExpanderItemsAsSampleControls()
    {
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            EnsureFluentTheme();
            IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, ShellBackendProxy>();
            using ZzzGlobalInputMonitor inputMonitor = new();
            ZzzFrontierAccountsPage accounts = new(backend);
            FrontierEnvironmentSettingsPage environment = new(backend, inputMonitor);
            try
            {
                Assert.IsType<FASettingsExpanderItem>(accounts.FindControl<Control>("GamePathItem"));
                Assert.IsType<FASettingsExpanderItem>(accounts.FindControl<Control>("AccountItem"));
                Assert.IsType<FASettingsExpanderItem>(environment.FindControl<Control>("PersonalProxyItem"));
            }
            finally
            {
                accounts.DisposePage();
                environment.DisposePage();
            }
        });
    }

    [Fact]
    public void IndependentOverlayAndEditorTopLevelsConstructAndCloseForBothShells()
    {
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            EnsureFluentTheme();
            IZzzAppBackend frontierBackend = DispatchProxy.Create<IZzzAppBackend, ShellBackendProxy>();
            using ServiceProvider frontierServices = CreateFrontierTestServices(frontierBackend);
            using ZzzShellViewModel frontierViewModel = new(frontierBackend, new ImmediateDispatcher());
            FrontierShellWindow frontier = new(
                frontierServices,
                CreateFrontierRegistry(new LifecyclePage(), new LifecyclePage()),
                new ZzzPageLifecycleService(),
                new ZzzShellNavigationService(),
                frontierViewModel,
                new RecordingWindowRuntime(),
                new ZzzRunRoot(Path.GetTempPath()),
                enableSplash: false);
            ZzzOverlayTechnicalWindow technical = new();
            ZzzOverlayInfoPanelWindow info = new();
            FrontierWorldPatrolLargeMapIconEditorWindow frontierEditor = new([]);
            try
            {
                Assert.IsType<FrontierShellWindow>(frontier);
                Assert.IsAssignableFrom<Window>(technical);
                Assert.IsAssignableFrom<Window>(info);
                Assert.IsAssignableFrom<Window>(frontierEditor);
            }
            finally
            {
                frontierEditor.Close();
                info.Close();
                technical.Close();
                frontier.Close();
            }
        });
    }

    [Fact]
    public void FrontierMainViewKeepsFrameHistoryCacheUnknownRouteAndSecondaryBackPriority()
    {
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            EnsureFluentTheme();
            LifecyclePage home = new();
            LifecyclePage settings = new();
            ZzzNavigationRegistry registry = CreateFrontierRegistry(home, settings);
            IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, ShellBackendProxy>();
            using ServiceProvider services = CreateFrontierTestServices(backend);
            using ZzzShellViewModel shellViewModel = new(backend, new ImmediateDispatcher());
            ZzzPageLifecycleService lifecycle = new();
            ZzzShellNavigationService navigation = new();
            RecordingWindowRuntime runtime = new();
            Window window = new();
            FrontierMainView view = new(
                services,
                registry,
                lifecycle,
                navigation,
                shellViewModel,
                runtime,
                new ZzzRunRoot(Path.GetTempPath()),
                window);
            try
            {
                WireTestPages(view, home, settings);
                FANavigationTransitionInfo? transition = null;
                view.NavigationFrame.Navigated += (_, args) => transition = args.NavigationTransitionInfo;
                Assert.Null(view.ActiveRoute);
                Assert.Equal(0, view.CreatedPageCount);
                Assert.Equal(0, home.Shown);
                view.StartInitialNavigation();
                Assert.IsType<FAEntranceNavigationTransitionInfo>(transition);
                Assert.Equal("test-home", view.ActiveRoute);
                Assert.Equal(1, view.CreatedPageCount);
                Assert.Equal(2, view.NavigationItems.Count);
                Assert.Equal(88, view.NavigationView.OpenPaneLength);
                Assert.Equal(48, view.NavigationView.CompactPaneLength);
                Assert.Equal("测试实例 × 绝区零 一条龙", shellViewModel.FrontierWindowTitle);
                Assert.Equal(1, home.Shown);
                Image windowIcon = Assert.IsType<Image>(view.FindControl<Image>("WindowIcon"));
                Border paneTitleSpacer = Assert.IsType<Border>(view.FindControl<Border>("PaneTitleSpacer"));
                Assert.Equal(12, windowIcon.Margin.Left);
                Assert.False(view.NavigationView.IsBackButtonVisible);
                Assert.True(paneTitleSpacer.IsVisible);
                Assert.Equal(40, paneTitleSpacer.Height);

                FANavigationViewItem homeItem = view.NavigationItems["test-home"];
                Assert.Equal("主页仪表盘", Avalonia.Automation.AutomationProperties.GetName(homeItem));
                Assert.Equal("主页仪表盘", ToolTip.GetTip(homeItem));
                StackPanel homeBody = Assert.IsType<StackPanel>(homeItem.Content);
                FAFontIcon homeIcon = Assert.Single(homeBody.Children.OfType<FAFontIcon>());
                TextBlock homeLabel = Assert.Single(homeBody.Children.OfType<TextBlock>());
                Assert.Equal(32, homeIcon.FontSize);
                Assert.Equal(12, homeLabel.FontSize);
                Assert.Equal("仪表盘", homeLabel.Text);
                Assert.True(homeLabel.IsVisible);

                Assert.True(view.NavigateForTesting("test-settings"));
                Assert.IsType<FAEntranceNavigationTransitionInfo>(transition);
                Assert.Equal("test-settings", view.ActiveRoute);
                Assert.Equal(2, view.CreatedPageCount);
                Assert.True(view.CanGoBack);
                Assert.Equal(1, home.Left);
                Assert.Equal(1, settings.Shown);
                Assert.Equal(48, windowIcon.Margin.Left);
                Assert.True(view.NavigationView.IsBackButtonVisible);
                Assert.False(paneTitleSpacer.IsVisible);

                Assert.True(view.NavigateForTesting("test-settings"));
                Assert.Equal(2, view.CreatedPageCount);
                Assert.False(view.NavigateForTesting("missing"));
                Assert.Equal("test-settings", view.ActiveRoute);

                settings.EnterSecondary();
                view.GoBackForTesting();
                Assert.Equal("test-settings", view.ActiveRoute);
                Assert.False(settings.CanGoBack);
                Assert.True(view.CanGoBack);

                view.GoBackForTesting();
                Assert.Equal("test-home", view.ActiveRoute);
                Assert.False(view.CanGoBack);
                Assert.Equal(12, windowIcon.Margin.Left);
                Assert.False(view.NavigationView.IsBackButtonVisible);
                Assert.True(paneTitleSpacer.IsVisible);

                Assert.True(view.NavigateForTesting("test-settings"));
                view.NavigationFrame.BackStack.Clear();
                Assert.True(view.CanGoBack);
                Assert.True(view.NavigationView.IsBackButtonVisible);

                view.GoBackForTesting();
                Assert.Equal("test-home", view.ActiveRoute);
                Assert.False(view.CanGoBack);
                Assert.False(view.NavigationView.IsBackButtonVisible);
            }
            finally
            {
                view.Dispose();
                window.Close();
            }

            Assert.Equal(1, home.Disposed);
            Assert.Equal(1, settings.Disposed);
        });
    }

    [Fact]
    public void FrontierShellWindowLoadsIndependentMainViewAndCloses()
    {
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            EnsureFluentTheme();
            LifecyclePage home = new();
            LifecyclePage settings = new();
            ZzzNavigationRegistry registry = CreateFrontierRegistry(home, settings);
            IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, ShellBackendProxy>();
            using ServiceProvider services = CreateFrontierTestServices(backend);
            using ZzzShellViewModel shellViewModel = new(backend, new ImmediateDispatcher());
            RecordingWindowRuntime runtime = new();
            FrontierShellWindow window = new(
                services,
                registry,
                new ZzzPageLifecycleService(),
                new ZzzShellNavigationService(),
                shellViewModel,
                runtime,
                new ZzzRunRoot(Path.GetTempPath()),
                enableSplash: false);
            try
            {
                ContentControl host = Assert.IsType<ContentControl>(window.FindControl<ContentControl>("MainViewHost"));
                Assert.Null(host.Content);
                Assert.IsAssignableFrom<FAAppWindow>(window);
                window.InitializeMainViewForTesting(view => WireTestPages(view, home, settings));
                Assert.IsType<FrontierMainView>(host.Content);
                window.Show();
                Assert.True(window.IsVisible);
            }
            finally
            {
                window.Close();
            }

            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void FrontierShellUsesSampleMaterialAndThemeAwareFallback()
    {
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            EnsureFluentTheme();
            LifecyclePage home = new();
            LifecyclePage settings = new();
            ZzzNavigationRegistry registry = CreateFrontierRegistry(home, settings);
            IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, ShellBackendProxy>();
            using ServiceProvider services = CreateFrontierTestServices(backend);
            using ZzzShellViewModel shellViewModel = new(backend, new ImmediateDispatcher());
            FrontierShellWindow window = new(
                services,
                registry,
                new ZzzPageLifecycleService(),
                new ZzzShellNavigationService(),
                shellViewModel,
                new RecordingWindowRuntime(),
                new ZzzRunRoot(Path.GetTempPath()),
                enableSplash: false);

            try
            {
                window.InitializeMainViewForTesting(view => WireTestPages(view, home, settings));
                window.Show();
                ContentControl host = Assert.IsType<ContentControl>(window.FindControl<ContentControl>("MainViewHost"));
                FrontierMainView view = Assert.IsType<FrontierMainView>(host.Content);
                Grid titleBar = Assert.IsType<Grid>(view.FindControl<Grid>("TitleBarHost"));

                window.RequestedThemeVariant = ThemeVariant.Light;
                Assert.Equal(Colors.Transparent, GetColor(window.Background));
                Assert.Equal(Color.Parse("#F3F3F3"), GetOpaqueColor(window.TransparencyBackgroundFallback));
                Assert.Equal([WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur], window.TransparencyLevelHint);
                Assert.Equal(48, window.TitleBar.Height);
                Assert.Equal(48, titleBar.Height);
                Assert.Equal(0, titleBar.GetValue(Panel.ZIndexProperty));

                window.RequestedThemeVariant = ThemeVariant.Dark;
                Assert.Equal(Colors.Transparent, GetColor(window.Background));
                Assert.Equal(byte.MaxValue, GetColor(window.TransparencyBackgroundFallback).A);
                Assert.Equal([WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur], window.TransparencyLevelHint);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FrontierBackButtonKeepsNavbarPositionAndExecutesFrameHistory()
    {
        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            EnsureFluentTheme();
            LifecyclePage home = new();
            LifecyclePage settings = new();
            ZzzNavigationRegistry registry = CreateFrontierRegistry(home, settings);
            IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, ShellBackendProxy>();
            using ServiceProvider services = CreateFrontierTestServices(backend);
            using ZzzShellViewModel shellViewModel = new(backend, new ImmediateDispatcher());
            FrontierShellWindow window = new(
                services,
                registry,
                new ZzzPageLifecycleService(),
                new ZzzShellNavigationService(),
                shellViewModel,
                new RecordingWindowRuntime(),
                new ZzzRunRoot(Path.GetTempPath()),
                enableSplash: false);

            try
            {
                window.InitializeMainViewForTesting(view => WireTestPages(view, home, settings));
                window.Show();
                ContentControl host = Assert.IsType<ContentControl>(window.FindControl<ContentControl>("MainViewHost"));
                FrontierMainView view = Assert.IsType<FrontierMainView>(host.Content);
                FANavigationViewItem homeItem = view.NavigationItems["test-home"];

                view.InvalidateMeasure();
                view.Measure(new Size(1140, 760));
                view.Arrange(new Rect(0, 0, 1140, 760));
                double rootItemTop = homeItem.TranslatePoint(default, view)!.Value.Y;
                Assert.True(homeItem.Bounds.Width > 0 && homeItem.Bounds.Height > 0);

                Assert.True(view.NavigateForTesting("test-settings"));
                Dispatcher.UIThread.RunJobs();
                view.NavigationView.InvalidateMeasure();
                view.InvalidateMeasure();
                view.Measure(new Size(1140, 760));
                view.Arrange(new Rect(0, 0, 1140, 760));
                double historyItemTop = homeItem.TranslatePoint(default, view)!.Value.Y;

                Assert.True(
                    Math.Abs(rootItemTop - historyItemTop) < 0.001,
                    $"Navbar item position changed: root={rootItemTop}, history={historyItemTop}");
                Button backButton = Assert.Single(
                    view.NavigationView.GetVisualDescendants().OfType<Button>(),
                    button => button.Name == "NavigationViewBackButton");
                Assert.True(backButton.IsVisible);
                Assert.True(backButton.IsEnabled);
                Assert.True(backButton.Bounds.Width > 0 && backButton.Bounds.Height > 0);

                backButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal("test-home", view.ActiveRoute);
                Assert.False(view.CanGoBack);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ZzzNavigationRegistry CreateFrontierRegistry(LifecyclePage home, LifecyclePage settings) => new(
        [
            new ZzzNavigationEntry("test-home", "仪表盘", "\uE80F", "\uEA8A", "主页仪表盘", ZzzNavigationPlacement.Primary),
            new ZzzNavigationEntry("test-settings", "设置", "\uE713", "\uE713", "设置选项", ZzzNavigationPlacement.Footer),
        ]);

    private static void WireTestPages(FrontierMainView view, LifecyclePage home, LifecyclePage settings) =>
        view.PageFactoryForTest.CreateRoutePageOverrideForTest = route => route.Key switch
        {
            "test-home" => home,
            "test-settings" => settings,
            _ => throw new InvalidOperationException($"测试注册表没有 {route.Key} 路由。"),
        };

    private static ServiceProvider CreateFrontierTestServices(IZzzAppBackend backend)
    {
        ZzzRunRoot runRoot = new(Path.Combine(Path.GetTempPath(), "zzz-frontier-tests", Guid.NewGuid().ToString("N")));
        return new ServiceCollection()
            .AddSingleton(backend)
            .AddSingleton(runRoot)
            .AddSingleton(new ZzzLauncherMediaService(runRoot, backend))
            .AddSingleton(new ZzzNoticeService(runRoot))
            .AddSingleton(new ZzzDashboardReadinessService(backend, runRoot))
            .AddSingleton<ZzzShellNavigationService>()
            .AddSingleton<ZzzGuiRunIntentService>()
            .AddSingleton<ZzzGuiOperationTracker>()
            .AddSingleton<IZzzDialogService, ZzzDialogService>()
            .BuildServiceProvider();
    }

    private static void EnsureFluentTheme()
    {
        if (Avalonia.Application.Current?.Styles.OfType<FluentAvaloniaTheme>().Any() == false)
        {
            Avalonia.Application.Current.Styles.Add(new FluentAvaloniaTheme());
        }
    }

    private static Color GetOpaqueColor(IBrush? brush)
    {
        ISolidColorBrush solid = Assert.IsAssignableFrom<ISolidColorBrush>(brush);
        Assert.Equal(1d, solid.Opacity);
        Assert.Equal(byte.MaxValue, solid.Color.A);
        return solid.Color;
    }

    private static Color GetColor(IBrush? brush)
    {
        ISolidColorBrush solid = Assert.IsAssignableFrom<ISolidColorBrush>(brush);
        return solid.Color;
    }
}
