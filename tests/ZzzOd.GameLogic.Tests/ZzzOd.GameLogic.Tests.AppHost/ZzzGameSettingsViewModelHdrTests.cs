using System.Reflection;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.Gui.PageModels.Settings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ZzzGameSettingsViewModelHdrTests
{
    public class RecordingBackendProxy : DispatchProxy
    {
        public string? RequestedScope { get; private set; }

        public int? RequestedInstanceIndex { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetCurrentInstance))
            {
                return ZzzBackendResult<ZzzInstanceDto>.Ok(new ZzzInstanceDto(3, "03", true, "config/03"));
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                RequestedScope = args![0] as string;
                RequestedInstanceIndex = args[1] as int?;
                ZzzConfigScopeDescriptorDto descriptor = new(
                    "instance",
                    "账户",
                    true,
                    false,
                    true,
                    Array.Empty<ZzzConfigSettingDescriptorDto>());
                return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                    new ZzzConfigScopeValuesDto(
                        descriptor,
                        RequestedInstanceIndex,
                        null,
                        new Dictionary<string, object?>
                        {
                            ["game_path"] = @"D:\Games\ZenlessZoneZero.exe",
                        }));
            }

            throw new NotSupportedException(targetMethod.Name);
        }
    }

    private sealed class AvailableGamepadDependencyChecker : IVirtualGamepadDependencyChecker
    {
        public bool IsAvailable() => true;
    }

    [Fact]
    public void HdrGamePathReadsActiveInstanceThroughViewModel()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzGameSettingsViewModel viewModel = new(backend, new AvailableGamepadDependencyChecker());

        string gamePath = viewModel.GetGamePath();

        Assert.Equal(@"D:\Games\ZenlessZoneZero.exe", gamePath);
        Assert.Equal("instance", proxy.RequestedScope);
        Assert.Equal(3, proxy.RequestedInstanceIndex);
    }
}
