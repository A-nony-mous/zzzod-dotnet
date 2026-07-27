using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzSuibianTempleAppSettingViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new()
        {
            ["auto_manage_enabled"] = true,
            ["yum_cha_sin"] = true,
            ["yum_cha_sin_period_refresh"] = false,
            ["adventure_duration"] = "HOUR_20",
            ["adventure_mission_1"] = "RESEARCH_3_4",
            ["adventure_mission_2"] = "RESEARCH_2_4",
            ["adventure_mission_3"] = "RESEARCH_1_4",
            ["adventure_mission_4"] = "COMMUNITY_3_4",
            ["craft_drag_times"] = 10,
            ["good_goods_purchase_enabled"] = false,
            ["boo_box_purchase_enabled"] = false,
            ["boo_box_adventure_price"] = "S4",
            ["boo_box_craft_price"] = "S4",
            ["boo_box_sell_price"] = "S4",
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                return Snapshot((int?)args![1], (string?)args[2], Values);
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
                "suibian-temple",
                "随便观",
                false,
                false,
                true,
                Array.Empty<ZzzConfigSettingDescriptorDto>());
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(descriptor, instanceIndex, groupId, values));
        }
    }

    [Fact]
    public void LoadsBindingsAndSavesScalarChanges()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzSuibianTempleAppSettingViewModel viewModel =
            new(backend, 1, "one_dragon");

        viewModel.OnPageShown();

        Assert.True(viewModel.AutoManageEnabled);
        Assert.False(viewModel.ManualSettingsVisible);
        Assert.Equal(10, viewModel.CraftDragTimes);

        viewModel.AutoManageEnabled = false;
        viewModel.CraftDragTimes = 12;
        viewModel.SelectedAdventureDuration = viewModel.AdventureDurationOptions[0];

        Assert.True(viewModel.ManualSettingsVisible);
        Assert.Equal(3, proxy.SaveRequests.Count);
        Assert.Equal("suibian-temple", proxy.SaveRequests[0].Scope);
        Assert.Equal(1, proxy.SaveRequests[0].InstanceIndex);
        Assert.Equal("one_dragon", proxy.SaveRequests[0].GroupId);
    }
}
