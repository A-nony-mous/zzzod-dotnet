using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using ZzzOd.AppHost;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

internal sealed partial class FrontierMainView : UserControl, IDisposable
{
    private static readonly FontFamily NavigationIconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private readonly ZzzPageLifecycleService _pageLifecycle;
    private readonly ZzzShellNavigationService _navigationService;
    private readonly ZzzShellViewModel _shellViewModel;
    private readonly IZzzShellWindowRuntime _windowRuntime;
    private readonly ZzzFrontierPageFactory _pageFactory;
    private readonly ZzzFrontierRoute _rootRoute;
    private readonly ZzzFrontierRoute _initialRoute;
    private readonly FANavigationView _navigation;
    private readonly FAFrame _frame;
    private readonly Border _paneTitleSpacer;
    private readonly Image _windowIcon;
    private readonly Dictionary<string, FANavigationViewItem> _navigationItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FAFontIcon> _navigationIcons = new(StringComparer.Ordinal);
    private IZzzShellBackNavigationHost? _backNavigationHost;
    private Bitmap? _windowIconBitmap;
    private string? _activeRoute;
    private string? _pendingPivotHeader;
    private CancellationTokenSource? _navigationIconAnimation;
    private bool _navigating;
    private bool _disposed;

    internal FrontierMainView(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzShellViewModel shellViewModel,
        IZzzShellWindowRuntime windowRuntime,
        ZzzRunRoot runRoot,
        Window window)
    {
        _pageLifecycle = pageLifecycle;
        _navigationService = navigationService;
        _shellViewModel = shellViewModel;
        _windowRuntime = windowRuntime;
        _pageFactory = new ZzzFrontierPageFactory(services, navigationRegistry);
        _rootRoute = _pageFactory.FindRoute("home") ?? _pageFactory.Routes[0];

        DataContext = shellViewModel;
        AvaloniaXamlLoader.Load(this);
        _navigation = Required<FANavigationView>("NavView");
        _frame = Required<FAFrame>("FrameView");
        _paneTitleSpacer = Required<Border>("PaneTitleSpacer");
        _windowIcon = Required<Image>("WindowIcon");
        FAInfoBar toastBar = Required<FAInfoBar>("ToastBar");

        _frame.NavigationPageFactory = _pageFactory;
        _frame.Navigated += OnFrameNavigated;
        _navigation.ItemInvoked += OnNavigationItemInvoked;
        _navigation.BackRequested += OnNavigationBackRequested;
        _navigationService.NavigationRequested += OnNavigationRequested;

        CreateNavigationItems();
        LoadWindowIcon(runRoot.Path);
        _windowRuntime.Attach(window, toastBar);

        string initialRoute = ZzzGuiEvidenceSelection.FromEnvironment().Page;
        _initialRoute = _pageFactory.FindRoute(initialRoute)
            ?? _rootRoute;
    }

    internal FAFrame NavigationFrame => _frame;

    internal FANavigationView NavigationView => _navigation;

    internal string? ActiveRoute => _activeRoute;

    internal int CreatedPageCount => _pageFactory.CreatedPages.Count;

    internal ZzzFrontierPageFactory PageFactoryForTest => _pageFactory;

    internal bool CanGoBack =>
        _backNavigationHost?.CanGoBack == true || _frame.CanGoBack || CanReturnToRoot;

    internal IReadOnlyDictionary<string, FANavigationViewItem> NavigationItems => _navigationItems;

    internal void StartInitialNavigation()
    {
        if (_disposed || _activeRoute is not null)
        {
            return;
        }

        NavigateRoute(_initialRoute);
    }

    internal void ShowToast(string title, string message, TimeSpan duration, FAInfoBarSeverity severity) =>
        _windowRuntime.ShowToast(title, message, duration, severity);

    internal bool NavigateForTesting(string routeKey)
    {
        ZzzFrontierRoute? route = _pageFactory.FindRoute(routeKey);
        if (route is null)
        {
            return false;
        }

        NavigateRoute(route);
        return string.Equals(_activeRoute, routeKey, StringComparison.Ordinal);
    }

    internal void GoBackForTesting() => GoBackCore();

    private void CreateNavigationItems()
    {
        List<FANavigationViewItem> primary = [];
        List<FANavigationViewItem> footer = [];
        foreach (ZzzFrontierRoute route in _pageFactory.Routes)
        {
            FAFontIcon icon = new()
            {
                Glyph = route.Entry.IconGlyph,
                FontFamily = NavigationIconFont,
                FontSize = 32,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                RenderTransformOrigin = RelativePoint.Center,
            };
            icon.Classes.Add("frontier-navigation-icon");

            TextBlock label = new()
            {
                Text = route.Entry.Text,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            };
            label.Classes.Add("frontier-navigation-label");

            StackPanel itemBody = new()
            {
                Width = 64,
                Height = 64,
                Spacing = 4,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children = { icon, label },
            };

            FANavigationViewItem item = new()
            {
                Tag = route,
                Content = itemBody,
            };
            item.Classes.Add("frontier-navigation-item");
            AutomationProperties.SetName(item, route.Entry.AccessibleName);
            ToolTip.SetTip(item, route.Entry.AccessibleName);
            _navigationItems.Add(route.Key, item);
            _navigationIcons.Add(route.Key, icon);

            if (route.Entry.Placement is ZzzNavigationPlacement.Primary)
            {
                primary.Add(item);
            }
            else
            {
                footer.Add(item);
            }
        }

        _navigation.MenuItemsSource = primary;
        _navigation.FooterMenuItemsSource = footer;
    }

    private void NavigateRoute(ZzzFrontierRoute route)
    {
        if (_disposed || _navigating || string.Equals(_activeRoute, route.Key, StringComparison.Ordinal))
        {
            ApplyPendingPivot();
            return;
        }

        _navigating = true;
        try
        {



            if (!_frame.NavigateFromObject(route))
            {
                _pendingPivotHeader = null;
            }
        }
        finally
        {
            _navigating = false;
        }
    }

    private void OnNavigationItemInvoked(object? sender, FANavigationViewItemInvokedEventArgs args)
    {
        ZzzFrontierRoute? route = (args.InvokedItemContainer as FANavigationViewItem)?.Tag as ZzzFrontierRoute
            ?? (args.InvokedItem as FANavigationViewItem)?.Tag as ZzzFrontierRoute
            ?? args.InvokedItem as ZzzFrontierRoute;
        if (route is not null)
        {
            NavigateRoute(route);
        }
    }

    private void OnFrameNavigated(object sender, FANavigationEventArgs args)
    {
        if (args.Content is not Control page || _pageFactory.FindRoute(page) is not { } route)
        {
            return;
        }

        bool routeChanged = !string.Equals(_activeRoute, route.Key, StringComparison.Ordinal);
        _activeRoute = route.Key;
        _pageFactory.MarkCurrent(page);
        BindBackNavigationHost(page as IZzzShellBackNavigationHost);
        _pageLifecycle.NavigateTo(page, route.Key);

        foreach ((string key, FANavigationViewItem item) in _navigationItems)
        {
            bool selected = string.Equals(key, route.Key, StringComparison.Ordinal);
            if (selected)
            {
                _navigation.SelectedItem = item;
            }

            ZzzFrontierRoute itemRoute = (ZzzFrontierRoute)item.Tag!;
            _navigationIcons[key].Glyph = selected
                ? itemRoute.Entry.SelectedIconGlyph
                : itemRoute.Entry.IconGlyph;
        }

        if (routeChanged
            && _navigationIcons.TryGetValue(route.Key, out FAFontIcon? icon)
            && TopLevel.GetTopLevel(icon) is not null)
        {
            StartSelectedIconAnimation(icon);
        }

        ApplyPendingPivot();
        UpdateBackState();
    }

    private void OnNavigationBackRequested(object? sender, FANavigationViewBackRequestedEventArgs args)
    {
        GoBackCore();
    }

    private void GoBackCore()
    {
        if (_backNavigationHost?.CanGoBack == true)
        {
            _backNavigationHost.GoBack();
        }
        else if (_frame.CanGoBack)
        {
            _frame.GoBack();
        }
        else if (CanReturnToRoot)
        {
            NavigateRoute(_rootRoute);
            _frame.BackStack.Clear();
        }

        UpdateBackState();
    }

    private void OnNavigationRequested(object? sender, string key)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ZzzShellNavigationTarget target = _navigationService.Resolve(key);
            ZzzFrontierRoute? route = _pageFactory.FindRoute(target.RootKey);
            if (route is null)
            {
                return;
            }

            _pendingPivotHeader = target.PivotHeader;
            NavigateRoute(route);
        });
    }

    private void ApplyPendingPivot()
    {
        if (string.IsNullOrWhiteSpace(_pendingPivotHeader)
            || _frame.Content is not IZzzPivotNavigationHost pivot)
        {
            return;
        }

        string header = _pendingPivotHeader;
        _pendingPivotHeader = null;
        pivot.SelectByHeader(header);
        UpdateBackState();
    }

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
    }

    private void OnBackNavigationStateChanged(object? sender, EventArgs args) => UpdateBackState();

    private void UpdateBackState()
    {
        bool canGoBack = CanGoBack;
        // FANavigationView recalculates its pane rows synchronously when the back button
        // visibility changes. Update the replacement title spacer first so the pane keeps
        // exactly one 48 DIP title-bar row in both states.
        _paneTitleSpacer.IsVisible = !canGoBack;
        _navigation.IsBackEnabled = canGoBack;
        _navigation.IsBackButtonVisible = canGoBack;
        _windowIcon.Margin = canGoBack
            ? new Thickness(48, 4, 12, 4)
            : new Thickness(12, 4);
    }

    private bool CanReturnToRoot =>
        _activeRoute is not null
        && !string.Equals(_activeRoute, _rootRoute.Key, StringComparison.Ordinal);

    private async void StartSelectedIconAnimation(FAFontIcon icon)
    {
        _navigationIconAnimation?.Cancel();
        CancellationTokenSource animation = new();
        _navigationIconAnimation = animation;

        try
        {
            await AnimateSelectedIconAsync(icon, animation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (animation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError("前卫导航图标动画失败: {0}", exception);
        }
        finally
        {
            icon.Opacity = 1;
            if (icon.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }

            if (ReferenceEquals(_navigationIconAnimation, animation))
            {
                _navigationIconAnimation = null;
            }

            animation.Dispose();
        }
    }

    private static async Task AnimateSelectedIconAsync(FAFontIcon icon, CancellationToken cancellationToken)
    {
        icon.RenderTransform = new ScaleTransform(1, 1);
        Animation animation = new()
        {
            Duration = TimeSpan.FromMilliseconds(180),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(OpacityProperty, 0.35d),
                        new Setter(ScaleTransform.ScaleXProperty, 0.82d),
                        new Setter(ScaleTransform.ScaleYProperty, 0.82d),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(0.65d),
                    Setters =
                    {
                        new Setter(OpacityProperty, 1d),
                        new Setter(ScaleTransform.ScaleXProperty, 1.08d),
                        new Setter(ScaleTransform.ScaleYProperty, 1.08d),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(OpacityProperty, 1d),
                        new Setter(ScaleTransform.ScaleXProperty, 1d),
                        new Setter(ScaleTransform.ScaleYProperty, 1d),
                    },
                },
            },
        };

        await animation.RunAsync(icon, cancellationToken).ConfigureAwait(true);
    }

    private void OnIssueClicked(object? sender, RoutedEventArgs args)
    {
        if (Uri.TryCreate(_shellViewModel.IssueUrl, UriKind.Absolute, out Uri? uri))
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
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
        _windowRuntime.ShowToast("已复制版本号", string.Empty, TimeSpan.FromSeconds(2), FAInfoBarSeverity.Success);
    }

    private void LoadWindowIcon(string runRoot)
    {
        string path = Path.Combine(runRoot, "assets", "ui", "logo.ico");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            _windowIconBitmap = new Bitmap(path);
            _windowIcon.Source = _windowIconBitmap;
        }
        catch
        {
            _windowIconBitmap?.Dispose();
            _windowIconBitmap = null;
            _windowIcon.Source = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frame.Navigated -= OnFrameNavigated;
        _navigation.ItemInvoked -= OnNavigationItemInvoked;
        _navigation.BackRequested -= OnNavigationBackRequested;
        _navigationService.NavigationRequested -= OnNavigationRequested;
        _navigationIconAnimation?.Cancel();
        BindBackNavigationHost(null);

        Control? current = _frame.Content as Control;
        _pageLifecycle.DisposeCurrent();
        _pageFactory.DisposeCachedPages(current);
        _windowRuntime.Dispose();
        _windowIconBitmap?.Dispose();
        _windowIconBitmap = null;
        _windowIcon.Source = null;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"前卫 MainView 缺少 {name}。");
}
