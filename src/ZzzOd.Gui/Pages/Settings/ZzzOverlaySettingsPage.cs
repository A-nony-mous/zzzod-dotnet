using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Settings;

internal sealed partial class ZzzOverlaySettingsAxamlPage : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "overlay";
    private readonly IZzzAppBackend _backend;
    private readonly ZzzOverlayController _overlayController;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly bool _systemSupported;
    private readonly InfoBar _unsupportedBar;
    private readonly InfoBar _errorBar;
    private readonly InfoBar _resultBar;
    private readonly SettingsExpanderItem _enabledItem;
    private readonly Button _resetGeometryButton;
    private readonly IReadOnlyDictionary<string, ToggleSwitch> _toggles;
    private readonly IReadOnlyDictionary<string, NumberBox> _numbers;
    private readonly IReadOnlyDictionary<string, TextBox> _texts;
    private readonly IReadOnlyDictionary<string, ToggleSwitch> _metricToggles;
    private Dictionary<string, bool> _performanceMetrics = new(StringComparer.Ordinal);
    private bool _loading;

    public ZzzOverlaySettingsAxamlPage(IZzzAppBackend backend, ZzzOverlayController overlayController, ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _overlayController = overlayController;
        _operations = operations ?? new ZzzGuiOperationTracker();
        _systemSupported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
        AvaloniaXamlLoader.Load(this);
        _unsupportedBar = Required<InfoBar>("UnsupportedBar");
        _errorBar = Required<InfoBar>("ErrorBar");
        _resultBar = Required<InfoBar>("ResultBar");
        _enabledItem = Required<SettingsExpanderItem>("EnabledItem");
        _resetGeometryButton = Required<Button>("ResetGeometryButton");
        _toggles = new Dictionary<string, ToggleSwitch>(StringComparer.Ordinal)
        {
            ["enabled"] = Required<ToggleSwitch>("EnabledToggle"),
            ["visible"] = Required<ToggleSwitch>("VisibleToggle"),
            ["anti_capture"] = Required<ToggleSwitch>("AntiCaptureToggle"),
            ["vision_layer_enabled"] = Required<ToggleSwitch>("VisionLayerToggle"),
            ["vision_yolo_enabled"] = Required<ToggleSwitch>("VisionYoloToggle"),
            ["vision_ocr_enabled"] = Required<ToggleSwitch>("VisionOcrToggle"),
            ["vision_template_enabled"] = Required<ToggleSwitch>("VisionTemplateToggle"),
            ["vision_cv_enabled"] = Required<ToggleSwitch>("VisionCvToggle"),
            ["log_panel_enabled"] = Required<ToggleSwitch>("LogPanelToggle"),
            ["state_panel_enabled"] = Required<ToggleSwitch>("StatePanelToggle"),
            ["decision_panel_enabled"] = Required<ToggleSwitch>("DecisionPanelToggle"),
            ["timeline_panel_enabled"] = Required<ToggleSwitch>("TimelinePanelToggle"),
            ["performance_panel_enabled"] = Required<ToggleSwitch>("PerformancePanelToggle"),
            ["panel_edit_mode"] = Required<ToggleSwitch>("PanelEditModeToggle"),
            ["patched_capture_enabled"] = Required<ToggleSwitch>("PatchedCaptureToggle"),
        };
        _numbers = new Dictionary<string, NumberBox>(StringComparer.Ordinal)
        {
            ["vision_offset_x"] = Required<NumberBox>("VisionOffsetXNumber"),
            ["vision_offset_y"] = Required<NumberBox>("VisionOffsetYNumber"),
            ["vision_scale_x"] = Required<NumberBox>("VisionScaleXNumber"),
            ["vision_scale_y"] = Required<NumberBox>("VisionScaleYNumber"),
            ["font_size"] = Required<NumberBox>("FontSizeNumber"),
            ["log_max_lines"] = Required<NumberBox>("LogMaxLinesNumber"),
            ["log_fade_seconds"] = Required<NumberBox>("LogFadeSecondsNumber"),
            ["follow_interval_ms"] = Required<NumberBox>("FollowIntervalNumber"),
            ["state_poll_interval_ms"] = Required<NumberBox>("StatePollIntervalNumber"),
            ["panel_opacity"] = Required<NumberBox>("PanelOpacityNumber"),
        };
        _texts = new Dictionary<string, TextBox>(StringComparer.Ordinal)
        {
            ["toggle_hotkey"] = Required<TextBox>("HotkeyTextBox"),
            ["panel_text_color"] = Required<TextBox>("PanelTextColorTextBox"),
            ["patched_capture_suffix"] = Required<TextBox>("PatchedSuffixTextBox"),
        };
        _metricToggles = new Dictionary<string, ToggleSwitch>(StringComparer.Ordinal)
        {
            ["ocr_ms"] = Required<ToggleSwitch>("OcrMetricToggle"),
            ["yolo_ms"] = Required<ToggleSwitch>("YoloMetricToggle"),
            ["cv_pipeline_ms"] = Required<ToggleSwitch>("CvMetricToggle"),
            ["operation_round_ms"] = Required<ToggleSwitch>("OperationMetricToggle"),
            ["overlay_refresh_ms"] = Required<ToggleSwitch>("OverlayMetricToggle"),
        };
        _unsupportedBar.IsOpen = !_systemSupported;
        _toggles["enabled"].IsEnabled = _systemSupported;
        _toggles["anti_capture"].IsEnabled = _systemSupported;
    }

    internal bool IsSystemSupported => _systemSupported;

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-overlay", "reload-overlay-settings");
        try
        {
            _operations.Complete(operationId, Reload() ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ShowError($"Overlay 设置读取失败：{exception.Message}");
        }
    }

    public void OnPageLeave()
    {
    }

    public void OnPageHidden()
    {
    }

    public void DisposePage()
    {
    }

    private bool Reload()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(ScopeName);
        if (!result.Success || result.Value is null)
        {
            SetInputsEnabled(false);
            ShowError(result.Error ?? "Overlay 设置读取失败。");
            return false;
        }

        return ApplyValues(result.Value.Values);
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading || sender is not ToggleSwitch { Tag: string key } toggle)
        {
            return;
        }

        if (key.StartsWith("perf:", StringComparison.Ordinal))
        {
            string metric = key[5..];
            Dictionary<string, bool> metrics = new(_performanceMetrics, StringComparer.Ordinal)
            {
                [metric] = toggle.IsChecked == true,
            };
            Save(new Dictionary<string, object?> { ["performance_metric_enabled_map"] = metrics });
            return;
        }

        Save(new Dictionary<string, object?> { [key] = toggle.IsChecked == true });
    }

    private void OnNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || sender.Tag is not string key || double.IsNaN(sender.Value))
        {
            return;
        }

        object value = key is "vision_scale_x" or "vision_scale_y"
            ? sender.Value
            : Convert.ToInt32(sender.Value, CultureInfo.InvariantCulture);
        Save(new Dictionary<string, object?> { [key] = value });
    }

    private void OnTextChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading || sender is not TextBox { Tag: string key } textBox)
        {
            return;
        }

        Save(new Dictionary<string, object?> { [key] = textBox.Text ?? string.Empty });
    }

    private void OnResetGeometryClicked(object? sender, RoutedEventArgs args)
    {
        ApplySaveResult(_overlayController.ResetPanelGeometry());
        if (!_errorBar.IsOpen)
        {
			_resultBar.Title = "已重置";
			_resultBar.Message = "Overlay 面板位置已重置";
            _resultBar.IsOpen = true;
        }
    }

    private void Save(IReadOnlyDictionary<string, object?> values)
    {
        _resultBar.IsOpen = false;
        ApplySaveResult(_overlayController.SaveConfiguration(values));
    }

    private void ApplySaveResult(ZzzBackendResult<ZzzConfigScopeValuesDto> result)
    {
        if (!result.Success || result.Value is null)
        {
			ShowError(result.Error ?? "Overlay 设置保存失败。");
            return;
        }

        ApplyValues(result.Value.Values);
    }

    private bool ApplyValues(IReadOnlyDictionary<string, object?> values)
    {
        _loading = true;
        try
        {
            foreach ((string key, ToggleSwitch toggle) in _toggles)
            {
                toggle.IsChecked = ReadBool(values, key);
            }

            foreach ((string key, NumberBox number) in _numbers)
            {
                number.Value = ReadDouble(values, key);
            }

            foreach ((string key, TextBox text) in _texts)
            {
                text.Text = ReadString(values, key);
            }

            _performanceMetrics = ReadBoolMap(values, "performance_metric_enabled_map");
            foreach ((string metric, ToggleSwitch toggle) in _metricToggles)
            {
                toggle.IsChecked = _performanceMetrics.TryGetValue(metric, out bool enabled) && enabled;
            }

            _enabledItem.Description = $"启用后可通过 Ctrl+Alt+{FormatHotkey(ReadString(values, "toggle_hotkey"))} 切换显隐";
            _errorBar.IsOpen = false;
            SetInputsEnabled(true);
            _overlayController.ReloadConfiguration(ZzzOverlaySettingsMapper.Create(values));
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or JsonException)
        {
            SetInputsEnabled(false);
            ShowError($"Overlay 设置读取失败：{exception.Message}");
            return false;
        }
        finally
        {
            _loading = false;
        }
    }

    private void SetInputsEnabled(bool enabled)
    {
        foreach (ToggleSwitch toggle in _toggles.Values)
        {
            toggle.IsEnabled = enabled;
        }

        foreach (NumberBox number in _numbers.Values)
        {
            number.IsEnabled = enabled;
        }

        foreach (TextBox text in _texts.Values)
        {
            text.IsEnabled = enabled;
        }

        foreach (ToggleSwitch toggle in _metricToggles.Values)
        {
            toggle.IsEnabled = enabled;
        }

        _resetGeometryButton.IsEnabled = enabled;

        _toggles["enabled"].IsEnabled = enabled && _systemSupported;
        _toggles["anti_capture"].IsEnabled = enabled && _systemSupported;
    }

    private void ShowError(string message)
    {
        _errorBar.Title = "Overlay 设置错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private static string FormatHotkey(string value) => string.IsNullOrWhiteSpace(value)
        ? "O"
        : value.Trim().ToUpperInvariant();

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToBoolean(RequiredValue(values, key), CultureInfo.InvariantCulture);

    private static int ReadInt(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToInt32(RequiredValue(values, key), CultureInfo.InvariantCulture);

    private static double ReadDouble(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToDouble(RequiredValue(values, key), CultureInfo.InvariantCulture);

    private static string ReadString(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToString(RequiredValue(values, key), CultureInfo.InvariantCulture)
        ?? throw new InvalidOperationException($"Overlay 配置 {key} 为空。");

    private static object RequiredValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) && value is not null
            ? value is JsonElement element
                ? ReadJsonElement(element) ?? throw new InvalidOperationException($"Overlay 配置 {key} 为空。")
                : value
            : throw new InvalidOperationException($"Overlay 配置缺少 {key}。");

    private static Dictionary<string, bool> ReadBoolMap(IReadOnlyDictionary<string, object?> values, string key)
    {
        Dictionary<string, object?> source = ReadObjectMap(RequiredValue(values, key));
        return source.ToDictionary(
            pair => pair.Key,
            pair => Convert.ToBoolean(pair.Value is JsonElement element ? ReadJsonElement(element) : pair.Value, CultureInfo.InvariantCulture),
            StringComparer.Ordinal);
    }

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

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Overlay 设置页缺少 {name}。");
}

