using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;

namespace ZzzOd.Gui.Overlay;

internal sealed class ZzzOverlayTechnicalWindow : Window
{
    private readonly ZzzOverlayVisionControl _visionControl = new();
    private ZzzOverlayGuiSettings _settings = new();
    private ZzzWindowStatusDto? _lastGameWindow;
    private ZzzOverlayFrameDto? _lastFrame;
    private double _geometryScaling = 1d;
    private bool _scalingRefreshQueued;

    public ZzzOverlayTechnicalWindow()
    {
        Title = string.Empty;
        Width = 1;
        Height = 1;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Content = _visionControl;
        Opened += (_, _) =>
        {
            ZzzOverlayNativeWindow.Apply(this, clickThrough: true, preventCapture: _settings.PreventCapture);
            if (_lastGameWindow is not null)
            {
                FollowGameWindow(_lastGameWindow);
            }
        };
        ScalingChanged += (_, _) => QueueScalingRefresh();
        Closed += (_, _) =>
        {
            _lastGameWindow = null;
            _lastFrame = null;
            _scalingRefreshQueued = false;
        };
    }

    public void ApplySettings(ZzzOverlayGuiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        ZzzOverlayNativeWindow.Apply(this, clickThrough: true, preventCapture: settings.PreventCapture);
    }

    public void FollowGameWindow(ZzzWindowStatusDto window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Dispatcher.UIThread.VerifyAccess();
        _lastGameWindow = window;
        if (!_settings.FollowGameWindow ||
            !window.X.HasValue ||
            !window.Y.HasValue ||
            window.Width is not > 0 ||
            window.Height is not > 0)
        {
            return;
        }

        Position = new PixelPoint(window.X.Value, window.Y.Value);
        _geometryScaling = Math.Max(0.5d, DesktopScaling);
        Width = window.Width.Value / _geometryScaling;
        Height = window.Height.Value / _geometryScaling;
    }

    public void Render(ZzzOverlayFrameDto? frame)
    {
        _lastFrame = frame;
        _visionControl.Update(frame?.Items ?? [], _settings, _geometryScaling);
    }

    public void Render(
        ZzzOverlayStatusDto _,
        ZzzOverlayFrameDto? frame,
        IReadOnlyList<ZzzOverlayPerformanceSampleDto> __) => Render(frame);

    internal PixelRect PhysicalBounds => new(
        Position.X,
        Position.Y,
        Math.Max(1, (int)Math.Round(Width * _geometryScaling)),
        Math.Max(1, (int)Math.Round(Height * _geometryScaling)));

    private void QueueScalingRefresh()
    {
        if (_scalingRefreshQueued || _lastGameWindow is null)
        {
            return;
        }

        _scalingRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scalingRefreshQueued = false;
            if (_lastGameWindow is null)
            {
                return;
            }

            FollowGameWindow(_lastGameWindow);
            Render(_lastFrame);
        });
    }

    internal static string FormatPerformancePanelText(
        IReadOnlyList<ZzzOverlayPerformanceSampleDto> samples,
        IReadOnlyDictionary<string, bool>? enabledMetricMap,
        DateTimeOffset? now = null)
    {
        DateTimeOffset current = now ?? DateTimeOffset.UtcNow;
        IEnumerable<ZzzOverlayPerformanceSampleDto> visible = samples.Where(sample =>
            enabledMetricMap is null ||
            (enabledMetricMap.TryGetValue(sample.Metric, out bool enabled) && enabled));
        return string.Join(
            Environment.NewLine,
            visible.Select(sample =>
                $"{sample.Metric}: {sample.Value:F2} {sample.Unit} ({Math.Max(0, (int)(current - sample.CreatedAt).TotalMilliseconds)}ms ago)"));
    }
}
