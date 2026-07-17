using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public sealed partial class FrontierShellWindow : MainWindow
{
    private readonly ZzzLauncherMediaService _mediaService;
    private readonly Image _backgroundImage;
    private Bitmap? _backgroundBitmap;

    [ActivatorUtilitiesConstructor]
    public FrontierShellWindow(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        IZzzDialogService dialogService,
        ZzzShellViewModel shellViewModel,
        ZzzRunRoot runRoot,
        ZzzLauncherMediaService mediaService)
        : base(services, navigationRegistry, pageLifecycle, navigationService, dialogService, shellViewModel, runRoot, false)
    {
        _mediaService = mediaService;
        AvaloniaXamlLoader.Load(this);
        _backgroundImage = this.FindControl<Image>("BackgroundImage")
            ?? throw new InvalidOperationException("前卫 Shell 缺少背景图像层。");
        InitializeShell(navigationRegistry, pageLifecycle, navigationService, runRoot);
        Opened += async (_, _) => await LoadBackgroundAsync().ConfigureAwait(true);
        Closed += (_, _) =>
        {
            _backgroundBitmap?.Dispose();
            _backgroundBitmap = null;
        };
    }

    private async Task LoadBackgroundAsync()
    {
        IReadOnlyList<ZzzLauncherMediaItem> items = await _mediaService.GetDashboardMediaAsync().ConfigureAwait(true);
        ZzzLauncherMediaItem? item = items.FirstOrDefault(media => media.IsImage && !string.IsNullOrWhiteSpace(media.LocalPath));
        if (item?.LocalPath is not { } path)
        {
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
}
