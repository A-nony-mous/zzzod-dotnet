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

namespace ZzzOd.Gui.Pages.OneDragon;

internal sealed record ZzzPredefinedTeamOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzPredefinedTeamRowModel : INotifyPropertyChanged
{
    private string _name;
    private ZzzPredefinedTeamOption? _selectedAutoBattle;
    private ZzzPredefinedTeamOption? _selectedAgent1;
    private ZzzPredefinedTeamOption? _selectedAgent2;
    private ZzzPredefinedTeamOption? _selectedAgent3;
    private readonly string _unlistedAutoBattle;
    private readonly string _unlistedAgent1;
    private readonly string _unlistedAgent2;
    private readonly string _unlistedAgent3;
    private string _savedName;
    private string _savedAutoBattle;
    private string _savedAgent1;
    private string _savedAgent2;
    private string _savedAgent3;

    public ZzzPredefinedTeamRowModel(
        PredefinedTeamInfo team,
        IReadOnlyList<ZzzPredefinedTeamOption> autoBattleOptions,
        IReadOnlyList<ZzzPredefinedTeamOption> agentOptions)
    {
        Index = team.Idx;
        _name = team.Name;
        AcceptedName = team.Name;
        AutoBattleOptions = autoBattleOptions;
        AgentOptions = agentOptions;
        _unlistedAutoBattle = team.AutoBattle;
        _unlistedAgent1 = team.AgentIdList.ElementAtOrDefault(0) ?? "unknown";
        _unlistedAgent2 = team.AgentIdList.ElementAtOrDefault(1) ?? "unknown";
        _unlistedAgent3 = team.AgentIdList.ElementAtOrDefault(2) ?? "unknown";
        _savedName = team.Name;
        _savedAutoBattle = team.AutoBattle;
        _savedAgent1 = _unlistedAgent1;
        _savedAgent2 = _unlistedAgent2;
        _savedAgent3 = _unlistedAgent3;
        _selectedAutoBattle = Find(autoBattleOptions, team.AutoBattle);
        _selectedAgent1 = Find(agentOptions, _unlistedAgent1);
        _selectedAgent2 = Find(agentOptions, _unlistedAgent2);
        _selectedAgent3 = Find(agentOptions, _unlistedAgent3);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string AcceptedName { get; set; }

    public IReadOnlyList<ZzzPredefinedTeamOption> AutoBattleOptions { get; }

    public IReadOnlyList<ZzzPredefinedTeamOption> AgentOptions { get; }

    public ZzzPredefinedTeamOption? SelectedAutoBattle
    {
        get => _selectedAutoBattle;
        set => SetField(ref _selectedAutoBattle, value);
    }

    public ZzzPredefinedTeamOption? SelectedAgent1
    {
        get => _selectedAgent1;
        set => SetField(ref _selectedAgent1, value);
    }

    public ZzzPredefinedTeamOption? SelectedAgent2
    {
        get => _selectedAgent2;
        set => SetField(ref _selectedAgent2, value);
    }

    public ZzzPredefinedTeamOption? SelectedAgent3
    {
        get => _selectedAgent3;
        set => SetField(ref _selectedAgent3, value);
    }

    public string AutoBattleValue => SelectedAutoBattle?.Value ?? _unlistedAutoBattle;

    public string Agent1Value => SelectedAgent1?.Value ?? _unlistedAgent1;

    public string Agent2Value => SelectedAgent2?.Value ?? _unlistedAgent2;

    public string Agent3Value => SelectedAgent3?.Value ?? _unlistedAgent3;

    public bool HasChanges => !string.Equals(Name, _savedName, StringComparison.Ordinal)
        || !string.Equals(AutoBattleValue, _savedAutoBattle, StringComparison.Ordinal)
        || !string.Equals(Agent1Value, _savedAgent1, StringComparison.Ordinal)
        || !string.Equals(Agent2Value, _savedAgent2, StringComparison.Ordinal)
        || !string.Equals(Agent3Value, _savedAgent3, StringComparison.Ordinal);

    public void MarkSaved()
    {
        _savedName = Name;
        _savedAutoBattle = AutoBattleValue;
        _savedAgent1 = Agent1Value;
        _savedAgent2 = Agent2Value;
        _savedAgent3 = Agent3Value;
    }

    private static ZzzPredefinedTeamOption? Find(IReadOnlyList<ZzzPredefinedTeamOption> options, string? value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

