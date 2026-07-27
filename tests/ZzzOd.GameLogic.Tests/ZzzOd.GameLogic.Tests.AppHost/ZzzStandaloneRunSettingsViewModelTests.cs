using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Views.FrontierPages.Standalone;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzStandaloneRunSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["app_list"] = new List<string> { "coffee", "charge_plan" },
            ["active_app_id"] = "coffee",
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public int LoadCount { get; private set; }

        public string? SaveError { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                LoadCount++;
                return Snapshot();
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                if (SaveError is not null)
                {
                    return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.Validation, SaveError);
                }

                foreach ((string key, object? value) in request.Values)
                {
                    Values[key] = value;
                }

                return Snapshot();
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot()
        {
            ZzzConfigScopeDescriptorDto descriptor = new("standalone-app", "独立运行", false, false, true, []);
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(
                    descriptor,
                    null,
                    null,
                    new Dictionary<string, object?>(Values, StringComparer.Ordinal)));
        }
    }

    [Fact]
    public void LoadAndNormalizeSelectionUseOneScopeRead()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzStandaloneRunSettingsViewModel viewModel = new(backend);

        viewModel.OnPageShown();
        string? selected = viewModel.NormalizeSelection(["coffee", "charge_plan"]);

        Assert.Equal(1, proxy.LoadCount);
        Assert.Equal(["coffee", "charge_plan"], viewModel.AppIds);
        Assert.Equal("coffee", selected);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void InvalidSelectionIsNormalizedAndSavedOnce()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.Values["active_app_id"] = "removed_app";
        ZzzStandaloneRunSettingsViewModel viewModel = new(backend);
        viewModel.OnPageShown();

        string? selected = viewModel.NormalizeSelection(["charge_plan"]);

        Assert.Equal("charge_plan", selected);
        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("charge_plan", Assert.Single(request.Values).Value);
    }

    [Fact]
    public void ListAndActiveSelectionPreserveExistingSaveShapes()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzStandaloneRunSettingsViewModel viewModel = new(backend);
        viewModel.OnPageShown();

        Assert.True(viewModel.SaveConfiguration(["charge_plan", "coffee"], "charge_plan"));
        Assert.True(viewModel.SaveActiveSelection("coffee"));

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.Equal(2, proxy.SaveRequests[0].Values.Count);
        Assert.Equal(["charge_plan", "coffee"], Assert.IsType<List<string>>(proxy.SaveRequests[0].Values["app_list"]));
        Assert.Equal("charge_plan", proxy.SaveRequests[0].Values["active_app_id"]);
        Assert.Equal("coffee", Assert.Single(proxy.SaveRequests[1].Values).Value);
    }

    [Fact]
    public void SaveFailureKeepsSelectionAndReportsError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzStandaloneRunSettingsViewModel viewModel = new(backend);
        viewModel.OnPageShown();
        proxy.SaveError = "独立运行配置保存失败";

        bool saved = viewModel.SaveActiveSelection("charge_plan");

        Assert.False(saved);
        Assert.Equal("charge_plan", viewModel.SelectedAppId);
        Assert.Equal("独立运行配置保存失败", viewModel.LastError);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
