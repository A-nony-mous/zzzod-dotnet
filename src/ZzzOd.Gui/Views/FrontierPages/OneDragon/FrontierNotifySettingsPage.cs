using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.OneDragon;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.OneDragon;

internal sealed partial class FrontierNotifySettingsPage : UserControl, IZzzPageLifecycle
{
    private readonly FAInfoBar _errorBar;
    private readonly ZzzNotifySettingsViewModel _viewModel;

    public FrontierNotifySettingsPage(IZzzAppBackend backend, int instanceIndex)
    {
        _viewModel = new ZzzNotifySettingsViewModel(backend, instanceIndex, ShowError);
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        DataContext = _viewModel;
    }

    public void OnPageShown() => _viewModel.OnPageShown();

    public void OnPageLeave()
    {
    }

    public void OnPageHidden()
    {
    }

    public void DisposePage() => _viewModel.DisposePage();

    internal static Dictionary<string, NotifyApplicationSetting> ReadApplications(IReadOnlyDictionary<string, object?> values) =>
        ZzzNotifySettingsViewModel.ReadApplications(values);

    private void OnApplicationModeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzNotifyAppRowModel row })
        {
            _viewModel.SaveApplicationMode(row);
        }
    }

    private void ShowError(string? message)
    {
        _errorBar.Message = message ?? string.Empty;
        _errorBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"通知设置页缺少 {name}。");
}
