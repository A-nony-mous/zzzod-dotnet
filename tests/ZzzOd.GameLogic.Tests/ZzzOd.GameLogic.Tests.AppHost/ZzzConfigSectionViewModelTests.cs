using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzConfigSectionViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public int LoadCount { get; private set; }

        public string? LoadError { get; set; }

        public string? SaveError { get; set; }

        public bool ThrowOnLoad { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                LoadCount++;
                if (ThrowOnLoad)
                {
                    throw new InvalidOperationException("读取异常原文");
                }

                return LoadError is null
                    ? Snapshot((string)args![0]!, args[1] as int?, args[2] as string)
                    : ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.Validation, LoadError);
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
                new ZzzConfigScopeValuesDto(
                    descriptor,
                    instanceIndex,
                    groupId,
                    new Dictionary<string, object?>(Values, StringComparer.Ordinal)));
        }
    }

    private sealed class TestSectionViewModel : ZzzConfigSectionViewModel
    {
        private static readonly ZzzConfigField CountField = new("count", typeof(int), 7);
        private static readonly ZzzConfigField EnabledField = new("enabled", typeof(bool), false);
        private static readonly ZzzConfigField NameField = new(
            "display_name",
            typeof(string),
            "默认名称",
            value => value?.ToString()?.Trim(),
            value => value?.ToString()?.ToUpperInvariant());

        private static readonly IReadOnlyList<ZzzConfigField> FieldList =
        [
            CountField,
            EnabledField,
            NameField,
        ];

        public TestSectionViewModel(IZzzAppBackend backend, Action<string?>? errorReporter = null)
            : base(backend, errorReporter)
        {
        }

        protected override string ScopeName => "test-scope";

        protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

        protected override int? InstanceIndex => 4;

        protected override string? GroupId => "test-group";

        public int Count
        {
            get => GetValue<int>(CountField);
            set => SetValue(CountField, value);
        }

        public bool Enabled
        {
            get => GetValue<bool>(EnabledField);
            set => SetValue(EnabledField, value);
        }

        public string DisplayName
        {
            get => GetValue<string>(NameField);
            set => SetValue(NameField, value);
        }
    }

    [Fact]
    public void OnPageShownLoadsAllFieldsWithOneScopeRead()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.Values["count"] = "12";
        proxy.Values["enabled"] = true;
        proxy.Values["display_name"] = " 录像店 ";
        TestSectionViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Equal(1, proxy.LoadCount);
        Assert.Equal(12, viewModel.Count);
        Assert.True(viewModel.Enabled);
        Assert.Equal("录像店", viewModel.DisplayName);
    }

    [Fact]
    public void MissingKeysUseDeclaredDefaults()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        TestSectionViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Equal(7, viewModel.Count);
        Assert.False(viewModel.Enabled);
        Assert.Equal("默认名称", viewModel.DisplayName);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void PropertyChangeSavesOneFieldWithScopeIdentityAndConverter()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        TestSectionViewModel viewModel = new(backend);
        viewModel.OnPageShown();

        viewModel.DisplayName = "coffee";

        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("test-scope", request.Scope);
        Assert.Equal(4, request.InstanceIndex);
        Assert.Equal("test-group", request.GroupId);
        Assert.Equal("COFFEE", Assert.Single(request.Values).Value);
    }

    [Fact]
    public void SaveFailureKeepsInputAndReportsBackendError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.SaveError = "保存失败原文";
        List<string?> errors = [];
        TestSectionViewModel viewModel = new(backend, errors.Add);
        viewModel.OnPageShown();

        viewModel.Count = 18;

        Assert.Equal(18, viewModel.Count);
        Assert.Equal("保存失败原文", viewModel.LastError);
        Assert.Equal("保存失败原文", errors.Last());
    }

    [Fact]
    public void LoadFailureDoesNotEscapeLifecycleAndReportsBackendError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.LoadError = "读取失败原文";
        List<string?> errors = [];
        TestSectionViewModel viewModel = new(backend, errors.Add);

        Exception? exception = Record.Exception(viewModel.OnPageShown);

        Assert.Null(exception);
        Assert.Equal("读取失败原文", viewModel.LastError);
        Assert.Equal("读取失败原文", errors.Last());
    }

    [Fact]
    public void LoadingNeverWritesValuesBackAndThrownErrorsStayInsideLifecycle()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.Values["count"] = 9;
        TestSectionViewModel viewModel = new(backend);
        viewModel.OnPageShown();
        Assert.Empty(proxy.SaveRequests);

        proxy.ThrowOnLoad = true;
        Exception? exception = Record.Exception(viewModel.OnPageShown);

        Assert.Null(exception);
        Assert.Equal("读取异常原文", viewModel.LastError);
        Assert.Empty(proxy.SaveRequests);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
