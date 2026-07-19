using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace ZzzOd.Gui.Shell;

public interface IZzzShellPageHost : IDisposable
{
    event EventHandler<string>? RouteChanged;

    void Initialize(string initialRoute);

    void ShowPage(string route);

    void NavigateToRequestedTarget(string key);

    void GoBack();
}

public sealed class ZzzShellPageHost : IZzzShellPageHost
{
    private readonly IServiceProvider _services;
    private readonly ZzzNavigationRegistry _navigationRegistry;
    private readonly ZzzPageLifecycleService _pageLifecycle;
    private readonly ZzzShellNavigationService _navigationService;
    private readonly FAFrame _contentFrame;
    private readonly FANavigationView _navigation;
    private readonly Dictionary<string, Control> _pageCache = [];
    private IZzzShellBackNavigationHost? _backNavigationHost;
    private string? _activeRoute;
    private string? _navigatingRoute;
    private bool _disposed;

    public ZzzShellPageHost(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        FAFrame contentFrame,
        FANavigationView navigation)
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
        string resolvedRoute = ResolveKnownRoute(route);
        if (string.Equals(_navigatingRoute, resolvedRoute, StringComparison.Ordinal)
            || string.Equals(_activeRoute, resolvedRoute, StringComparison.Ordinal))
        {
            return;
        }

        _navigatingRoute = resolvedRoute;
        try
        {
            ZzzNavigationEntry entry = _navigationRegistry.GetRequired(resolvedRoute);
            if (!ReferenceEquals(_navigation.SelectedItem, entry))
            {
                _navigation.SelectedItem = entry;
            }

            Control page = GetPage(entry);
            _contentFrame.Content = page;
            BindBackNavigationHost(page as IZzzShellBackNavigationHost);
            _pageLifecycle.NavigateTo(page, entry.Key);
            _activeRoute = entry.Key;
            RouteChanged?.Invoke(this, entry.Key);
        }
        finally
        {
            _navigatingRoute = null;
        }
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
        _activeRoute = null;
        _navigatingRoute = null;
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
