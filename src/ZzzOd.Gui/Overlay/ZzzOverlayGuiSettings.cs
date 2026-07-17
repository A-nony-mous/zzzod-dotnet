namespace ZzzOd.Gui.Overlay;

internal sealed class ZzzOverlayGuiSettings
{
    public bool Enabled { get; set; }

    public bool ShowByDefault { get; set; } = true;

    public string Hotkey { get; set; } = "o";

    public bool PreventCapture { get; set; } = true;

    public bool ClickThrough { get; set; } = true;

    public bool FollowGameWindow { get; set; } = true;

    public bool VisionLayerEnabled { get; set; } = true;

    public bool DpiAware { get; set; } = true;

    public bool LayoutEditMode { get; set; }

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSize { get; set; } = 12d;

    public double Opacity { get; set; } = 0.70d;

    public int RefreshIntervalMs { get; set; } = 200;

    public string PanelTextColor { get; set; } = "#f2f2f2";

    public int LogMaxLines { get; set; } = 120;

    public int LogFadeSeconds { get; set; } = 12;

    public int FollowIntervalMs { get; set; } = 120;

    public int StatePollIntervalMs { get; set; } = 200;

    public bool PatchedCaptureEnabled { get; set; }

    public string PatchedCaptureSuffix { get; set; } = "_patched";

    public Dictionary<string, bool> PerformanceMetrics { get; set; } = new(StringComparer.Ordinal)
    {
        ["ocr_ms"] = true,
        ["yolo_ms"] = true,
        ["cv_pipeline_ms"] = true,
        ["operation_round_ms"] = true,
        ["overlay_refresh_ms"] = true,
    };

    public ZzzOverlayVisualSettings Visual { get; set; } = new();

    public List<ZzzOverlayPanelSettings> Panels { get; set; } =
    [
        new("log", "日志面板", true, 100, 100, 480, 200),
        new("state", "状态面板", true, 0, 0, 300, 120),
        new("decision", "决策面板", true, 0, 0, 300, 140),
        new("timeline", "时间轴面板", true, 0, 0, 300, 170),
        new("performance", "性能面板", true, 0, 0, 300, 110),
    ];
}

internal sealed class ZzzOverlayVisualSettings
{
    public bool ShowYolo { get; set; } = true;

    public bool ShowOcr { get; set; } = true;

    public bool ShowTemplate { get; set; } = true;

    public bool ShowCv { get; set; } = true;

    public int OffsetX { get; set; }

    public int OffsetY { get; set; }

    public double ScaleX { get; set; } = 1d;

    public double ScaleY { get; set; } = 1d;

    public double Scale
    {
        get => (ScaleX + ScaleY) / 2d;
        set
        {
            ScaleX = value;
            ScaleY = value;
        }
    }
}

internal sealed class ZzzOverlayPanelSettings
{
    public ZzzOverlayPanelSettings()
    {
    }

    public ZzzOverlayPanelSettings(string id, string title, bool enabled, double x, double y, double width, double height)
    {
        Id = id;
        Title = title;
        Enabled = enabled;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }
}
