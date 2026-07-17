using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.Gui.Services.Notices;

namespace ZzzOd.Gui.Controls.Home;

public sealed partial class ZzzNoticeCard : UserControl
{
    private readonly DispatcherTimer _bannerTimer;
    private readonly Image _bannerImage;
    private readonly ListBox _gameGuidesList;
    private readonly ListBox _softwareResearchList;
    private readonly ListBox _announcementsList;
    private readonly InfoBar _failureInfoBar;
    private readonly Button _retryButton;
    private IReadOnlyList<ZzzNoticeBannerViewItem> _banners = [];
    private Bitmap? _bannerBitmap;
    private int _bannerIndex;
    private bool _loaded;
    private bool _active;

    public ZzzNoticeCard()
    {
        AvaloniaXamlLoader.Load(this);
        _bannerImage = GetRequiredControl<Image>("BannerImage");
        _gameGuidesList = GetRequiredControl<ListBox>("GameGuidesList");
        _softwareResearchList = GetRequiredControl<ListBox>("SoftwareResearchList");
        _announcementsList = GetRequiredControl<ListBox>("AnnouncementsList");
        _failureInfoBar = GetRequiredControl<InfoBar>("FailureInfoBar");
        _retryButton = GetRequiredControl<Button>("RetryButton");
        _bannerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _bannerTimer.Tick += (_, _) => ShowNextBanner();
    }

    public string? FailureMessage { get; private set; }

    public bool LastLoadTimedOut { get; private set; }

    public event EventHandler? RetryRequested;

    public ZzzNoticeContent? NoticeContent { get; private set; }

    public async Task LoadAsync(
        ZzzNoticeService service,
        string noticeUrl,
        CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            Resume();
            return;
        }

        ZzzNoticeLoadResult result = await service.LoadAsync(noticeUrl, cancellationToken).ConfigureAwait(true);
        if (!result.Success || result.Content is null)
        {
            NoticeContent = null;
            FailureMessage = result.Error ?? "公告加载失败。";
            LastLoadTimedOut = result.TimedOut;
            _failureInfoBar.Message = FailureMessage;
            _failureInfoBar.IsOpen = true;
            _retryButton.IsVisible = true;
            ClearLists();
            return;
        }

        NoticeContent = result.Content;
        FailureMessage = null;
        LastLoadTimedOut = result.TimedOut;
        _failureInfoBar.IsOpen = false;
        _retryButton.IsVisible = false;
        _gameGuidesList.ItemsSource = NoticeContent.GameGuides;
        _softwareResearchList.ItemsSource = NoticeContent.SoftwareResearch;
        _announcementsList.ItemsSource = NoticeContent.Announcements;

        Task<ZzzNoticeBannerViewItem?>[] bannerTasks = NoticeContent.Banners
            .Select(async banner =>
            {
                string? path = await service.GetBannerImagePathAsync(banner.ImageUrl, cancellationToken).ConfigureAwait(false);
                return path is null ? null : new ZzzNoticeBannerViewItem(banner, path);
            })
            .ToArray();
        ZzzNoticeBannerViewItem?[] loadedBanners = await Task.WhenAll(bannerTasks).ConfigureAwait(true);
        _banners = loadedBanners.Where(item => item is not null).Cast<ZzzNoticeBannerViewItem>().ToArray();
        _bannerIndex = 0;
        ShowBanner();
        _loaded = true;
        StartBannerRotation();
    }

    public void Resume()
    {
        _active = true;
        StartBannerRotation();
    }

    public void Pause()
    {
        _active = false;
        _bannerTimer.Stop();
    }

    public void DisposeNotice()
    {
        _active = false;
        _bannerTimer.Stop();
        _bannerBitmap?.Dispose();
        _bannerBitmap = null;
    }

    private void ShowNextBanner()
    {
        if (_banners.Count == 0)
        {
            return;
        }

        _bannerIndex = (_bannerIndex + 1) % _banners.Count;
        ShowBanner();
    }

    private void StartBannerRotation()
    {
        if (_active && _banners.Count > 1)
        {
            _bannerTimer.Start();
        }
    }

    private void ShowBanner()
    {
        _bannerBitmap?.Dispose();
        _bannerBitmap = null;
        _bannerImage.Source = null;
        if (_banners.Count == 0)
        {
            return;
        }

        try
        {
            _bannerBitmap = new Bitmap(_banners[_bannerIndex].Path);
            _bannerImage.Source = _bannerBitmap;
        }
        catch
        {
            _banners = _banners.Where((_, index) => index != _bannerIndex).ToArray();
            _bannerIndex = 0;
            if (_banners.Count > 0)
            {
                ShowBanner();
            }
        }
    }

    private void OnBannerPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_banners.Count > 0)
        {
            OpenUri(_banners[_bannerIndex].Banner.Link);
        }
    }

    private void OnRetryClicked(object? sender, RoutedEventArgs args) => RetryRequested?.Invoke(this, EventArgs.Empty);

    private void OnPostSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender is not ListBox { SelectedItem: ZzzNoticePost post } list)
        {
            return;
        }

        list.SelectedItem = null;
        OpenUri(post.Link);
    }

    private void ClearLists()
    {
        _gameGuidesList.ItemsSource = null;
        _softwareResearchList.ItemsSource = null;
        _announcementsList.ItemsSource = null;
    }

    private T GetRequiredControl<T>(string name)
        where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"公告模块缺少控件 {name}。");

    private static void OpenUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }
}

internal sealed record ZzzNoticeBannerViewItem(ZzzNoticeBanner Banner, string Path);

