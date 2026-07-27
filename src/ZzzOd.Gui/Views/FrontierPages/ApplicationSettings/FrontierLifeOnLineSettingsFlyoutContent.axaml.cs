using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.ApplicationSettings;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed partial class FrontierLifeOnLineSettingsFlyoutContent : UserControl, IZzzPageLifecycle
{
    private readonly ZzzLifeOnLineSettingsFlyoutViewModel _viewModel;
    private readonly FAInfoBar _errorBar;

    public FrontierLifeOnLineSettingsFlyoutContent(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _viewModel = new ZzzLifeOnLineSettingsFlyoutViewModel(
            backend,
            instanceIndex,
            groupId,
            ShowError);
        DataContext = _viewModel;
        _viewModel.OnPageShown();
    }

    internal ZzzLifeOnLineSettingsFlyoutViewModel ViewModel => _viewModel;

    public void OnPageShown() => _viewModel.OnPageShown();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage() => _viewModel.DisposePage();

    internal bool SaveForTest(string key, object value) => _viewModel.SaveForTest(key, value);

    private void ShowError(string? message)
    {
        if (message is null)
        {
            _errorBar.IsOpen = false;
            return;
        }

        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"生命热线设置缺少 {name}。");
}
