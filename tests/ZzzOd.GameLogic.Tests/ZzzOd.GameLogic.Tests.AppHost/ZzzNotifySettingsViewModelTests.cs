using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.PageModels.OneDragon;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzNotifySettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetOneDragonApps):
                    return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Ok(
                    [
                        new ZzzOneDragonAppDto("coffee", "咖啡店", true, true, true, false, true, null, null),
                        new ZzzOneDragonAppDto("hidden", "隐藏", true, false, false, false, true, null, null),
                    ]);
                case nameof(IZzzAppBackend.GetConfigScope):
                    return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                        new ZzzConfigScopeDescriptorDto("notify", "通知", true, false, true, []),
                        0,
                        null,
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["merge_error_immediate_notify"] = true,
                            ["applications"] = new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal)
                            {
                                ["coffee"] = new() { Lifecycle = NotifyLifecycleModes.Off, Detail = NotifyDetailModes.ErrorOnly },
                            },
                        }));
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
                    SaveRequests.Add(request);
                    return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                        new ZzzConfigScopeDescriptorDto("notify", "通知", true, false, true, []),
                        0,
                        null,
                        new Dictionary<string, object?>(request.Values, StringComparer.Ordinal)));
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }

    [Fact]
    public void ReloadBuildsOnlyVisibleRowsAndDoesNotWrite()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzNotifySettingsViewModel viewModel = new(backend, 0);

        viewModel.OnPageShown();

        Assert.True(viewModel.ValuesAvailable);
        Assert.True(viewModel.MergeErrorImmediateNotify);
        Assert.Single(viewModel.Rows);
        Assert.Equal("coffee", viewModel.Rows[0].AppId);
        Assert.Equal(NotifyLifecycleModes.Off, viewModel.Rows[0].SelectedLifecycle?.Value);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void MergeAndApplicationChangesUseNotifyScope()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzNotifySettingsViewModel viewModel = new(backend, 0);
        viewModel.OnPageShown();

        viewModel.MergeErrorImmediateNotify = false;
        viewModel.Rows[0].SelectedDetail = new ZzzNotifyModeOption("逐条", NotifyDetailModes.All);
        Assert.True(viewModel.SaveApplicationMode(viewModel.Rows[0]));

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.All(proxy.SaveRequests, request =>
        {
            Assert.Equal("notify", request.Scope);
            Assert.Equal(0, request.InstanceIndex);
        });
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
