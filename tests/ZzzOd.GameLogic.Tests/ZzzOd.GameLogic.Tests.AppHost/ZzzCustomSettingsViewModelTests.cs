using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.Settings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzCustomSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["ui_language"] = "zh",
            ["theme"] = "Dark",
            ["background_type"] = "static_background",
            ["close_window_action"] = "exit",
            ["custom_theme_color"] = true,
            ["custom_banner"] = false,
            ["global_theme_color"] = "71,104,179",
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public int LoadCount { get; private set; }

        public string? LoadError { get; set; }

        public string? SaveError { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                LoadCount++;
                return LoadError is null
                    ? Snapshot()
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

                return Snapshot();
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot()
        {
            ZzzConfigScopeDescriptorDto descriptor = new(
                "custom",
                "自定义",
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

    [Fact]
    public void ReloadLoadsAllDeclaredFieldsWithoutSaving()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCustomSettingsViewModel viewModel = new(backend);

        Assert.True(viewModel.Reload());

        Assert.Equal(1, proxy.LoadCount);
        Assert.Equal("zh", viewModel.SelectedLanguageValue);
        Assert.Equal("Dark", viewModel.SelectedThemeValue);
        Assert.Equal("static_background", viewModel.SelectedBackgroundTypeValue);
        Assert.Equal("exit", viewModel.SelectedCloseWindowActionValue);
        Assert.True(viewModel.CustomThemeColor);
        Assert.False(viewModel.CustomBanner);
        Assert.Equal("71,104,179", viewModel.GlobalThemeColor);
        Assert.False(viewModel.IsLoading);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void PropertyChangesUseBindingLayerAndKeepExistingEvents()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCustomSettingsViewModel viewModel = new(backend);
        string? restartLanguage = null;
        viewModel.RestartRequested += (_, language) => restartLanguage = language;
        viewModel.Reload();

        viewModel.SelectedLanguageValue = "en";
        viewModel.SelectedBackgroundTypeValue = "dynamic_background";
        viewModel.SelectedCloseWindowActionValue = "tray";
        viewModel.CustomBanner = true;

        Assert.Equal("en", restartLanguage);
        Assert.Equal(
            ["ui_language", "background_type", "close_window_action", "custom_banner"],
            proxy.SaveRequests.Select(request => Assert.Single(request.Values).Key).ToArray());
        Assert.All(proxy.SaveRequests, request => Assert.Equal("custom", request.Scope));
    }

    [Fact]
    public void SaveFailureKeepsInputAndReportsBackendError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCustomSettingsViewModel viewModel = new(backend);
        int restartCount = 0;
        viewModel.RestartRequested += (_, _) => restartCount++;
        viewModel.Reload();
        proxy.SaveError = "语言保存失败";

        viewModel.SelectedLanguageValue = "en";

        Assert.Equal("en", viewModel.SelectedLanguageValue);
        Assert.Equal("语言保存失败", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
        Assert.Equal(0, restartCount);
    }

    [Fact]
    public void ReloadFailureReturnsFalseWithoutThrowing()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.LoadError = "自定义配置不可用";
        ZzzCustomSettingsViewModel viewModel = new(backend);

        bool loaded = viewModel.Reload();

        Assert.False(loaded);
        Assert.Equal("自定义配置不可用", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public void ExplicitThemeColorAndBannerPersistenceKeepCompatibilityMethods()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzCustomSettingsViewModel viewModel = new(backend);
        viewModel.Reload();

        Assert.True(viewModel.SaveThemeColor("1,2,3"));
        Assert.True(viewModel.PersistCustomBanner());

        Assert.Equal("1,2,3", viewModel.GlobalThemeColor);
        Assert.Equal("global_theme_color", Assert.Single(proxy.SaveRequests[0].Values).Key);
        Assert.Equal("custom_banner", Assert.Single(proxy.SaveRequests[1].Values).Key);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
