using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzLifeOnLineSettingsFlyoutViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new()
        {
            ["daily_plan_times"] = 30,
            ["predefined_team_idx"] = 2,
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                string scope = (string)args![0]!;
                IReadOnlyDictionary<string, object?> values = scope == "team"
                    ? new Dictionary<string, object?>
                    {
                        ["team_list"] = new List<PredefinedTeamInfo>
                        {
                            new(2, "二队", "全配队通用", []),
                        },
                    }
                    : Values;
                return Snapshot(scope, (int?)args[1], (string?)args[2], values);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.GetLifeOnLineRunRecord))
            {
                return ZzzBackendResult<ZzzLifeOnLineRunRecordDto>.Ok(new(2, 7));
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                foreach ((string key, object? value) in request.Values)
                {
                    Values[key] = value;
                }

                return Snapshot(request.Scope, request.InstanceIndex, request.GroupId, Values);
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private static ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(
            string scope,
            int? instanceIndex,
            string? groupId,
            IReadOnlyDictionary<string, object?> values) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new(
                    new(scope, scope, false, false, true, Array.Empty<ZzzConfigSettingDescriptorDto>()),
                    instanceIndex,
                    groupId,
                    values));
    }

    [Fact]
    public void LoadsConfigurationTeamsAndRunRecordThenSavesBindings()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzLifeOnLineSettingsFlyoutViewModel viewModel = new(backend, 2, "daily");

        viewModel.OnPageShown();

        Assert.Equal(30, viewModel.DailyPlanTimes);
        Assert.Equal(2, viewModel.PredefinedTeamIndex);
        Assert.Equal("二队", viewModel.SelectedTeam?.Label);
        Assert.Equal("当日: 7", viewModel.DoneText);

        viewModel.DailyPlanTimes = 31;
        viewModel.SelectedTeam = viewModel.TeamOptions[0];

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.Equal("daily_plan_times", Assert.Single(proxy.SaveRequests[0].Values).Key);
        Assert.Equal("predefined_team_idx", Assert.Single(proxy.SaveRequests[1].Values).Key);
        Assert.All(proxy.SaveRequests, request =>
        {
            Assert.Equal(2, request.InstanceIndex);
            Assert.Equal("daily", request.GroupId);
        });
    }
}
