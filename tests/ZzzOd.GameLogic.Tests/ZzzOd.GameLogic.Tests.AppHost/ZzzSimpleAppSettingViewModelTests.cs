using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzSimpleAppSettingViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, Dictionary<string, object?>> Scopes { get; } =
            new(StringComparer.Ordinal)
            {
                ["daily-signin"] = new(StringComparer.Ordinal)
                {
                    ["selected_sign"] = "trigrams_collection",
                },
                ["drive-disc-dismantle"] = new(StringComparer.Ordinal)
                {
                    ["dismantle_level"] = "S及以下",
                    ["dismantle_abandon"] = false,
                },
            };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope) && args is not null)
            {
                string scope = (string)args[0]!;
                return Snapshot(scope, args[1] as int?, args[2] as string);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                foreach ((string key, object? value) in request.Values)
                {
                    Scopes[request.Scope][key] = value;
                }

                return Snapshot(request.Scope, request.InstanceIndex, request.GroupId);
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(
            string scope,
            int? instanceIndex,
            string? groupId)
        {
            ZzzConfigScopeDescriptorDto descriptor = new(
                scope,
                scope,
                false,
                false,
                true,
                Array.Empty<ZzzConfigSettingDescriptorDto>());
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(descriptor, instanceIndex, groupId, Scopes[scope]));
        }
    }

    [Fact]
    public void DailySignInUsesCompiledSelectionAndPreservesSaveContract()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzDailySignInSettingsViewModel viewModel = new(backend, 2, "one_dragon");
        viewModel.OnPageShown();

        Assert.Equal("卦象集录", viewModel.SelectedShop?.Label);
        viewModel.SelectedShop = viewModel.ShopOptions.Single(option => option.Value == "scratch_card");

        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("daily-signin", request.Scope);
        Assert.Equal("scratch_card", request.Values["selected_sign"]);
        Assert.Equal(2, request.InstanceIndex);
        Assert.Equal("one_dragon", request.GroupId);
    }

    [Fact]
    public void DriveDiscUsesPythonDefaultsAndSavesBoundValues()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzDriveDiscDismantleSettingsViewModel viewModel = new(backend, 2, "one_dragon");
        viewModel.OnPageShown();

        Assert.Equal("S及以下", viewModel.SelectedLevel?.Value);
        Assert.False(viewModel.DismantleAbandon);
        viewModel.DismantleAbandon = true;

        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("drive-disc-dismantle", request.Scope);
        Assert.True((bool)request.Values["dismantle_abandon"]!);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
