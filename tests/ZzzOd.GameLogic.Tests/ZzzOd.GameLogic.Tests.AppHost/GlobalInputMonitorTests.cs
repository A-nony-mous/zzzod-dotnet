using System;
using System.IO;
using Xunit;
using ZzzOd.Gui.Services.Windows;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// Windows 全局键鼠监听的 BaselineParity 名称映射与页面接线测试。
/// </summary>
public sealed class GlobalInputMonitorTests
{
	/// <summary>
	/// 虚拟键应转换为 BaselineParity PcButtonListener 使用的按键名称。
	/// </summary>
	[Theory]
	[InlineData(new object[] { 65u, "a" })]
	[InlineData(new object[] { 90u, "z" })]
	[InlineData(new object[] { 48u, "0" })]
	[InlineData(new object[] { 112u, "f1" })]
	[InlineData(new object[] { 123u, "f12" })]
	[InlineData(new object[] { 96u, "numpad_0" })]
	[InlineData(new object[] { 105u, "numpad_9" })]
	[InlineData(new object[] { 27u, "esc" })]
	[InlineData(new object[] { 37u, "left" })]
	public void VirtualKeysUsePythonCompatibleNames(uint virtualKey, string expected)
	{
		Assert.Equal(expected, ZzzGlobalInputMonitor.NormalizeVirtualKey(virtualKey));
	}

	/// <summary>
	/// 游戏和脚本环境设置页应共享产品全局键鼠监听器。
	/// </summary>
	[Fact]
	public void SettingsPagesUseSharedGlobalInputMonitor()
	{
		string path = FindRepositoryRoot();
		string path2 = Path.Combine(path, "src", "ZzzOd.Gui");
		string actualString = File.ReadAllText(Path.Combine(path2, "Services", "Windows", "ZzzGlobalInputMonitor.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path2, "Views", "FrontierPages", "Settings", "FrontierGameSettingsPage.axaml.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(path2, "Views", "FrontierPages", "Settings", "FrontierEnvironmentSettingsPage.axaml.cs"));
		string actualString4 = File.ReadAllText(Path.Combine(path2, "Program.cs"));
		Assert.Contains("SetWindowsHookExW(WhKeyboardLl", actualString, StringComparison.Ordinal);
		Assert.Contains("SetWindowsHookExW(WhMouseLl", actualString, StringComparison.Ordinal);
		Assert.Contains("_inputMonitor.InputPressed += OnGlobalInputPressed", actualString2, StringComparison.Ordinal);
		Assert.Contains("_inputMonitor.InputPressed += OnGlobalInputPressed", actualString3, StringComparison.Ordinal);
		Assert.Contains("SuspendHotkeyActions()", actualString3, StringComparison.Ordinal);
		Assert.Contains("ReinitializeContextAsync()", actualString3, StringComparison.Ordinal);
		Assert.Contains("services.AddSingleton<ZzzGlobalInputMonitor>()", actualString4, StringComparison.Ordinal);
		Assert.Contains("services.AddHostedService", actualString4, StringComparison.Ordinal);
		Assert.DoesNotContain("mock", actualString, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 当前 Windows 会话应能真实安装并卸载键盘和鼠标低层 hook。
	/// </summary>
	[Fact]
	public void MonitorStartsAndStopsInCurrentWindowsSession()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}
		using ZzzGlobalInputMonitor zzzGlobalInputMonitor = new ZzzGlobalInputMonitor();
		Assert.True(zzzGlobalInputMonitor.EnsureStarted(), zzzGlobalInputMonitor.LastError);
	}

	/// <summary>
	/// 连续创建和释放监听器后不应遗留仍在运行的消息线程。
	/// </summary>
	[Fact]
	public void MonitorRepeatedStartupAndDisposalStopsEveryMessageThread()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		for (int index = 0; index < 8; index++)
		{
			ZzzGlobalInputMonitor monitor = new();
			Assert.True(monitor.EnsureStarted(), monitor.LastError);
			Assert.True(monitor.IsRunningForTest);
			monitor.Dispose();
			Assert.False(monitor.IsRunningForTest);
		}
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
