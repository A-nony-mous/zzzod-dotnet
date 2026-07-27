using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.PageModels.OneDragon;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.OneDragon;

internal sealed partial class FrontierPredefinedTeamPage : UserControl, IZzzPageLifecycle
{
    private const string HelpContent = "▎编队名称\n\n"
        + "请确保编队名称与游戏内完全一致，顺序随意，\n"
        + "名称不匹配会导致无法识别。\n\n"
        + "不建议使用默认的数字命名编队，\n"
        + "OCR 识别数字容易出错，建议使用中文名称。\n\n"
        + "▎自动识别\n\n"
        + "点击「开始」后将自动打开游戏内预备编队页面，\n"
        + "通过截图识别各编队中的代理人并填入左侧配置。";

    private readonly ZzzPredefinedTeamSettingsViewModel _viewModel;
    private readonly ZzzRunPanel _runPanel;
    private readonly FAInfoBar _errorBar;
    private readonly Button _helpButton;

    public FrontierPredefinedTeamPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _viewModel = new ZzzPredefinedTeamSettingsViewModel(backend, ShowError);
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("TeamErrorBar");
        _helpButton = Required<Button>("HelpButton");
        _runPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.PredefinedTeamChecker,
            "预备编队检查",
            runIntent);
        Required<ContentControl>("RunPanelHost").Content = _runPanel;
        _helpButton.Click += OnHelpClicked;
        DataContext = _viewModel;
    }

    internal IReadOnlyList<ZzzPredefinedTeamRowModel> Teams => _viewModel.Rows;

    internal ZzzRunPanel RunPanel => _runPanel;

    public void OnPageShown()
    {
        Reload();
        _runPanel.OnPageShown();
    }

    public void OnPageHidden() => _runPanel.OnPageHidden();

    public void OnPageLeave() => _runPanel.OnPageLeave();

    public void DisposePage()
    {
        _helpButton.Click -= OnHelpClicked;
        _viewModel.DisposePage();
        _runPanel.DisposePage();
    }

    internal void Reload() => _viewModel.OnPageShown();

    internal static IReadOnlyList<ZzzPredefinedTeamOption> CreateAgentOptions() =>
        ZzzPredefinedTeamSettingsViewModel.CreateAgentOptions();

    internal static bool IsTeamNameWithinLimit(string value) =>
        ZzzPredefinedTeamSettingsViewModel.IsTeamNameWithinLimit(value);

    internal void SaveTeam(ZzzPredefinedTeamRowModel row) => _viewModel.SaveTeam(row);

    private void OnTeamNameChanged(object? sender, TextChangedEventArgs args)
    {
        if (sender is not TextBox { DataContext: ZzzPredefinedTeamRowModel row } textBox)
        {
            return;
        }

        string value = textBox.Text ?? string.Empty;
        if (!IsTeamNameWithinLimit(value))
        {
            row.Name = row.AcceptedName;
            textBox.Text = row.AcceptedName;
            textBox.CaretIndex = row.AcceptedName.Length;
            return;
        }

        row.AcceptedName = value;
        SaveTeam(row);
    }

    private void OnTeamSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzPredefinedTeamRowModel row })
        {
            SaveTeam(row);
        }
    }

    private async void OnHelpClicked(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            ShowError("当前窗口不可用。");
            return;
        }

        FAContentDialog dialog = new()
        {
            Title = "使用说明",
            Content = new TextBlock
            {
                Text = HelpContent,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 560,
            },
            PrimaryButtonText = "确认",
            DefaultButton = FAContentDialogButton.Primary,
        };
        await dialog.ShowAsync(owner).ConfigureAwait(true);
    }

    private void ShowError(string? message)
    {
        _errorBar.Message = message ?? string.Empty;
        _errorBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"预备编队页缺少 {name}。");
}
