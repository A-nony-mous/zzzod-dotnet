using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.Gui.Pages.Settings;
using ZzzOd.Gui.Services.LauncherMedia;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 自定义设置页 AXAML、真实配置和主题媒体行为审计。
/// </summary>
public sealed class CustomSettingsAxamlPageTests
{
	/// <summary>
	/// 页面应按 BaselineParity 顺序使用 Fluent 控件，并接入真实配置、主题和媒体服务。
	/// </summary>
	[Fact]
	public void CustomSettingsUsesAxamlFluentControlsAndRealServices()
	{
		string path = FindSettingsDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzCustomSettingsPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzCustomSettingsPage.cs"));
		AssertOrder(text, "外观", "界面语言", "界面主题", "自定义主题色", "主页背景类型", "自定义主页背景");
		Assert.Contains("fa:SettingsExpander", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("fa:ContentDialog", text, StringComparison.Ordinal);
		Assert.Contains("PasswordChar=\"●\"", text, StringComparison.Ordinal);
		Assert.Contains("ValueChanged=\"OnThemeColorValueChanged\"", text, StringComparison.Ordinal);
		Assert.Contains("SaveConfigScope", actualString, StringComparison.Ordinal);
		Assert.Contains("ApplyAccentColor", actualString, StringComparison.Ordinal);
		Assert.Contains("SaveCustomBackgroundAsync", actualString, StringComparison.Ordinal);
		Assert.Contains("RequestedThemeVariant", actualString, StringComparison.Ordinal);
		Assert.Contains("Language changed successfully. Please restart the application for changes to take effect.", actualString, StringComparison.Ordinal);
		Assert.Contains("Restart Now", actualString, StringComparison.Ordinal);
		Assert.Contains("Restart Later", actualString, StringComparison.Ordinal);
		Assert.Contains("SHA256.HashData", actualString, StringComparison.Ordinal);
		Assert.Contains("RequiredBool(current.Value.Values, \"custom_banner\")", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("[\"custom_banner\"] = true", actualString, StringComparison.Ordinal);
		Assert.Contains("\"*.webm\", \"*.mp4\", \"*.mkv\"", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("*.avif", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("*.jxl", actualString, StringComparison.Ordinal);
		Assert.Contains("*.avi", actualString, StringComparison.Ordinal);
		Assert.Contains("*.mov", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzSettingCard", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
		Assert.DoesNotContain("来源", text, StringComparison.Ordinal);
	}

	/// <summary>
	/// 两个 BaselineParity 密码哈希应使用真实 SHA-256 校验。
	/// </summary>
	[Fact]
	public void CustomSettingsUsesPythonPasswordHashes()
	{
		Assert.False(ZzzCustomSettingsAxamlPage.VerifyPasswordForTest("", "b0cd76b7d7829362d581b739c0b295abf53182792609078bb17a9dd917ffba7c"));
		Assert.False(ZzzCustomSettingsAxamlPage.VerifyPasswordForTest("", "d678f04ece93caaa4d030696429101725cbf31657dd9ded4fdc3b71b3ee05c54"));
	}

	/// <summary>
	/// BaselineParity 支持的 AVI 和 MOV 背景文件应通过真实媒体校验。
	/// </summary>
	[Theory]
	[InlineData(new object[] { ".avi" })]
	[InlineData(new object[] { ".mov" })]
	public void LauncherMediaAcceptsPythonVideoExtensions(string extension)
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-custom-media-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		string path = Path.Combine(text, "background" + extension);
		try
		{
			File.WriteAllBytes(path, "RIFF0000AVI "u8.ToArray());
			ZzzLauncherMediaService.ValidateCustomBackground(path);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
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

	private static string FindSettingsDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "Settings";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到设置页目录。");
	}
}
