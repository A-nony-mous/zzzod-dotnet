using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using FluentAvalonia.Styling;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public sealed partial class FrontierShellWindow : MainWindow
{
    protected override string NavigationControlName => "FrontierNavigation";
    protected override string ContentFrameControlName => "FrontierContentFrame";
    protected override string TitleBarControlName => "FrontierTitleBar";
    protected override string TitleBarIconControlName => "FrontierTitleBarIcon";
    protected override string ToastBarControlName => "FrontierToastBar";

    private readonly ZzzLauncherMediaService _mediaService;
    private readonly Image _backgroundImage;
    private readonly ContentControl _backgroundVideoHost;
    private readonly Border _contentSurface;
    private Bitmap? _backgroundBitmap;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;

    [ActivatorUtilitiesConstructor]
    public FrontierShellWindow(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzShellViewModel shellViewModel,
        IZzzShellWindowRuntime windowRuntime,
        ZzzRunRoot runRoot,
        ZzzLauncherMediaService mediaService)
        : base(services, navigationRegistry, pageLifecycle, navigationService, shellViewModel, windowRuntime, runRoot, false)
    {
        _mediaService = mediaService;
        AvaloniaXamlLoader.Load(this);
        _backgroundImage = this.FindControl<Image>("BackgroundImage")
            ?? throw new InvalidOperationException("前卫 Shell 缺少背景图像层。");
        _backgroundVideoHost = this.FindControl<ContentControl>("BackgroundVideoHost")
            ?? throw new InvalidOperationException("前卫 Shell 缺少背景视频层。");
        InitializeShell(navigationRegistry, pageLifecycle, navigationService, runRoot);
        _contentSurface = this.FindControl<Border>("FrontierContentSurface")
            ?? throw new InvalidOperationException("前卫 Shell 缺少内容表面。");
        ApplyHighContrastSurface();
        Opened += async (_, _) => await LoadBackgroundSafelyAsync().ConfigureAwait(true);
        Closed += (_, _) =>
        {
            _backgroundBitmap?.Dispose();
            _backgroundBitmap = null;
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
        };
    }

    private async Task LoadBackgroundSafelyAsync()
    {
        try
        {
            await LoadBackgroundAsync().ConfigureAwait(true);
        }
        catch
        {
            _backgroundImage.Source = null;
            _backgroundImage.IsVisible = false;
            _backgroundVideoHost.Content = null;
            _backgroundVideoHost.IsVisible = false;
        }
    }

    private async Task LoadBackgroundAsync()
    {
        if (IsHighContrast())
        {
            return;
        }
        IReadOnlyList<ZzzLauncherMediaItem> items = await _mediaService.GetDashboardMediaAsync().ConfigureAwait(true);
        ZzzLauncherMediaItem? item = items.FirstOrDefault(media => !string.IsNullOrWhiteSpace(media.LocalPath));
        if (item?.LocalPath is not { } path)
        {
            return;
        }

        if (item.IsVideo)
        {
            Core.Initialize();
            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);
            _backgroundVideoHost.Content = new VideoView { MediaPlayer = _mediaPlayer };
            using Media media = new(_libVlc, new Uri(path));
            _mediaPlayer.Play(media);
            _backgroundVideoHost.IsVisible = true;
            return;
        }

        try
        {
            _backgroundBitmap?.Dispose();
            _backgroundBitmap = new Bitmap(path);
            _backgroundImage.Source = _backgroundBitmap;
            _backgroundImage.IsVisible = true;
        }
        catch
        {
            _backgroundImage.Source = null;
            _backgroundImage.IsVisible = false;
        }
    }

    private void ApplyHighContrastSurface()
    {
        if (!IsHighContrast())
        {
            return;
        }

        NavigationControl.Background = Brushes.Black;
        NavigationControl.Foreground = Brushes.White;
        _contentSurface.Background = Brushes.Black;
        _contentSurface.BorderBrush = Brushes.White;
        _contentSurface.Opacity = 1;
    }

    private static bool IsHighContrast() =>
        Application.Current?.ActualThemeVariant == FluentAvaloniaTheme.HighContrastTheme;

    protected override void OnRouteChanged(object? sender, string routeKey)
    {
        base.OnRouteChanged(sender, routeKey);
        if (_mediaPlayer is null)
        {
            return;
        }

        if (string.Equals(routeKey, "home", StringComparison.Ordinal))
        {
            _mediaPlayer.Play();
            return;
        }

        _mediaPlayer.Pause();
    }
}
