using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Resources;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed partial class FrontierResourceDownloadPage : UserControl, IZzzPageLifecycle
{
    private readonly IZzzAppBackend _backend;
    private readonly IZzzResourceDownloadService _resourceService;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly ZzzResourceDownloadSettingsViewModel _viewModel;
    private readonly FAInfoBar _configErrorBar;
    private readonly ZzzLogDisplayCard _logCard;
    private readonly Dictionary<string, FAComboBox> _modelCombos;
    private readonly Dictionary<string, ToggleSwitch> _gpuToggles;
    private readonly Dictionary<string, Button> _downloadButtons;
    private readonly Dictionary<string, Button> _cancelButtons;
    private readonly Dictionary<string, FASettingsExpander> _items;
    private bool _shown;

    public FrontierResourceDownloadPage(
        IZzzAppBackend backend,
        IZzzResourceDownloadService resourceService,
        ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _resourceService = resourceService;
        _operations = operations ?? new ZzzGuiOperationTracker();
        AvaloniaXamlLoader.Load(this);

        _configErrorBar = Required<FAInfoBar>("ConfigErrorBar");
        _viewModel = new ZzzResourceDownloadSettingsViewModel(backend, resourceService, ShowConfigError);
        DataContext = _viewModel;
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
        _items = new Dictionary<string, FASettingsExpander>(StringComparer.Ordinal)
        {
            ["ocr"] = Required<FASettingsExpander>("OcrItem"),
            ["flash_classifier"] = Required<FASettingsExpander>("FlashClassifierItem"),
            ["hollow_zero_event"] = Required<FASettingsExpander>("HollowZeroEventItem"),
            ["lost_void_det"] = Required<FASettingsExpander>("LostVoidDetectorItem"),
        };

        _modelCombos["ocr"].Tag = "ocr";
        _modelCombos["flash_classifier"].Tag = "flash_classifier";
        _modelCombos["hollow_zero_event"].Tag = "hollow_zero_event";
        _modelCombos["lost_void_det"].Tag = "lost_void_det";

        _logCard = new ZzzLogDisplayCard(backend);
        Required<ContentControl>("LogHost").Content = _logCard;
    }

    internal bool HasActiveConfigError => _configErrorBar.IsOpen;

    public void OnPageShown()
    {
        _shown = true;
        _resourceService.StatusChanged -= OnResourceStatusChanged;
        _resourceService.StatusChanged += OnResourceStatusChanged;
        Guid operationId = _operations.Start("settings-resource-download", "reload-resource-settings");
        try
        {
            _viewModel.OnPageShown();
            bool loaded = _viewModel.LastError is null;
            if (!loaded)
            {
                SetInputsEnabled(false);
            }
            else
            {
                SetInputsEnabled(true);
            }

            foreach (ZzzResourceDownloadItemDto resource in _resourceService.GetItems())
            {
                ApplyStatus(resource.Status);
            }

            _operations.Complete(operationId, loaded ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
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
        _viewModel.DisposePage();
        _logCard.DisposePage();
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
        if (!_items.TryGetValue(status.ResourceId, out FASettingsExpander? item))
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

    private void ShowConfigError(string? message)
    {
        if (message is null)
        {
            _configErrorBar.IsOpen = false;
            return;
        }

        _configErrorBar.Title = "模型配置错误";
        _configErrorBar.Message = message;
        _configErrorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"资源下载页缺少 {name}。");
}
