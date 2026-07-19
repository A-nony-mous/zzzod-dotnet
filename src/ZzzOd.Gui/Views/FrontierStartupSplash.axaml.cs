using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAvalonia.UI.Windowing;

namespace ZzzOd.Gui.Views;

internal sealed partial class FrontierStartupSplashContent : UserControl, IDisposable
{
    private Bitmap? _logoBitmap;

    internal FrontierStartupSplashContent(string runRoot)
    {
        AvaloniaXamlLoader.Load(this);

        string logoPath = Path.Combine(runRoot, "assets", "ui", "logo.ico");
        if (!File.Exists(logoPath))
        {
            return;
        }

        try
        {
            _logoBitmap = new Bitmap(logoPath);
            Required<Image>("LogoImage").Source = _logoBitmap;
        }
        catch
        {
            _logoBitmap?.Dispose();
            _logoBitmap = null;
        }
    }

    public void Dispose()
    {
        Required<Image>("LogoImage").Source = null;
        _logoBitmap?.Dispose();
        _logoBitmap = null;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"前卫启动画面缺少 {name}。");
}

internal sealed class FrontierStartupSplashScreen : IFAApplicationSplashScreen, IDisposable
{
    private readonly FrontierStartupSplashContent _content;
    private readonly Action _createMainView;
    private readonly Action _startInitialNavigation;

    internal FrontierStartupSplashScreen(
        string runRoot,
        Action createMainView,
        Action startInitialNavigation)
    {
        _content = new FrontierStartupSplashContent(runRoot);
        _createMainView = createMainView;
        _startInitialNavigation = startInitialNavigation;
    }

    public string AppName => string.Empty;

    public IImage AppIcon => null!;

    public object SplashScreenContent => _content;

    public int MinimumShowTime => 0;

    public async Task RunTasks(CancellationToken cancellationToken)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                _createMainView,
                DispatcherPriority.Background,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(
                _startInitialNavigation,
                DispatcherPriority.Background,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public void Dispose() => _content.Dispose();
}
