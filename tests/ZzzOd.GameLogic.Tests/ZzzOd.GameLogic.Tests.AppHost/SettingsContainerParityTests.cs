using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 设置容器的 BaselineParity 顺序、AXAML Fluent Pivot 和排除边界测试。
/// </summary>
public sealed class SettingsContainerParityTests
{
	private static readonly string[] ExpectedHeaders = new string[6] { "游戏设置", "Overlay", "资源下载", "脚本环境", "通知设置", "自定义设置" };

	/// <summary>
	/// 设置容器应使用 AXAML Fluent TabView，并保持 BaselineParity 的六页顺序。
	/// </summary>
	[Fact]
	public void SettingsContainerUsesAxamlFluentPivotInPythonOrder()
	{
		string text = FindRepositoryRoot();
		string[] buffer = new string[5];
		buffer[0] = text;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "Settings";
		string path = Path.Combine(buffer);
		string text2 = Path.Combine(path, "ZzzSettingsPage.axaml");
		string path2 = Path.Combine(path, "ZzzSettingsPage.cs");
		string[] buffer2 = new string[5];
		buffer2[0] = text;
		buffer2[1] = "src";
		buffer2[2] = "ZzzOd.Gui";
		buffer2[3] = "Pages";
		buffer2[4] = "ZzzPageFactory.cs";
		string path3 = Path.Combine(buffer2);
		XDocument xDocument = XDocument.Load(text2);
		XNamespace xNamespace = "using:FluentAvalonia.UI.Controls";
		string[] actual = (from item in xDocument.Descendants(xNamespace + "TabViewItem")
			select (string?)item.Attribute("Header") into header
			where header != null
			select header).Cast<string>().ToArray();
		string actualString = File.ReadAllText(text2);
		string actualString2 = File.ReadAllText(path2);
		string text3 = File.ReadAllText(path3);
		Assert.Equal<string[]>(ExpectedHeaders, actual);
		Assert.Contains("<fa:TabView", actualString, StringComparison.Ordinal);
		Assert.Equal(6, xDocument.Descendants(xNamespace + "Frame").Count());
		Assert.Contains("new ZzzPushSettingsAxamlPage(backend, pushNotificationService, _operations)", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzPushSettingsPage(backend)", actualString2, StringComparison.Ordinal);
		Assert.Contains("_environmentRuntimeCoordinator", text3, StringComparison.Ordinal);
		Assert.Contains("new ZzzSettingsPage(", text3, StringComparison.Ordinal);
		int num = text3.IndexOf("CreateSettingsPage", StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzPivotPage", text3.Substring(num, text3.IndexOf("private Control CreateInstancesPage", StringComparison.Ordinal) - num), StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
	}

	/// <summary>
	/// 产品设置容器和页面工厂不得保留排除页面入口。
	/// </summary>
	[Fact]
	public void SettingsProductContainerOmitsExcludedPagesAndScreenshotKeys()
	{
		string text = FindRepositoryRoot();
		string[] buffer = new string[5];
		buffer[0] = text;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "Settings";
		string path = Path.Combine(buffer);
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzSettingsPage.axaml")) + File.ReadAllText(Path.Combine(path, "ZzzSettingsPage.cs"));
		string[] buffer2 = new string[5];
		buffer2[0] = text;
		buffer2[1] = "src";
		buffer2[2] = "ZzzOd.Gui";
		buffer2[3] = "Pages";
		buffer2[4] = "ZzzPageFactory.cs";
		string actualString2 = File.ReadAllText(Path.Combine(buffer2));
		Assert.DoesNotContain("settings-api", actualString, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("settings-app-config", actualString, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("CreateApiSettingsPage", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("settings-api", actualString2, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("settings-app-config", actualString2, StringComparison.OrdinalIgnoreCase);
	}

	private static string FindRepositoryRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "ZzzOneDragon.slnx")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzzod-dotnet 仓库根目录。");
	}
}
