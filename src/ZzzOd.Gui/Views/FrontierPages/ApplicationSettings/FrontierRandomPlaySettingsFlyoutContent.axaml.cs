using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed partial class FrontierRandomPlaySettingsFlyoutContent : UserControl, IZzzPageLifecycle
{
    private readonly ZzzRandomPlaySettingsFlyoutViewModel _viewModel;
    private readonly FAInfoBar _errorBar;
    private readonly FAComboBox _transportPointCombo;
    private readonly FAComboBox _agent1Combo;
    private readonly FAComboBox _agent2Combo;
    private bool _loading;

    public FrontierRandomPlaySettingsFlyoutContent(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        _viewModel = new ZzzRandomPlaySettingsFlyoutViewModel(backend, instanceIndex, groupId);
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _transportPointCombo = Required<FAComboBox>("TransportPointCombo");
        _agent1Combo = Required<FAComboBox>("Agent1Combo");
        _agent2Combo = Required<FAComboBox>("Agent2Combo");
        _transportPointCombo.ItemsSource = _viewModel.TransportPointOptions;
        _agent1Combo.ItemsSource = _viewModel.AgentOptions;
        _agent2Combo.ItemsSource = _viewModel.AgentOptions;
        Reload();
    }

    internal ZzzRandomPlaySettingsFlyoutViewModel ViewModel => _viewModel;

    public void OnPageShown() => Reload();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
    }

    internal bool SaveForTest(string key, string value) => Save(key, value);

    private void Reload()
    {
        _loading = true;
        try
        {
            if (!_viewModel.Reload())
            {
                ShowError(_viewModel.Error ?? "录像店营业配置读取失败。");
                return;
            }

            Select(_transportPointCombo, _viewModel.TransportPointOptions, _viewModel.TransportPoint);
            Select(_agent1Combo, _viewModel.AgentOptions, _viewModel.AgentName1);
            Select(_agent2Combo, _viewModel.AgentOptions, _viewModel.AgentName2);
            _errorBar.IsOpen = false;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnTransportPointChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _transportPointCombo.SelectedItem is ZzzRandomPlaySettingOption option)
        {
            Save("transport_point", option.Value);
        }
    }

    private void OnAgent1Changed(object? sender, SelectionChangedEventArgs args) =>
        SaveSelectedAgent(_agent1Combo, "agent_name_1");

    private void OnAgent2Changed(object? sender, SelectionChangedEventArgs args) =>
        SaveSelectedAgent(_agent2Combo, "agent_name_2");

    private void OnAgent1LostFocus(object? sender, RoutedEventArgs args) =>
        SaveAgentInput(_agent1Combo, "agent_name_1");

    private void OnAgent2LostFocus(object? sender, RoutedEventArgs args) =>
        SaveAgentInput(_agent2Combo, "agent_name_2");

    private void SaveSelectedAgent(FAComboBox comboBox, string key)
    {
        if (!_loading && comboBox.SelectedItem is ZzzRandomPlaySettingOption option)
        {
            comboBox.Text = option.Label;
            Save(key, option.Value);
        }
    }

    private void SaveAgentInput(FAComboBox comboBox, string key)
    {
        if (_loading)
        {
            return;
        }

        string text = (comboBox.Text ?? string.Empty).Trim();
        ZzzRandomPlaySettingOption? option = _viewModel.AgentOptions.FirstOrDefault(item =>
            string.Equals(item.Label, text, StringComparison.Ordinal)
            || string.Equals(item.Value, text, StringComparison.Ordinal));
        if (option is not null)
        {
            comboBox.SelectedItem = option;
            comboBox.Text = option.Label;
            Save(key, option.Value);
        }
    }

    private bool Save(string key, string value)
    {
        if (_viewModel.Save(key, value))
        {
            _errorBar.IsOpen = false;
            return true;
        }

        ShowError(_viewModel.Error ?? $"{key} 保存失败。");
        return false;
    }

    private void ShowError(string message)
    {
        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private static void Select(
        FAComboBox comboBox,
        IReadOnlyList<ZzzRandomPlaySettingOption> options,
        string value)
    {
        ZzzRandomPlaySettingOption? selected = options.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.Ordinal));
        comboBox.SelectedItem = selected;
        comboBox.Text = selected?.Label ?? string.Empty;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"录像店营业设置缺少 {name}。");
}
