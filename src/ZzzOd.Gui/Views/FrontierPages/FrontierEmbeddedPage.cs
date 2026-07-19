using Avalonia.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages;

/// <summary>
/// Frontier 根页面的生命周期和二级导航转发。具体页面只声明自己的 sample 容器，业务控件仍由真实页面工厂提供。
/// </summary>
internal abstract class FrontierEmbeddedPage : UserControl, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private readonly Control _content;
    private readonly IZzzPageLifecycle? _lifecycle;
    private readonly IZzzShellBackNavigationHost? _backHost;
    private readonly IZzzPivotNavigationHost? _pivotHost;
    private bool _disposed;

    protected FrontierEmbeddedPage(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
        _lifecycle = content as IZzzPageLifecycle;
        _backHost = content as IZzzShellBackNavigationHost;
        _pivotHost = content as IZzzPivotNavigationHost;

        if (_lifecycle is null)
        {
            throw new InvalidOperationException($"Frontier 页面 {content.GetType().Name} 未实现 IZzzPageLifecycle。");
        }

        if (_backHost is not null)
        {
            _backHost.BackNavigationStateChanged += OnBackNavigationStateChanged;
        }
    }

    protected Control EmbeddedContent => _content;

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
            _backHost.BackNavigationStateChanged -= OnBackNavigationStateChanged;
        }

        _lifecycle?.DisposePage();
    }

    protected void SetContent(ContentControl host)
    {
        ArgumentNullException.ThrowIfNull(host);
        host.Content = _content;
    }

    private void OnBackNavigationStateChanged(object? sender, EventArgs args) =>
        BackNavigationStateChanged?.Invoke(this, args);
}
