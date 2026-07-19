using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 开发工具容器的 BaselineParity 顺序、AXAML Fluent Pivot 和批准页面边界测试。
/// </summary>
public sealed class DevtoolsContainerParityTests
{
	private static readonly string[] ExpectedHeaders = new string[6] { "图像分析", "模板管理", "画面管理", "代理人模板生成", "截图助手", "指令调试" };

	/// <summary>
	/// 开发工具容器应使用 AXAML Fluent TabView 和六个 Frame，并保持 BaselineParity 子页顺序。
	/// </summary>
	[Fact]
	public void DevtoolsContainerUsesAxamlFluentPivotInPythonOrder()
	{
		string text = FindRepositoryRoot();
		string[] buffer = new string[5];
		buffer[0] = text;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "Devtools";
		string path = Path.Combine(buffer);
		string text2 = Path.Combine(path, "ZzzDevtoolsPage.axaml");
		string path2 = Path.Combine(path, "ZzzDevtoolsPage.cs");
		string path3 = Path.Combine(path, "ZzzDevtoolsShared.cs");
		XDocument xDocument = XDocument.Load(text2);
		XNamespace xNamespace = "using:FluentAvalonia.UI.Controls";
		string[] array = (from item in xDocument.Descendants(xNamespace + "FATabViewItem")
			select (string?)item.Attribute("Header") into header
			where header != null
			select header).Cast<string>().ToArray();
		string actualString = File.ReadAllText(text2);
		string actualString2 = File.ReadAllText(path2);
		string actualString3 = File.ReadAllText(path3);
		Assert.Equal<string[]>(ExpectedHeaders, array);
		Assert.Contains("<fa:FATabView", actualString, StringComparison.Ordinal);
		Assert.Equal(6, xDocument.Descendants(xNamespace + "FAFrame").Count());
		Assert.Contains("new ZzzImageAnalysisPage(backend, imageAnalysisService)", actualString2, StringComparison.Ordinal);
		Assert.Contains("new ZzzTemplateHelperAxamlPage(backend)", actualString2, StringComparison.Ordinal);
		Assert.Contains("new ZzzScreenManagePage(screenManageService)", actualString2, StringComparison.Ordinal);
		Assert.Contains("new ZzzAgentTemplateGeneratorPage(backend)", actualString2, StringComparison.Ordinal);
		Assert.Contains("new ZzzScreenshotHelperAxamlPage(backend, runIntent)", actualString2, StringComparison.Ordinal);
		Assert.Contains("new ZzzOperationDebugAxamlPage(backend, runIntent)", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzScreenshotHelperPage(backend, runIntent)", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzOperationDebugPage(backend, runIntent)", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("internal sealed class ZzzDevtoolsPage : ZzzPivotPage", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("class ZzzDiagnosticsPage", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain<string>("like", array, StringComparer.OrdinalIgnoreCase);
		Assert.DoesNotContain<string>("code-sync", array, StringComparer.OrdinalIgnoreCase);
		Assert.DoesNotContain<string>("PIP", array, StringComparer.OrdinalIgnoreCase);
		Assert.DoesNotContain<string>("diagnostics", array, StringComparer.OrdinalIgnoreCase);
	}

	private static string FindRepositoryRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "ZzzOneDragon.slnx")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "src", "ZzzOd.Gui")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzzod-dotnet 仓库根目录。");
	}
}
