using System;
using System.Collections.Generic;
using System.Linq;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Backend;

internal static class ZzzOneDragonAppListMerger
{
	public static ZzzOneDragonAppMergeResult Merge(IEnumerable<OneDragonApplicationConfigItem> savedApps, IReadOnlyList<string> registeredAppIds)
	{
		List<OneDragonApplicationConfigItem> list = savedApps.Select((OneDragonApplicationConfigItem item) => new OneDragonApplicationConfigItem(item.AppId, item.Enabled)).ToList();
		HashSet<string> registeredSet = registeredAppIds.ToHashSet<string>(StringComparer.Ordinal);
		HashSet<string> hashSet = list.Select((OneDragonApplicationConfigItem item) => item.AppId).ToHashSet<string>(StringComparer.Ordinal);
		bool changed = false;
		foreach (string registeredAppId in registeredAppIds)
		{
			if (hashSet.Add(registeredAppId))
			{
				list.Add(new OneDragonApplicationConfigItem(registeredAppId, enabled: false));
				changed = true;
			}
		}
		return new ZzzOneDragonAppMergeResult(list, list.Where((OneDragonApplicationConfigItem item) => registeredSet.Contains(item.AppId)).ToArray(), changed);
	}

	public static IReadOnlyList<OneDragonApplicationConfigItem> ApplyVisibleOrder(IEnumerable<OneDragonApplicationConfigItem> savedApps, IReadOnlySet<string> registeredAppIds, IReadOnlyList<ZzzOneDragonAppUpdateDto> visibleApps)
	{
		List<OneDragonApplicationConfigItem> list = savedApps.Select((OneDragonApplicationConfigItem item) => new OneDragonApplicationConfigItem(item.AppId, item.Enabled)).ToList();
		List<int> list2 = (from pair in list.Select((OneDragonApplicationConfigItem item, int index) => (item: item, index: index))
			where registeredAppIds.Contains(pair.item.AppId)
			select pair.index).ToList();
		if (list2.Count != visibleApps.Count || visibleApps.Any((ZzzOneDragonAppUpdateDto item) => !registeredAppIds.Contains(item.AppId)) || visibleApps.Select((ZzzOneDragonAppUpdateDto item) => item.AppId).Distinct<string>(StringComparer.Ordinal).Count() != visibleApps.Count)
		{
			throw new ArgumentException("一条龙应用列表与真实注册表不一致，请刷新后重试。", "visibleApps");
		}
		for (int num = 0; num < list2.Count; num++)
		{
			ZzzOneDragonAppUpdateDto zzzOneDragonAppUpdateDto = visibleApps[num];
			list[list2[num]] = new OneDragonApplicationConfigItem(zzzOneDragonAppUpdateDto.AppId, zzzOneDragonAppUpdateDto.Enabled);
		}
		return list;
	}
}
