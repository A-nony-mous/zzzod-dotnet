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
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly ZzzNavigationRegistry _navigationRegistry;
    private readonly ZzzPageLifecycleService _pageLifecycle;
    private readonly ZzzShellNavigationService _navigationService;
    private readonly IZzzDialogService _dialogService;
    private readonly ZzzShellViewModel _shellViewModel;
    private readonly ZzzOverlayController _overlayController;
    private readonly ZzzGlobalInputMonitor _globalInputMonitor;
    private readonly ZzzWindowBackdropService _backdropService;
    private readonly string? _evidenceRoute;
    private readonly InfoBar _toastBar;
    private readonly DispatcherTimer _toastTimer;
    private readonly Frame _contentFrame;
    private readonly NavigationView _navigation;
    private readonly Grid _titleBar;
    private readonly Image _titleBarIcon;
    private readonly Dictionary<string, Control> _pageCache = [];
    private Bitmap? _titleBarIconBitmap;
    private IZzzShellBackNavigationHost? _backNavigationHost;

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
        IZzzDialogService dialogService,
        ZzzShellViewModel shellViewModel,
        ZzzRunRoot runRoot)
    {
        _services = services;
        _navigationRegistry = navigationRegistry;
        _pageLifecycle = pageLifecycle;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _shellViewModel = shellViewModel;
        _overlayController = services.GetRequiredService<ZzzOverlayController>();
        _globalInputMonitor = services.GetRequiredService<ZzzGlobalInputMonitor>();
        _backdropService = services.GetRequiredService<ZzzWindowBackdropService>();
        DataContext = shellViewModel;
        AvaloniaXamlLoader.Load(this);
        _navigation = this.FindControl<NavigationView>("Navigation")
            ?? throw new InvalidOperationException("MainWindow 缺少 NavigationView?");
        _contentFrame = this.FindControl<Frame>("ContentFrame")
            ?? throw new InvalidOperationException("MainWindow 缺少 Frame?");
        _titleBar = this.FindControl<Grid>("TitleBar")
            ?? throw new InvalidOperationException("MainWindow 缺少标题栏。");
        _titleBarIcon = this.FindControl<Image>("TitleBarIcon")
            ?? throw new InvalidOperationException("MainWindow 缺少标题栏图标。");
        _toastBar = this.FindControl<InfoBar>("ToastBar")
            ?? throw new InvalidOperationException("MainWindow 缺少 InfoBar?");
        LoadTitleBarIcon(runRoot.Path);

        ZzzGuiEvidenceSelection evidenceSelection = ZzzGuiEvidenceSelection.FromEnvironment();
        ApplyEvidenceSize(evidenceSelection);
        _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4),
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            _toastBar.IsOpen = false;
        };
        _navigation.MenuItemsSource = _navigationRegistry.Entries
            .Where(entry => entry.Placement is ZzzNavigationPlacement.Primary)
            .ToArray();
        _navigation.FooterMenuItemsSource = _navigationRegistry.Entries
            .Where(entry => entry.Placement is ZzzNavigationPlacement.Footer)
            .ToArray();
        ApplyEvidencePaneState(_navigation, evidenceSelection);

        string initialPage = evidenceSelection.Page;
        if (_navigationRegistry.Entries.All(entry => !string.Equals(entry.Key, initialPage, StringComparison.Ordinal)))
        {
            initialPage = "home";
        }

        _evidenceRoute = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PAGE"))
            ? null
            : initialPage;

        ShowPage(initialPage);
        Opened += OnOpened;
        Closed += (_, _) =>
        {
            _globalInputMonitor.InputPressed -= OnGlobalInputPressed;
            _overlayController.Hide();
            _toastTimer.Stop();
            _titleBarIconBitmap?.Dispose();
            _titleBarIconBitmap = null;
            BindBackNavigationHost(null);
            _pageLifecycle.DisposeCurrent();
            object? current = _contentFrame.Content;
            foreach (Control page in _pageCache.Values)
            {
                if (ReferenceEquals(page, current))
                {
                    continue;
                }

                if (page is IZzzPageLifecycle lifecycle)
                {
                    lifecycle.DisposePage();
                }
            }
        };
        _dialogService.ToastRequested += OnToastRequested;
        _navigationService.NavigationRequested += OnNavigationRequested;
        _globalInputMonitor.InputPressed += OnGlobalInputPressed;
        _ = _globalInputMonitor.EnsureStarted();
    }

    private void OnNavigationSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs args)
    {
        string tag = args.SelectedItem switch
        {
            ZzzNavigationEntry entry => entry.Key,
            NavigationViewItem item when item.Tag is string itemTag => itemTag,
            _ => "home",
        };
        if (_evidenceRoute is not null && !string.Equals(tag, _evidenceRoute, StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(() => ShowPage(_evidenceRoute));
            return;
        }

        ShowPage(tag);
    }

    private void OnNavigationBackRequested(object? sender, NavigationViewBackRequestedEventArgs args)
    {
        if (_backNavigationHost?.CanGoBack == true)
        {
            _backNavigationHost.GoBack();
        }
    }

    private void ShowPage(string tag)
    {
        ZzzNavigationEntry entry = _navigationRegistry.GetRequired(tag);
        ApplyRouteVisualState(entry.Key);
        if (!ReferenceEquals(_navigation.SelectedItem, entry))
        {
            _navigation.SelectedItem = entry;
        }

        Control page = GetPage(entry);
        _contentFrame.Content = page;
        BindBackNavigationHost(page as IZzzShellBackNavigationHost);
        _pageLifecycle.NavigateTo(page, entry.Key);
    }

    private void ApplyRouteVisualState(string routeKey)
    {
        ZzzShellRouteVisualState state = ZzzShellRouteVisualState.ForRoute(routeKey);
        _contentFrame.Margin = state.ContentMargin;
        _titleBar.Classes.Set("home-mode", state.IsHomeMode);
    }

    private Control GetPage(ZzzNavigationEntry entry)
    {
        if (_pageCache.TryGetValue(entry.Key, out Control? page))
        {
            return page;
        }

        page = entry.CreatePage(_services);
        _pageCache[entry.Key] = page;
        return page;
    }

    private void OnToastRequested(object? sender, ZzzToastRequest request)
    {
        Dispatcher.UIThread.Post(() => ShowToast(request.Title, request.Message, TimeSpan.FromSeconds(4), InfoBarSeverity.Informational));
    }

    private void OnNavigationRequested(object? sender, string key)
    {
        Dispatcher.UIThread.Post(() => NavigateToRequestedTarget(key));
    }

    private void NavigateToRequestedTarget(string key)
    {
        ZzzShellNavigationTarget target = _navigationService.Resolve(key);
        ShowPage(target.RootKey);
        if (!string.IsNullOrWhiteSpace(target.PivotHeader)
            && _contentFrame.Content is IZzzPivotNavigationHost pivot)
        {
            pivot.SelectByHeader(target.PivotHeader);
        }
    }

    private void OnIssueClicked(object? sender, RoutedEventArgs args)
    {
        if (!Uri.TryCreate(_shellViewModel.IssueUrl, UriKind.Absolute, out Uri? uri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private async void OnLauncherVersionClicked(object? sender, RoutedEventArgs args) =>
        await CopyVersionAsync(_shellViewModel.LauncherVersion).ConfigureAwait(true);

    private async void OnCodeVersionClicked(object? sender, RoutedEventArgs args) =>
        await CopyVersionAsync(_shellViewModel.CodeVersion).ConfigureAwait(true);

    private async Task CopyVersionAsync(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(version).ConfigureAwait(true);
        ShowToast("已复制版本号", string.Empty, TimeSpan.FromSeconds(2), InfoBarSeverity.Success);
    }

    private void ShowToast(string title, string message, TimeSpan duration, InfoBarSeverity severity)
    {
        _toastBar.Title = title;
        _toastBar.Message = message;
        _toastBar.Severity = severity;
        _toastBar.IsOpen = true;
        _toastTimer.Stop();
        _toastTimer.Interval = duration;
        _toastTimer.Start();
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs args) =>
        WindowState = WindowState.Minimized;

    private void OnOpened(object? sender, EventArgs args)
    {
        _backdropService.Apply(this);
        Activate();
        _overlayController.Start();
    }

    private void OnGlobalInputPressed(object? sender, string key) =>
        _overlayController.TryToggleFromHotkey(key);

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs args)
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

    private void OnMaximizeClicked(object? sender, RoutedEventArgs args) =>
        WindowState = WindowState is WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClicked(object? sender, RoutedEventArgs args) => Close();

    private void BindBackNavigationHost(IZzzShellBackNavigationHost? host)
    {
        if (_backNavigationHost is not null)
        {
            _backNavigationHost.BackNavigationStateChanged -= OnBackNavigationStateChanged;
        }

        _backNavigationHost = host;
        if (_backNavigationHost is not null)
        {
            _backNavigationHost.BackNavigationStateChanged += OnBackNavigationStateChanged;
        }

        UpdateBackNavigationState();
    }

    private void OnBackNavigationStateChanged(object? sender, EventArgs args) =>
        UpdateBackNavigationState();

    private void UpdateBackNavigationState()
    {
        _navigation.IsBackEnabled = _backNavigationHost?.CanGoBack == true;
    }

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
