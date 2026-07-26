using System.Globalization;
using System.Text.Json;

namespace ZzzOd.Gui.Overlay;

internal static class ZzzOverlaySettingsMapper
{
    private static readonly string[] PanelConfigNames =
    [
        "log_panel",
        "state_panel",
        "decision_panel",
        "timeline_panel",
        "performance_panel",
    ];

    public static ZzzOverlayGuiSettings Create(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Dictionary<string, object?> geometry = ReadObjectMap(values, "panel_geometry");
        bool configuredPanelLockToGameWindow = ReadBool(values, "panel_lock_to_game_window");
        Dictionary<string, bool> panelFreeModes = NormalizePanelFreeModes(
            ReadBoolMap(values, "panel_free_mode_map"),
            configuredPanelLockToGameWindow);
        Dictionary<string, object?> panelAppearance = ReadObjectMap(values, "panel_appearance");
        bool panelLockToGameWindow = panelFreeModes.Values.All(static isFreeMode => !isFreeMode);
        double fontSize = ReadDouble(values, "font_size");
        double panelOpacity = ReadDouble(values, "panel_opacity");
        return new ZzzOverlayGuiSettings
        {
            Enabled = ReadBool(values, "enabled"),
            ShowByDefault = ReadBool(values, "visible"),
            Hotkey = ReadString(values, "toggle_hotkey"),
            PreventCapture = ReadBool(values, "anti_capture"),
            VisionLayerEnabled = ReadBool(values, "vision_layer_enabled"),
            LayoutEditMode = ReadBool(values, "panel_edit_mode"),
            FontFamily = ReadString(values, "font_family"),
            FontSize = fontSize,
            Opacity = panelOpacity / 100d,
            PanelTextColor = ReadString(values, "panel_text_color"),
            LogMaxLines = ReadInt(values, "log_max_lines"),
            LogFadeSeconds = ReadInt(values, "log_fade_seconds"),
            FollowIntervalMs = ReadInt(values, "follow_interval_ms"),
            InputPollIntervalMs = ReadInt(values, "input_poll_interval_ms"),
            StatePollIntervalMs = ReadInt(values, "state_poll_interval_ms"),
            PanelLockToGameWindow = panelLockToGameWindow,
            PanelFreeModeMap = panelFreeModes,
            PatchedCaptureEnabled = ReadBool(values, "patched_capture_enabled"),
            PatchedCaptureSuffix = ReadString(values, "patched_capture_suffix"),
            PerformanceMetrics = ReadBoolMap(values, "performance_metric_enabled_map"),
            Visual = new ZzzOverlayVisualSettings
            {
                ShowYolo = ReadBool(values, "vision_yolo_enabled"),
                YoloDedupIouThreshold = ReadDouble(values, "vision_yolo_dedup_iou_threshold"),
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
                CreatePanel("log", "日志面板", "log_panel", ReadBool(values, "log_panel_enabled"), geometry, panelFreeModes, panelAppearance, panelLockToGameWindow, fontSize, panelOpacity),
                CreatePanel("state", "状态面板", "state_panel", ReadBool(values, "state_panel_enabled"), geometry, panelFreeModes, panelAppearance, panelLockToGameWindow, fontSize, panelOpacity),
                CreatePanel("decision", "决策面板", "decision_panel", ReadBool(values, "decision_panel_enabled"), geometry, panelFreeModes, panelAppearance, panelLockToGameWindow, fontSize, panelOpacity),
                CreatePanel("timeline", "时间轴面板", "timeline_panel", ReadBool(values, "timeline_panel_enabled"), geometry, panelFreeModes, panelAppearance, panelLockToGameWindow, fontSize, panelOpacity),
                CreatePanel("performance", "性能面板", "performance_panel", ReadBool(values, "performance_panel_enabled"), geometry, panelFreeModes, panelAppearance, panelLockToGameWindow, fontSize, panelOpacity),
            ],
        };
    }

    public static Dictionary<string, object?> DefaultPanelGeometry() => new(StringComparer.Ordinal)
    {
        ["log_panel"] = CreateDefaultPanelGeometry(100, 100, 480, 200),
        ["state_panel"] = CreateDefaultPanelGeometry(0, 0, 300, 120),
        ["decision_panel"] = CreateDefaultPanelGeometry(0, 0, 300, 140),
        ["timeline_panel"] = CreateDefaultPanelGeometry(0, 0, 300, 170),
        ["performance_panel"] = CreateDefaultPanelGeometry(0, 0, 300, 110),
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
        IReadOnlyDictionary<string, object?> geometry,
        IReadOnlyDictionary<string, bool> panelFreeModes,
        IReadOnlyDictionary<string, object?> panelAppearance,
        bool panelLockToGameWindow,
        double fallbackFontSize,
        double fallbackOpacity)
    {
        if (!geometry.TryGetValue(configName, out object? raw))
        {
            throw new InvalidOperationException($"panel_geometry 缺少 {configName}。");
        }

        Dictionary<string, object?> panel = ReadObjectMap(raw);
        Dictionary<string, object?> appearance = panelAppearance.TryGetValue(configName, out object? rawAppearance)
            ? ReadObjectMap(rawAppearance)
            : [];
        ZzzOverlayPanelSettings result = new(
            id,
            title,
            enabled,
            ReadDouble(panel, "x"),
            ReadDouble(panel, "y"),
            ReadDouble(panel, "w"),
            ReadDouble(panel, "h"));
        result.IsFreeMode = panelFreeModes.TryGetValue(configName, out bool freeMode)
            ? freeMode
            : !panelLockToGameWindow;
        result.LayoutVersion = ReadOptionalInt(panel, "layout_version");
        result.LockedX = ReadOptionalDouble(panel, "locked_x");
        result.LockedY = ReadOptionalDouble(panel, "locked_y");
        result.LockedWidth = ReadOptionalDouble(panel, "locked_w");
        result.LockedHeight = ReadOptionalDouble(panel, "locked_h");
        result.FreeX = ReadOptionalDouble(panel, "free_x");
        result.FreeY = ReadOptionalDouble(panel, "free_y");
        result.FreeWidth = ReadOptionalDouble(panel, "free_w");
        result.FreeHeight = ReadOptionalDouble(panel, "free_h");
        result.FreeDpi = (uint)Math.Max(1, ReadOptionalInt(panel, "free_dpi", 96));
        result.FreeDisplayName = ReadOptionalString(panel, "free_display_name");
        result.FreeWorkAreaX = ReadOptionalDouble(panel, "free_work_area_x");
        result.FreeWorkAreaY = ReadOptionalDouble(panel, "free_work_area_y");
        result.FreeWorkAreaWidth = ReadOptionalDouble(panel, "free_work_area_w");
        result.FreeWorkAreaHeight = ReadOptionalDouble(panel, "free_work_area_h");
        result.PendingSourceIsFreeMode = ReadOptionalBool(panel, "pending_source_free_mode");
        result.FontSize = ReadOptionalDouble(appearance, "font_size", fallbackFontSize);
        result.Opacity = ReadOptionalDouble(appearance, "opacity", fallbackOpacity);
        return result;
    }

    internal static Dictionary<string, object> CreatePanelGeometry(IReadOnlyList<ZzzOverlayPanelSettings> panels)
    {
        ArgumentNullException.ThrowIfNull(panels);
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        foreach (ZzzOverlayPanelSettings panel in panels)
        {
            result[$"{panel.Id}_panel"] = new Dictionary<string, object>
            {
                ["x"] = (int)Math.Round(panel.X),
                ["y"] = (int)Math.Round(panel.Y),
                ["w"] = (int)Math.Round(panel.Width),
                ["h"] = (int)Math.Round(panel.Height),
                ["layout_version"] = PersistedLayoutVersion(panel),
                ["locked_x"] = panel.LockedX ?? 0d,
                ["locked_y"] = panel.LockedY ?? 0d,
                ["locked_w"] = panel.LockedWidth ?? 0d,
                ["locked_h"] = panel.LockedHeight ?? 0d,
                ["free_x"] = panel.FreeX ?? 0d,
                ["free_y"] = panel.FreeY ?? 0d,
                ["free_w"] = panel.FreeWidth ?? 0d,
                ["free_h"] = panel.FreeHeight ?? 0d,
                ["free_dpi"] = Math.Max(1u, panel.FreeDpi),
                ["free_display_name"] = panel.FreeDisplayName,
                ["free_work_area_x"] = panel.FreeWorkAreaX,
                ["free_work_area_y"] = panel.FreeWorkAreaY,
                ["free_work_area_w"] = panel.FreeWorkAreaWidth,
                ["free_work_area_h"] = panel.FreeWorkAreaHeight,
                ["pending_source_free_mode"] = panel.PendingSourceIsFreeMode,
            };
        }

        return result;
    }

    internal static Dictionary<string, object> CreatePanelAppearance(IReadOnlyList<ZzzOverlayPanelSettings> panels)
    {
        ArgumentNullException.ThrowIfNull(panels);
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        foreach (ZzzOverlayPanelSettings panel in panels)
        {
            result[$"{panel.Id}_panel"] = new Dictionary<string, object>
            {
                ["font_size"] = (int)Math.Round(Math.Clamp(panel.FontSize, 10d, 28d)),
                ["opacity"] = (int)Math.Round(Math.Clamp(panel.Opacity, 5d, 100d)),
            };
        }

        return result;
    }

    private static object RequiredValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) && value is not null
            ? value is JsonElement element
                ? ReadJsonElement(element) ?? throw new InvalidOperationException($"Overlay 配置 {key} 为空。")
                : value
            : throw new InvalidOperationException($"Overlay 配置缺少 {key}。");

    private static int ReadOptionalInt(IReadOnlyDictionary<string, object?> values, string key, int fallback = 0) =>
        values.TryGetValue(key, out object? raw) && raw is not null
            ? Convert.ToInt32(raw is JsonElement element ? ReadJsonElement(element) : raw, CultureInfo.InvariantCulture)
            : fallback;

    private static bool? ReadOptionalBool(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? raw) && raw is not null
            ? Convert.ToBoolean(raw is JsonElement element ? ReadJsonElement(element) : raw, CultureInfo.InvariantCulture)
            : null;

    private static string? ReadOptionalString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        string? value = Convert.ToString(raw is JsonElement element ? ReadJsonElement(element) : raw, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static double? ReadOptionalDouble(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? raw) && raw is not null
            ? Convert.ToDouble(raw is JsonElement element ? ReadJsonElement(element) : raw, CultureInfo.InvariantCulture)
            : null;

    private static double ReadOptionalDouble(IReadOnlyDictionary<string, object?> values, string key, double fallback) =>
        ReadOptionalDouble(values, key) ?? fallback;

    private static Dictionary<string, bool> NormalizePanelFreeModes(
        IReadOnlyDictionary<string, bool> source,
        bool panelLockToGameWindow)
    {
        return PanelConfigNames.ToDictionary(
            panelName => panelName,
            panelName => source.TryGetValue(panelName, out bool isFreeMode)
                ? isFreeMode
                : !panelLockToGameWindow,
            StringComparer.Ordinal);
    }

    private static int PersistedLayoutVersion(ZzzOverlayPanelSettings panel)
    {
        int layoutVersion = Math.Max(1, panel.LayoutVersion);
        bool hasCurrentModeV3Layout = panel.IsFreeMode
            ? ZzzOverlayPanelLayout.HasV3FreeLayout(panel)
            : ZzzOverlayPanelLayout.HasV3LockedLayout(panel);
        return !hasCurrentModeV3Layout
            ? Math.Min(layoutVersion, 2)
            : layoutVersion;
    }

    private static Dictionary<string, object?> CreateDefaultPanelGeometry(
        double x,
        double y,
        double width,
        double height) => new(StringComparer.Ordinal)
    {
        ["x"] = x,
        ["y"] = y,
        ["w"] = width,
        ["h"] = height,
        // Reset geometry contains the v2 locked/free coordinate fields.
        ["layout_version"] = 2,
        ["locked_x"] = 0d,
        ["locked_y"] = 0d,
        ["locked_w"] = 0d,
        ["locked_h"] = 0d,
        ["free_x"] = x,
        ["free_y"] = y,
        ["free_w"] = width,
        ["free_h"] = height,
        ["free_dpi"] = 96u,
        ["free_display_name"] = null,
        ["free_work_area_x"] = null,
        ["free_work_area_y"] = null,
        ["free_work_area_w"] = null,
        ["free_work_area_h"] = null,
        ["pending_source_free_mode"] = null,
    };

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
