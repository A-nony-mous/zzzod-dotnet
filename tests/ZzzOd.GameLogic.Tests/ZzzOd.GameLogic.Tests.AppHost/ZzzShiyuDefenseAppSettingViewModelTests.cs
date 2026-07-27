using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzShiyuDefenseAppSettingViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> ShiyuValues { get; } = new()
        {
            ["team_list"] = new List<ShiyuDefenseTeamConfig>
            {
                new()
                {
                    TeamIndex = 1,
                    ForCritical = true,
                    WeaknessList = [DmgTypeEnum.ELECTRIC],
                },
            },
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope) && args is not null)
            {
                string scope = (string)args[0]!;
                IReadOnlyDictionary<string, object?> values = scope == "team"
                    ? new Dictionary<string, object?>
                    {
                        ["team_list"] = new List<PredefinedTeamInfo>
                        {
                            new() { Idx = 1, Name = "一队", AutoBattle = "全配队通用" },
                            new() { Idx = 2, Name = "二队", AutoBattle = "全配队通用" },
                        },
                    }
                    : ShiyuValues;
                return Snapshot(scope, args[1] as int?, args[2] as string, values);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                foreach ((string key, object? value) in request.Values)
                {
                    ShiyuValues[key] = value;
                }

                return Snapshot(request.Scope, request.InstanceIndex, request.GroupId, ShiyuValues);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.ResetShiyuDefenseRunRecord))
            {
                return ZzzBackendResult<ZzzShiyuDefenseRunRecordDto>.Ok(
                    new ZzzShiyuDefenseRunRecordDto((int)args![0]!, Array.Empty<int>()));
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
    public void LoadsRowsAndSavesRowChangesThroughConfigBinding()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzShiyuDefenseAppSettingViewModel viewModel =
            new(backend, 4, "one_dragon");

        viewModel.OnPageShown();

        ZzzShiyuDefenseTeamRowModel first = viewModel.Rows[0];
        Assert.Equal("一队", first.TeamName);
        Assert.True(first.ForCritical);
        Assert.True(first.Electric);

        first.ForCritical = false;
        first.Ice = true;

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.All(proxy.SaveRequests, request =>
        {
            Assert.Equal("shiyu-defense", request.Scope);
            Assert.Equal(4, request.InstanceIndex);
            Assert.Equal("one_dragon", request.GroupId);
            Assert.Contains("team_list", request.Values.Keys);
        });
    }

    [Fact]
    public void RelayCommandResetsRunRecord()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        ZzzShiyuDefenseAppSettingViewModel viewModel =
            new(backend, 2, "one_dragon");

        viewModel.ResetRunRecordForTest();

        Assert.Null(viewModel.LastError);
    }
}
