using System.Xml.Linq;
using Xunit;
using ZzzOd.Gui.Shell;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 前卫容器 pivot 的 BaselineParity 顺序与排除页边界(承接被删 classic 容器 parity 测试的产品事实)。
/// 页签元素同时匹配 FATabViewItem 与原生 TabItem,pivot 控件选型由设计体系审计约束,本测试只锚定顺序。
/// </summary>
public sealed class FrontierContainerPivotParityTests
{
    private static readonly string[] ExpectedDevtoolsHeaders =
        ["图像分析", "模板管理", "画面管理", "代理人模板生成", "截图助手", "指令调试"];

    [Fact]
    public void DevtoolsContainerKeepsPythonPivotOrderAndExclusions()
    {
        string axaml = Path.Combine(FindGuiRoot(), "Views", "FrontierPages", "DevTools", "FrontierDevtoolsPage.axaml");
        string[] headers = ReadPivotHeaders(axaml);

        Assert.Equal(ExpectedDevtoolsHeaders, headers);
        Assert.DoesNotContain("like", headers, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("code-sync", headers, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("PIP", headers, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("diagnostics", headers, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsContainerKeepsApprovedTabOrderAndOmitsExcludedPages()
    {
        string guiRoot = FindGuiRoot();
        string axaml = Path.Combine(guiRoot, "Views", "FrontierPages", "Settings", "FrontierSettingsPage.axaml");
        string[] headers = ReadPivotHeaders(axaml);

        Assert.Equal(ZzzGuiParityRouteScope.ApprovedSettingsTabs, headers);

        string axamlText = File.ReadAllText(axaml);
        string codeBehind = File.ReadAllText(Path.Combine(guiRoot, "Views", "FrontierPages", "Settings", "FrontierSettingsPage.axaml.cs"));
        Assert.DoesNotContain("settings-api", axamlText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings-app-config", axamlText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings-api", codeBehind, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings-app-config", codeBehind, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ReadPivotHeaders(string axamlPath)
    {
        XDocument document = XDocument.Load(axamlPath);
        XNamespace fa = "using:FluentAvalonia.UI.Controls";
        XNamespace avalonia = "https://github.com/avaloniaui";
        return document.Descendants()
            .Where(element => element.Name == fa + "FATabViewItem" || element.Name == avalonia + "TabItem")
            .Select(element => (string?)element.Attribute("Header"))
            .Where(header => header is not null)
            .Cast<string>()
            .ToArray();
    }

    private static string FindGuiRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
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
