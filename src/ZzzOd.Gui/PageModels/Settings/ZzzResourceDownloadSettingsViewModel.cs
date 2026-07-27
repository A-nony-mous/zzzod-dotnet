using System.Globalization;
using System.Text.Json;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Resources;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed class ZzzResourceDownloadSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField OcrField = new("ocr", typeof(string), string.Empty, ReadString);
    private static readonly ZzzConfigField OcrGpuField = new("ocr_use_gpu", typeof(bool), false, ReadBool);
    private static readonly ZzzConfigField FlashClassifierField = new("flash_classifier", typeof(string), string.Empty, ReadString);
    private static readonly ZzzConfigField FlashClassifierGpuField = new("flash_classifier_gpu", typeof(bool), false, ReadBool);
    private static readonly ZzzConfigField HollowZeroEventField = new("hollow_zero_event", typeof(string), string.Empty, ReadString);
    private static readonly ZzzConfigField HollowZeroEventGpuField = new("hollow_zero_event_gpu", typeof(bool), false, ReadBool);
    private static readonly ZzzConfigField LostVoidDetectorField = new("lost_void_det", typeof(string), string.Empty, ReadString);
    private static readonly ZzzConfigField LostVoidDetectorGpuField = new("lost_void_det_gpu", typeof(bool), false, ReadBool);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        OcrField, OcrGpuField, FlashClassifierField, FlashClassifierGpuField,
        HollowZeroEventField, HollowZeroEventGpuField, LostVoidDetectorField, LostVoidDetectorGpuField,
    ];

    private readonly IZzzResourceDownloadService _resourceService;
    private IReadOnlyList<ZzzResourceModelOption> _ocrOptions = [];
    private IReadOnlyList<ZzzResourceModelOption> _flashClassifierOptions = [];
    private IReadOnlyList<ZzzResourceModelOption> _hollowZeroEventOptions = [];
    private IReadOnlyList<ZzzResourceModelOption> _lostVoidDetectorOptions = [];

    public ZzzResourceDownloadSettingsViewModel(
        IZzzAppBackend backend,
        IZzzResourceDownloadService resourceService,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _resourceService = resourceService;
    }

    protected override string ScopeName => "model";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public IReadOnlyList<ZzzResourceModelOption> OcrOptions
    {
        get => _ocrOptions;
        private set => SetProperty(ref _ocrOptions, value);
    }

    public IReadOnlyList<ZzzResourceModelOption> FlashClassifierOptions
    {
        get => _flashClassifierOptions;
        private set => SetProperty(ref _flashClassifierOptions, value);
    }

    public IReadOnlyList<ZzzResourceModelOption> HollowZeroEventOptions
    {
        get => _hollowZeroEventOptions;
        private set => SetProperty(ref _hollowZeroEventOptions, value);
    }

    public IReadOnlyList<ZzzResourceModelOption> LostVoidDetectorOptions
    {
        get => _lostVoidDetectorOptions;
        private set => SetProperty(ref _lostVoidDetectorOptions, value);
    }

    public string Ocr
    {
        get => GetValue<string>(OcrField);
        set { if (SetValue(OcrField, value)) OnPropertyChanged(nameof(SelectedOcr)); }
    }

    public bool OcrUseGpu
    {
        get => GetValue<bool>(OcrGpuField);
        set => SetValue(OcrGpuField, value);
    }

    public string FlashClassifier
    {
        get => GetValue<string>(FlashClassifierField);
        set { if (SetValue(FlashClassifierField, value)) OnPropertyChanged(nameof(SelectedFlashClassifier)); }
    }

    public bool FlashClassifierGpu
    {
        get => GetValue<bool>(FlashClassifierGpuField);
        set => SetValue(FlashClassifierGpuField, value);
    }

    public string HollowZeroEvent
    {
        get => GetValue<string>(HollowZeroEventField);
        set { if (SetValue(HollowZeroEventField, value)) OnPropertyChanged(nameof(SelectedHollowZeroEvent)); }
    }

    public bool HollowZeroEventGpu
    {
        get => GetValue<bool>(HollowZeroEventGpuField);
        set => SetValue(HollowZeroEventGpuField, value);
    }

    public string LostVoidDet
    {
        get => GetValue<string>(LostVoidDetectorField);
        set { if (SetValue(LostVoidDetectorField, value)) OnPropertyChanged(nameof(SelectedLostVoidDetector)); }
    }

    public bool LostVoidDetGpu
    {
        get => GetValue<bool>(LostVoidDetectorGpuField);
        set => SetValue(LostVoidDetectorGpuField, value);
    }

    public ZzzResourceModelOption? SelectedOcr { get => Find(OcrOptions, Ocr); set { if (value is not null) Ocr = value.Value; } }
    public ZzzResourceModelOption? SelectedFlashClassifier { get => Find(FlashClassifierOptions, FlashClassifier); set { if (value is not null) FlashClassifier = value.Value; } }
    public ZzzResourceModelOption? SelectedHollowZeroEvent { get => Find(HollowZeroEventOptions, HollowZeroEvent); set { if (value is not null) HollowZeroEvent = value.Value; } }
    public ZzzResourceModelOption? SelectedLostVoidDetector { get => Find(LostVoidDetectorOptions, LostVoidDet); set { if (value is not null) LostVoidDet = value.Value; } }

    public override void OnPageShown()
    {
        base.OnPageShown();
        IReadOnlyList<ZzzResourceDownloadItemDto> resources = _resourceService.GetItems();
        foreach (ZzzResourceDownloadItemDto resource in resources)
        {
            IReadOnlyList<ZzzResourceModelOption> options = resource.Options
                .Select(option => new ZzzResourceModelOption(option.Label, option.ModelId))
                .ToArray();
            switch (resource.ResourceId)
            {
                case "ocr": OcrOptions = options; OnPropertyChanged(nameof(SelectedOcr)); break;
                case "flash_classifier": FlashClassifierOptions = options; OnPropertyChanged(nameof(SelectedFlashClassifier)); break;
                case "hollow_zero_event": HollowZeroEventOptions = options; OnPropertyChanged(nameof(SelectedHollowZeroEvent)); break;
                case "lost_void_det": LostVoidDetectorOptions = options; OnPropertyChanged(nameof(SelectedLostVoidDetector)); break;
            }
        }
    }

    private static ZzzResourceModelOption? Find(IReadOnlyList<ZzzResourceModelOption> options, string value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private static object? ReadString(object? value) => value is JsonElement element ? element.GetString() ?? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    private static object? ReadBool(object? value) => value is JsonElement element ? element.ValueKind == JsonValueKind.True : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
}
