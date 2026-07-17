using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// BaselineParity AppSettingManager provider 清单和产品入口审计。
/// </summary>
public sealed class AppSettingProviderRegistryTests
{
	/// <summary>
	/// 注册表应匹配 BaselineParity provider，并只暴露已迁移的 AXAML 目标。
	/// </summary>
	[Fact]
	public void RegistryMatchesCurrentPythonProvidersAndOnlyExposesMigratedTarget()
	{
		Assert.Equal(13, ZzzAppSettingProviderRegistry.All.Count);
		string[] buffer = new string[13];
		buffer[0] = "world_patrol";
		buffer[1] = "withered_domain";
		buffer[2] = "charge_plan";
		buffer[3] = "drive_disc_dismantle";
		buffer[4] = "redemption_code";
		buffer[5] = "lost_void";
		buffer[6] = "suibian_temple";
		buffer[7] = "coffee";
		buffer[8] = "notorious_hunt";
		buffer[9] = "random_play";
		buffer[10] = "life_on_line";
		buffer[11] = "intel_board";
		buffer[12] = "shiyu_defense";
		Assert.Equal(buffer, ZzzAppSettingProviderRegistry.All.Select((ZzzAppSettingProviderDescriptor zzzAppSettingProviderDescriptor) => zzzAppSettingProviderDescriptor.AppId).ToArray());
		string[] buffer2 = new string[4];
		buffer2[0] = "drive_disc_dismantle";
		buffer2[1] = "random_play";
		buffer2[2] = "life_on_line";
		buffer2[3] = "intel_board";
		Assert.Equal(buffer2, (from zzzAppSettingProviderDescriptor in ZzzAppSettingProviderRegistry.All
			where zzzAppSettingProviderDescriptor.SettingType == ZzzAppSettingType.Flyout
			select zzzAppSettingProviderDescriptor.AppId).ToArray());
		string[] buffer3 = new string[13];
		buffer3[0] = "world_patrol";
		buffer3[1] = "withered_domain";
		buffer3[2] = "charge_plan";
		buffer3[3] = "drive_disc_dismantle";
		buffer3[4] = "redemption_code";
		buffer3[5] = "lost_void";
		buffer3[6] = "suibian_temple";
		buffer3[7] = "coffee";
		buffer3[8] = "notorious_hunt";
		buffer3[9] = "random_play";
		buffer3[10] = "life_on_line";
		buffer3[11] = "intel_board";
		buffer3[12] = "shiyu_defense";
		Assert.Equal(buffer3, (from zzzAppSettingProviderDescriptor in ZzzAppSettingProviderRegistry.All
			where zzzAppSettingProviderDescriptor.IsImplemented
			select zzzAppSettingProviderDescriptor.AppId).ToArray());
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("world_patrol", out ZzzAppSettingProviderDescriptor provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("charge_plan", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("withered_domain", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("drive_disc_dismantle", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("redemption_code", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("lost_void", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("suibian_temple", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("coffee", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("notorious_hunt", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("random_play", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("life_on_line", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("intel_board", out provider));
		Assert.True(ZzzAppSettingProviderRegistry.TryGetImplemented("shiyu_defense", out provider));
	}

	/// <summary>
	/// 一条龙和独立运行页应通过 provider 导航器打开真实设置目标。
	/// </summary>
	[Fact]
	public void RunPagesUseRegistryBackedSettingClickInsteadOfDisabledButtons()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzOneDragonRunPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "Standalone", "ZzzStandaloneAppRunPage.axaml"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzOneDragonRunPage.cs"));
		string actualString4 = File.ReadAllText(Path.Combine(path, "Pages", "Standalone", "ZzzStandaloneAppRunPage.cs"));
		string actualString5 = File.ReadAllText(Path.Combine(path, "Pages", "ApplicationSettings", "ZzzAppSettingNavigator.cs"));
		Assert.Contains("Click=\"OnAppSettingClicked\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Click=\"OnAppSettingClicked\"", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("ToolTip.Tip=\"应用设置\"\r\n                              IsVisible=\"{Binding SettingVisible}\"\r\n                              IsEnabled=\"False\"", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("ToolTip.Tip=\"应用设置\"\r\n                            IsVisible=\"{Binding SettingVisible}\"\r\n                            IsEnabled=\"False\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("ZzzAppSettingProviderRegistry.TryGetImplemented", actualString4, StringComparison.Ordinal);
		Assert.Contains("SecondaryPageRequested", actualString3, StringComparison.Ordinal);
		Assert.Contains("SecondaryPageRequested", actualString4, StringComparison.Ordinal);
		Assert.Contains("FlyoutBase.ShowAttachedFlyout(target)", actualString5, StringComparison.Ordinal);
		Assert.Contains("_backend.GetCurrentInstance()", actualString5, StringComparison.Ordinal);
		Assert.Contains("CreateTarget(provider.ImplementedTarget!, current.Value.Index, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzWorldPatrolAppSettingPage(_backend, worldPatrolBackend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzChargePlanPage(_backend)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzWitheredDomainAppSettingPage(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzDriveDiscDismantleSettingsFlyoutContent(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzRedemptionCodeAppSettingPage(redemptionCodeBackend)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzLostVoidAppSettingPage(_backend, lostVoidBackend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzSuibianTempleAppSettingPage(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzCoffeeAppSettingPage(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzNotoriousHuntAppSettingPage(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzRandomPlaySettingsFlyoutContent(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzLifeOnLineSettingsFlyoutContent(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzIntelBoardSettingsFlyoutContent(_backend, progressBackend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.Contains("new ZzzShiyuDefenseAppSettingPage(_backend, instanceIndex, groupId)", actualString5, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzApplicationSettingsPage", actualString5, StringComparison.Ordinal);
	}

	/// <summary>
	/// 新增的应用设置 scope 应写入 BaselineParity group 路径，并保持实例和 group 绑定。
	/// </summary>
	[Fact]
	public void ApplicationScopesWritePythonGroupPaths()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-app-setting-scopes", Guid.NewGuid().ToString("N"));
		try
		{
			ZzzConfigScopeService zzzConfigScopeService = new ZzzConfigScopeService(text);
			(string, string, string, object)[] array = new(string, string, string, object)[5]
			{
				("drive-disc-dismantle", "drive_disc_dismantle", "dismantle_abandon", true),
				("random-play", "random_play", "agent_name_1", "随机"),
				("life-on-line", "life_on_line", "daily_plan_times", 12),
				("intel-board", "intel_board", "exp_grind_mode", true),
				("suibian-temple", "suibian_temple", "auto_manage_enabled", false)
			};
			(string, string, string, object)[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				(string, string, string, object) tuple = array2[i];
				string item = tuple.Item1;
				string item2 = tuple.Item2;
				string item3 = tuple.Item3;
				object item4 = tuple.Item4;
				ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = zzzConfigScopeService.Save(new ZzzSaveConfigScopeRequest(item, new Dictionary<string, object> { [item3] = item4 }, 2, "daily"));
				Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
				Assert.Equal(2, zzzBackendResult.Value.InstanceIndex);
				Assert.Equal("daily", zzzBackendResult.Value.GroupId);
				string[] buffer = new string[5];
				buffer[0] = text;
				buffer[1] = "config";
				buffer[2] = "02";
				buffer[3] = "daily";
				buffer[4] = item2 + ".yml";
				Assert.True(File.Exists(Path.Combine(buffer)));
				string[] buffer2 = new string[6];
				buffer2[0] = text;
				buffer2[1] = "config";
				buffer2[2] = "02";
				buffer2[3] = "app_config";
				buffer2[4] = "daily";
				buffer2[5] = item2 + ".yml";
				Assert.False(File.Exists(Path.Combine(buffer2)));
			}
		}
		finally
		{
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
		}
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
