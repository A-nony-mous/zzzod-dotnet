using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using OpenCvSharp;
using AvaloniaWindow = Avalonia.Controls.Window;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Home;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Notices;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Controls.Home;
using ZzzOd.Gui.PageModels.Home;

namespace ZzzOd.Gui.Views.FrontierPages.Home;

public sealed partial class FrontierHomePage : UserControl, IZzzPageLifecycle
{
    private static readonly TimeSpan DashboardLoadTimeout = TimeSpan.FromSeconds(20);
    private readonly ZzzLauncherMediaService _mediaService;
    private readonly ZzzNoticeService _noticeService;
    private readonly ZzzHomeProjectSettingsViewModel _projectSettings;
    private readonly ZzzHomeThemeSettingsViewModel _themeSettings;
    private readonly ZzzDashboardReadinessService _readinessService;
    private readonly ZzzShellNavigationService _navigation;
    private readonly ZzzGuiRunIntentService _runIntent;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly DispatcherTimer _bannerTimer;
    private readonly Image _bannerImage;
    private readonly Border _mediaPlaceholder;
    private readonly ZzzNoticeCard _noticeCard;
    private readonly TextBlock _mediaPageCount;
    private readonly Button _startButton;
    private readonly TextBlock _startButtonText;
    private readonly FASymbolIcon _startButtonIcon;
    private readonly FATeachingTip _quickLinkTeachingTip;
    private readonly FAContentDialog _preFlightDialog;
    private readonly ItemsControl _preFlightIssueList;
    private CancellationTokenSource? _activationCancellation;
    private IReadOnlyList<ZzzLauncherMediaItem> _mediaItems = [];
    private int _mediaIndex;
    private bool _shown;
    private bool _pointerOverHome;
    private bool _windowActive = true;
    private AvaloniaWindow? _window;
    private string _readinessText = string.Empty;
    private string _mediaStatusText = string.Empty;
    private ZzzHomeMediaLoadState _mediaLoadState = ZzzHomeMediaLoadState.Placeholder;
    private readonly string _noticeUrl;

    public FrontierHomePage()
    {
        throw new InvalidOperationException("FrontierHomePage 必须通过页面工厂提供真实服务。");
    }

    public FrontierHomePage(
        IZzzAppBackend backend,
        ZzzLauncherMediaService mediaService,
        ZzzNoticeService noticeService,
        ZzzDashboardReadinessService readinessService,
        ZzzShellNavigationService navigation,
        ZzzGuiRunIntentService runIntent,
        IZzzDialogService dialogService,
        ZzzGuiOperationTracker? operations = null)
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        _projectSettings = new ZzzHomeProjectSettingsViewModel(backend);
        _themeSettings = new ZzzHomeThemeSettingsViewModel(backend);
        _mediaService = mediaService;
        _noticeService = noticeService;
        _readinessService = readinessService;
        _navigation = navigation;
        _runIntent = runIntent;
        _operations = operations ?? new ZzzGuiOperationTracker();
        AvaloniaXamlLoader.Load(this);

        _bannerImage = this.FindControl<Image>("BannerImage")
            ?? throw new InvalidOperationException("首页缺少背景媒体承载控件。");
        _mediaPlaceholder = this.FindControl<Border>("MediaPlaceholder")
            ?? throw new InvalidOperationException("首页缺少媒体占位层。");
        _noticeCard = this.FindControl<ZzzNoticeCard>("NoticeCard")
            ?? throw new InvalidOperationException("首页缺少 NoticeCard。");
        _noticeCard.RetryRequested += OnNoticeRetryRequested;
        _mediaPageCount = this.FindControl<TextBlock>("MediaPageCountText")
            ?? throw new InvalidOperationException("首页缺少媒体页码。");
        _startButton = this.FindControl<Button>("StartButton")
            ?? throw new InvalidOperationException("首页缺少启动按钮。");
        _startButtonText = this.FindControl<TextBlock>("StartButtonText")
            ?? throw new InvalidOperationException("首页缺少启动按钮文本。");
        _startButtonIcon = this.FindControl<FASymbolIcon>("StartButtonIcon")
            ?? throw new InvalidOperationException("首页缺少启动按钮图标。");
        _quickLinkTeachingTip = this.FindControl<FATeachingTip>("QuickLinkTeachingTip")
            ?? throw new InvalidOperationException("首页缺少 TeachingTip。");
        _preFlightDialog = (FAContentDialog)Resources["PreFlightDialog"]!
            ?? throw new InvalidOperationException("首页缺少 ContentDialog。");
        _preFlightIssueList = (ItemsControl)((StackPanel)_preFlightDialog.Content!).Children[1];

        DataContext = _projectSettings;
        _projectSettings.OnPageShown();
        QuickLinks = _projectSettings.QuickLinks;
        _noticeUrl = _projectSettings.NoticeUrl;
        ApplyQuickLinkAvailability();

        _bannerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _bannerTimer.Tick += (_, _) => ShowNextMedia();
    }

    public IReadOnlyList<ZzzHomeQuickLink> QuickLinks { get; }

    public IReadOnlyList<string> VisibleActionLabels => QuickLinks.Select(link => link.Label).ToArray();

    public ZzzDashboardReadinessResult LastReadiness { get; private set; } = new(true, []);

    public ZzzLauncherMediaReadiness MediaReadiness { get; private set; } = new(false, false, false, false, false);

    public IReadOnlyList<ZzzLauncherMediaItem> MediaItems => _mediaItems;

    public string PrimaryActionText => _startButtonText.Text ?? string.Empty;

    public string ReadinessText => _readinessText;

    public string RuntimeText => string.Empty;

    public string MediaTitle => _mediaItems.Count == 0 ? string.Empty : _mediaItems[_mediaIndex].Title;

    public string MediaStatusText => _mediaStatusText;

    public ZzzHomeMediaLoadState MediaLoadState => _mediaLoadState;

    public bool IsMediaPlaceholderVisible => _mediaPlaceholder.IsVisible;

    public string MediaPageCount => _mediaPageCount.Text ?? string.Empty;

    public bool IsMediaRotationActive => _bannerTimer.IsEnabled;

    public void OnPageShown() => _ = ActivateDashboardAsync();

    public async Task ActivateDashboardAsync()
    {
        CancellationTokenSource activationCancellation = BeginActivation();
        using CancellationTokenSource timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(activationCancellation.Token);
        timeoutCancellation.CancelAfter(DashboardLoadTimeout);
        CancellationToken cancellationToken = timeoutCancellation.Token;
        Guid operationId = _operations.Start("home", "activate-dashboard");
        _shown = true;
        try
        {
            AttachWindowLifecycle();
            RefreshReadiness();
            _noticeCard.Resume();
            await Task.WhenAll(
                LoadMediaAsync(cancellationToken, activationCancellation.Token),
                LoadNoticesAsync(cancellationToken, activationCancellation.Token)).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentActivation(activationCancellation))
            {
                return;
            }

            StartRotation();
            _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
        }
        catch (OperationCanceledException exception) when (activationCancellation.IsCancellationRequested)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Canceled, "page-leave", exception);
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.TimedOut, "dashboard-load-timeout", exception);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
        }
        finally
        {
            if (!IsCurrentActivation(activationCancellation))
            {
                activationCancellation.Dispose();
            }
        }
    }

    public void CancelPageOperations(string reason)
    {
        _activationCancellation?.Cancel();
    }

    public void OnPageHidden()
    {
        _shown = false;
        _bannerTimer.Stop();
        _noticeCard.Pause();
        _quickLinkTeachingTip.IsOpen = false;
    }

    public void OnPageLeave()
    {
        CancelPageOperations("page-leave");
        _bannerTimer.Stop();
        _noticeCard.Pause();
        _quickLinkTeachingTip.IsOpen = false;
    }

    public void DisposePage()
    {
        _bannerTimer.Stop();
        if (_window is not null)
        {
            _window.Activated -= OnWindowActivated;
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        ClearCurrentMedia();
        _noticeCard.DisposeNotice();
        _noticeCard.RetryRequested -= OnNoticeRetryRequested;
        _projectSettings.DisposePage();
        _themeSettings.DisposePage();

        // 原子摘下再释放：DisposePage 可能被页面宿主和缓存清理重复调用，
        // 之前的写法在第二次调用时会对已释放的 CTS 调 Cancel，异常一路抛到窗口 OnClosed。
        CancellationTokenSource? activation = Interlocked.Exchange(ref _activationCancellation, null);
        if (activation is null)
        {
            return;
        }

        try
        {
            activation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        activation.Dispose();
    }

    public void InvokePrimaryAction() => _ = HandleStartAsync();

    private async Task LoadMediaAsync(CancellationToken cancellationToken, CancellationToken pageCancellationToken)
    {
        Guid operationId = _operations.Start("home", "load-media");
        _mediaLoadState = ZzzHomeMediaLoadState.Loading;
        UpdateMediaPlaceholder();
        try
        {
            MediaReadiness = _mediaService.GetCachedMediaReadiness();
            IReadOnlyList<ZzzLauncherMediaItem> mediaItems = await _mediaService.GetDashboardMediaAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            _mediaItems = mediaItems;
            MediaReadiness = _mediaService.GetCachedMediaReadiness();
            _mediaIndex = 0;
            ShowMedia();
            if (_mediaLoadState == ZzzHomeMediaLoadState.Failed)
            {
                _operations.Complete(
                    operationId,
                    ZzzGuiOperationState.Failed,
                    "media-load-failed");
                return;
            }

            _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
        }
        catch (OperationCanceledException exception) when (pageCancellationToken.IsCancellationRequested)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Canceled, "page-leave", exception);
            throw;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.TimedOut, "media-load-timeout", exception);
            throw;
        }
        catch (Exception exception)
        {
            _mediaItems = [];
            _mediaIndex = 0;
            _mediaLoadState = ZzzHomeMediaLoadState.Failed;
            _mediaStatusText = "media-load-failed";
            UpdateMediaPlaceholder();
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
        }
    }

    private async Task LoadNoticesAsync(CancellationToken cancellationToken, CancellationToken pageCancellationToken)
    {
        Guid operationId = _operations.Start("home", "load-notices");
        try
        {
            await _noticeCard.LoadAsync(_noticeService, _noticeUrl, cancellationToken).ConfigureAwait(true);
            if (_noticeCard.LastLoadTimedOut)
            {
                _operations.Complete(operationId, ZzzGuiOperationState.TimedOut, "notice-load-timeout");
            }
            else if (_noticeCard.FailureMessage is null)
            {
                _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
            }
            else
            {
                _operations.Complete(
                    operationId,
                    ZzzGuiOperationState.Failed,
                    exception: new InvalidOperationException(_noticeCard.FailureMessage));
            }
        }
        catch (OperationCanceledException exception) when (pageCancellationToken.IsCancellationRequested)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Canceled, "page-leave", exception);
            throw;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.TimedOut, "notice-load-timeout", exception);
            throw;
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
        }
    }

    private CancellationTokenSource BeginActivation()
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref _activationCancellation, new CancellationTokenSource());
        previous?.Cancel();
        return _activationCancellation!;
    }

    private bool IsCurrentActivation(CancellationTokenSource cancellation) =>
        ReferenceEquals(_activationCancellation, cancellation) && !cancellation.IsCancellationRequested;

    private void OnNoticeRetryRequested(object? sender, EventArgs args)
    {
        if (_shown)
        {
            _ = ActivateDashboardAsync();
        }
    }

    private void ShowNextMedia()
    {
        if (_mediaItems.Count == 0)
        {
            _mediaLoadState = ZzzHomeMediaLoadState.Placeholder;
            UpdateMediaPlaceholder();
            return;
        }

        _mediaIndex = (_mediaIndex + 1) % _mediaItems.Count;
        ShowMedia();
    }

    private void ShowMedia()
    {
        ClearCurrentMedia();
        _mediaStatusText = string.Empty;
        _mediaPageCount.Text = _mediaItems.Count > 1 ? $"{_mediaIndex + 1}/{_mediaItems.Count}" : string.Empty;
        if (_mediaItems.Count == 0)
        {
            return;
        }

        ZzzLauncherMediaItem item = _mediaItems[Math.Clamp(_mediaIndex, 0, _mediaItems.Count - 1)];
        if (item.LocalPath is null)
        {
            _mediaLoadState = ZzzHomeMediaLoadState.Unavailable;
            _mediaStatusText = "media-unavailable";
            UpdateMediaPlaceholder();
            return;
        }

        ApplyThemeColor(item);
        if (item.IsVideo)
        {
            Bitmap? representativeFrame = LoadVideoRepresentativeFrame(item.LocalPath);
            _bannerImage.Source = representativeFrame;
            _mediaLoadState = representativeFrame is not null ? ZzzHomeMediaLoadState.Ready : ZzzHomeMediaLoadState.Failed;
            _mediaStatusText = representativeFrame is not null ? "video-frame-ready" : "video-frame-load-failed";
            UpdateMediaPlaceholder();
            return;
        }

        try
        {
            _bannerImage.Source = new Bitmap(item.LocalPath);
            _mediaLoadState = ZzzHomeMediaLoadState.Ready;
            _mediaStatusText = "image-ready";
            UpdateMediaPlaceholder();
        }
        catch
        {
            _mediaLoadState = ZzzHomeMediaLoadState.Failed;
            _mediaStatusText = "image-load-failed";
            UpdateMediaPlaceholder();
        }
    }

    private void UpdateMediaPlaceholder() =>
        _mediaPlaceholder.IsVisible = _mediaLoadState is not ZzzHomeMediaLoadState.Ready;

    private void StartRotation()
    {
        _bannerTimer.Stop();
        if (_shown && _windowActive && !_pointerOverHome && _mediaItems.Count > 1)
        {
            _bannerTimer.Start();
        }
    }

    private void AttachWindowLifecycle()
    {
        if (_window is not null)
        {
            return;
        }

        _window = TopLevel.GetTopLevel(this) as AvaloniaWindow;
        if (_window is null)
        {
            return;
        }

        _window.Activated += OnWindowActivated;
        _window.Deactivated += OnWindowDeactivated;
    }

    private void OnWindowActivated(object? sender, EventArgs args)
    {
        _windowActive = true;
        StartRotation();
    }

    private void OnWindowDeactivated(object? sender, EventArgs args)
    {
        _windowActive = false;
        _bannerTimer.Stop();
    }

    private void OnHomePointerEntered(object? sender, PointerEventArgs args)
    {
        _pointerOverHome = true;
        _bannerTimer.Stop();
        _mediaPageCount.IsVisible = _mediaItems.Count > 1;
    }

    private void OnHomePointerExited(object? sender, PointerEventArgs args)
    {
        _pointerOverHome = false;
        _mediaPageCount.IsVisible = false;
        StartRotation();
    }

    private static Bitmap? LoadVideoRepresentativeFrame(string path)
    {
        using VideoCapture capture = new(path);
        if (!capture.IsOpened())
        {
            return null;
        }

        using Mat frame = new();
        if (!capture.Read(frame) || frame.Empty())
        {
            return null;
        }

        if (!Cv2.ImEncode(".png", frame, out byte[] encoded))
        {
            return null;
        }

        using MemoryStream stream = new(encoded, writable: false);
        return new Bitmap(stream);
    }

    private void ClearCurrentMedia()
    {
        if (_bannerImage.Source is IDisposable disposableImage)
        {
            disposableImage.Dispose();
        }

        _bannerImage.Source = null;
    }

    private async void OnStartButtonClicked(object? sender, RoutedEventArgs args)
    {
        await HandleStartAsync().ConfigureAwait(true);
    }

    private async Task HandleStartAsync()
    {
        RefreshReadiness();
        if (LastReadiness.Ready)
        {
            ContinueStart();
            return;
        }

        _preFlightIssueList.ItemsSource = LastReadiness.Issues.Select(issue => issue.Message).ToArray();
        AvaloniaWindow? owner = TopLevel.GetTopLevel(this) as AvaloniaWindow;
        if (owner is null)
        {
            return;
        }

        FAContentDialogResult result = await _preFlightDialog.ShowAsync(owner).ConfigureAwait(true);
        if (result is FAContentDialogResult.Primary)
        {
            _navigation.RequestNavigate(LastReadiness.Issues[0].TargetNavigationKey);
            return;
        }

        ContinueStart();
    }

    private void ContinueStart()
    {
        _runIntent.RequestStartOneDragon();
        _navigation.RequestNavigate("one-dragon");
    }

    private void RefreshReadiness()
    {
        LastReadiness = _readinessService.Check();
        _readinessText = string.Join(Environment.NewLine, LastReadiness.Issues.Select(issue => issue.Message));
        _startButtonText.Text = LastReadiness.Ready
            ? "启动一条龙"
            : $"{LastReadiness.Issues.Count} 项待配置 ";
        _startButtonIcon.Symbol = LastReadiness.Ready ? FASymbol.PlayFilled : FASymbol.Settings;
    }

    private void OnQuickLinkClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string key })
        {
            return;
        }

        ZzzHomeQuickLink? link = QuickLinks.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(link?.Uri))
        {
            OpenUri(link.Uri);
        }
    }

    private void OnQuickLinkPointerEntered(object? sender, PointerEventArgs args)
    {
        if (sender is not Button { Tag: string key } button)
        {
            return;
        }

        ZzzHomeQuickLink? link = QuickLinks.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        if (link is null)
        {
            return;
        }

        _quickLinkTeachingTip.Target = button;
        _quickLinkTeachingTip.Title = link.Label;
        _quickLinkTeachingTip.Subtitle = link.Tooltip;
        _quickLinkTeachingTip.IsOpen = true;
    }

    private void OnQuickLinkPointerExited(object? sender, PointerEventArgs args)
    {
        _quickLinkTeachingTip.IsOpen = false;
    }

    private void ApplyQuickLinkAvailability()
    {
        IReadOnlyDictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal)
        {
            ["home"] = GetRequiredButton("HomeLinkButton"),
            ["github"] = GetRequiredButton("GithubLinkButton"),
            ["docs"] = GetRequiredButton("DocsLinkButton"),
            ["official-channel"] = GetRequiredButton("ChannelLinkButton"),
        };
        foreach (ZzzHomeQuickLink link in QuickLinks)
        {
            buttons[link.Key].IsEnabled = !string.IsNullOrWhiteSpace(link.Uri);
        }
    }

    private Button GetRequiredButton(string name) =>
        this.FindControl<Button>(name) ?? throw new InvalidOperationException($"首页缺少按钮 {name}。");

    private void ApplyThemeColor(ZzzLauncherMediaItem item)
    {
        _themeSettings.Reload();
        if (_themeSettings.CustomThemeColor
            && TryParseColor(_themeSettings.GlobalThemeColor, out Color customColor))
        {
            ApplyAccentAndStartButton(customColor);
            return;
        }

        if (item.LocalPath is null || !TryExtractThemeColor(item.LocalPath, item.IsVideo, out Color mediaColor))
        {
            if (TryParseColor(_themeSettings.GlobalThemeColor, out Color savedColor))
            {
                ApplyAccentAndStartButton(savedColor);
            }

            return;
        }

        ApplyAccentAndStartButton(mediaColor);
        _themeSettings.SaveExtractedThemeColor($"{mediaColor.R},{mediaColor.G},{mediaColor.B}");
    }

    private void ApplyAccentAndStartButton(Color color)
    {
        App.ApplyAccentColor(color);
        double luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        Color foreground = luminance >= 160 ? Colors.Black : Colors.White;
        SolidColorBrush backgroundBrush = new(color);
        SolidColorBrush foregroundBrush = new(foreground);
        SolidColorBrush transparentBrush = new(Colors.Transparent);
        _startButton.Resources["ZzzHomeStartBackgroundBrush"] = backgroundBrush;
        _startButton.Resources["ZzzHomeStartForegroundBrush"] = foregroundBrush;
        _startButton.Resources["ButtonBackgroundPointerOver"] = foregroundBrush;
        _startButton.Resources["ButtonForegroundPointerOver"] = backgroundBrush;
        _startButton.Resources["ButtonBorderBrushPointerOver"] = transparentBrush;
        _startButton.Resources["ButtonBackgroundPressed"] = foregroundBrush;
        _startButton.Resources["ButtonForegroundPressed"] = backgroundBrush;
        _startButton.Resources["ButtonBorderBrushPressed"] = transparentBrush;
    }

    private static bool TryExtractThemeColor(string path, bool video, out Color color)
        => ZzzHomeThemeColorExtractor.TryExtract(path, video, out color);

    private static bool TryParseColor(string value, out Color color)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3
            && byte.TryParse(parts[0], out byte red)
            && byte.TryParse(parts[1], out byte green)
            && byte.TryParse(parts[2], out byte blue))
        {
            color = Color.FromRgb(red, green, blue);
            return true;
        }

        color = default;
        return false;
    }

    private static void OpenUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? target))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
    }
}

public enum ZzzHomeMediaLoadState
{
    Loading,

    Ready,

    Placeholder,

    Unavailable,

    Failed,
}
