using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.OneDragon;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzOneDragonRunSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> OneDragonValues { get; } = new(StringComparer.Ordinal)
        {
            ["instance_run"] = "仅运行当前",
            ["after_done"] = "无",
        };

        public Dictionary<string, object?> NotifyValues { get; } = new(StringComparer.Ordinal)
        {
            ["enable_notify"] = true,
            ["applications"] = new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal)
            {
                ["coffee"] = new()
                {
                    Lifecycle = "start_and_finish",
                    Detail = "all",
                },
            },
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public List<ZzzSaveOneDragonAppsRequest> SaveAppRequests { get; } = [];

        public Dictionary<string, int> LoadCounts { get; } = new(StringComparer.Ordinal);

        public string? SaveError { get; set; }

        public IReadOnlyList<ZzzOneDragonAppDto> Apps { get; private set; } =
        [
            new("coffee", "咖啡店", false, true, true, true, true, null, null),
            new("charge_plan", "体力刷本", true, true, true, true, true, null, null),
        ];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetCurrentInstance):
                    return ZzzBackendResult<ZzzInstanceDto>.Ok(new ZzzInstanceDto(2, "02", true, "config/02"));
                case nameof(IZzzAppBackend.GetConfigScope):
                {
                    string scope = Assert.IsType<string>(args![0]);
                    LoadCounts[scope] = LoadCounts.GetValueOrDefault(scope) + 1;
                    return Snapshot(scope);
                }
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
                    SaveRequests.Add(request);
                    if (SaveError is not null)
                    {
                        return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(
                            ZzzBackendErrorCode.Validation,
                            SaveError);
                    }

                    Dictionary<string, object?> values = Values(request.Scope);
                    foreach ((string key, object? value) in request.Values)
                    {
                        values[key] = value;
                    }

                    return Snapshot(request.Scope);
                case nameof(IZzzAppBackend.GetOneDragonApps):
                    return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Ok(Apps);
                case nameof(IZzzAppBackend.SaveOneDragonApps) when args is [ZzzSaveOneDragonAppsRequest request]:
                    SaveAppRequests.Add(request);
                    Dictionary<string, ZzzOneDragonAppDto> byId = Apps.ToDictionary(app => app.AppId, StringComparer.Ordinal);
                    Apps = request.Apps
                        .Where(update => byId.ContainsKey(update.AppId))
                        .Select(update => byId[update.AppId] with { Enabled = update.Enabled })
                        .ToArray();
                    return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Ok(Apps);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }

        private Dictionary<string, object?> Values(string scope) => scope switch
        {
            "one-dragon" => OneDragonValues,
            "notify" => NotifyValues,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "未知测试配置 scope。"),
        };

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(string scope)
        {
            ZzzConfigScopeDescriptorDto descriptor = new(scope, scope, scope == "notify", false, true, []);
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                descriptor,
                scope == "notify" ? 2 : null,
                null,
                new Dictionary<string, object?>(Values(scope), StringComparer.Ordinal)));
        }
    }

    [Fact]
    public void ReloadUsesOneReadPerSectionAndDoesNotWriteBack()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzOneDragonRunSettings settings = new(backend);

        settings.Reload();

        Assert.Equal(1, proxy.LoadCounts["one-dragon"]);
        Assert.Equal(1, proxy.LoadCounts["notify"]);
        Assert.Empty(proxy.SaveRequests);
        Assert.Equal(2, settings.InstanceIndex);
        Assert.Equal("仅运行当前", settings.InstanceRun);
        Assert.Equal("无", settings.AfterDone);
        Assert.True(settings.NotifyEnabled);
        Assert.True(settings.AppRows.Single(row => row.AppId == "coffee").NotifyEnabled);
    }

    [Fact]
    public void BoundPropertiesSaveTheirOwnFieldsOnce()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzOneDragonRunSettings settings = new(backend);
        settings.Reload();

        settings.InstanceRun = "全部实例";
        settings.AfterDone = "关机";
        settings.NotifyEnabled = false;

        Assert.Equal(3, proxy.SaveRequests.Count);
        Assert.Collection(
            proxy.SaveRequests,
            request =>
            {
                Assert.Equal("one-dragon", request.Scope);
                Assert.Equal("全部实例", Assert.Single(request.Values).Value);
            },
            request =>
            {
                Assert.Equal("one-dragon", request.Scope);
                Assert.Equal("关机", Assert.Single(request.Values).Value);
            },
            request =>
            {
                Assert.Equal("notify", request.Scope);
                Assert.Equal(2, request.InstanceIndex);
                Assert.False(Assert.IsType<bool>(Assert.Single(request.Values).Value));
            });
        Assert.False(settings.AppRows.Single(row => row.AppId == "coffee").NotifyEnabled);
    }

    [Fact]
    public void SaveFailureKeepsBoundValuesAndReportsTheBackendError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzOneDragonRunSettings settings = new(backend);
        settings.Reload();
        proxy.SaveError = "一条龙设置保存失败";

        settings.InstanceRun = "全部实例";
        settings.AfterDone = "关机";
        settings.NotifyEnabled = false;

        Assert.Equal("全部实例", settings.InstanceRun);
        Assert.Equal("关机", settings.AfterDone);
        Assert.False(settings.NotifyEnabled);
        Assert.Equal("一条龙设置保存失败", settings.LastError);
        Assert.Equal(3, proxy.SaveRequests.Count);
    }

    [Fact]
    public void AppNotificationUsesLoadedStateAndSavesApplicationsOnce()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzOneDragonRunSettings settings = new(backend);
        settings.Reload();

        Assert.True(settings.TryGetAppNotifyModes("coffee", out string lifecycle, out string detail));
        Assert.Equal("start_and_finish", lifecycle);
        Assert.Equal("all", detail);
        Assert.True(settings.SetAppNotifyModes("coffee", "finish_only", "merge"));

        Assert.Equal(1, proxy.LoadCounts["notify"]);
        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("notify", request.Scope);
        Assert.Equal(2, request.InstanceIndex);
        Dictionary<string, NotifyApplicationSetting> applications =
            Assert.IsType<Dictionary<string, NotifyApplicationSetting>>(request.Values["applications"]);
        Assert.Equal("finish_only", applications["coffee"].Lifecycle);
        Assert.Equal("merge", applications["coffee"].Detail);
    }

    [Fact]
    public void AppRowChangesKeepOrderAndActiveInstanceSaveSemantics()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzOneDragonRunSettings settings = new(backend);
        settings.Reload();

        settings.SetAppEnabled("coffee", true);
        settings.MoveApp("charge_plan", -1);

        Assert.Equal(2, proxy.SaveAppRequests.Count);
        Assert.All(proxy.SaveAppRequests, request => Assert.Equal(2, request.InstanceIndex));
        Assert.Equal(["charge_plan", "coffee"], proxy.SaveAppRequests[^1].Apps.Select(app => app.AppId));
        Assert.All(proxy.SaveAppRequests[^1].Apps, app => Assert.True(app.Enabled));
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
