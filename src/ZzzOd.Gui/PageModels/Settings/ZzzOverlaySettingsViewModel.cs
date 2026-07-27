using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed partial class ZzzOverlaySettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField EnabledField = Bool("enabled", false);
    private static readonly ZzzConfigField VisibleField = Bool("visible", true);
    private static readonly ZzzConfigField AntiCaptureField = Bool("anti_capture", true);
    private static readonly ZzzConfigField PanelLockField = Bool("panel_lock_to_game_window", true);
    private static readonly ZzzConfigField VisionLayerField = Bool("vision_layer_enabled", true);
    private static readonly ZzzConfigField VisionYoloField = Bool("vision_yolo_enabled", true);
    private static readonly ZzzConfigField VisionOcrField = Bool("vision_ocr_enabled", true);
    private static readonly ZzzConfigField VisionTemplateField = Bool("vision_template_enabled", true);
    private static readonly ZzzConfigField VisionCvField = Bool("vision_cv_enabled", true);
    private static readonly ZzzConfigField LogPanelField = Bool("log_panel_enabled", true);
    private static readonly ZzzConfigField StatePanelField = Bool("state_panel_enabled", true);
    private static readonly ZzzConfigField DecisionPanelField = Bool("decision_panel_enabled", true);
    private static readonly ZzzConfigField TimelinePanelField = Bool("timeline_panel_enabled", true);
    private static readonly ZzzConfigField PerformancePanelField = Bool("performance_panel_enabled", true);
    private static readonly ZzzConfigField PanelEditModeField = Bool("panel_edit_mode", false);
    private static readonly ZzzConfigField PatchedCaptureField = Bool("patched_capture_enabled", false);
    private static readonly ZzzConfigField VisionOffsetXField = Integer("vision_offset_x", 0);
    private static readonly ZzzConfigField VisionOffsetYField = Integer("vision_offset_y", 0);
    private static readonly ZzzConfigField VisionScaleXField = Double("vision_scale_x", 1d);
    private static readonly ZzzConfigField VisionScaleYField = Double("vision_scale_y", 1d);
    private static readonly ZzzConfigField FontSizeField = Integer("font_size", 12);
    private static readonly ZzzConfigField LogMaxLinesField = Integer("log_max_lines", 120);
    private static readonly ZzzConfigField LogFadeSecondsField = Integer("log_fade_seconds", 12);
    private static readonly ZzzConfigField FollowIntervalField = Integer("follow_interval_ms", 120);
    private static readonly ZzzConfigField StatePollIntervalField = Integer("state_poll_interval_ms", 200);
    private static readonly ZzzConfigField InputPollIntervalField = Integer("input_poll_interval_ms", 50);
    private static readonly ZzzConfigField PanelOpacityField = Integer("panel_opacity", 70);
    private static readonly ZzzConfigField ToggleHotkeyField = Text("toggle_hotkey", "o");
    private static readonly ZzzConfigField PanelTextColorField = Text("panel_text_color", "#f2f2f2");
    private static readonly ZzzConfigField PatchedCaptureSuffixField = Text("patched_capture_suffix", "_patched");
    private static readonly ZzzConfigField PerformanceMetricsField = new(
        "performance_metric_enabled_map",
        typeof(Dictionary<string, bool>),
        DefaultPerformanceMetrics(),
        ReadBoolMap);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        EnabledField, VisibleField, AntiCaptureField, PanelLockField,
        VisionLayerField, VisionYoloField, VisionOcrField, VisionTemplateField, VisionCvField,
        LogPanelField, StatePanelField, DecisionPanelField, TimelinePanelField, PerformancePanelField,
        PanelEditModeField, PatchedCaptureField,
        VisionOffsetXField, VisionOffsetYField, VisionScaleXField, VisionScaleYField,
        FontSizeField, LogMaxLinesField, LogFadeSecondsField, FollowIntervalField,
        StatePollIntervalField, InputPollIntervalField, PanelOpacityField,
        ToggleHotkeyField, PanelTextColorField, PatchedCaptureSuffixField, PerformanceMetricsField,
    ];

    private readonly ZzzOverlayController _overlayController;

    public ZzzOverlaySettingsViewModel(
        IZzzAppBackend backend,
        ZzzOverlayController overlayController,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _overlayController = overlayController;
    }

    protected override string ScopeName => "overlay";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public event EventHandler? GeometryReset;

    public bool Enabled { get => GetValue<bool>(EnabledField); set => SetValue(EnabledField, value); }
    public bool Visible { get => GetValue<bool>(VisibleField); set => SetValue(VisibleField, value); }
    public bool AntiCapture { get => GetValue<bool>(AntiCaptureField); set => SetValue(AntiCaptureField, value); }
    public bool PanelLockToGameWindow { get => GetValue<bool>(PanelLockField); set => SetValue(PanelLockField, value); }
    public bool VisionLayerEnabled { get => GetValue<bool>(VisionLayerField); set => SetValue(VisionLayerField, value); }
    public bool VisionYoloEnabled { get => GetValue<bool>(VisionYoloField); set => SetValue(VisionYoloField, value); }
    public bool VisionOcrEnabled { get => GetValue<bool>(VisionOcrField); set => SetValue(VisionOcrField, value); }
    public bool VisionTemplateEnabled { get => GetValue<bool>(VisionTemplateField); set => SetValue(VisionTemplateField, value); }
    public bool VisionCvEnabled { get => GetValue<bool>(VisionCvField); set => SetValue(VisionCvField, value); }
    public bool LogPanelEnabled { get => GetValue<bool>(LogPanelField); set => SetValue(LogPanelField, value); }
    public bool StatePanelEnabled { get => GetValue<bool>(StatePanelField); set => SetValue(StatePanelField, value); }
    public bool DecisionPanelEnabled { get => GetValue<bool>(DecisionPanelField); set => SetValue(DecisionPanelField, value); }
    public bool TimelinePanelEnabled { get => GetValue<bool>(TimelinePanelField); set => SetValue(TimelinePanelField, value); }
    public bool PerformancePanelEnabled { get => GetValue<bool>(PerformancePanelField); set => SetValue(PerformancePanelField, value); }
    public bool PanelEditMode { get => GetValue<bool>(PanelEditModeField); set => SetValue(PanelEditModeField, value); }
    public bool PatchedCaptureEnabled { get => GetValue<bool>(PatchedCaptureField); set => SetValue(PatchedCaptureField, value); }
    public int VisionOffsetX { get => GetValue<int>(VisionOffsetXField); set => SetValue(VisionOffsetXField, value); }
    public int VisionOffsetY { get => GetValue<int>(VisionOffsetYField); set => SetValue(VisionOffsetYField, value); }
    public double VisionScaleX { get => GetValue<double>(VisionScaleXField); set => SetValue(VisionScaleXField, value); }
    public double VisionScaleY { get => GetValue<double>(VisionScaleYField); set => SetValue(VisionScaleYField, value); }
    public int FontSize { get => GetValue<int>(FontSizeField); set => SetValue(FontSizeField, value); }
    public int LogMaxLines { get => GetValue<int>(LogMaxLinesField); set => SetValue(LogMaxLinesField, value); }
    public int LogFadeSeconds { get => GetValue<int>(LogFadeSecondsField); set => SetValue(LogFadeSecondsField, value); }
    public int FollowIntervalMs { get => GetValue<int>(FollowIntervalField); set => SetValue(FollowIntervalField, value); }
    public int StatePollIntervalMs { get => GetValue<int>(StatePollIntervalField); set => SetValue(StatePollIntervalField, value); }
    public int InputPollIntervalMs { get => GetValue<int>(InputPollIntervalField); set => SetValue(InputPollIntervalField, value); }
    public int PanelOpacity { get => GetValue<int>(PanelOpacityField); set => SetValue(PanelOpacityField, value); }
    public string ToggleHotkey { get => GetValue<string>(ToggleHotkeyField); set => SetValue(ToggleHotkeyField, value); }
    public string PanelTextColor { get => GetValue<string>(PanelTextColorField); set => SetValue(PanelTextColorField, value); }
    public string PatchedCaptureSuffix { get => GetValue<string>(PatchedCaptureSuffixField); set => SetValue(PatchedCaptureSuffixField, value); }

    public bool OcrMetricEnabled { get => GetMetric("ocr_ms"); set => SetMetric("ocr_ms", value); }
    public bool YoloMetricEnabled { get => GetMetric("yolo_ms"); set => SetMetric("yolo_ms", value); }
    public bool CvPipelineMetricEnabled { get => GetMetric("cv_pipeline_ms"); set => SetMetric("cv_pipeline_ms", value); }
    public bool OperationRoundMetricEnabled { get => GetMetric("operation_round_ms"); set => SetMetric("operation_round_ms", value); }
    public bool OverlayRefreshMetricEnabled { get => GetMetric("overlay_refresh_ms"); set => SetMetric("overlay_refresh_ms", value); }

    protected override ZzzBackendResult<ZzzConfigScopeValuesDto> SaveFieldCore(
        ZzzConfigField field,
        object? value)
    {
        Dictionary<string, object?> requested = new(StringComparer.Ordinal)
        {
            [field.Key] = field.Write(value),
        };
        if (field == FontSizeField || field == PanelOpacityField)
        {
            double fontSize = field == FontSizeField
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : 0d;
            double opacity = field == PanelOpacityField
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : 0d;
            requested["panel_appearance"] = _overlayController.Settings.Panels.ToDictionary(
                panel => $"{panel.Id}_panel",
                panel => (object?)new Dictionary<string, object>
                {
                    ["font_size"] = (int)Math.Round(Math.Clamp(field == FontSizeField ? fontSize : panel.FontSize, 10d, 28d)),
                    ["opacity"] = (int)Math.Round(Math.Clamp(field == PanelOpacityField ? opacity : panel.Opacity, 5d, 100d)),
                },
                StringComparer.Ordinal);
        }

        return _overlayController.SaveConfiguration(requested);
    }

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        NotifyMetricProperties();
        _overlayController.ReloadConfiguration(ZzzOverlaySettingsMapper.Create(values.Values));
    }

    protected override void OnFieldSaved(ZzzConfigField field, ZzzConfigScopeValuesDto values) =>
        ApplyScopeValues(values);

    [RelayCommand]
    private void ResetPanelGeometry()
    {
        if (ApplyScopeResult(_overlayController.ResetPanelGeometry(), "Overlay 面板位置重置失败。"))
        {
            GeometryReset?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool GetMetric(string key)
    {
        Dictionary<string, bool> metrics = GetValue<Dictionary<string, bool>>(PerformanceMetricsField);
        return metrics.TryGetValue(key, out bool enabled) && enabled;
    }

    private void SetMetric(string key, bool value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        Dictionary<string, bool> metrics = new(GetValue<Dictionary<string, bool>>(PerformanceMetricsField), StringComparer.Ordinal)
        {
            [key] = value,
        };
        SetValue(PerformanceMetricsField, metrics, propertyName);
    }

    private void NotifyMetricProperties()
    {
        OnPropertyChanged(nameof(OcrMetricEnabled));
        OnPropertyChanged(nameof(YoloMetricEnabled));
        OnPropertyChanged(nameof(CvPipelineMetricEnabled));
        OnPropertyChanged(nameof(OperationRoundMetricEnabled));
        OnPropertyChanged(nameof(OverlayRefreshMetricEnabled));
    }

    private static ZzzConfigField Bool(string key, bool defaultValue) => new(key, typeof(bool), defaultValue, ReadBool);
    private static ZzzConfigField Integer(string key, int defaultValue) => new(key, typeof(int), defaultValue, ReadScalar);
    private static ZzzConfigField Double(string key, double defaultValue) => new(key, typeof(double), defaultValue, ReadScalar);
    private static ZzzConfigField Text(string key, string defaultValue) => new(key, typeof(string), defaultValue, ReadScalar);

    private static object? ReadBool(object? value) =>
        value is JsonElement element
            ? element.ValueKind == JsonValueKind.True
            : value;

    private static object? ReadScalar(object? value) =>
        value is JsonElement element ? ReadJsonElement(element) : value;

    private static object? ReadBoolMap(object? value)
    {
        if (value is IReadOnlyDictionary<string, bool> booleans)
        {
            return booleans.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is IReadOnlyDictionary<string, object?> objects)
        {
            return objects.ToDictionary(
                pair => pair.Key,
                pair => Convert.ToBoolean(pair.Value is JsonElement element ? ReadJsonElement(element) : pair.Value, CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            return json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.GetBoolean(),
                StringComparer.Ordinal);
        }

        return DefaultPerformanceMetrics();
    }

    private static object? ReadJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when element.TryGetInt32(out int integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Null => null,
        _ => element.ToString(),
    };

    private static Dictionary<string, bool> DefaultPerformanceMetrics() => new(StringComparer.Ordinal)
    {
        ["ocr_ms"] = true,
        ["yolo_ms"] = true,
        ["cv_pipeline_ms"] = true,
        ["operation_round_ms"] = true,
        ["overlay_refresh_ms"] = true,
    };
}
