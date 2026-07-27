using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed partial class FrontierOverlaySettingsPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzOverlaySettingsViewModel _viewModel;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly bool _systemSupported;
    private readonly FAInfoBar _unsupportedBar;
    private readonly FAInfoBar _errorBar;
    private readonly FAInfoBar _resultBar;
    private readonly Button _resetGeometryButton;
    private readonly IReadOnlyList<Control> _inputs;

    public FrontierOverlaySettingsPage(
        IZzzAppBackend backend,
        ZzzOverlayController overlayController,
        ZzzGuiOperationTracker? operations = null)
    {
        _operations = operations ?? new ZzzGuiOperationTracker();
        _systemSupported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
        AvaloniaXamlLoader.Load(this);
        _unsupportedBar = Required<FAInfoBar>("UnsupportedBar");
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _resultBar = Required<FAInfoBar>("ResultBar");
        _resetGeometryButton = Required<Button>("ResetGeometryButton");
        Required<ComboBox>("FontFamilyCombo").ItemsSource = FontManager.Current.SystemFonts
            .Select(font => font.Name)
            .Append("Segoe UI")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        _inputs =
        [
            Required<ToggleSwitch>("EnabledToggle"), Required<ToggleSwitch>("VisibleToggle"),
            Required<ToggleSwitch>("AntiCaptureToggle"), Required<TextBox>("HotkeyTextBox"),
            Required<ToggleSwitch>("VisionLayerToggle"), Required<ToggleSwitch>("VisionYoloToggle"),
            Required<ToggleSwitch>("VisionOcrToggle"), Required<ToggleSwitch>("VisionTemplateToggle"),
            Required<ToggleSwitch>("VisionCvToggle"), Required<FANumberBox>("VisionOffsetXNumber"),
            Required<FANumberBox>("VisionOffsetYNumber"), Required<FANumberBox>("VisionScaleXNumber"),
            Required<FANumberBox>("VisionScaleYNumber"), Required<ToggleSwitch>("LogPanelToggle"),
            Required<ToggleSwitch>("StatePanelToggle"), Required<ToggleSwitch>("BattlePanelToggle"),
            Required<TextBox>("BattleStateFilterTextBox"), Required<ToggleSwitch>("DecisionPanelToggle"),
            Required<ToggleSwitch>("TimelinePanelToggle"), Required<ToggleSwitch>("PerformancePanelToggle"),
            Required<ToggleSwitch>("PanelEditModeToggle"), Required<ToggleSwitch>("PanelLockToGameWindowToggle"),
            Required<ComboBox>("FontFamilyCombo"), Required<FANumberBox>("FontSizeNumber"),
            Required<TextBox>("PanelTextColorTextBox"),
            Required<FANumberBox>("LogMaxLinesNumber"), Required<FANumberBox>("LogFadeSecondsNumber"),
            Required<FANumberBox>("FollowIntervalNumber"), Required<FANumberBox>("InputPollIntervalNumber"),
            Required<FANumberBox>("StatePollIntervalNumber"), Required<FANumberBox>("PanelOpacityNumber"),
            _resetGeometryButton, Required<ToggleSwitch>("OcrMetricToggle"),
            Required<ToggleSwitch>("YoloMetricToggle"), Required<ToggleSwitch>("CvMetricToggle"),
            Required<ToggleSwitch>("OperationMetricToggle"), Required<ToggleSwitch>("OverlayMetricToggle"),
            Required<ToggleSwitch>("PatchedCaptureToggle"), Required<TextBox>("PatchedSuffixTextBox"),
        ];
        _viewModel = new ZzzOverlaySettingsViewModel(backend, overlayController, ShowError);
        _viewModel.GeometryReset += OnGeometryReset;
        DataContext = _viewModel;
        _unsupportedBar.IsOpen = !_systemSupported;
        SetInputsEnabled(true);
    }

    internal bool IsSystemSupported => _systemSupported;

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-overlay", "reload-overlay-settings");
        try
        {
            _viewModel.OnPageShown();
            SetInputsEnabled(_viewModel.LastError is null);
            _operations.Complete(
                operationId,
                _viewModel.LastError is null ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ShowError(exception.Message);
            SetInputsEnabled(false);
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
        _viewModel.GeometryReset -= OnGeometryReset;
        _viewModel.DisposePage();
    }

    private void OnGeometryReset(object? sender, EventArgs args)
    {
        _resultBar.Title = "已重置";
        _resultBar.Message = "Overlay 面板位置已重置";
        _resultBar.IsOpen = true;
    }

    private void SetInputsEnabled(bool enabled)
    {
        foreach (Control input in _inputs)
        {
            input.IsEnabled = enabled;
        }

        _resetGeometryButton.IsEnabled = enabled;
        if (enabled)
        {
            Required<ToggleSwitch>("EnabledToggle").IsEnabled = _systemSupported;
            Required<ToggleSwitch>("AntiCaptureToggle").IsEnabled = _systemSupported;
        }
    }

    private void ShowError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _errorBar.IsOpen = false;
            return;
        }

        _resultBar.IsOpen = false;
        _errorBar.Title = "Overlay 设置错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Overlay 设置页缺少 {name}。");
}
