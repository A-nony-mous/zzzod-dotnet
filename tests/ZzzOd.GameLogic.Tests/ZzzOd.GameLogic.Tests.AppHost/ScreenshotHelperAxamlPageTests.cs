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
