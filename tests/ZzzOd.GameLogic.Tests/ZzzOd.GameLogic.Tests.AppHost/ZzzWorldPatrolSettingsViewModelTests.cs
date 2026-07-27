using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.WorldPatrol;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzWorldPatrolSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["auto_battle"] = "全配队通用",
            ["route_list"] = "默认名单",
            ["ui_disappear_action"] = "restart_and_retry",
            ["route_retry_action"] = "retry_on_stuck_again",
            ["ui_disappear_seconds"] = 12,
            ["route_retry_times"] = 2,
            ["daily_loop_count"] = 3,
            ["loop_interval_seconds"] = 1800,
        };

        public List<(string Scope, int? InstanceIndex, string? GroupId)> GetRequests { get; } = [];

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetConfigScope):
                {
                    string scope = Assert.IsType<string>(args![0]);
                    int? instanceIndex = args[1] as int?;
                    string? groupId = args[2] as string;
                    GetRequests.Add((scope, instanceIndex, groupId));
                    return ScopeResult(scope, instanceIndex, groupId);
                }
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
                    SaveRequests.Add(request);
                    foreach ((string key, object? value) in request.Values)
                    {
                        Values[key] = value;
                    }

                    return ScopeResult(request.Scope, request.InstanceIndex, request.GroupId);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> ScopeResult(
            string scope,
            int? instanceIndex,
            string? groupId) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                new ZzzConfigScopeDescriptorDto(scope, scope, true, true, true, []),
                instanceIndex,
                groupId,
                new Dictionary<string, object?>(Values, StringComparer.Ordinal)));
    }

    [Fact]
    public void ReloadLoadsEightFieldsOnceWithoutWriting()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzWorldPatrolSettingsViewModel viewModel = new(backend, 2, "daily");

        viewModel.OnPageShown();

        Assert.Equal(("world-patrol", 2, "daily"), Assert.Single(proxy.GetRequests));
        Assert.Empty(proxy.SaveRequests);
        Assert.Equal("全配队通用", viewModel.AutoBattle);
        Assert.Equal("默认名单", viewModel.RouteList);
        Assert.Equal("restart_and_retry", viewModel.UiDisappearAction);
        Assert.Equal("retry_on_stuck_again", viewModel.RouteRetryAction);
        Assert.Equal(12d, viewModel.UiDisappearSeconds);
        Assert.Equal(2d, viewModel.RouteRetryTimes);
        Assert.Equal(3d, viewModel.DailyLoopCount);
        Assert.Equal(1800d, viewModel.LoopIntervalSeconds);
    }

    [Fact]
    public void EightBoundPropertiesSaveOneFieldWithInstanceAndGroup()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzWorldPatrolSettingsViewModel viewModel = new(backend, 2, "daily");
        viewModel.OnPageShown();

        viewModel.AutoBattle = "自动战斗B";
        viewModel.RouteList = "名单B";
        viewModel.UiDisappearAction = "silent_fail";
        viewModel.RouteRetryAction = "skip_on_stuck_again";
        viewModel.UiDisappearSeconds = 15d;
        viewModel.RouteRetryTimes = 4d;
        viewModel.DailyLoopCount = 5d;
        viewModel.LoopIntervalSeconds = 2400d;

        Assert.Equal(8, proxy.SaveRequests.Count);
        Assert.All(proxy.SaveRequests, request =>
        {
            Assert.Equal("world-patrol", request.Scope);
            Assert.Equal(2, request.InstanceIndex);
            Assert.Equal("daily", request.GroupId);
            Assert.Single(request.Values);
        });
        Assert.Equal(
            [
                "auto_battle",
                "route_list",
                "ui_disappear_action",
                "route_retry_action",
                "ui_disappear_seconds",
                "route_retry_times",
                "daily_loop_count",
                "loop_interval_seconds",
            ],
            proxy.SaveRequests.Select(request => request.Values.Keys.Single()));
        Assert.All(proxy.SaveRequests.Skip(4), request => Assert.IsType<int>(request.Values.Values.Single()));
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
