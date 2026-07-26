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
