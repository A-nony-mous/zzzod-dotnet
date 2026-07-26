using System;
using System.IO;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// 账户页 AXAML 与 BaselineParity SettingInstanceInterface 的静态合同。
/// </summary>
[Trait("Category", "Audit")]
public sealed class AccountsPageParityTests
{
	/// <summary>
	/// 账户页应使用 FluentAvalonia 设置项、真实输入控件和 BaselineParity 原顺序。
	/// </summary>
	[Fact]
	public void AccountsPageUsesAxamlFluentControlsInPythonOrder()
	{
		string text = File.ReadAllText(Path.Combine(FindGuiRoot(), "Pages", "Accounts", "ZzzAccountsPage.axaml"));
		Assert.Contains("fa:FASettingsExpander", text, StringComparison.Ordinal);
		Assert.Contains("fa:FASettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("fa:FACommandBarButton", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAContentDialog", text, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding Name, Mode=TwoWay}\"", text, StringComparison.Ordinal);
		Assert.DoesNotContain("ComboBox x:Name=\"InstanceName", text, StringComparison.Ordinal);
		int num = text.IndexOf("Content=\"使用说明\"", StringComparison.Ordinal);
		int num2 = text.IndexOf("Header=\"当前账户设置\"", StringComparison.Ordinal);
		int num3 = text.IndexOf("Text=\"账户列表\"", StringComparison.Ordinal);
		Assert.True(num >= 0 && num2 > num && num3 > num2);
	}

	/// <summary>
	/// 账户页不得恢复摘要卡、fallback、证据说明或 C# 动态主体视觉树。
	/// </summary>
	[Fact]
	public void AccountsPageRemovesLegacySummaryAndFallbackUi()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "Accounts", "ZzzAccountsPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "Accounts", "ZzzAccountsPages.cs"));
		Assert.DoesNotContain("实例摘要", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("当前实例", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("game_account.yml", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("fallback", actualString, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("对应 Python", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new Border", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new TextBox", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ComboBox", actualString2, StringComparison.Ordinal);
	}

	private static string FindGuiRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string text = Path.Combine(directoryInfo.FullName, "src", "ZzzOd.Gui");
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("找不到 src/ZzzOd.Gui。");
	}
}
