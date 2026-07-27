using System.Reflection;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Views;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class GuiShellPresetTests
{
    public class PresetBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> CustomValues { get; } = new(StringComparer.Ordinal);

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope))
            {
                ZzzConfigScopeDescriptorDto descriptor = new("custom", "custom", false, false, true, Array.Empty<ZzzConfigSettingDescriptorDto>());
                return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(
                    new ZzzConfigScopeValuesDto(descriptor, null, null, new Dictionary<string, object?>(CustomValues, StringComparer.Ordinal)));
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope) && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(null!);
            }

            throw new NotSupportedException(targetMethod.Name);
        }
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("mixed")]
    public void Read_LegacyConfiguredValue_NormalizesToFrontierOnce(string legacy)
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, PresetBackendProxy>();
        PresetBackendProxy proxy = (PresetBackendProxy)backend;
        proxy.CustomValues[ZzzGuiShellPresetService.ConfigKey] = legacy;

        ZzzGuiShellPresetResolution resolution = new ZzzGuiShellPresetService(backend).Read();

        Assert.True(resolution.Success);
        Assert.Equal(ZzzGuiShellPreset.Frontier, resolution.Preset);
        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("custom", request.Scope);
        Assert.Equal("frontier", request.Values[ZzzGuiShellPresetService.ConfigKey]);
    }

    [Theory]
    [InlineData("frontier")]
    [InlineData(null)]
    public void Read_FrontierOrMissingValue_DoesNotWriteBack(string? configured)
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, PresetBackendProxy>();
        PresetBackendProxy proxy = (PresetBackendProxy)backend;
        if (configured is not null)
        {
            proxy.CustomValues[ZzzGuiShellPresetService.ConfigKey] = configured;
        }

        ZzzGuiShellPresetResolution resolution = new ZzzGuiShellPresetService(backend).Read();

        Assert.True(resolution.Success);
        Assert.Empty(proxy.SaveRequests);
    }

    [Fact]
    public void Read_UnregisteredValue_ReportsErrorAndKeepsConfiguredValue()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, PresetBackendProxy>();
        PresetBackendProxy proxy = (PresetBackendProxy)backend;
        proxy.CustomValues[ZzzGuiShellPresetService.ConfigKey] = "store-fluent";

        ZzzGuiShellPresetResolution resolution = new ZzzGuiShellPresetService(backend).Read();

        Assert.False(resolution.Success);
        Assert.Equal(ZzzGuiShellPreset.Frontier, resolution.Preset);
        Assert.Contains("store-fluent", resolution.Error, StringComparison.Ordinal);
        Assert.Empty(proxy.SaveRequests);
    }

    [Theory]
    [InlineData("classic", ZzzGuiShellPreset.Frontier)]
    [InlineData("mixed", ZzzGuiShellPreset.Frontier)]
    [InlineData("frontier", ZzzGuiShellPreset.Frontier)]
    [InlineData(" FRONTIER ", ZzzGuiShellPreset.Frontier)]
    public void TryParse_ValidConfiguredValue_ReturnsPreset(string value, ZzzGuiShellPreset expected)
    {
        bool success = ZzzGuiShellPresetService.TryParse(value, out ZzzGuiShellPreset actual);

        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("mixed")]
    public void FromValues_LegacyConfiguredValue_ResolvesFrontierWithoutError(string value)
    {
        ZzzGuiShellPresetResolution resolution = ZzzGuiShellPresetResolution.FromValues(
            new Dictionary<string, object?> { [ZzzGuiShellPresetService.ConfigKey] = value });

        Assert.True(resolution.Success);
        Assert.Equal(ZzzGuiShellPreset.Frontier, resolution.Preset);
        Assert.Null(resolution.Error);
    }

    [Fact]
    public void FromValues_KeyMissing_UsesFrontierDefault()
    {
        ZzzGuiShellPresetResolution resolution = ZzzGuiShellPresetResolution.FromValues(new Dictionary<string, object?>());

        Assert.True(resolution.Success);
        Assert.Equal(ZzzGuiShellPreset.Frontier, resolution.Preset);
        Assert.Null(resolution.Error);
    }

    [Fact]
    public void FromValues_InvalidValue_ReturnsErrorWithoutChangingConfiguredValue()
    {
        ZzzGuiShellPresetResolution resolution = ZzzGuiShellPresetResolution.FromValues(
            new Dictionary<string, object?> { [ZzzGuiShellPresetService.ConfigKey] = "store-fluent" });

        Assert.False(resolution.Success);
        Assert.Equal(ZzzGuiShellPreset.Frontier, resolution.Preset);
        Assert.Contains("gui_shell_preset", resolution.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ZzzGuiShellPreset.Frontier, "frontier")]
    public void ToConfigValue_MapsEachPresetToPersistentValue(ZzzGuiShellPreset preset, string expected)
    {
        Assert.Equal(expected, ZzzGuiShellPresetService.ToConfigValue(preset));
    }

    [Theory]
    [InlineData(false, "regular")]
    [InlineData(true, "selected")]
    public void NavigationIconConverter_UsesSelectionState(bool selected, string expected)
    {
        object? actual = ZzzNavigationIconConverter.Instance.Convert(["regular", "selected", selected], typeof(string), null, null!);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FrontierShellUsesSampleMaterialWithoutLegacyBackdropService()
    {
        string guiRoot = FindGuiRoot();
        string frontierCode = File.ReadAllText(Path.Combine(guiRoot, "Views", "FrontierShellWindow.cs"));
        string text = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(guiRoot, "Views", "FrontierShellWindow.axaml")),
            File.ReadAllText(Path.Combine(guiRoot, "Shell", "ZzzShellWindowRuntime.cs")));

        Assert.Contains("FAAppWindow", frontierCode, StringComparison.Ordinal);
        Assert.Contains("WindowTransparencyLevel.Mica", frontierCode, StringComparison.Ordinal);
        Assert.Contains("WindowTransparencyLevel.AcrylicBlur", frontierCode, StringComparison.Ordinal);
        Assert.Contains("TransparencyBackgroundFallback", frontierCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Mica", text, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(guiRoot, "Services", "Windows", "ZzzWindowBackdropService.cs")));
    }

    private static string FindGuiRoot()
    {
        for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "zzzod-dotnet", "src", "ZzzOd.Gui");
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        throw new DirectoryNotFoundException("未找到 ZzzOd.Gui 源码目录。");
    }
}
