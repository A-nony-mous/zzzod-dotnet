using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Views.FrontierPages.Settings;

namespace ZzzOd.GameLogic.Tests.AppHost;

[Collection("Settings environment variables")]
public sealed class ZzzEnvironmentSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["screenshot_method"] = "mss",
            ["is_debug"] = false,
            ["copy_screenshot"] = true,
            ["proxy_type"] = "None",
            ["personal_proxy"] = string.Empty,
            ["key_start_running"] = "f9",
            ["key_stop_running"] = "f10",
            ["key_screenshot"] = "f11",
            ["key_debug"] = "f12",
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public int LoadCount { get; private set; }

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
                "env",
                "脚本环境",
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

    private sealed class RecordingRuntimeCoordinator : IZzzEnvironmentRuntimeCoordinator
    {
        public int ConfigurationUpdateCount { get; private set; }

        public int ReinitializeCount { get; private set; }

        public Task<ZzzBackendResult<bool>> ReinitializeContextAsync(CancellationToken cancellationToken = default)
        {
            ReinitializeCount++;
            return Task.FromResult(ZzzBackendResult<bool>.Ok(true));
        }

        public IDisposable SuspendHotkeyActions() => new EmptyDisposable();

        public void UpdateEnvironmentConfiguration(ZzzConfigScopeValuesDto values) => ConfigurationUpdateCount++;

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public void OnPageShownLoadsEnvScopeOptionsAndRuntimeConfiguration()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        RecordingRuntimeCoordinator coordinator = new();
        ZzzEnvironmentSettingsViewModel viewModel = CreateViewModel(backend, coordinator);

        viewModel.OnPageShown();

        Assert.Equal(1, proxy.LoadCount);
        Assert.Equal("mss", viewModel.ScreenshotMethod);
        Assert.Equal("bitblt", viewModel.SelectedScreenshotMethod?.Value);
        Assert.Equal("None", viewModel.SelectedProxyType?.Value);
        Assert.False(viewModel.PersonalProxyVisible);
        Assert.Equal("f11", viewModel.GetHotkey("key_screenshot"));
        Assert.Equal(1, coordinator.ConfigurationUpdateCount);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void ProxyAndDebugChangesUseBindingLayerAndRefreshRuntimeState()
    {
        string? oldHttpProxy = Environment.GetEnvironmentVariable("HTTP_PROXY");
        string? oldHttpsProxy = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        try
        {
            (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
            RecordingRuntimeCoordinator coordinator = new();
            ZzzEnvironmentSettingsViewModel viewModel = CreateViewModel(backend, coordinator);
            viewModel.OnPageShown();

            Assert.True(viewModel.SaveString("personal_proxy", "http://127.0.0.1:8080"));
            Assert.True(viewModel.SaveString("proxy_type", "personal"));
            viewModel.IsDebug = true;

            Assert.True(viewModel.PersonalProxyVisible);
            Assert.Equal("http://127.0.0.1:8080", Environment.GetEnvironmentVariable("HTTP_PROXY"));
            Assert.Equal(3, proxy.SaveRequests.Count);
            Assert.All(proxy.SaveRequests, request => Assert.Equal("env", request.Scope));
            Assert.Equal(4, coordinator.ConfigurationUpdateCount);
            Assert.Equal(1, coordinator.ReinitializeCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HTTP_PROXY", oldHttpProxy);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", oldHttpsProxy);
        }
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }

    private static ZzzEnvironmentSettingsViewModel CreateViewModel(
        IZzzAppBackend backend,
        IZzzEnvironmentRuntimeCoordinator coordinator) =>
        new(
            backend,
            [
                new ZzzEnvironmentOption("自动", "auto"),
                new ZzzEnvironmentOption("BitBlt", "bitblt"),
                new ZzzEnvironmentOption("Print Window", "print_window"),
            ],
            [
                new ZzzEnvironmentOption("无", "None"),
                new ZzzEnvironmentOption("个人代理", "personal"),
            ],
            coordinator);
}
