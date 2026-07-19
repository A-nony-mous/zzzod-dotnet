using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Pages.OneDragon;

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

    private readonly IZzzAppBackend _backend;
    private readonly ObservableCollection<ZzzPredefinedTeamRowModel> _rows = [];
    private readonly ZzzRunPanel _runPanel;
    private readonly FASettingsExpander _teamList;
    private readonly FAInfoBar _errorBar;
    private readonly Button _helpButton;
    private bool _loading;

    public FrontierPredefinedTeamPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _backend = backend;
        AvaloniaXamlLoader.Load(this);
        _teamList = Required<FASettingsExpander>("TeamList");
        _errorBar = Required<FAInfoBar>("TeamErrorBar");
        _helpButton = Required<Button>("HelpButton");
        _runPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.PredefinedTeamChecker,
            "预备编队检查",
            runIntent);
        Required<ContentControl>("RunPanelHost").Content = _runPanel;
        _teamList.ItemsSource = _rows;
        _helpButton.Click += OnHelpClicked;
    }

    internal IReadOnlyList<ZzzPredefinedTeamRowModel> Teams => _rows;

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
        _runPanel.DisposePage();
    }

    internal void Reload()
    {
        _loading = true;
        _errorBar.IsOpen = false;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> teamResult = _backend.GetConfigScope("team");
            if (!teamResult.Success
                || teamResult.Value is null
                || !teamResult.Value.Values.TryGetValue("team_list", out object? rawTeams)
                || rawTeams is not List<PredefinedTeamInfo> teams)
            {
                _rows.Clear();
                ShowError(teamResult.Error ?? "预备编队配置读取失败。");
                return;
            }

            ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> catalogResult = _backend.GetBattleAssistantConfigCatalog();
            if (!catalogResult.Success || catalogResult.Value is null)
            {
                _rows.Clear();
                ShowError(catalogResult.Error ?? "自动战斗配置读取失败。");
                return;
            }

            IReadOnlyList<ZzzPredefinedTeamOption> autoBattleOptions = catalogResult.Value.AutoBattle
                .Select(value => new ZzzPredefinedTeamOption(value, value))
                .ToArray();
            IReadOnlyList<ZzzPredefinedTeamOption> agentOptions = CreateAgentOptions();
            _rows.Clear();
            foreach (PredefinedTeamInfo team in teams)
            {
                team.EnsureThreeAgents();
                _rows.Add(new ZzzPredefinedTeamRowModel(team, autoBattleOptions, agentOptions));
            }
        }
        finally
        {
            _loading = false;
        }
    }

    internal static IReadOnlyList<ZzzPredefinedTeamOption> CreateAgentOptions() =>
        [
            new("代理人", "unknown"),
            .. AgentEnum.Values.Select(item => new ZzzPredefinedTeamOption(item.Value.AgentName, item.Value.AgentId)),
        ];

    internal static bool IsTeamNameWithinLimit(string value) =>
        value.Sum(character => character > 127 ? 2 : 1) <= 14;

    internal void SaveTeam(ZzzPredefinedTeamRowModel row) => Save(row);

    private void OnTeamNameChanged(object? sender, TextChangedEventArgs args)
    {
        if (_loading || sender is not TextBox { DataContext: ZzzPredefinedTeamRowModel row } textBox)
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
        if (_loading || sender is not Control { DataContext: ZzzPredefinedTeamRowModel row })
        {
            return;
        }

        SaveTeam(row);
    }

    private void Save(ZzzPredefinedTeamRowModel changedRow)
    {
        if (!changedRow.HasChanges)
        {
            return;
        }

        List<PredefinedTeamInfo> teams = _rows.Select(row => new PredefinedTeamInfo(
            row.Index,
            row.Name,
            row.AutoBattleValue,
            [
                row.Agent1Value,
                row.Agent2Value,
                row.Agent3Value,
            ])).ToList();

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "team",
            new Dictionary<string, object?> { ["team_list"] = teams }));
        if (!result.Success)
        {
            ShowError(result.Error ?? "预备编队配置保存失败。");
            return;
        }

        _errorBar.IsOpen = false;
        foreach (ZzzPredefinedTeamRowModel row in _rows)
        {
            row.MarkSaved();
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

    private void ShowError(string message)
    {
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"预备编队页缺少 {name}。");
}
