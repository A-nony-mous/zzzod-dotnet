using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.RandomPlay;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzRandomPlaySettingOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzRandomPlaySettingsFlyoutViewModel : ZzzConfigSectionViewModel
{
    internal const string ScopeNameValue = "random-play";

    private static readonly ZzzConfigField TransportPointField =
        new("transport_point", typeof(string), RandomPlayTransportPoint.VideoStoreCounter.Value);
    private static readonly ZzzConfigField AgentName1Field =
        new("agent_name_1", typeof(string), RandomPlayConstants.RandomAgentName);
    private static readonly ZzzConfigField AgentName2Field =
        new("agent_name_2", typeof(string), RandomPlayConstants.RandomAgentName);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        TransportPointField,
        AgentName1Field,
        AgentName2Field,
    ];

    private readonly int _instanceIndex;
    private readonly string _groupId;

    public ZzzRandomPlaySettingsFlyoutViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        TransportPointOptions = RandomPlayTransportPoint.All
            .Select(point => new ZzzRandomPlaySettingOption(
                $"{point.AreaName} - {point.TransportPointName}",
                point.Value))
            .ToArray();
        AgentOptions =
        [
            new(RandomPlayConstants.RandomAgentName, RandomPlayConstants.RandomAgentName),
            .. AgentEnum.Values.Select(agent => new ZzzRandomPlaySettingOption(
                agent.Value.AgentName,
                agent.Value.AgentName)),
        ];
    }

    protected override string ScopeName => ScopeNameValue;

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public IReadOnlyList<ZzzRandomPlaySettingOption> TransportPointOptions { get; }

    public IReadOnlyList<ZzzRandomPlaySettingOption> AgentOptions { get; }

    public string TransportPoint
    {
        get => GetValue<string>(TransportPointField);
        set
        {
            if (SetValue(TransportPointField, value))
            {
                OnPropertyChanged(nameof(SelectedTransportPoint));
            }
        }
    }

    public string AgentName1
    {
        get => GetValue<string>(AgentName1Field);
        set
        {
            if (SetValue(AgentName1Field, value))
            {
                OnPropertyChanged(nameof(SelectedAgent1));
            }
        }
    }

    public string AgentName2
    {
        get => GetValue<string>(AgentName2Field);
        set
        {
            if (SetValue(AgentName2Field, value))
            {
                OnPropertyChanged(nameof(SelectedAgent2));
            }
        }
    }

    public ZzzRandomPlaySettingOption? SelectedTransportPoint
    {
        get => Find(TransportPointOptions, TransportPoint);
        set
        {
            if (value is not null)
            {
                TransportPoint = value.Value;
            }
        }
    }

    public ZzzRandomPlaySettingOption? SelectedAgent1
    {
        get => Find(AgentOptions, AgentName1);
        set
        {
            if (value is not null)
            {
                AgentName1 = value.Value;
            }
        }
    }

    public ZzzRandomPlaySettingOption? SelectedAgent2
    {
        get => Find(AgentOptions, AgentName2);
        set
        {
            if (value is not null)
            {
                AgentName2 = value.Value;
            }
        }
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        OnPropertyChanged(nameof(SelectedTransportPoint));
        OnPropertyChanged(nameof(SelectedAgent1));
        OnPropertyChanged(nameof(SelectedAgent2));
    }

    internal bool TrySetAgentInput(int slot, string text)
    {
        ZzzRandomPlaySettingOption? option = AgentOptions.FirstOrDefault(item =>
            string.Equals(item.Label, text.Trim(), StringComparison.Ordinal)
            || string.Equals(item.Value, text.Trim(), StringComparison.Ordinal));
        if (option is null)
        {
            return false;
        }

        if (slot == 1)
        {
            AgentName1 = option.Value;
        }
        else if (slot == 2)
        {
            AgentName2 = option.Value;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return LastError is null;
    }

    internal bool SaveForTest(string key, string value)
    {
        switch (key)
        {
            case "transport_point": TransportPoint = value; break;
            case "agent_name_1": AgentName1 = value; break;
            case "agent_name_2": AgentName2 = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(key), key, "未知的录像店营业配置字段。");
        }

        return LastError is null;
    }

    private static ZzzRandomPlaySettingOption? Find(
        IReadOnlyList<ZzzRandomPlaySettingOption> options,
        string value) => options.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.Ordinal));
}
