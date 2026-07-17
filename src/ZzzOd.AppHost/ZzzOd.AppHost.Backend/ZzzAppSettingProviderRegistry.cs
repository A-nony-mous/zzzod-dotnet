using System;
using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// BaselineParity AppSettingManager provider 清单的 .NET 等价注册表。
/// </summary>
public static class ZzzAppSettingProviderRegistry
{
	/// <summary>
	/// 与 BaselineParity 当前全部 *_app_setting.py 一致的 provider 清单。
	/// </summary>
	public static IReadOnlyList<ZzzAppSettingProviderDescriptor> All { get; } = new ZzzAppSettingProviderDescriptor[13]
	{
		new ZzzAppSettingProviderDescriptor("world_patrol", ZzzAppSettingType.Interface, "world-patrol-settings"),
		new ZzzAppSettingProviderDescriptor("withered_domain", ZzzAppSettingType.Interface, "withered-domain-settings"),
		new ZzzAppSettingProviderDescriptor("charge_plan", ZzzAppSettingType.Interface, "one-dragon-charge-plan"),
		new ZzzAppSettingProviderDescriptor("drive_disc_dismantle", ZzzAppSettingType.Flyout, "drive-disc-dismantle-flyout"),
		new ZzzAppSettingProviderDescriptor("redemption_code", ZzzAppSettingType.Interface, "redemption-code-settings"),
		new ZzzAppSettingProviderDescriptor("lost_void", ZzzAppSettingType.Interface, "lost-void-settings"),
		new ZzzAppSettingProviderDescriptor("suibian_temple", ZzzAppSettingType.Interface, "suibian-temple-settings"),
		new ZzzAppSettingProviderDescriptor("coffee", ZzzAppSettingType.Interface, "coffee-settings"),
		new ZzzAppSettingProviderDescriptor("notorious_hunt", ZzzAppSettingType.Interface, "notorious-hunt-settings"),
		new ZzzAppSettingProviderDescriptor("random_play", ZzzAppSettingType.Flyout, "random-play-flyout"),
		new ZzzAppSettingProviderDescriptor("life_on_line", ZzzAppSettingType.Flyout, "life-on-line-flyout"),
		new ZzzAppSettingProviderDescriptor("intel_board", ZzzAppSettingType.Flyout, "intel-board-flyout"),
		new ZzzAppSettingProviderDescriptor("shiyu_defense", ZzzAppSettingType.Interface, "shiyu-defense-settings")
	};

	private static readonly IReadOnlyDictionary<string, ZzzAppSettingProviderDescriptor> ByAppId = All.ToDictionary<ZzzAppSettingProviderDescriptor, string>((ZzzAppSettingProviderDescriptor provider) => provider.AppId, StringComparer.Ordinal);

	/// <summary>
	/// 查找已经迁移到产品 AXAML 的 provider。
	/// </summary>
	public static bool TryGetImplemented(string appId, out ZzzAppSettingProviderDescriptor provider)
	{
		if (ByAppId.TryGetValue(appId, out ZzzAppSettingProviderDescriptor value) && value.IsImplemented)
		{
			provider = value;
			return true;
		}
		provider = null;
		return false;
	}
}
