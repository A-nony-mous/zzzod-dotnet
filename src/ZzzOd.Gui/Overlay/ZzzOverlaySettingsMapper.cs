using System.Globalization;
using System.Text.Json;

namespace ZzzOd.Gui.Overlay;

internal static class ZzzOverlaySettingsMapper
{
    public static ZzzOverlayGuiSettings Create(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Dictionary<string, object?> geometry = ReadObjectMap(values, "panel_geometry");
        return new ZzzOverlayGuiSettings
        {
            Enabled = ReadBool(values, "enabled"),
            ShowByDefault = ReadBool(values, "visible"),
            Hotkey = ReadString(values, "toggle_hotkey"),
            PreventCapture = ReadBool(values, "anti_capture"),
            VisionLayerEnabled = ReadBool(values, "vision_layer_enabled"),
            LayoutEditMode = ReadBool(values, "panel_edit_mode"),
            FontSize = ReadDouble(values, "font_size"),
            Opacity = ReadDouble(values, "panel_opacity") / 100d,
            PanelTextColor = ReadString(values, "panel_text_color"),
            LogMaxLines = ReadInt(values, "log_max_lines"),
            LogFadeSeconds = ReadInt(values, "log_fade_seconds"),
            FollowIntervalMs = ReadInt(values, "follow_interval_ms"),
            StatePollIntervalMs = ReadInt(values, "state_poll_interval_ms"),
            PatchedCaptureEnabled = ReadBool(values, "patched_capture_enabled"),
            PatchedCaptureSuffix = ReadString(values, "patched_capture_suffix"),
            PerformanceMetrics = ReadBoolMap(values, "performance_metric_enabled_map"),
            Visual = new ZzzOverlayVisualSettings
            {
                ShowYolo = ReadBool(values, "vision_yolo_enabled"),
                ShowOcr = ReadBool(values, "vision_ocr_enabled"),
                ShowTemplate = ReadBool(values, "vision_template_enabled"),
                ShowCv = ReadBool(values, "vision_cv_enabled"),
                OffsetX = ReadInt(values, "vision_offset_x"),
                OffsetY = ReadInt(values, "vision_offset_y"),
                ScaleX = ReadDouble(values, "vision_scale_x"),
                ScaleY = ReadDouble(values, "vision_scale_y"),
            },
            Panels =
            [
                CreatePanel("log", "日志面板", "log_panel", ReadBool(values, "log_panel_enabled"), geometry),
                CreatePanel("state", "状态面板", "state_panel", ReadBool(values, "state_panel_enabled"), geometry),
                CreatePanel("decision", "决策面板", "decision_panel", ReadBool(values, "decision_panel_enabled"), geometry),
                CreatePanel("timeline", "时间轴面板", "timeline_panel", ReadBool(values, "timeline_panel_enabled"), geometry),
                CreatePanel("performance", "性能面板", "performance_panel", ReadBool(values, "performance_panel_enabled"), geometry),
            ],
        };
    }

    public static Dictionary<string, object?> DefaultPanelGeometry() => new(StringComparer.Ordinal)
    {
        ["log_panel"] = new Dictionary<string, object?> { ["x"] = 100, ["y"] = 100, ["w"] = 480, ["h"] = 200 },
        ["state_panel"] = new Dictionary<string, object?> { ["x"] = 0, ["y"] = 0, ["w"] = 300, ["h"] = 120 },
        ["decision_panel"] = new Dictionary<string, object?> { ["x"] = 0, ["y"] = 0, ["w"] = 300, ["h"] = 140 },
        ["timeline_panel"] = new Dictionary<string, object?> { ["x"] = 0, ["y"] = 0, ["w"] = 300, ["h"] = 170 },
        ["performance_panel"] = new Dictionary<string, object?> { ["x"] = 0, ["y"] = 0, ["w"] = 300, ["h"] = 110 },
    };

    internal static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToBoolean(RequiredValue(values, key), CultureInfo.InvariantCulture);

    internal static int ReadInt(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToInt32(RequiredValue(values, key), CultureInfo.InvariantCulture);

    internal static double ReadDouble(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToDouble(RequiredValue(values, key), CultureInfo.InvariantCulture);

    internal static string ReadString(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToString(RequiredValue(values, key), CultureInfo.InvariantCulture)
        ?? throw new InvalidOperationException($"Overlay 配置 {key} 为空。");

    internal static Dictionary<string, bool> ReadBoolMap(IReadOnlyDictionary<string, object?> values, string key)
    {
        Dictionary<string, object?> source = ReadObjectMap(RequiredValue(values, key));
        return source.ToDictionary(
            pair => pair.Key,
            pair => Convert.ToBoolean(pair.Value is JsonElement element ? ReadJsonElement(element) : pair.Value, CultureInfo.InvariantCulture),
            StringComparer.Ordinal);
    }

    private static ZzzOverlayPanelSettings CreatePanel(
        string id,
        string title,
        string configName,
        bool enabled,
        IReadOnlyDictionary<string, object?> geometry)
    {
        if (!geometry.TryGetValue(configName, out object? raw))
        {
            throw new InvalidOperationException($"panel_geometry 缺少 {configName}。");
        }

        Dictionary<string, object?> panel = ReadObjectMap(raw);
        return new ZzzOverlayPanelSettings(
            id,
            title,
            enabled,
            ReadDouble(panel, "x"),
            ReadDouble(panel, "y"),
            ReadDouble(panel, "w"),
            ReadDouble(panel, "h"));
    }

    private static object RequiredValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) && value is not null
            ? value is JsonElement element
                ? ReadJsonElement(element) ?? throw new InvalidOperationException($"Overlay 配置 {key} 为空。")
                : value
            : throw new InvalidOperationException($"Overlay 配置缺少 {key}。");

    private static Dictionary<string, object?> ReadObjectMap(IReadOnlyDictionary<string, object?> values, string key) =>
        ReadObjectMap(RequiredValue(values, key));

    private static Dictionary<string, object?> ReadObjectMap(object? value)
    {
        if (value is IReadOnlyDictionary<string, object?> objects)
        {
            return objects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is IReadOnlyDictionary<string, bool> booleans)
        {
            return booleans.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            return json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ReadJsonElement(property.Value),
                StringComparer.Ordinal);
        }

        throw new InvalidOperationException("Overlay 复合配置格式无效。");
    }

    private static object? ReadJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when element.TryGetInt32(out int integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => ReadJsonElement(property.Value),
            StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(ReadJsonElement).ToArray(),
        JsonValueKind.Null => null,
        _ => element.ToString(),
    };
}
