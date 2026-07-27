using System.Reflection;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Views.FrontierPages.Settings;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class OverlaySettingsViewModelTests
{
    [Fact]
    public void OnPageShownLoadsOverlayScopeOnceAndPublishesValues()
    {
        string root = CreateTempRoot();
        try
        {
            IZzzAppBackend backend = CreateBackend(new ZzzConfigScopeService(root), out OverlayBackendProxy proxy);
            ZzzOverlayController controller = new(new ZzzOverlayService(), backend);
            proxy.GetCount = 0;
            ZzzOverlaySettingsViewModel viewModel = new(backend, controller);

            viewModel.OnPageShown();

            Assert.Equal(1, proxy.GetCount);
            Assert.False(viewModel.Enabled);
            Assert.True(viewModel.Visible);
            Assert.Equal(12, viewModel.FontSize);
            Assert.Equal(70, viewModel.PanelOpacity);
            Assert.True(viewModel.OcrMetricEnabled);
            Assert.Null(viewModel.LastError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FontAndOpacityChangesUsePanelAppearanceAndApplyNormalizedSnapshot()
    {
        string root = CreateTempRoot();
        try
        {
            IZzzAppBackend backend = CreateBackend(new ZzzConfigScopeService(root), out OverlayBackendProxy proxy);
            ZzzOverlayController controller = new(new ZzzOverlayService(), backend);
            ZzzOverlaySettingsViewModel viewModel = new(backend, controller);
            viewModel.OnPageShown();

            viewModel.FontSize = 100;

            Assert.Equal(28, viewModel.FontSize);
            Assert.Single(proxy.SavedRequests);
            Assert.Contains("panel_appearance", proxy.SavedRequests[0].Values.Keys);
            Assert.Equal(28, controller.Settings.FontSize);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MetricChangeSavesTheMergedMetricMap()
    {
        string root = CreateTempRoot();
        try
        {
            IZzzAppBackend backend = CreateBackend(new ZzzConfigScopeService(root), out OverlayBackendProxy proxy);
            ZzzOverlayController controller = new(new ZzzOverlayService(), backend);
            ZzzOverlaySettingsViewModel viewModel = new(backend, controller);
            viewModel.OnPageShown();

            viewModel.OcrMetricEnabled = false;

            IReadOnlyDictionary<string, object?> values = proxy.SavedRequests.Single().Values;
            Dictionary<string, bool> metrics = Assert.IsType<Dictionary<string, bool>>(values["performance_metric_enabled_map"]);
            Assert.False(metrics["ocr_ms"]);
            Assert.True(metrics["yolo_ms"]);
            Assert.False(viewModel.OcrMetricEnabled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveFailureKeepsUserInputAndReportsBackendError()
    {
        string root = CreateTempRoot();
        try
        {
            IZzzAppBackend backend = CreateBackend(new ZzzConfigScopeService(root), out OverlayBackendProxy proxy);
            ZzzOverlayController controller = new(new ZzzOverlayService(), backend);
            string? error = null;
            ZzzOverlaySettingsViewModel viewModel = new(backend, controller, value => error = value);
            viewModel.OnPageShown();
            proxy.FailNextSave = true;

            viewModel.PanelTextColor = "invalid";

            Assert.Equal("invalid", viewModel.PanelTextColor);
            Assert.Equal("Overlay 保存失败。", error);
            Assert.Equal("Overlay 保存失败。", viewModel.LastError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResetGeometryUsesControllerAndPublishesCompletion()
    {
        string root = CreateTempRoot();
        try
        {
            IZzzAppBackend backend = CreateBackend(new ZzzConfigScopeService(root), out OverlayBackendProxy proxy);
            ZzzOverlayController controller = new(new ZzzOverlayService(), backend);
            ZzzOverlaySettingsViewModel viewModel = new(backend, controller);
            viewModel.OnPageShown();
            proxy.SavedRequests.Clear();
            bool completed = false;
            viewModel.GeometryReset += (_, _) => completed = true;

            viewModel.ResetPanelGeometryCommand.Execute(null);

            Assert.True(completed);
            Assert.Single(proxy.SavedRequests);
            Assert.Contains("panel_geometry", proxy.SavedRequests[0].Values.Keys);
            Assert.Null(viewModel.LastError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"zzz-overlay-vm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static IZzzAppBackend CreateBackend(
        ZzzConfigScopeService scopes,
        out OverlayBackendProxy proxy)
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, OverlayBackendProxy>();
        proxy = (OverlayBackendProxy)backend;
        proxy.Scopes = scopes;
        return backend;
    }

    private class OverlayBackendProxy : DispatchProxy
    {
        public ZzzConfigScopeService Scopes { get; set; } = null!;

        public int GetCount { get; set; }

        public bool FailNextSave { get; set; }

        public List<ZzzSaveConfigScopeRequest> SavedRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            args ??= [];
            return targetMethod.Name switch
            {
                "GetConfigScope" => Read(args),
                "SaveConfigScope" => Save((ZzzSaveConfigScopeRequest)args[0]),
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Read(object?[] args)
        {
            GetCount++;
            return Scopes.Read((string)args[0]!, (int?)args[1], (string?)args[2]);
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Save(ZzzSaveConfigScopeRequest request)
        {
            SavedRequests.Add(request);
            if (FailNextSave)
            {
                FailNextSave = false;
                return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(
                    ZzzBackendErrorCode.Validation,
                    "Overlay 保存失败。");
            }

            return Scopes.Save(request);
        }
    }
}
