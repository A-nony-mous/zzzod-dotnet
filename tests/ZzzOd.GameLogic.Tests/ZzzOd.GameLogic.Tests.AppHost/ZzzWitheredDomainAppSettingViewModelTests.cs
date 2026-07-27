using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzWitheredDomainAppSettingViewModelTests
{
    public interface IWitheredTestBackend : IZzzAppBackend, IZzzWitheredDomainSettingsBackend
    {
    }

    public class RecordingBackendProxy : DispatchProxy
    {
        public static RecordingBackendProxy? Current { get; private set; }

        public RecordingBackendProxy()
        {
            Current = this;
        }

        public Dictionary<string, object?> Values { get; } = new()
        {
            ["mission_name"] = "任务一",
            ["challenge_config"] = "挑战一",
            ["weekly_plan_times"] = 4,
            ["daily_plan_times"] = 6,
            ["extra_task"] = "刷满周期奖励",
            ["extra_exit"] = "通关",
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public int ResetCalls { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetConfigScope):
                    return ConfigSnapshot((int?)args![1], (string?)args[2]);
                case nameof(IZzzAppBackend.SaveConfigScope):
                    return SaveConfig((ZzzSaveConfigScopeRequest)args![0]!);
                case nameof(IZzzWitheredDomainSettingsBackend.GetWitheredDomainSettingsCatalog):
                    return ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto>.Ok(CreateCatalog());
                case nameof(IZzzWitheredDomainSettingsBackend.ResetWitheredDomainRunRecord):
                    ResetCalls++;
                    return ZzzBackendResult<ZzzWitheredDomainRunRecordDto>.Ok(new(0, 0, 0, false, false));
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }

        private object SaveConfig(ZzzSaveConfigScopeRequest request)
        {
            SaveRequests.Add(request);
            foreach ((string key, object? value) in request.Values)
            {
                Values[key] = value;
            }

            return ConfigSnapshot(request.InstanceIndex, request.GroupId);
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> ConfigSnapshot(int? instanceIndex, string? groupId) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new(
                    new("withered-domain", "枯萎之都", false, false, true, Array.Empty<ZzzConfigSettingDescriptorDto>()),
                    instanceIndex,
                    groupId,
                    Values));

        private static ZzzWitheredDomainSettingsCatalogDto CreateCatalog() =>
            new(
                ["任务一", "任务二"],
                [new("挑战一", false, "自动战斗", [], [], [null, null, null], WitheredDomainPathFinding.Default, [], [], [], true)],
                ["自动战斗"],
                [new("艾莲", "ellen")],
                [new("默认", WitheredDomainPathFinding.Default)],
                [],
                [],
                [],
                new(0, 3, 2, false, false),
                "新配置");
    }

    [Fact]
    public void LoadsBaseFieldsCatalogAndResetCommand()
    {
        IWitheredTestBackend backend = DispatchProxy.Create<IWitheredTestBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = RecordingBackendProxy.Current ?? throw new InvalidOperationException("测试后端代理未创建。");
        ZzzWitheredDomainAppSettingViewModel viewModel = new(backend, 2, "default");

        viewModel.OnPageShown();

        Assert.Equal("任务一", viewModel.MissionName);
        Assert.Equal("挑战一", viewModel.ChallengeConfigName);
        Assert.Equal(4, viewModel.WeeklyPlanTimes);
        Assert.Equal(6, viewModel.DailyPlanTimes);
        Assert.Equal("刷满周期奖励", viewModel.ExtraTask);
        Assert.Equal("通关", viewModel.ExtraExit);
        Assert.Equal(2, viewModel.MissionOptions.Count);
        Assert.Single(viewModel.BaseChallengeOptions);

        viewModel.WeeklyPlanTimes = 5;
        viewModel.ResetRunRecordCommand.Execute(null);

        Assert.Single(proxy.SaveRequests);
        Assert.Equal("weekly_plan_times", Assert.Single(proxy.SaveRequests[0].Values).Key);
        Assert.Equal(2, proxy.SaveRequests[0].InstanceIndex);
        Assert.Equal("default", proxy.SaveRequests[0].GroupId);
        Assert.Equal(1, proxy.ResetCalls);
        Assert.Contains("本日: 0", viewModel.RunRecordDescription);
    }
}
