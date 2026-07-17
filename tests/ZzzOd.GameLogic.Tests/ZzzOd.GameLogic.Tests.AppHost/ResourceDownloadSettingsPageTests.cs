using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 资源下载页 AXAML 与真实资源服务合同。
/// </summary>
public sealed class ResourceDownloadSettingsPageTests
{
	/// <summary>页面应保留 BaselineParity 模型卡顺序、下载、取消、GPU 和日志。</summary>
	[Fact]
	public void AxamlKeepsPythonModelCardOrderDownloadCancelGpuAndProductionLog()
	{
		string path = FindGuiDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzResourceDownloadPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzResourceDownloadPage.cs"));
		AssertOrder(text, "下载说明", "OCR识别", "闪光识别", "空洞格子识别", "迷失之地识别", "日志显示");
		Assert.Equal(4, Count(text, "Content=\"下载\""));
		Assert.Equal(4, Count(text, "Content=\"取消\""));
		Assert.Equal(4, Count(text, "OnContent=\"GPU\""));
		Assert.Equal(4, Count(text, "OffContent=\"CPU\""));
		Assert.Contains("OnDownloadClicked", text, StringComparison.Ordinal);
		Assert.Contains("OnCancelClicked", text, StringComparison.Ordinal);
		Assert.Contains("ContentControl x:Name=\"LogHost\"", text, StringComparison.Ordinal);
		Assert.Contains("IZzzResourceDownloadService", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("SetInputsEnabled(true)", actualString, StringComparison.Ordinal);
		Assert.Contains("download.IsEnabled = !status.IsRunning && !status.IsInstalled", actualString, StringComparison.Ordinal);
		Assert.Contains("DownloadAsync(resourceId, selected.Value)", actualString, StringComparison.Ordinal);
		Assert.Contains("Cancel(resourceId)", actualString, StringComparison.Ordinal);
		Assert.Contains("StatusChanged", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzLogDisplayCard(backend)", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("ProgressBar", text, StringComparison.Ordinal);
		Assert.DoesNotContain("暂不开放", text, StringComparison.Ordinal);
		Assert.DoesNotContain("未接入", text, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
		Assert.DoesNotContain("Pip", text, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("启动器", text, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzSettingCard", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
	}

	/// <summary>产品 DI 应注册并注入真实资源下载服务。</summary>
	[Fact]
	public void ProductWiringRegistersAndInjectsRealResourceDownloadService()
	{
		string text = FindRepoRoot();
		string actualString = File.ReadAllText(Path.Combine(text, "src", "ZzzOd.AppHost", "ZzzOd.AppHost", "ZzzAppHostServiceCollectionExtensions.cs"));
		string[] buffer = new string[5];
		buffer[0] = text;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "ZzzPageFactory.cs";
		string actualString2 = File.ReadAllText(Path.Combine(buffer));
		string[] buffer2 = new string[6];
		buffer2[0] = text;
		buffer2[1] = "src";
		buffer2[2] = "ZzzOd.Gui";
		buffer2[3] = "Pages";
		buffer2[4] = "Settings";
		buffer2[5] = "ZzzSettingsPage.cs";
		string actualString3 = File.ReadAllText(Path.Combine(buffer2));
		Assert.Contains("AddSingleton<IZzzResourceDownloadService, ZzzResourceDownloadService>()", actualString, StringComparison.Ordinal);
		Assert.Contains("IZzzResourceDownloadService resourceDownloadService", actualString2, StringComparison.Ordinal);
		Assert.Contains("_resourceDownloadService", actualString2, StringComparison.Ordinal);
		Assert.Contains("new ZzzResourceDownloadSettingsAxamlPage(backend, resourceDownloadService, _operations)", actualString3, StringComparison.Ordinal);
	}

	private static int Count(string text, string marker)
	{
		return (text.Length - text.Replace(marker, string.Empty, StringComparison.Ordinal).Length) / marker.Length;
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

	private static string FindGuiDirectory()
	{
		string[] buffer = new string[5];
		buffer[0] = FindRepoRoot();
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "Settings";
		return Path.Combine(buffer);
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
		throw new DirectoryNotFoundException("未找到 zzzod-dotnet 仓库根目录。");
	}
}
