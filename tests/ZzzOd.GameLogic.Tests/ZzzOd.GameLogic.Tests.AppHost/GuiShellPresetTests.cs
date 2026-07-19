using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Views;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class GuiShellPresetTests
{
    [Theory]
    [InlineData("classic", ZzzGuiShellPreset.Classic)]
    [InlineData("mixed", ZzzGuiShellPreset.Frontier)]
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

    [Theory]
    [InlineData(ZzzGuiShellPreset.Classic, typeof(MainWindow))]
    [InlineData(ZzzGuiShellPreset.Frontier, typeof(FrontierShellWindow))]
    public void ShellWindowFactory_MapsEachPresetToDedicatedWindow(ZzzGuiShellPreset preset, Type expected)
    {
        Assert.Equal(expected, ZzzShellWindowFactory.GetWindowType(preset));
    }

    [Fact]
    public void FrontierShellUsesSampleMaterialWithoutLegacyBackdropService()
    {
        string guiRoot = FindGuiRoot();
        string frontierCode = File.ReadAllText(Path.Combine(guiRoot, "Views", "FrontierShellWindow.cs"));
        string text = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(guiRoot, "Views", "MainWindow.axaml")),
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
