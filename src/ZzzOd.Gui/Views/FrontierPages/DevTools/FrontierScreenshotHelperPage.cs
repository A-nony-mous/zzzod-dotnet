using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.Devtools;

namespace ZzzOd.Gui.Views.FrontierPages.DevTools;

internal sealed partial class FrontierScreenshotHelperPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzGuiRunIntentService _runIntent;
    private readonly FAInfoBar _configErrorBar;
    private readonly Button _saveKeyButton;
    private readonly ZzzScreenshotHelperSettingsViewModel _viewModel;
    private volatile bool _capturingKey;
    private IDisposable? _inputSuspension;

    public FrontierScreenshotHelperPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _runIntent = runIntent;
        _viewModel = new ZzzScreenshotHelperSettingsViewModel(backend, ShowConfigError);
        _runIntent.GlobalInputPressed += OnGlobalInputPressed;
        RunPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.ScreenshotHelper,
            runIntent: runIntent,
            fixedGroupId: ScreenshotHelperConstants.DefaultGroupId);

        AvaloniaXamlLoader.Load(this);
        _configErrorBar = Required<FAInfoBar>("ConfigErrorBar");
        _saveKeyButton = Required<Button>("SaveKeyButton");
        Required<ContentControl>("RunHost").Content = RunPanel;
        DataContext = _viewModel;
        SyncSaveKeyButton();
    }

    internal ZzzRunPanel RunPanel { get; }

    internal int? InstanceIndex => _viewModel.ActiveInstanceIndex;

    public void OnPageShown()
    {
        _viewModel.OnPageShown();
        SyncSaveKeyButton();
        RunPanel.OnPageShown();
    }

    public void OnPageHidden()
    {
        StopKeyCapture();
        RunPanel.OnPageHidden();
    }

    public void OnPageLeave()
    {
        StopKeyCapture();
        RunPanel.OnPageLeave();
    }

    public void DisposePage()
    {
        StopKeyCapture();
        _runIntent.GlobalInputPressed -= OnGlobalInputPressed;
        RunPanel.DisposePage();
    }

    private void OnSaveKeyClicked(object? sender, RoutedEventArgs args)
    {
        StopKeyCapture();
        _capturingKey = true;
        _inputSuspension = ScreenshotHelperGlobalInputSource.Suspend();
        SyncSaveKeyButton();
        _saveKeyButton.Focus();
    }

    private void OnGlobalInputPressed(object? sender, string key)
    {
        if (_capturingKey)
        {
            Dispatcher.UIThread.Post(() => CompleteKeyCapture(key));
        }
    }

    private void CompleteKeyCapture(string key)
    {
        _capturingKey = false;
        _inputSuspension?.Dispose();
        _inputSuspension = null;
        _viewModel.KeySave = key;
        SyncSaveKeyButton();
    }

    private void StopKeyCapture()
    {
        _capturingKey = false;
        _inputSuspension?.Dispose();
        _inputSuspension = null;
        SyncSaveKeyButton();
    }

    private void SyncSaveKeyButton() => _saveKeyButton.Content = _capturingKey ? "请按键" : _viewModel.KeySaveLabel;

    private void ShowConfigError(string message)
    {
        _configErrorBar.Title = "截图助手配置错误";
        _configErrorBar.Message = message;
        _configErrorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"截图助手页缺少 {name}。");
}
