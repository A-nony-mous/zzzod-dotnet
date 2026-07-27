using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.Run;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzRunPanelHotkeyViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["key_start_running"] = "f9",
            ["key_stop_running"] = "f10",
        };

        public int GetCount { get; private set; }

        public string? Error { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name != nameof(IZzzAppBackend.GetConfigScope))
            {
                throw new NotSupportedException(targetMethod.Name);
            }

            Assert.Equal("env", Assert.IsType<string>(args![0]));
            GetCount++;
            return Error is null
                ? ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                    new ZzzConfigScopeDescriptorDto("env", "env", false, false, true, []),
                    null,
                    null,
                    new Dictionary<string, object?>(Values, StringComparer.Ordinal)))
                : ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.Validation, Error);
        }
    }

    [Fact]
    public void ReloadReadsAndNormalizesRunHotkeys()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzRunPanelHotkeyViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Equal(1, proxy.GetCount);
        Assert.Equal("F9", viewModel.StartHotkey);
        Assert.Equal("F10", viewModel.StopHotkey);
        Assert.Null(viewModel.LastError);
    }

    [Fact]
    public void ReloadFailureReportsErrorAndLeavesHotkeysEmpty()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        proxy.Error = "运行热键读取失败";
        string? reported = null;
        ZzzRunPanelHotkeyViewModel viewModel = new(backend, error => reported = error);

        viewModel.OnPageShown();

        Assert.Equal("运行热键读取失败", viewModel.LastError);
        Assert.Equal("运行热键读取失败", reported);
        Assert.Empty(viewModel.StartHotkey);
        Assert.Empty(viewModel.StopHotkey);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
