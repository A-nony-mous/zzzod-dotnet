using System.Globalization;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzLifeOnLineTeamOption(string Label, int Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzLifeOnLineSettingsFlyoutViewModel
{
    internal const string ScopeName = "life-on-line";

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;

    public ZzzLifeOnLineSettingsFlyoutViewModel(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    public int DailyPlanTimes { get; private set; }

    public int DailyRunTimes { get; private set; }

    public int PredefinedTeamIndex { get; private set; }

    public IReadOnlyList<ZzzLifeOnLineTeamOption> TeamOptions { get; private set; } = [];

    public string? Error { get; private set; }

    public bool Reload()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> config = _backend.GetConfigScope(
            ScopeName,
            _instanceIndex,
            _groupId);
        if (!config.Success || config.Value is null)
        {
            Error = config.Error ?? "生命热线配置读取失败。";
            return false;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> teamConfig = _backend.GetConfigScope(
            "team",
            _instanceIndex);
        if (!teamConfig.Success || teamConfig.Value is null)
        {
            Error = teamConfig.Error ?? "预备编队配置读取失败。";
            return false;
        }

        ZzzBackendResult<ZzzLifeOnLineRunRecordDto> runRecord =
            _backend.GetLifeOnLineRunRecord(_instanceIndex);
        if (!runRecord.Success || runRecord.Value is null)
        {
            Error = runRecord.Error ?? "生命热线运行记录读取失败。";
            return false;
        }

        try
        {
            if (!teamConfig.Value.Values.TryGetValue("team_list", out object? rawTeams)
                || rawTeams is not List<PredefinedTeamInfo> teams)
            {
                throw new InvalidOperationException("预备编队配置缺少 team_list?");
            }

            DailyPlanTimes = RequiredInt(config.Value.Values, "daily_plan_times");
            PredefinedTeamIndex = RequiredInt(config.Value.Values, "predefined_team_idx");
            DailyRunTimes = runRecord.Value.DailyRunTimes;
            TeamOptions =
            [
                new ZzzLifeOnLineTeamOption("游戏内配队", -1),
                .. teams.Select(team => new ZzzLifeOnLineTeamOption(team.Name, team.Idx)),
            ];
            Error = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            Error = exception.Message;
            return false;
        }
    }

    public bool Save(string key, object value)
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

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"生命热线配置缺少 {key}。");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}
