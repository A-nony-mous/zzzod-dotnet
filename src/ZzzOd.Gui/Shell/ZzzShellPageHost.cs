using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace ZzzOd.Gui.Shell;

public sealed class ZzzShellPageHost : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ZzzNavigationRegistry _navigationRegistry;
    private readonly ZzzPageLifecycleService _pageLifecycle;
    private readonly ZzzShellNavigationService _navigationService;
    private readonly Frame _contentFrame;
    private readonly NavigationView _navigation;
    private readonly Dictionary<string, Control> _pageCache = [];
    private IZzzShellBackNavigationHost? _backNavigationHost;
    private bool _disposed;

    public ZzzShellPageHost(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        Frame contentFrame,
        NavigationView navigation)
    {
        _services = services;
        _navigationRegistry = navigationRegistry;
        _pageLifecycle = pageLifecycle;
        _navigationService = navigationService;
        _contentFrame = contentFrame;
        _navigation = navigation;
    }

    public event EventHandler<string>? RouteChanged;

    public void Initialize(string initialRoute)
    {
        ThrowIfDisposed();
        _navigation.MenuItemsSource = _navigationRegistry.Entries
            .Where(entry => entry.Placement is ZzzNavigationPlacement.Primary)
            .ToArray();
        _navigation.FooterMenuItemsSource = _navigationRegistry.Entries
            .Where(entry => entry.Placement is ZzzNavigationPlacement.Footer)
            .ToArray();
        ShowPage(ResolveKnownRoute(initialRoute));
    }

    public void ShowPage(string route)
    {
        ThrowIfDisposed();
        ZzzNavigationEntry entry = _navigationRegistry.GetRequired(ResolveKnownRoute(route));
        if (!ReferenceEquals(_navigation.SelectedItem, entry))
        {
            _navigation.SelectedItem = entry;
        }

        Control page = GetPage(entry);
        _contentFrame.Content = page;
        BindBackNavigationHost(page as IZzzShellBackNavigationHost);
        _pageLifecycle.NavigateTo(page, entry.Key);
        RouteChanged?.Invoke(this, entry.Key);
    }

    public void NavigateToRequestedTarget(string key)
    {
        ThrowIfDisposed();
        ZzzShellNavigationTarget target = _navigationService.Resolve(key);
        ShowPage(target.RootKey);
        if (!string.IsNullOrWhiteSpace(target.PivotHeader)
            && _contentFrame.Content is IZzzPivotNavigationHost pivot)
        {
            pivot.SelectByHeader(target.PivotHeader);
        }
    }

    public void GoBack()
    {
        ThrowIfDisposed();
        if (_backNavigationHost?.CanGoBack == true)
        {
            _backNavigationHost.GoBack();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        BindBackNavigationHost(null);
        _pageLifecycle.DisposeCurrent();
        object? current = _contentFrame.Content;
        foreach (Control page in _pageCache.Values)
        {
            if (!ReferenceEquals(page, current) && page is IZzzPageLifecycle lifecycle)
            {
                lifecycle.DisposePage();
            }
        }

        _pageCache.Clear();
    }

    private string ResolveKnownRoute(string route) =>
        _navigationRegistry.Entries.Any(entry => string.Equals(entry.Key, route, StringComparison.Ordinal))
            ? route
            : "home";

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

        _navigation.IsBackEnabled = _backNavigationHost?.CanGoBack == true;
    }

    private void OnBackNavigationStateChanged(object? sender, EventArgs args) =>
        _navigation.IsBackEnabled = _backNavigationHost?.CanGoBack == true;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
