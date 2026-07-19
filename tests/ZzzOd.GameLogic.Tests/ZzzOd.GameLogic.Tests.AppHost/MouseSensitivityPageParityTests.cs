using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 灵敏度校准页面的 BaselineParity parity 合同测试。
/// </summary>
public sealed class MouseSensitivityPageParityTests
{
	/// <summary>
	/// AXAML 应保留 BaselineParity HelpCard 原文，并托管共享生产运行面。
	/// </summary>
	[Fact]
	public void AxamlMatchesPythonHelpCardAndUsesSharedProductionRunPanel()
	{
		string text = FindRepoRoot();
		string[] buffer = new string[6];
		buffer[0] = text;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "OneDragon";
		buffer[5] = "ZzzMouseSensitivityCheckerPage.axaml";
		string actualString = File.ReadAllText(Path.Combine(buffer));
		string[] buffer2 = new string[6];
		buffer2[0] = text;
		buffer2[1] = "src";
		buffer2[2] = "ZzzOd.Gui";
		buffer2[3] = "Pages";
		buffer2[4] = "OneDragon";
		buffer2[5] = "ZzzMouseSensitivityCheckerPage.cs";
		string actualString2 = File.ReadAllText(Path.Combine(buffer2));
		Assert.Contains("x:Class=\"ZzzOd.Gui.Pages.OneDragon.ZzzMouseSensitivityCheckerPage\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FASettingsExpanderItem", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"使用说明\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Description=\"点击「开始」后将自动校准鼠标/手柄的转向灵敏度，用于视角转动\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"SensitivityRunHost\"", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("打开", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("one-dragon.com", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzRunPanel(", actualString2, StringComparison.Ordinal);
		Assert.Contains("ZzzApplicationIds.MouseSensitivityChecker", actualString2, StringComparison.Ordinal);
		Assert.Equal("mouse_sensitivity_checker", "mouse_sensitivity_checker");
	}

	/// <summary>
	/// 生产应用应要求真实窗口，并从控制器读取截图和执行输入。
	/// </summary>
	[Fact]
	public void ProductionApplicationRequiresWindowAndUsesControllerInputAndScreenshot()
	{
		string text = FindRepoRoot();
		string actualString = File.ReadAllText(Path.Combine(text, "src", "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker", "MouseSensitivityCheckerApp.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(text, "src", "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker", "MouseSensitivityCheckerOperation.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(text, "src", "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker", "DefaultMouseSensitivityCheckerOperationServices.cs"));
		string actualString4 = File.ReadAllText(Path.Combine(text, "src", "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application", "ZApplication.cs"));
		Assert.Contains(": base(context, \"mouse_sensitivity_checker\", runRecord, \"鼠标灵敏度检测\")", actualString, StringComparison.Ordinal);
		Assert.Contains("bool needCheckGameWindow = true", actualString4, StringComparison.Ordinal);
		Assert.Contains("context.Controller?.Screenshot()", actualString3, StringComparison.Ordinal);
		Assert.Contains("zPcController.TurnByDistance(distance)", actualString3, StringComparison.Ordinal);
		Assert.Contains("new DefaultMouseSensitivityCheckerOperationServices()", actualString2, StringComparison.Ordinal);
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
}
