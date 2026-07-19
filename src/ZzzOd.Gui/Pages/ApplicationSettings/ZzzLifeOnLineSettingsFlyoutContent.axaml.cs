using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed partial class ZzzLifeOnLineSettingsFlyoutContent : UserControl, IZzzPageLifecycle
{
    private readonly ZzzLifeOnLineSettingsFlyoutViewModel _viewModel;
    private readonly FAInfoBar _errorBar;
    private readonly FANumberBox _dailyPlanTimesNumber;
    private readonly TextBlock _doneValueText;
    private readonly FAComboBox _predefinedTeamCombo;
    private bool _loading;

    public ZzzLifeOnLineSettingsFlyoutContent(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        _viewModel = new ZzzLifeOnLineSettingsFlyoutViewModel(backend, instanceIndex, groupId);
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _dailyPlanTimesNumber = Required<FANumberBox>("DailyPlanTimesNumber");
        _doneValueText = Required<TextBlock>("DoneValueText");
        _predefinedTeamCombo = Required<FAComboBox>("PredefinedTeamCombo");
        Reload();
    }

    internal ZzzLifeOnLineSettingsFlyoutViewModel ViewModel => _viewModel;

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

    internal bool SaveForTest(string key, object value) => Save(key, value);

    private void Reload()
    {
        _loading = true;
        try
        {
            if (!_viewModel.Reload())
            {
                ShowError(_viewModel.Error ?? "生命热线设置读取失败。");
                return;
            }

            _dailyPlanTimesNumber.Value = _viewModel.DailyPlanTimes;
            _doneValueText.Text = $"当日: {_viewModel.DailyRunTimes}";
            _predefinedTeamCombo.ItemsSource = _viewModel.TeamOptions;
            _predefinedTeamCombo.SelectedItem = _viewModel.TeamOptions.FirstOrDefault(option =>
                option.Value == _viewModel.PredefinedTeamIndex);
            _errorBar.IsOpen = false;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnDailyPlanTimesChanged(FANumberBox sender, FANumberBoxValueChangedEventArgs args)
    {
        if (!_loading && !double.IsNaN(args.NewValue))
        {
            Save("daily_plan_times", Convert.ToInt32(args.NewValue));
        }
    }

    private void OnPredefinedTeamChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _predefinedTeamCombo.SelectedItem is ZzzLifeOnLineTeamOption option)
        {
            Save("predefined_team_idx", option.Value);
        }
    }

    private bool Save(string key, object value)
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

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"生命热线设置缺少 {name}。");
}
