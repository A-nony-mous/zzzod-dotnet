using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed record ZzzSuibianOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed partial class FrontierSuibianTempleAppSettingPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzSuibianTempleAppSettingViewModel _viewModel;
    private readonly FAInfoBar _errorBar;

    public FrontierSuibianTempleAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _viewModel = new ZzzSuibianTempleAppSettingViewModel(
            backend,
            instanceIndex,
            groupId,
            ShowError);
        DataContext = _viewModel;
        _viewModel.OnPageShown();
    }

    public void OnPageShown() => _viewModel.OnPageShown();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage() => _viewModel.DisposePage();

    internal bool ManualSettingsVisible => _viewModel.ManualSettingsVisible;

    internal void SaveForTest(string key, object? value) => _viewModel.SaveForTest(key, value);

    private void ShowError(string? message)
    {
        if (message is null)
        {
            _errorBar.IsOpen = false;
            return;
        }

        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.Severity = FAInfoBarSeverity.Error;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"随便观设置缺少 {name}。");
}
