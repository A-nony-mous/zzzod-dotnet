using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

[Trait("Category", "Audit")]
public sealed class GuiDependencyBaselineTests
{
    [Fact]
    public void GuiAndGuiTestsUseSingleAvalonia12FluentAvalonia3Baseline()
    {
        string repoRoot = FindRepositoryRoot();
        string guiProject = Path.Combine(repoRoot, "src", "ZzzOd.Gui", "ZzzOd.Gui.csproj");
        string testProject = Path.Combine(repoRoot, "tests", "ZzzOd.GameLogic.Tests", "ZzzOd.GameLogic.Tests.csproj");

        AssertPackageVersion(guiProject, "Avalonia", "12.0.0");
        AssertPackageVersion(guiProject, "Avalonia.Desktop", "12.0.0");
        AssertPackageVersion(guiProject, "Avalonia.Markup.Xaml.Loader", "12.0.0");
        AssertPackageVersion(guiProject, "Avalonia.Controls.ColorPicker", "12.0.0");
        AssertPackageVersion(guiProject, "Avalonia.Controls.DataGrid", "12.0.0");
        AssertPackageVersion(guiProject, "FluentAvaloniaUI", "3.0.2");
        AssertPackageVersion(testProject, "Avalonia", "12.0.0");
        AssertPackageVersion(testProject, "Avalonia.Desktop", "12.0.0");
        AssertPackageVersion(testProject, "FluentAvaloniaUI", "3.0.2");

        AssertPackageMissing(guiProject, "HotAvalonia");
        AssertPackageMissing(guiProject, "LibVLCSharp.Avalonia");
        AssertPackageMissing(guiProject, "VideoLAN.LibVLC.Windows");
    }

    [Fact]
    public void RestoredGuiAssetsContainNoAvalonia11RuntimeOrRemovedShellPackages()
    {
        string repoRoot = FindRepositoryRoot();
        string[] assetsFiles =
        [
            Path.Combine(repoRoot, "src", "ZzzOd.Gui", "obj", "project.assets.json"),
            Path.Combine(repoRoot, "tests", "ZzzOd.GameLogic.Tests", "obj", "project.assets.json"),
        ];

        foreach (string assetsFile in assetsFiles)
        {
            Assert.True(File.Exists(assetsFile), $"缺少 restore 资产文件: {assetsFile}");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assetsFile));
            string[] libraries = document.RootElement.GetProperty("libraries")
                .EnumerateObject()
                .Select(property => property.Name)
                .ToArray();

            string[] forbidden = libraries
                .Where(name => IsForbiddenLibrary(name))
                .ToArray();
            Assert.Empty(forbidden);
            Assert.Contains("Tmds.DBus.Protocol/0.92.0", libraries);
        }
    }

    private static bool IsForbiddenLibrary(string name)
    {
        if (string.Equals(name, "Avalonia.BuildServices/11.3.2", StringComparison.Ordinal))
        {
            return false;
        }

        return (name.StartsWith("Avalonia", StringComparison.Ordinal) && name.Contains("/11.", StringComparison.Ordinal))
            || name.StartsWith("FluentAvaloniaUI/2.", StringComparison.Ordinal)
            || name.StartsWith("HotAvalonia", StringComparison.Ordinal)
            || name.StartsWith("LibVLCSharp.Avalonia/", StringComparison.Ordinal)
            || name.StartsWith("VideoLAN.LibVLC", StringComparison.Ordinal);
    }

    private static void AssertPackageVersion(string projectPath, string packageName, string expectedVersion)
    {
        XElement package = ReadPackages(projectPath)
            .Single(element => string.Equals((string?)element.Attribute("Include"), packageName, StringComparison.Ordinal));
        Assert.Equal(expectedVersion, (string?)package.Attribute("Version"));
    }

    private static void AssertPackageMissing(string projectPath, string packageName)
    {
        Assert.DoesNotContain(
            ReadPackages(projectPath),
            element => string.Equals((string?)element.Attribute("Include"), packageName, StringComparison.Ordinal));
    }

    private static IEnumerable<XElement> ReadPackages(string projectPath) =>
        XDocument.Load(projectPath).Descendants("PackageReference");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "ZzzOd.Gui", "ZzzOd.Gui.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("找不到 zzzod-dotnet 仓库根目录。");
    }
}
