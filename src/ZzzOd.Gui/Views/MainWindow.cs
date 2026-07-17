using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly ZzzShellNavigationService _navigationService;
    private readonly ZzzShellViewModel _shellViewModel;
    private readonly IZzzShellWindowRuntime _windowRuntime;
    private string? _evidenceRoute;
    private InfoBar _toastBar = null!;
    private Frame _contentFrame = null!;
    private NavigationView _navigation = null!;
    private Grid _titleBar = null!;
    private Image _titleBarIcon = null!;
    private IZzzShellPageHost _pageHost = null!;
    private Bitmap? _titleBarIconBitmap;

    protected virtual string NavigationControlName => "Navigation";

    protected virtual string ContentFrameControlName => "ContentFrame";

    protected virtual string TitleBarControlName => "TitleBar";

    protected virtual string TitleBarIconControlName => "TitleBarIcon";

    protected virtual string ToastBarControlName => "ToastBar";

    protected NavigationView NavigationControl => _navigation;

    public MainWindow()
    {
        throw new InvalidOperationException("MainWindow 必须通过应用宿主的依赖注入创建。");
    }

    [ActivatorUtilitiesConstructor]
    public MainWindow(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzShellViewModel shellViewModel,
        IZzzShellWindowRuntime windowRuntime,
        ZzzRunRoot runRoot)
        : this(services, navigationRegistry, pageLifecycle, navigationService, shellViewModel, windowRuntime, runRoot, true)
    {
    }

    protected MainWindow(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzShellViewModel shellViewModel,
        IZzzShellWindowRuntime windowRuntime,
        ZzzRunRoot runRoot,
        bool loadMainWindowAxaml)
    {
        _services = services;
        _navigationService = navigationService;
        _shellViewModel = shellViewModel;
        _windowRuntime = windowRuntime;
        DataContext = shellViewModel;
        if (loadMainWindowAxaml)
        {
            AvaloniaXamlLoader.Load(this);
            InitializeShell(navigationRegistry, pageLifecycle, navigationService, runRoot);
        }
    }

    protected void InitializeShell(
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzRunRoot runRoot)
    {
        _navigation = this.FindControl<NavigationView>(NavigationControlName)
            ?? throw new InvalidOperationException("MainWindow 缺少 NavigationView?");
        _contentFrame = this.FindControl<Frame>(ContentFrameControlName)
            ?? throw new InvalidOperationException("MainWindow 缺少 Frame?");
        _titleBar = this.FindControl<Grid>(TitleBarControlName)
            ?? throw new InvalidOperationException("MainWindow 缺少标题栏。");
        _titleBarIcon = this.FindControl<Image>(TitleBarIconControlName)
            ?? throw new InvalidOperationException("MainWindow 缺少标题栏图标。");
        _toastBar = this.FindControl<InfoBar>(ToastBarControlName)
            ?? throw new InvalidOperationException("MainWindow 缺少 InfoBar?");
        LoadTitleBarIcon(runRoot.Path);

        ZzzGuiEvidenceSelection evidenceSelection = ZzzGuiEvidenceSelection.FromEnvironment();
        ApplyEvidenceSize(evidenceSelection);
        ApplyEvidencePaneState(_navigation, evidenceSelection);

        _pageHost = new ZzzShellPageHost(
            _services,
            navigationRegistry,
            pageLifecycle,
            navigationService,
            _contentFrame,
            _navigation);
        _pageHost.RouteChanged += OnRouteChanged;
        string initialPage = evidenceSelection.Page;

        _evidenceRoute = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PAGE"))
            ? null
            : initialPage;

        _pageHost.Initialize(initialPage);
        Closed += (_, _) =>
        {
            _titleBarIconBitmap?.Dispose();
            _titleBarIconBitmap = null;
            _pageHost.RouteChanged -= OnRouteChanged;
            _pageHost.Dispose();
            _navigationService.NavigationRequested -= OnNavigationRequested;
        };
        _navigationService.NavigationRequested += OnNavigationRequested;
        _windowRuntime.Attach(this, _toastBar);
    }

    protected void OnNavigationSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs args)
    {
        string tag = args.SelectedItem switch
        {
            ZzzNavigationEntry entry => entry.Key,
            NavigationViewItem item when item.Tag is string itemTag => itemTag,
            _ => "home",
        };
        if (_evidenceRoute is not null && !string.Equals(tag, _evidenceRoute, StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(() => _pageHost.ShowPage(_evidenceRoute));
            return;
        }

        _pageHost.ShowPage(tag);
    }

    protected void OnNavigationBackRequested(object? sender, NavigationViewBackRequestedEventArgs args)
    {
        _pageHost.GoBack();
    }

    protected virtual void OnRouteChanged(object? sender, string routeKey)
    {
        ZzzShellRouteVisualState state = ZzzShellRouteVisualState.ForRoute(routeKey);
        _contentFrame.Margin = state.ContentMargin;
        _titleBar.Classes.Set("home-mode", state.IsHomeMode);
    }

    private void OnNavigationRequested(object? sender, string key)
    {
        Dispatcher.UIThread.Post(() => _pageHost.NavigateToRequestedTarget(key));
    }

    protected void OnIssueClicked(object? sender, RoutedEventArgs args)
    {
        if (!Uri.TryCreate(_shellViewModel.IssueUrl, UriKind.Absolute, out Uri? uri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    protected async void OnLauncherVersionClicked(object? sender, RoutedEventArgs args) =>
        await CopyVersionAsync(_shellViewModel.LauncherVersion).ConfigureAwait(true);

    protected async void OnCodeVersionClicked(object? sender, RoutedEventArgs args) =>
        await CopyVersionAsync(_shellViewModel.CodeVersion).ConfigureAwait(true);

    private async Task CopyVersionAsync(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(version).ConfigureAwait(true);
        _windowRuntime.ShowToast("已复制版本号", string.Empty, TimeSpan.FromSeconds(2), InfoBarSeverity.Success);
    }

    protected void OnMinimizeClicked(object? sender, RoutedEventArgs args) =>
        WindowState = WindowState.Minimized;

    protected void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        PointerPoint point = args.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        if (args.ClickCount == 2)
        {
            WindowState = WindowState is WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (!IsActive)
        {
            Activate();
        }

        BeginMoveDrag(args);
    }

    protected void OnMaximizeClicked(object? sender, RoutedEventArgs args) =>
        WindowState = WindowState is WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    protected void OnCloseClicked(object? sender, RoutedEventArgs args) => Close();

    private void LoadTitleBarIcon(string runRoot)
    {
        string iconPath = Path.Combine(runRoot, "assets", "ui", "logo.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        try
        {
            _titleBarIconBitmap = new Bitmap(iconPath);
            _titleBarIcon.Source = _titleBarIconBitmap;
        }
        catch
        {
            _titleBarIconBitmap?.Dispose();
            _titleBarIconBitmap = null;
            _titleBarIcon.Source = null;
        }
    }

    private void ApplyEvidenceSize(ZzzGuiEvidenceSelection evidenceSelection)
    {
        if (evidenceSelection.Width is not double width || evidenceSelection.Height is not double height)
        {
            return;
        }

        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);
    }

    private static void ApplyEvidencePaneState(NavigationView navigation, ZzzGuiEvidenceSelection evidenceSelection)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PANE")))
        {
            return;
        }

        if (string.Equals(evidenceSelection.Pane, "compact", StringComparison.OrdinalIgnoreCase))
        {
            navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftCompact;
            navigation.IsPaneOpen = false;
            return;
        }

        if (string.Equals(evidenceSelection.Pane, "expanded", StringComparison.OrdinalIgnoreCase))
        {
            navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            navigation.IsPaneOpen = true;
        }
    }
}
