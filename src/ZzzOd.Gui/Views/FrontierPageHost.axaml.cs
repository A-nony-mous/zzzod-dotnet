using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

internal enum ZzzFrontierPageLayout
{
    Standard,
    Surface,
}

/// <summary>
/// 前卫页面的视觉边界。业务页面仍由现有工厂创建，宿主只负责 sample 容器和生命周期转发。
/// </summary>
internal partial class FrontierPageHost : UserControl, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private readonly Control _page;
    private readonly IZzzPageLifecycle? _lifecycle;
    private readonly IZzzShellBackNavigationHost? _backHost;
    private readonly IZzzPivotNavigationHost? _pivotHost;
    private readonly TextBlock _headerText;
    private readonly Grid _standardLayout;
    private readonly Grid _surfaceLayout;
    private readonly ContentControl _standardContent;
    private readonly ContentControl _surfaceContent;
    private bool _disposed;

    public FrontierPageHost(string routeKey, string title, Control page, ZzzFrontierPageLayout layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(page);

        RouteKey = routeKey;
        Title = title;
        _page = page;
        Classes.Add("frontier-page-host");
        page.Classes.Add("frontier-page-content");
        _lifecycle = page as IZzzPageLifecycle;
        _backHost = page as IZzzShellBackNavigationHost;
        _pivotHost = page as IZzzPivotNavigationHost;

        AvaloniaXamlLoader.Load(this);
        _headerText = Required<TextBlock>("HeaderText");
        _standardLayout = Required<Grid>("StandardLayout");
        _surfaceLayout = Required<Grid>("SurfaceLayout");
        _standardContent = Required<ContentControl>("StandardContent");
        _surfaceContent = Required<ContentControl>("SurfaceContent");

        _headerText.Text = title;
        bool standard = layout is ZzzFrontierPageLayout.Standard;
        _standardLayout.IsVisible = standard;
        _surfaceLayout.IsVisible = !standard;
        if (standard)
        {
            _standardContent.Content = page;
        }
        else
        {
            _surfaceContent.Content = page;
        }

        if (page is not IZzzPageLifecycle)
        {
            throw new InvalidOperationException($"前卫路由 {routeKey} 的页面未实现 IZzzPageLifecycle。");
        }

        if (_backHost is not null)
        {
            _backHost.BackNavigationStateChanged += OnInnerBackNavigationStateChanged;
        }
    }

    public string RouteKey { get; }

    public string Title { get; }

    public Control InnerPage => _page;

    public event EventHandler? BackNavigationStateChanged;

    public bool CanGoBack => _backHost?.CanGoBack == true;

    public bool SelectByHeader(string header) => _pivotHost?.SelectByHeader(header) == true;

    public void GoBack() => _backHost?.GoBack();

    public void OnPageShown() => _lifecycle?.OnPageShown();

    public void OnPageHidden() => _lifecycle?.OnPageHidden();

    public void OnPageLeave() => _lifecycle?.OnPageLeave();

    public void CancelPageOperations(string reason) => _lifecycle?.CancelPageOperations(reason);

    public void DisposePage()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_backHost is not null)
        {
            _backHost.BackNavigationStateChanged -= OnInnerBackNavigationStateChanged;
        }

        _lifecycle?.DisposePage();
    }

    private void OnInnerBackNavigationStateChanged(object? sender, EventArgs args) =>
        BackNavigationStateChanged?.Invoke(this, args);

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"前卫页面宿主缺少 {name}。");
}
