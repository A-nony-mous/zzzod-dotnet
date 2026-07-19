using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Resources;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Settings;

internal sealed record ZzzResourceModelOption(string Label, string Value);

internal sealed partial class ZzzResourceDownloadSettingsAxamlPage : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "model";
    private readonly IZzzAppBackend _backend;
    private readonly IZzzResourceDownloadService _resourceService;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly FAInfoBar _configErrorBar;
    private readonly ZzzLogDisplayCard _logCard;
    private readonly Dictionary<string, FAComboBox> _modelCombos;
    private readonly Dictionary<string, ToggleSwitch> _gpuToggles;
    private readonly Dictionary<string, Button> _downloadButtons;
    private readonly Dictionary<string, Button> _cancelButtons;
    private readonly Dictionary<string, FASettingsExpanderItem> _items;
    private readonly Dictionary<string, IReadOnlyList<ZzzResourceModelOption>> _options = new(StringComparer.Ordinal);
    private bool _loading;
    private bool _shown;

    public ZzzResourceDownloadSettingsAxamlPage(
        IZzzAppBackend backend,
        IZzzResourceDownloadService resourceService,
        ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _resourceService = resourceService;
        _operations = operations ?? new ZzzGuiOperationTracker();
        AvaloniaXamlLoader.Load(this);

        _configErrorBar = Required<FAInfoBar>("ConfigErrorBar");
        _modelCombos = new Dictionary<string, FAComboBox>(StringComparer.Ordinal)
        {
            ["ocr"] = Required<FAComboBox>("OcrProfileCombo"),
            ["flash_classifier"] = Required<FAComboBox>("FlashClassifierCombo"),
            ["hollow_zero_event"] = Required<FAComboBox>("HollowZeroEventCombo"),
            ["lost_void_det"] = Required<FAComboBox>("LostVoidDetectorCombo"),
        };
        _gpuToggles = new Dictionary<string, ToggleSwitch>(StringComparer.Ordinal)
        {
            ["ocr"] = Required<ToggleSwitch>("OcrGpuToggle"),
            ["flash_classifier"] = Required<ToggleSwitch>("FlashClassifierGpuToggle"),
            ["hollow_zero_event"] = Required<ToggleSwitch>("HollowZeroEventGpuToggle"),
            ["lost_void_det"] = Required<ToggleSwitch>("LostVoidDetectorGpuToggle"),
        };
        _downloadButtons = new Dictionary<string, Button>(StringComparer.Ordinal)
        {
            ["ocr"] = Required<Button>("OcrDownloadButton"),
            ["flash_classifier"] = Required<Button>("FlashClassifierDownloadButton"),
            ["hollow_zero_event"] = Required<Button>("HollowZeroEventDownloadButton"),
            ["lost_void_det"] = Required<Button>("LostVoidDetectorDownloadButton"),
        };
        _cancelButtons = new Dictionary<string, Button>(StringComparer.Ordinal)
        {
            ["ocr"] = Required<Button>("OcrCancelButton"),
            ["flash_classifier"] = Required<Button>("FlashClassifierCancelButton"),
            ["hollow_zero_event"] = Required<Button>("HollowZeroEventCancelButton"),
            ["lost_void_det"] = Required<Button>("LostVoidDetectorCancelButton"),
        };
        _items = new Dictionary<string, FASettingsExpanderItem>(StringComparer.Ordinal)
        {
            ["ocr"] = Required<FASettingsExpanderItem>("OcrItem"),
            ["flash_classifier"] = Required<FASettingsExpanderItem>("FlashClassifierItem"),
            ["hollow_zero_event"] = Required<FASettingsExpanderItem>("HollowZeroEventItem"),
            ["lost_void_det"] = Required<FASettingsExpanderItem>("LostVoidDetectorItem"),
        };

        _modelCombos["ocr"].Tag = "ocr";
        _modelCombos["flash_classifier"].Tag = "flash_classifier";
        _modelCombos["hollow_zero_event"].Tag = "hollow_zero_event";
        _modelCombos["lost_void_det"].Tag = "lost_void_det";

        _logCard = new ZzzLogDisplayCard(backend);
        Required<ContentControl>("LogHost").Content = _logCard;
    }

    internal IReadOnlyList<ZzzResourceModelOption> OcrOptions =>
        _options.TryGetValue("ocr", out IReadOnlyList<ZzzResourceModelOption>? options) ? options : [];

    internal bool HasActiveConfigError => _configErrorBar.IsOpen;

    public void OnPageShown()
    {
        _shown = true;
        _resourceService.StatusChanged -= OnResourceStatusChanged;
        _resourceService.StatusChanged += OnResourceStatusChanged;
        Guid operationId = _operations.Start("settings-resource-download", "reload-resource-settings");
        try
        {
            _operations.Complete(operationId, Reload() ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            SetInputsEnabled(false);
            ShowConfigError(exception.Message);
        }

        _logCard.OnPageShown();
    }

    public void OnPageHidden()
    {
        _shown = false;
        _resourceService.StatusChanged -= OnResourceStatusChanged;
        _logCard.OnPageHidden();
    }

    public void OnPageLeave() => OnPageHidden();

    public void DisposePage()
    {
        _shown = false;
        _resourceService.StatusChanged -= OnResourceStatusChanged;
        _logCard.DisposePage();
    }

    private bool Reload()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(ScopeName);
        if (!result.Success || result.Value is null)
        {
            SetInputsEnabled(false);
            ShowConfigError(result.Error ?? "模型配置读取失败。");
            return false;
        }

        return ApplyValues(result.Value.Values, _resourceService.GetItems());
    }

    private void OnModelSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading
            || sender is not FAComboBox { Tag: string key, SelectedItem: ZzzResourceModelOption selected })
        {
            return;
        }

        Save(new Dictionary<string, object?> { [key] = selected.Value });
    }

    private void OnGpuChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading || sender is not ToggleSwitch { Tag: string key } toggle)
        {
            return;
        }

        Save(new Dictionary<string, object?> { [key] = toggle.IsChecked == true });
    }

    private async void OnDownloadClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string resourceId }
            || !_modelCombos.TryGetValue(resourceId, out FAComboBox? combo)
            || combo.SelectedItem is not ZzzResourceModelOption selected)
        {
            return;
        }

        try
        {
            await _resourceService.DownloadAsync(resourceId, selected.Value).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ShowConfigError(exception.Message);
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string resourceId })
        {
            _ = _resourceService.Cancel(resourceId);
        }
    }

    private void Save(IReadOnlyDictionary<string, object?> values)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(
            new ZzzSaveConfigScopeRequest(ScopeName, values));
        if (!result.Success || result.Value is null)
        {
            ShowConfigError(result.Error ?? "模型配置保存失败。");
            return;
        }

        ApplyValues(result.Value.Values, _resourceService.GetItems());
    }

    private bool ApplyValues(
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyList<ZzzResourceDownloadItemDto> resources)
    {
        _loading = true;
        try
        {
            foreach (ZzzResourceDownloadItemDto resource in resources)
            {
                IReadOnlyList<ZzzResourceModelOption> options = resource.Options
                    .Select(option => new ZzzResourceModelOption(option.Label, option.ModelId))
                    .ToArray();
                _options[resource.ResourceId] = options;
                FAComboBox combo = _modelCombos[resource.ResourceId];
                combo.ItemsSource = options;
                string selectedValue = resource.ResourceId == "ocr"
                    ? ReadString(values, "ocr", resource.SelectedModelId)
                    : ReadString(values, resource.ResourceId, resource.SelectedModelId);
                Select(combo, options, selectedValue);
                _gpuToggles[resource.ResourceId].IsChecked = resource.ResourceId == "ocr"
                    ? ReadBool(values, "ocr_use_gpu")
                    : ReadBool(values, resource.ResourceId + "_gpu");
                _gpuToggles[resource.ResourceId].IsEnabled = true;
                ApplyStatus(resource.Status);
            }

            _configErrorBar.IsOpen = false;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or JsonException)
        {
            SetInputsEnabled(false);
            ShowConfigError($"模型配置读取失败：{exception.Message}");
            return false;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnResourceStatusChanged(object? sender, ZzzResourceDownloadStatusDto status)
    {
        if (!_shown)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => ApplyStatus(status));
    }

    private void ApplyStatus(ZzzResourceDownloadStatusDto status)
    {
        if (!_items.TryGetValue(status.ResourceId, out FASettingsExpanderItem? item))
        {
            return;
        }

        item.Description = string.IsNullOrWhiteSpace(status.Error)
            ? status.Message
            : $"{status.Message}：{status.Error}";
        FAComboBox combo = _modelCombos[status.ResourceId];
        Button download = _downloadButtons[status.ResourceId];
        Button cancel = _cancelButtons[status.ResourceId];
        combo.IsEnabled = !status.IsRunning;
        download.Content = status.IsCancelling ? "取消中" : status.IsRunning ? "下载中" : status.IsInstalled ? "已下载" : "下载";
        download.IsEnabled = !status.IsRunning && !status.IsInstalled;
        cancel.IsVisible = status.IsRunning;
        cancel.IsEnabled = status.IsRunning && !status.IsCancelling;
    }

    private void SetInputsEnabled(bool enabled)
    {
        foreach (FAComboBox combo in _modelCombos.Values)
        {
            combo.IsEnabled = enabled;
        }

        foreach (ToggleSwitch toggle in _gpuToggles.Values)
        {
            toggle.IsEnabled = enabled;
        }

        foreach (Button button in _downloadButtons.Values)
        {
            button.IsEnabled = enabled;
        }
    }

    private void ShowConfigError(string message)
    {
        _configErrorBar.Title = "模型配置错误";
        _configErrorBar.Message = message;
        _configErrorBar.IsOpen = true;
    }

    private static void Select(
        SelectingItemsControl combo,
        IReadOnlyList<ZzzResourceModelOption> options,
        string value)
    {
        combo.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value) || value is null)
        {
            return false;
        }

        return value is JsonElement element
            ? element.ValueKind == JsonValueKind.True
            : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> values, string key, string fallback)
    {
        if (!values.TryGetValue(key, out object? value) || value is null)
        {
            return fallback;
        }

        return value is JsonElement element
            ? element.GetString() ?? fallback
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"资源下载页缺少 {name}。");
}

