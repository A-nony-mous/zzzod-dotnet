using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzRandomPlaySettingsFlyoutViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new()
        {
            ["transport_point"] = "柜台",
            ["agent_name_1"] = "安比",
            ["agent_name_2"] = "随机",
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                return Snapshot((int?)args![1], (string?)args[2]);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                foreach ((string key, object? value) in request.Values)
                {
                    Values[key] = value;
                }

                return Snapshot(request.InstanceIndex, request.GroupId);
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(int? instanceIndex, string? groupId) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new(
                    new("random-play", "录像店营业", false, false, true, Array.Empty<ZzzConfigSettingDescriptorDto>()),
                    instanceIndex,
                    groupId,
                    Values));
    }

    [Fact]
    public void LoadsCatalogAndSavesBindingFields()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzRandomPlaySettingsFlyoutViewModel viewModel = new(backend, 5, "daily");

        viewModel.OnPageShown();

        Assert.Equal("柜台", viewModel.TransportPoint);
        Assert.Equal("安比", viewModel.AgentName1);
        Assert.Equal("随机", viewModel.AgentName2);
        Assert.Equal("安比", viewModel.SelectedAgent1?.Value);

        viewModel.TransportPoint = viewModel.TransportPointOptions[1].Value;
        Assert.True(viewModel.TrySetAgentInput(2, viewModel.AgentOptions[1].Label));

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.Equal("transport_point", Assert.Single(proxy.SaveRequests[0].Values).Key);
        Assert.Equal("agent_name_2", Assert.Single(proxy.SaveRequests[1].Values).Key);
        Assert.All(proxy.SaveRequests, request =>
        {
            Assert.Equal(5, request.InstanceIndex);
            Assert.Equal("daily", request.GroupId);
        });
    }
}
