using ZzzOd.Gui.Shell;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class GuiShellPresetTests
{
    [Theory]
    [InlineData("classic", ZzzGuiShellPreset.Classic)]
    [InlineData("mixed", ZzzGuiShellPreset.Mixed)]
    [InlineData("frontier", ZzzGuiShellPreset.Frontier)]
    [InlineData(" FRONTIER ", ZzzGuiShellPreset.Frontier)]
    public void TryParse_ValidConfiguredValue_ReturnsPreset(string value, ZzzGuiShellPreset expected)
    {
        bool success = ZzzGuiShellPresetService.TryParse(value, out ZzzGuiShellPreset actual);

        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FromValues_KeyMissing_UsesClassicPythonParityBaseline()
    {
        ZzzGuiShellPresetResolution resolution = ZzzGuiShellPresetResolution.FromValues(new Dictionary<string, object?>());

        Assert.True(resolution.Success);
        Assert.Equal(ZzzGuiShellPreset.Classic, resolution.Preset);
        Assert.Null(resolution.Error);
    }

    [Fact]
    public void FromValues_InvalidValue_ReturnsErrorWithoutChangingConfiguredValue()
    {
        ZzzGuiShellPresetResolution resolution = ZzzGuiShellPresetResolution.FromValues(
            new Dictionary<string, object?> { [ZzzGuiShellPresetService.ConfigKey] = "store-fluent" });

        Assert.False(resolution.Success);
        Assert.Equal(ZzzGuiShellPreset.Classic, resolution.Preset);
        Assert.Contains("gui_shell_preset", resolution.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ZzzGuiShellPreset.Classic, "classic")]
    [InlineData(ZzzGuiShellPreset.Mixed, "mixed")]
    [InlineData(ZzzGuiShellPreset.Frontier, "frontier")]
    public void ToConfigValue_MapsEachPresetToPersistentValue(ZzzGuiShellPreset preset, string expected)
    {
        Assert.Equal(expected, ZzzGuiShellPresetService.ToConfigValue(preset));
    }
}
