using System.Globalization;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.RandomPlay;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzRandomPlaySettingOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzRandomPlaySettingsFlyoutViewModel
{
    internal const string ScopeName = "random-play";

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;

    public ZzzRandomPlaySettingsFlyoutViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        TransportPointOptions = RandomPlayTransportPoint.All
            .Select(point => new ZzzRandomPlaySettingOption(
                $"{point.AreaName} - {point.TransportPointName}",
                point.Value))
            .ToArray();
        AgentOptions =
        [
            new ZzzRandomPlaySettingOption(
                RandomPlayConstants.RandomAgentName,
                RandomPlayConstants.RandomAgentName),
            .. AgentEnum.Values.Select(agent => new ZzzRandomPlaySettingOption(
                agent.Value.AgentName,
                agent.Value.AgentName)),
        ];
    }

    public IReadOnlyList<ZzzRandomPlaySettingOption> TransportPointOptions { get; }

    public IReadOnlyList<ZzzRandomPlaySettingOption> AgentOptions { get; }

    public string TransportPoint { get; private set; } = string.Empty;

    public string AgentName1 { get; private set; } = string.Empty;

    public string AgentName2 { get; private set; } = string.Empty;

    public string? Error { get; private set; }

    public bool Reload()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(
            ScopeName,
            _instanceIndex,
            _groupId);
        if (!result.Success || result.Value is null)
        {
            Error = result.Error ?? "录像店营业配置读取失败。";
            return false;
        }

        try
        {
            IReadOnlyDictionary<string, object?> values = result.Value.Values;
            TransportPoint = RequiredString(values, "transport_point");
            AgentName1 = RequiredString(values, "agent_name_1");
            AgentName2 = RequiredString(values, "agent_name_2");
            Error = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            Error = exception.Message;
            return false;
        }
    }

    public bool Save(string key, string value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(
            new ZzzSaveConfigScopeRequest(
                ScopeName,
                new Dictionary<string, object?> { [key] = value },
                _instanceIndex,
                _groupId));
        if (!result.Success)
        {
            Error = result.Error ?? $"{key} 保存失败。";
            return false;
        }

        Error = null;
        return true;
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"录像店营业配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
