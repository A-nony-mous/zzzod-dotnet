using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.Gui.PageModels.Devtools;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzScreenshotHelperSettingsViewModelTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal)
        {
            ["frequency_second"] = 0.25d,
            ["length_second"] = 2.5d,
            ["key_save"] = "f10",
            ["dodge_detect"] = true,
            ["screenshot_before_key"] = false,
            ["mini_map_angle_detect"] = true,
        };

        public List<(string Scope, int? InstanceIndex, string? GroupId)> GetRequests { get; } = [];

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        public string? SaveError { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IZzzAppBackend.GetCurrentInstance):
                    return ZzzBackendResult<ZzzInstanceDto>.Ok(new ZzzInstanceDto(7, "实例 07", true, "config/07"));
                case nameof(IZzzAppBackend.GetConfigScope):
                {
                    string scope = Assert.IsType<string>(args![0]);
                    int? instanceIndex = args[1] is int index ? index : null;
                    string? groupId = args[2] as string;
                    GetRequests.Add((scope, instanceIndex, groupId));
                    return ScopeResult(scope);
                }
                case nameof(IZzzAppBackend.SaveConfigScope) when args is [ZzzSaveConfigScopeRequest request]:
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

                    return ScopeResult(request.Scope);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> ScopeResult(string scope) =>
            ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(
                new ZzzConfigScopeDescriptorDto(scope, scope, true, true, true, []),
                7,
                ScreenshotHelperConstants.DefaultGroupId,
                new Dictionary<string, object?>(Values, StringComparer.Ordinal)));
    }

    [Fact]
    public void ReloadUsesCurrentInstanceAndDoesNotWrite()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzScreenshotHelperSettingsViewModel viewModel = new(backend);

        viewModel.OnPageShown();

        Assert.Equal(7, viewModel.ActiveInstanceIndex);
        Assert.Single(proxy.GetRequests);
        Assert.Equal(("screenshot-helper", 7, ScreenshotHelperConstants.DefaultGroupId), proxy.GetRequests[0]);
        Assert.Empty(proxy.SaveRequests);
        Assert.True(viewModel.ValuesAvailable);
        Assert.Equal(0.25d, viewModel.FrequencySecond);
        Assert.Equal(2.5d, viewModel.LengthSecond);
        Assert.Equal("F10", viewModel.KeySaveLabel);
        Assert.True(viewModel.DodgeDetect);
        Assert.False(viewModel.ScreenshotBeforeKey);
        Assert.True(viewModel.MiniMapAngleDetect);
    }

    [Fact]
    public void SixBoundPropertiesSaveSingleFieldsForCurrentInstance()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        ZzzScreenshotHelperSettingsViewModel viewModel = new(backend);
        viewModel.OnPageShown();

        viewModel.FrequencySecond = 0.5d;
        viewModel.LengthSecond = 5d;
        viewModel.KeySave = "f8";
        viewModel.DodgeDetect = false;
        viewModel.ScreenshotBeforeKey = true;
        viewModel.MiniMapAngleDetect = false;

        Assert.Equal(6, proxy.SaveRequests.Count);
        Assert.All(proxy.SaveRequests, request =>
        {
            Assert.Equal("screenshot-helper", request.Scope);
            Assert.Equal(7, request.InstanceIndex);
            Assert.Equal(ScreenshotHelperConstants.DefaultGroupId, request.GroupId);
            Assert.Single(request.Values);
        });
        Assert.Equal(
            [
                "frequency_second",
                "length_second",
                "key_save",
                "dodge_detect",
                "screenshot_before_key",
                "mini_map_angle_detect",
            ],
            proxy.SaveRequests.Select(request => request.Values.Keys.Single()));
    }

    [Fact]
    public void SaveFailureKeepsInputAndReportsBackendError()
    {
        (IZzzAppBackend backend, RecordingBackendProxy proxy) = CreateBackend();
        string? reportedError = null;
        ZzzScreenshotHelperSettingsViewModel viewModel = new(backend, error => reportedError = error);
        viewModel.OnPageShown();
        proxy.SaveError = "截图助手配置保存失败";

        viewModel.KeySave = "f8";

        Assert.Equal("f8", viewModel.KeySave);
        Assert.Equal("截图助手配置保存失败", viewModel.LastError);
        Assert.Equal("截图助手配置保存失败", reportedError);
    }

    private static (IZzzAppBackend Backend, RecordingBackendProxy Proxy) CreateBackend()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        return (backend, (RecordingBackendProxy)backend);
    }
}
