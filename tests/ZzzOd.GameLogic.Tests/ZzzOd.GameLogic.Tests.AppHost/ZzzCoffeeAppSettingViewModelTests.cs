using System.Reflection;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.ApplicationSettings;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzCoffeeAppSettingViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> CoffeeValues { get; } = new(StringComparer.Ordinal)
        {
            ["transport_point"] = "澄辉坪 - 汀曼咖啡",
            ["choose_way"] = "优先体力计划",
            ["challenge_way"] = "只挑战体力计划",
            ["card_num"] = "1",
            ["predefined_team_idx"] = 2,
            ["auto_battle"] = "智能战斗",
            ["run_charge_plan_afterwards"] = true,
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope) && args is not null)
            {
                string scope = (string)args[0]!;
                IReadOnlyDictionary<string, object?> values = scope == "coffee"
                    ? CoffeeValues
                    : new Dictionary<string, object?>
                    {
                        ["team_list"] = new List<PredefinedTeamInfo>
                        {
                            new() { Idx = 1, Name = "一队" },
                            new() { Idx = 2, Name = "二队" },
                        },
                    };
                return Snapshot(scope, args[1] as int?, args[2] as string, values);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.GetBattleAssistantConfigCatalog))
            {
                return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(
                    new ZzzBattleAssistantConfigCatalogDto(["全配队通用", "智能战斗"], []));
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                foreach ((string key, object? value) in request.Values)
                {
                    CoffeeValues[key] = value;
                }

                return Snapshot(request.Scope, request.InstanceIndex, request.GroupId, CoffeeValues);
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private static ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(
            string scope,
            int? instanceIndex,
            string? groupId,
            IReadOnlyDictionary<string, object?> values)
        {
            ZzzConfigScopeDescriptorDto descriptor = new(
                scope,
                scope,
                false,
                false,
                true,
                Array.Empty<ZzzConfigSettingDescriptorDto>());
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(descriptor, instanceIndex, groupId, values));
        }
    }

    [Fact]
    public void CoffeeViewModelLoadsConfigAndAuxiliaryOptionsWithoutWritingBack()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCoffeeAppSettingViewModel viewModel = new(backend, 3, "one_dragon");

        viewModel.OnPageShown();

        Assert.Equal("澄辉坪 - 汀曼咖啡", viewModel.SelectedTransportPoint?.Value);
        Assert.Equal("只挑战体力计划", viewModel.SelectedChallengeWay?.Value);
        Assert.Equal("二队", viewModel.SelectedPredefinedTeam?.Label);
        Assert.True(viewModel.AutoBattleVisible is false);
        Assert.Equal("智能战斗", viewModel.SelectedAutoBattle?.Value);
        Assert.True(viewModel.RunChargePlanAfterwards);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void CoffeeViewModelSelectionAndToggleChangesUseBindingSavePath()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCoffeeAppSettingViewModel viewModel = new(backend, 3, "one_dragon");
        viewModel.OnPageShown();

        viewModel.SelectedChooseWay = viewModel.ChooseWayOptions[1];
        viewModel.RunChargePlanAfterwards = false;

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.Equal("choose_way", proxy.SaveRequests[0].Values.Keys.Single());
        Assert.Equal("汀曼特调", proxy.SaveRequests[0].Values["choose_way"]);
        Assert.Equal("run_charge_plan_afterwards", proxy.SaveRequests[1].Values.Keys.Single());
        Assert.False((bool)proxy.SaveRequests[1].Values["run_charge_plan_afterwards"]!);
    }

    [Fact]
    public void CoffeeTransportPointOptions_IncludeBuyastePointThree()
    {
        (IZzzAppBackend backend, _) = CreateBackend();
        ZzzCoffeeAppSettingViewModel viewModel = new(backend, 3, "one_dragon");

        Assert.Equal(3, viewModel.TransportPointOptions.Count);
        Assert.Equal("六分街 - 咖啡店", viewModel.TransportPointOptions[0].Value);
        Assert.Equal("澄辉坪 - 汀曼咖啡", viewModel.TransportPointOptions[1].Value);
        Assert.Equal("布亚斯特城区 - 片刻闲", viewModel.TransportPointOptions[2].Value);
    }

    [Fact]
    public void CoffeePageKeepsCompiledViewModelAndExistingControlNames()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();

        GuiParityAndFacadeTests.RunOnUiThread(() =>
        {
            FrontierCoffeeAppSettingPage page = new(backend, 3, "one_dragon");
            try
            {
                Assert.IsType<ZzzCoffeeAppSettingViewModel>(page.DataContext);
                Assert.NotNull(page.FindControl<FAComboBox>("TransportPointCombo"));
                Assert.NotNull(page.FindControl<FAComboBox>("ChooseWayCombo"));
                Assert.NotNull(page.FindControl<FAComboBox>("ChallengeWayCombo"));
                Assert.NotNull(page.FindControl<FAComboBox>("CardNumCombo"));
                Assert.NotNull(page.FindControl<FAComboBox>("PredefinedTeamCombo"));
                Assert.NotNull(page.FindControl<FAComboBox>("AutoBattleCombo"));
                Assert.NotNull(page.FindControl<ToggleSwitch>("RunChargePlanAfterwardsToggle"));
                Assert.Empty(proxy.SaveRequests);

                page.SaveForTest("card_num", "默认数量");
                Assert.Equal("默认数量", Assert.Single(proxy.SaveRequests).Values["card_num"]);
            }
            finally
            {
                page.DisposePage();
            }
        });
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
