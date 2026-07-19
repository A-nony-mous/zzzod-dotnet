using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 随便观应用设置页 AXAML 和 BaselineParity 条件显示审计。
/// </summary>
public sealed class SuibianTempleAppSettingPageTests
{
	/// <summary>
	/// 页面应按 BaselineParity 顺序使用 Fluent 控件，并按自动托管隐藏手动经营项。
	/// </summary>
	[Fact]
	public void PageUsesAxamlFluentControlsAndPythonVisibilityRule()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzSuibianTempleAppSettingPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzSuibianTempleAppSettingPage.axaml.cs"));
		AssertOrder(text, "自动托管", "饮茶仙", "饮茶仙-委托刷新", "派遣-时长", "派遣-副本优先级", "制造坊-最大下拉次数", "好物铺购买", "邦巢-购买", "邦巢-最低购买价格");
		Assert.Contains("fa:FASettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("fa:FANumberBox", text, StringComparison.Ordinal);
		Assert.Contains("ToggleSwitch", text, StringComparison.Ordinal);
		Assert.Contains("UpdateManualVisibility(autoManage)", actualString, StringComparison.Ordinal);
		Assert.Contains("item.IsVisible = !autoManage", actualString, StringComparison.Ordinal);
		Assert.Contains("SuibianTempleAdventureMission.Options", actualString, StringComparison.Ordinal);
		Assert.Contains("SuibianTempleBangbooPrice.Options", actualString, StringComparison.Ordinal);
		Assert.Contains("SaveConfigScope", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
	}

	private static void AssertOrder(string text, params string[] markers)
	{
		int num = -1;
		foreach (string text2 in markers)
		{
			int num2 = text.IndexOf(text2, StringComparison.Ordinal);
			Assert.True(num2 > num, "未按顺序找到 " + text2 + "。");
			num = num2;
		}
	}

	private static string FindDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "ApplicationSettings";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到应用设置目录。");
	}
}
