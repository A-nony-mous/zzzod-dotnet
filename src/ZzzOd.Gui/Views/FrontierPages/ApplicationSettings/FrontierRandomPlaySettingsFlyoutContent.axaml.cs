using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.ApplicationSettings;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed partial class FrontierRandomPlaySettingsFlyoutContent : UserControl, IZzzPageLifecycle
{
    private readonly ZzzRandomPlaySettingsFlyoutViewModel _viewModel;
    private readonly FAInfoBar _errorBar;
    private readonly FAComboBox _agent1Combo;
    private readonly FAComboBox _agent2Combo;

    public FrontierRandomPlaySettingsFlyoutContent(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _agent1Combo = Required<FAComboBox>("Agent1Combo");
        _agent2Combo = Required<FAComboBox>("Agent2Combo");
        _viewModel = new ZzzRandomPlaySettingsFlyoutViewModel(
            backend,
            instanceIndex,
            groupId,
            ShowError);
        DataContext = _viewModel;
        _viewModel.OnPageShown();
    }

    internal ZzzRandomPlaySettingsFlyoutViewModel ViewModel => _viewModel;

    public void OnPageShown() => _viewModel.OnPageShown();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage() => _viewModel.DisposePage();

    internal bool SaveForTest(string key, string value) => _viewModel.SaveForTest(key, value);

    private void OnAgent1LostFocus(object? sender, RoutedEventArgs args) => SaveAgentInput(_agent1Combo, 1);

    private void OnAgent2LostFocus(object? sender, RoutedEventArgs args) => SaveAgentInput(_agent2Combo, 2);

    private void SaveAgentInput(FAComboBox comboBox, int slot)
    {
        _viewModel.TrySetAgentInput(slot, comboBox.Text ?? string.Empty);
    }

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
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"录像店营业设置缺少 {name}。");
}
