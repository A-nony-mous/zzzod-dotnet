using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Resources;
using ZzzOd.Gui.Views.FrontierPages.Settings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ResourceDownloadSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["ocr"] = "ppocrv5",
            ["ocr_use_gpu"] = true,
            ["flash_classifier"] = "yolov8n",
            ["flash_classifier_gpu"] = false,
            ["hollow_zero_event"] = "hollow-v2",
            ["hollow_zero_event_gpu"] = true,
            ["lost_void_det"] = "lost-v1",
            ["lost_void_det_gpu"] = false,
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public string? SaveError { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                return Snapshot();
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope)
                && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                if (SaveError is not null)
                {
                    return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(
                        ZzzBackendErrorCode.Validation,
                        SaveError);
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
            ZzzConfigScopeDescriptorDto descriptor = new(
                "model",
                "模型",
                false,
                false,
                true,
                Array.Empty<ZzzConfigSettingDescriptorDto>());
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                new ZzzConfigScopeValuesDto(
                    descriptor,
                    null,
                    null,
                    new Dictionary<string, object?>(Values, StringComparer.Ordinal)));
        }
    }

    private sealed class RecordingResourceService : IZzzResourceDownloadService
    {
        public event EventHandler<ZzzResourceDownloadStatusDto>? StatusChanged;

        public IReadOnlyList<ZzzResourceDownloadItemDto> Items { get; set; } = [];

        public IReadOnlyList<ZzzResourceDownloadItemDto> GetItems() => Items;

        public Task DownloadAsync(string resourceId, string modelId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public bool Cancel(string resourceId) => true;

        public void Publish(ZzzResourceDownloadStatusDto status) => StatusChanged?.Invoke(this, status);
    }

    [Fact]
    public void OnPageShownLoadsModelScopeAndResourceOptionCatalogs()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.Values["lost_void_det_gpu"] = true;
        RecordingResourceService service = CreateResourceService();
        ZzzResourceDownloadSettingsViewModel viewModel = new(backend, service);
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.OnPageShown();

        Assert.Equal("ppocrv5", viewModel.Ocr);
        Assert.True(viewModel.OcrUseGpu);
        Assert.Equal("yolov8n", viewModel.FlashClassifier);
        Assert.False(viewModel.FlashClassifierGpu);
        Assert.Equal("ppocrv5", viewModel.SelectedOcr?.Value);
        Assert.Equal(["ppocrv5", "ppocrv6"], viewModel.OcrOptions.Select(option => option.Value));
        Assert.Equal(["yolov8n", "yolov8x"], viewModel.FlashClassifierOptions.Select(option => option.Value));
        Assert.Contains(nameof(viewModel.OcrUseGpu), changedProperties);
        Assert.Contains(nameof(viewModel.LostVoidDetGpu), changedProperties);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void SelectedModelAndGpuChangesSaveThroughModelScope()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        RecordingResourceService service = CreateResourceService();
        ZzzResourceDownloadSettingsViewModel viewModel = new(backend, service);
        viewModel.OnPageShown();

        viewModel.SelectedOcr = viewModel.OcrOptions.Single(option => option.Value == "ppocrv6");
        viewModel.LostVoidDetGpu = true;

        Assert.Equal(2, proxy.SaveRequests.Count);
        Assert.Equal("model", proxy.SaveRequests[0].Scope);
        Assert.Equal("ppocrv6", proxy.SaveRequests[0].Values["ocr"]);
        Assert.Equal(true, proxy.SaveRequests[1].Values["lost_void_det_gpu"]);
    }

    [Fact]
    public void SaveFailureKeepsSelectedModelAndReportsError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.SaveError = "模型保存失败原文";
        RecordingResourceService service = CreateResourceService();
        List<string?> errors = [];
        ZzzResourceDownloadSettingsViewModel viewModel = new(backend, service, errors.Add);
        viewModel.OnPageShown();

        viewModel.SelectedFlashClassifier = viewModel.FlashClassifierOptions.Single(option => option.Value == "yolov8x");

        Assert.Equal("yolov8x", viewModel.FlashClassifier);
        Assert.Equal("模型保存失败原文", viewModel.LastError);
        Assert.Equal("模型保存失败原文", errors.Last());
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }

    private static RecordingResourceService CreateResourceService() => new()
    {
        Items =
        [
            CreateItem("ocr", "OCR识别", "ppocrv5", ("ppocrv5", "ppocrv5"), ("ppocrv6", "ppocrv6")),
            CreateItem("flash_classifier", "闪光识别", "yolov8n", ("yolov8n", "yolov8n"), ("yolov8x", "yolov8x")),
            CreateItem("hollow_zero_event", "空洞格子识别", "hollow-v2", ("hollow-v2", "hollow-v2")),
            CreateItem("lost_void_det", "迷失之地识别", "lost-v1", ("lost-v1", "lost-v1")),
        ],
    };

    private static ZzzResourceDownloadItemDto CreateItem(
        string resourceId,
        string title,
        string selectedModelId,
        params (string Label, string ModelId)[] options) =>
        new(
            resourceId,
            title,
            options.Select(option => new ZzzResourceModelOptionDto(option.Label, option.ModelId)).ToArray(),
            selectedModelId,
            false,
            new ZzzResourceDownloadStatusDto(
                resourceId,
                selectedModelId,
                false,
                false,
                false,
                null,
                "就绪"));
}
