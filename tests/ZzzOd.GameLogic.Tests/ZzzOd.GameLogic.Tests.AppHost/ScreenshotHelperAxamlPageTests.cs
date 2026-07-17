using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.Gui.Services.RunIntent;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 截图助手独立 AXAML 页合同测试。
/// </summary>
[Collection("Screenshot helper global input source")]
public sealed class ScreenshotHelperAxamlPageTests
{
	/// <summary>
	/// 页面保持 BaselineParity 控件顺序，并绑定当前实例、默认组和真实运行面。
	/// </summary>
	[Fact]
	public void PageUsesAxamlFluentControlsCurrentInstanceAndProductionRunPanel()
	{
		string path = FindDevtoolsDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzScreenshotHelperPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzScreenshotHelperPage.cs"));
		AssertOrder(text, "截图间隔(秒)", "持续时间(秒)", "保存截图按键", "闪避检测", "按键前截图", "小地图朝向检测", "RunHost");
		Assert.Contains("x:Class=\"ZzzOd.Gui.Pages.Devtools.ZzzScreenshotHelperAxamlPage\"", text, StringComparison.Ordinal);
		Assert.Contains("fa:SettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:NumberBox", text, StringComparison.Ordinal);
		Assert.Contains("ToggleSwitch", text, StringComparison.Ordinal);
		Assert.Contains("new ZzzRunPanel(", actualString, StringComparison.Ordinal);
		Assert.Contains("ZzzApplicationIds.ScreenshotHelper", actualString, StringComparison.Ordinal);
		Assert.Contains("fixedGroupId: ScreenshotHelperConstants.DefaultGroupId", actualString, StringComparison.Ordinal);
		Assert.Contains("GetCurrentInstance()", actualString, StringComparison.Ordinal);
		Assert.Contains("GetConfigScope(", actualString, StringComparison.Ordinal);
		Assert.Contains("SaveConfigScope", actualString, StringComparison.Ordinal);
		Assert.Contains("_instanceIndex", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("InstanceIndex: 0", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("instanceIndex: 0", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzBackendConfigBinding", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzSettingCard", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
		Assert.DoesNotContain("来源", text, StringComparison.Ordinal);
		Assert.DoesNotContain("未接入", text, StringComparison.Ordinal);
		Assert.DoesNotContain("fallback", actualString, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 页面应把 GUI 全局按键转发给真实 screenshot helper app。
	/// </summary>
	[Fact]
	public void PageBridgesGlobalInputWithoutInventingDetectorState()
	{
		string path = FindDevtoolsDirectory();
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzScreenshotHelperPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "ZzzScreenshotHelperPage.cs"));
		Assert.Contains("GlobalInputPressed += OnGlobalInputPressed", actualString2, StringComparison.Ordinal);
		Assert.Contains("ScreenshotHelperGlobalInputSource.Suspend()", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("NullScreenshotHelper", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("detector", actualString, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("全局按键", actualString, StringComparison.Ordinal);
	}

	/// <summary>
	/// 生产 app 必须使用真实闪避和小地图 detector，并让订阅跟随 app 生命周期。
	/// </summary>
	[Fact]
	public void ProductionAppUsesRealDetectorsAndAppLifetimeGlobalInputSubscription()
	{
		string text = FindRepositoryRoot();
		string path = Path.Combine(text, "src", "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper");
		string actualString = File.ReadAllText(Path.Combine(path, "ScreenshotHelperApp.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "ZContextScreenshotHelperDodgeDetector.cs")) + File.ReadAllText(Path.Combine(path, "ZContextScreenshotHelperMiniMapAngleDetector.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "ScreenshotHelperService.cs"));
		string[] buffer2 = new string[6];
		buffer2[0] = text;
		buffer2[1] = "src";
		buffer2[2] = "ZzzOd.Gui";
		buffer2[3] = "Services";
		buffer2[4] = "RunIntent";
		buffer2[5] = "ZzzGuiRunIntentService.cs";
		string actualString4 = File.ReadAllText(Path.Combine(buffer2));
		Assert.Contains("new ZContextScreenshotHelperDodgeDetector(context)", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZContextScreenshotHelperMiniMapAngleDetector(context)", actualString, StringComparison.Ordinal);
		Assert.Contains("Context.AutoBattleContext.InitAutoOp(", actualString, StringComparison.Ordinal);
		Assert.Contains("ScreenshotHelperGlobalInputSource.Subscribe(_service.HandleKeyPress)", actualString, StringComparison.Ordinal);
		Assert.Contains("AutoBattleContext.DodgeContext.CheckDodgeFlash", actualString2, StringComparison.Ordinal);
		Assert.Contains("WorldPatrolService.CutMiniMap", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("NullScreenshotHelper", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("dodgeDetector ??", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("miniMapAngleDetector ??", actualString3, StringComparison.Ordinal);
		Assert.Contains("ScreenshotHelperGlobalInputSource.Publish(key)", actualString4, StringComparison.Ordinal);
	}

	/// <summary>
	/// GUI 输入桥附着于 run-intent 单例，不依赖截图助手页面的显示生命周期。
	/// </summary>
	[Fact]
	public void GuiInputBridgeContinuesPublishingWithoutPageLifecycleCallbacks()
	{
		ZzzGuiRunIntentService zzzGuiRunIntentService = new ZzzGuiRunIntentService();
		List<string> received = new List<string>();
		using (ScreenshotHelperGlobalInputSource.Subscribe(delegate(string key)
		{
			received.Add(key);
			return true;
		}))
		{
			zzzGuiRunIntentService.PublishGlobalInputPressed("1");
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "1";
			Assert.Equal<List<string>>(list, received);
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

	private static string FindDevtoolsDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "Devtools";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到开发工具页目录。");
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
