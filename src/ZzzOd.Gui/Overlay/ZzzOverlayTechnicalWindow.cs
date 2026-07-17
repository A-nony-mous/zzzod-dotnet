using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using ZzzOd.AppHost.Overlay;

namespace ZzzOd.Gui.Overlay;

internal sealed class ZzzOverlayTechnicalWindow : Window
{
    private readonly Canvas _canvas = new();
    private ZzzOverlayGuiSettings _settings = new();

    public ZzzOverlayTechnicalWindow()
    {
        Title = "ZZZ Overlay";
        Width = 1280;
        Height = 720;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Content = _canvas;
        Opened += (_, _) => ZzzOverlayNativeWindow.Apply(this, _settings.ClickThrough, _settings.PreventCapture);
    }

    public void ApplySettings(ZzzOverlayGuiSettings settings)
    {
        _settings = settings;
        Opacity = Math.Clamp(settings.Opacity, 0.1d, 1d);
        ZzzOverlayNativeWindow.Apply(this, settings.ClickThrough && !settings.LayoutEditMode, settings.PreventCapture);
    }

    public void FollowGameWindow(string? windowTitle)
    {
        if (!_settings.FollowGameWindow || string.IsNullOrWhiteSpace(windowTitle))
        {
            return;
        }

        if (!TryGetClientRect(windowTitle, out NativeRect rect))
        {
            return;
        }

        double scale = _settings.DpiAware ? Math.Max(0.5d, DesktopScaling) : 1d;
        Position = new PixelPoint(rect.Left, rect.Top);
        Width = Math.Max(320, (rect.Right - rect.Left) / scale);
        Height = Math.Max(180, (rect.Bottom - rect.Top) / scale);
    }

    public void Render(
        ZzzOverlayStatusDto status,
        ZzzOverlayFrameDto? frame,
        IReadOnlyList<ZzzOverlayPerformanceSampleDto> performanceSamples)
    {
        _canvas.Children.Clear();
        foreach (ZzzOverlayPanelSettings panel in _settings.Panels.Where(panel => panel.Enabled))
        {
            _canvas.Children.Add(CreatePanel(panel, status, performanceSamples));
        }

        if (frame is null)
        {
            return;
        }

        foreach (ZzzOverlayDrawItemDto item in frame.Items)
        {
            _canvas.Children.Add(CreateDrawItem(item));
        }
    }

    private Control CreatePanel(
        ZzzOverlayPanelSettings panel,
        ZzzOverlayStatusDto status,
        IReadOnlyList<ZzzOverlayPerformanceSampleDto> performanceSamples)
    {
        Border border = new()
        {
            Width = panel.Width,
            Height = panel.Height,
            Background = new SolidColorBrush(Color.FromArgb(180, 18, 20, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, 96, 176, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = panel.Title,
                        Foreground = Brushes.White,
                        FontFamily = new FontFamily(_settings.FontFamily),
                        FontSize = _settings.FontSize + 1,
                    },
                    new TextBlock
                    {
                        Text = FormatPanelText(panel.Id, status, performanceSamples, _settings.PerformanceMetrics),
                        Foreground = new SolidColorBrush(Color.FromRgb(210, 228, 240)),
                        FontFamily = new FontFamily(_settings.FontFamily),
                        FontSize = _settings.FontSize,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
        Canvas.SetLeft(border, panel.X);
        Canvas.SetTop(border, panel.Y);
        return border;
    }

    private Control CreateDrawItem(ZzzOverlayDrawItemDto item)
    {
        Border border = new()
        {
            Width = Math.Max(12, item.Bounds.Width * _settings.Visual.ScaleX),
            Height = Math.Max(12, item.Bounds.Height * _settings.Visual.ScaleY),
            BorderBrush = ParseBrush(item.Color),
            BorderThickness = new Thickness(2),
            Child = string.IsNullOrWhiteSpace(item.Text)
                ? null
                : new TextBlock
                {
                    Text = item.Text,
                    Foreground = ParseBrush(item.Color),
                    FontSize = Math.Max(10, _settings.FontSize - 1),
                },
        };
        Canvas.SetLeft(border, (item.Bounds.X + _settings.Visual.OffsetX) * _settings.Visual.ScaleX);
        Canvas.SetTop(border, (item.Bounds.Y + _settings.Visual.OffsetY) * _settings.Visual.ScaleY);
        return border;
    }

    private static IBrush ParseBrush(string? color)
    {
        return Color.TryParse(color, out Color parsed)
            ? new SolidColorBrush(parsed)
            : new SolidColorBrush(Color.FromRgb(100, 220, 255));
    }

    internal static string FormatPanelText(
        string id,
        ZzzOverlayStatusDto status,
        IReadOnlyList<ZzzOverlayPerformanceSampleDto> performanceSamples,
        IReadOnlyDictionary<string, bool>? enabledMetricMap = null,
        DateTimeOffset? now = null)
    {
        return id switch
        {
            "log" => "显示运行日志和最近事件。",
            "state" => $"启用：{status.Enabled}\n绘制项：{status.ItemCount}",
            "decision" => "显示当前任务、下一步动作和异常分支。",
            "timeline" => $"最后绘制：{status.LastFrameAt?.ToLocalTime().ToString("HH:mm:ss") ?? "-"}",
            "performance" => FormatPerformancePanelText(performanceSamples, enabledMetricMap, now),
            _ => string.Empty,
        };
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

    private static bool TryGetClientRect(string windowTitle, out NativeRect rect)
    {
        rect = default;
        nint hwnd = FindWindowW(null, windowTitle);
        if (hwnd == 0 || !GetClientRect(hwnd, out NativeRect clientRect))
        {
            return false;
        }

        NativePoint point = new(clientRect.Left, clientRect.Top);
        if (!ClientToScreen(hwnd, ref point))
        {
            return false;
        }

        rect = new NativeRect
        {
            Left = point.X,
            Top = point.Y,
            Right = point.X + clientRect.Right,
            Bottom = point.Y + clientRect.Bottom,
        };
        return rect.Right > rect.Left && rect.Bottom > rect.Top;
    }

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hWnd, ref NativePoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }
}
