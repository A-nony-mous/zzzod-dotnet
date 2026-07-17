using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// 独立运行页的 BaselineParity parity 静态合同。
/// </summary>
public sealed class StandaloneRunPageAuditTests
{
	private static readonly string RepoRoot = FindRepoRoot();

	private static readonly string PageDirectory;

	/// <summary>
	/// 页面主体应由 AXAML Fluent 控件声明，并保持 BaselineParity 左右分栏和真实空态。
	/// </summary>
	[Fact]
	public void StandaloneRunPageUsesAxamlFluentSplitLayoutWithoutProductPlaceholders()
	{
		string text = Path.Combine(PageDirectory, "ZzzStandaloneAppRunPage.axaml");
		string path = Path.Combine(PageDirectory, "ZzzStandaloneAppRunPage.cs");
		XDocument xDocument = XDocument.Load(text);
		string actualString = File.ReadAllText(text);
		string actualString2 = File.ReadAllText(path);
		Assert.Equal("UserControl", xDocument.Root?.Name.LocalName);
		Assert.Equal("Grid", xDocument.Root?.Elements().Single().Name.LocalName);
		Assert.Contains("SettingsExpanderItem", actualString, StringComparison.Ordinal);
		Assert.Contains("ItemsControl", actualString, StringComparison.Ordinal);
		Assert.Contains("ContentControl x:Name=\"RunHost\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"添加应用\"", actualString, StringComparison.Ordinal);
		Assert.Contains("应用运行说明", actualString, StringComparison.Ordinal);
		Assert.Contains("从应用列表中选择单个功能模块独立运行，无需跑完整的一条龙流程", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("暂无应用", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("设置入口已选择", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("暂无可配置项", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new Border", actualString2, StringComparison.Ordinal);
	}

	/// <summary>
	/// 页面应使用独立运行配置、默认组注册和真实排序行为。
	/// </summary>
	[Fact]
	public void StandaloneRunPageBindsPythonConfigKeysAndRealDefaultGroupApps()
	{
		string actualString = File.ReadAllText(Path.Combine(PageDirectory, "ZzzStandaloneAppRunPage.cs"));
		Assert.Contains("app.DefaultGroup", actualString, StringComparison.Ordinal);
		Assert.Contains("\"standalone-app\"", actualString, StringComparison.Ordinal);
		Assert.Contains("[\"app_list\"]", actualString, StringComparison.Ordinal);
		Assert.Contains("[\"active_app_id\"]", actualString, StringComparison.Ordinal);
		Assert.Contains("ZOneDragonAppConstants.DefaultGroupId", actualString, StringComparison.Ordinal);
		Assert.Contains("GetStandaloneApps", actualString, StringComparison.Ordinal);
		Assert.Contains("DoDragDropAsync", actualString, StringComparison.Ordinal);
		Assert.Contains("< 10", actualString, StringComparison.Ordinal);
		Assert.Contains("RemoveApp", actualString, StringComparison.Ordinal);
		Assert.Contains("SelectApp", actualString, StringComparison.Ordinal);
		Assert.Contains("SaveActiveSelection", actualString, StringComparison.Ordinal);
		Assert.Contains("ZzzAppSettingProviderRegistry.TryGetImplemented", actualString, StringComparison.Ordinal);
	}

	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "ZzzOneDragon.slnx")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("找不到 zzzod-dotnet 仓库根目录。");
	}

	static StandaloneRunPageAuditTests()
	{
		string[] buffer = new string[5];
		buffer[0] = RepoRoot;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "Standalone";
		PageDirectory = Path.Combine(buffer);
	}
}
