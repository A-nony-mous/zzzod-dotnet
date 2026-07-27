using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzNotoriousHuntAppSettingViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new()
        {
            ["weekly_challenge_start_weekday"] = 4,
            ["loop"] = false,
            ["plan_list"] = new List<ChargePlanItem>
            {
                new()
                {
                    TabName = "训练",
                    CategoryName = "恶名狩猎",
                    MissionTypeName = "初生死路屠夫",
                    Level = "等级Lv.65",
                    PredefinedTeamIndex = -1,
                    AutoBattleConfig = "全配队通用",
                    PlanTimes = 1,
                },
            },
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                return Snapshot((int?)args![1], (string?)args[2], Values);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.GetChargePlanCatalog))
            {
                ZzzChargePlanCatalogDto catalog = new(
                [
                    new ZzzChargePlanCategoryDto(
                        "恶名狩猎",
                        "恶名狩猎",
                        [new ZzzChargePlanMissionTypeDto("初生死路屠夫", "初生死路屠夫", [])]),
                ],
                [new ZzzChargePlanTeamDto(0, "一队")],
                ["全配队通用"]);
                return ZzzBackendResult<ZzzChargePlanCatalogDto>.Ok(catalog);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                foreach ((string key, object? value) in request.Values)
                {
                    Values[key] = value;
                }

                return Snapshot(request.InstanceIndex, request.GroupId, Values);
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private static ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(
            int? instanceIndex,
            string? groupId,
            IReadOnlyDictionary<string, object?> values)
        {
            ZzzConfigScopeDescriptorDto descriptor = new(
                "notorious-hunt",
                "恶名狩猎",
                false,
                false,
                true,
                Array.Empty<ZzzConfigSettingDescriptorDto>());
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(descriptor, instanceIndex, groupId, values));
        }
    }

    [Fact]
    public void LoadsCatalogAndSavesScalarAndPlanChanges()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzNotoriousHuntAppSettingViewModel viewModel =
            new(backend, 3, "weekly");

        viewModel.OnPageShown();

        Assert.Equal(4, viewModel.WeeklyChallengeStartWeekday);
        Assert.False(viewModel.Loop);
        Assert.Single(viewModel.Plans);
        Assert.Single(viewModel.CreateRows());

        viewModel.Loop = true;
        viewModel.UpdatePlan(0, plan => plan.PlanTimes = 3);

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.Equal("loop", Assert.Single(proxy.SaveRequests[0].Values).Key);
        Assert.Equal("plan_list", Assert.Single(proxy.SaveRequests[1].Values).Key);
        Assert.Equal(3, proxy.SaveRequests[1].InstanceIndex);
        Assert.Equal("weekly", proxy.SaveRequests[1].GroupId);
    }
}
