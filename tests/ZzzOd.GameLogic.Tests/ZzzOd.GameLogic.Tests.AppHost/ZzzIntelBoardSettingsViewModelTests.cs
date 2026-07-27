using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzIntelBoardSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["predefined_team_idx"] = -1,
            ["auto_battle_config"] = "智能战斗",
            ["exp_grind_mode"] = true,
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope) && args is not null)
            {
                string scope = (string)args[0]!;
                IReadOnlyDictionary<string, object?> values = scope == "intel-board"
                    ? Values
                    : new Dictionary<string, object?>
                    {
                        ["team_list"] = new List<PredefinedTeamInfo>
                        {
                            new() { Idx = 1, Name = "一队" },
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

    private sealed class ProgressBackend : IZzzIntelBoardProgressBackend
    {
        public int? InstanceIndex { get; private set; }

        public ZzzBackendResult<bool> ResetIntelBoardProgress(int? instanceIndex = null)
        {
            InstanceIndex = instanceIndex;
            return ZzzBackendResult<bool>.Ok(true);
        }
    }

    [Fact]
    public void IntelBoardLoadsConfigAndCatalogAndKeepsAutoBattleVisibilityInBinding()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzIntelBoardSettingsViewModel viewModel = new(backend, new ProgressBackend(), 3, "one_dragon");
        viewModel.OnPageShown();

        Assert.True(viewModel.ExpGrindMode);
        Assert.Equal("智能战斗", viewModel.SelectedAutoBattle?.Value);
        Assert.True(viewModel.AutoBattleVisible);
        Assert.Equal("一队", viewModel.PredefinedTeamOptions[1].Label);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void IntelBoardRelayCommandResetsProgressAndScalarBindingsSave()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ProgressBackend progress = new();
        ZzzIntelBoardSettingsViewModel viewModel = new(backend, progress, 3, "one_dragon");
        viewModel.OnPageShown();

        viewModel.ExpGrindMode = false;
        viewModel.ResetProgressForTest();

        Assert.False(viewModel.ExpGrindMode);
        Assert.Equal("已重置", viewModel.ResetButtonText);
        Assert.False(viewModel.ResetButtonEnabled);
        Assert.Equal(3, progress.InstanceIndex);
        Assert.Equal("exp_grind_mode", Assert.Single(proxy.SaveRequests).Values.Keys.Single());
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
