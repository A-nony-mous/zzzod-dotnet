using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ZzzOd.AppHost.Overlay;

namespace ZzzOd.Gui.Overlay;

internal sealed class ZzzOverlayVisionControl : Control
{
    private const double StandardGameWidth = 1920d;
    private const double StandardGameHeight = 1080d;
    private static readonly IReadOnlyDictionary<string, Color> PythonSourceColors = new Dictionary<string, Color>(StringComparer.Ordinal)
    {
        ["ocr"] = Color.Parse("#ff4fa3"),
        ["template"] = Color.Parse("#ffd166"),
        ["yolo"] = Color.Parse("#24d7ff"),
        ["cv"] = Color.Parse("#64d98b"),
    };

    private IReadOnlyList<ZzzOverlayDrawItemDto> _items = [];
    private ZzzOverlayGuiSettings _settings = new();
    private double _desktopScaling = 1d;

    public ZzzOverlayVisionControl()
    {
        IsHitTestVisible = false;
    }

    public void Update(IReadOnlyList<ZzzOverlayDrawItemDto> items, ZzzOverlayGuiSettings settings, double desktopScaling = 1d)
    {
        _items = items?.ToArray() ?? [];
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _desktopScaling = Math.Max(0.5d, desktopScaling);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!_settings.VisionLayerEnabled || Bounds.Width <= 0d || Bounds.Height <= 0d)
        {
            return;
        }

        double standardScaleX = Bounds.Width / StandardGameWidth;
        double standardScaleY = Bounds.Height / StandardGameHeight;
        foreach (ZzzOverlayDrawItemDto item in _items)
        {
            if (item.Kind != ZzzOverlayDrawItemKind.VisionDrawItem || !IsEnabledSource(item))
            {
                continue;
            }

            IBrush brush = ResolveBrush(item);
            Rect bounds = MapToClient(item.Bounds, standardScaleX, standardScaleY);
            context.DrawRectangle(null, new Pen(brush, 2d), bounds);
            IReadOnlyList<Point> pathPoints = ParsePathPoints(item);
            if (pathPoints.Count > 1)
            {
                Pen pen = new(brush, 2d);
                Point previous = MapStandardPoint(pathPoints[0], Bounds.Width, Bounds.Height, _settings.Visual, _desktopScaling);
                for (int index = 1; index < pathPoints.Count; index++)
                {
                    Point current = MapStandardPoint(pathPoints[index], Bounds.Width, Bounds.Height, _settings.Visual, _desktopScaling);
                    context.DrawLine(pen, previous, current);
                    previous = current;
                }
            }

            if (!string.IsNullOrWhiteSpace(item.Text))
            {
                FormattedText text = new(
                    item.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    Math.Max(10d, _settings.FontSize - 1d),
                    brush);
                context.DrawText(text, bounds.TopLeft);
            }
        }
    }

    internal static bool IsEnabledSource(ZzzOverlayDrawItemDto item, ZzzOverlayGuiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.VisionLayerEnabled)
        {
            return false;
        }

        string source = SourceOf(item);
        if (source.Contains("yolo", StringComparison.OrdinalIgnoreCase))
        {
            return settings.Visual.ShowYolo;
        }

        if (source.Contains("ocr", StringComparison.OrdinalIgnoreCase))
        {
            return settings.Visual.ShowOcr;
        }

        if (source.Contains("template", StringComparison.OrdinalIgnoreCase))
        {
            return settings.Visual.ShowTemplate;
        }

        return !source.Contains("cv", StringComparison.OrdinalIgnoreCase) || settings.Visual.ShowCv;
    }

    private bool IsEnabledSource(ZzzOverlayDrawItemDto item) => IsEnabledSource(item, _settings);

    internal static string SourceOf(ZzzOverlayDrawItemDto item)
    {
        if (item.Metadata?.TryGetValue("source", out string? source) == true && !string.IsNullOrWhiteSpace(source))
        {
            return source.Trim().ToLowerInvariant();
        }

        int separator = item.Id.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 ? item.Id[..separator].Trim().ToLowerInvariant() : string.Empty;
    }

    internal static Color ResolveColor(ZzzOverlayDrawItemDto item)
    {
        if (Color.TryParse(item.Color, out Color structuredColor))
        {
            return structuredColor;
        }

        string source = SourceOf(item);
        foreach ((string key, Color color) in PythonSourceColors)
        {
            if (source.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return color;
            }
        }

        return Color.Parse("#bdbdbd");
    }

    private IBrush ResolveBrush(ZzzOverlayDrawItemDto item) => new SolidColorBrush(ResolveColor(item));

    private Rect MapToClient(ZzzOverlayRectDto bounds, double standardScaleX, double standardScaleY)
    {
        return MapStandardBounds(
            bounds,
            standardScaleX * StandardGameWidth,
            standardScaleY * StandardGameHeight,
            _settings.Visual,
            _desktopScaling);
    }

    internal static Rect MapStandardBounds(
        ZzzOverlayRectDto bounds,
        double clientWidth,
        double clientHeight,
        ZzzOverlayVisualSettings visual,
        double desktopScaling)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(visual);
        double standardScaleX = Math.Max(0d, clientWidth) / StandardGameWidth;
        double standardScaleY = Math.Max(0d, clientHeight) / StandardGameHeight;
        double x = bounds.X * standardScaleX;
        double y = bounds.Y * standardScaleY;
        double width = Math.Max(1d, bounds.Width * standardScaleX);
        double height = Math.Max(1d, bounds.Height * standardScaleY);
        double scaling = Math.Max(0.5d, desktopScaling);
        x = x * visual.ScaleX + visual.OffsetX / scaling;
        y = y * visual.ScaleY + visual.OffsetY / scaling;
        width *= visual.ScaleX;
        height *= visual.ScaleY;
        return new Rect(x, y, width, height);
    }

    internal static Point MapStandardPoint(
        Point point,
        double clientWidth,
        double clientHeight,
        ZzzOverlayVisualSettings visual,
        double desktopScaling)
    {
        ArgumentNullException.ThrowIfNull(visual);
        return new Point(
            point.X * Math.Max(0d, clientWidth) / StandardGameWidth * visual.ScaleX + visual.OffsetX,
            point.Y * Math.Max(0d, clientHeight) / StandardGameHeight * visual.ScaleY + visual.OffsetY);
    }

    internal static IReadOnlyList<Point> ParsePathPoints(ZzzOverlayDrawItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Metadata?.TryGetValue("path_points", out string? raw) != true || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        if (raw.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(raw);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                List<Point> points = [];
                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    if (TryReadPoint(element, out Point point))
                    {
                        points.Add(point);
                    }
                }

                return points;
            }
            catch (JsonException)
            {
                return [];
            }
        }

        List<Point> delimitedPoints = [];
        foreach (string segment in raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string[] values = segment.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 2 &&
                double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                delimitedPoints.Add(new Point(x, y));
            }
        }

        return delimitedPoints;
    }

    private static bool TryReadPoint(JsonElement element, out Point point)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            JsonElement[] values = element.EnumerateArray().Take(2).ToArray();
            if (values.Length == 2 && TryReadDouble(values[0], out double x) && TryReadDouble(values[1], out double y))
            {
                point = new Point(x, y);
                return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object &&
                 element.TryGetProperty("x", out JsonElement xValue) &&
                 element.TryGetProperty("y", out JsonElement yValue) &&
                 TryReadDouble(xValue, out double x) &&
                 TryReadDouble(yValue, out double y))
        {
            point = new Point(x, y);
            return true;
        }

        point = default;
        return false;
    }

    private static bool TryReadDouble(JsonElement value, out double number)
    {
        if (value.TryGetDouble(out number))
        {
            return true;
        }

        return value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
