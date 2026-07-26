using System;
using System.Collections.Generic;
using System.Linq;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 一条龙应用列表合并器：区分默认组、已迁移（注册但非默认组）与未注册三种状态，
/// 新注册应用以未持久化临时项置顶展示且不触发写盘。
/// </summary>
internal static class ZzzOneDragonAppListMerger
{
	/// <summary>
	/// 合并用户保存的应用列表与当前注册状态。
	/// </summary>
	/// <param name="savedApps">用户保存的完整有序列表。</param>
	/// <param name="defaultGroupAppIds">当前默认组应用（按默认顺序）。</param>
	/// <param name="isAppRegistered">判断应用当前是否已注册。</param>
	public static ZzzOneDragonAppMergeResult Merge(
		IEnumerable<OneDragonApplicationConfigItem> savedApps,
		IReadOnlyList<string> defaultGroupAppIds,
		Func<string, bool> isAppRegistered)
	{
		HashSet<string> defaultSet = defaultGroupAppIds.ToHashSet<string>(StringComparer.Ordinal);
		List<OneDragonApplicationConfigItem> keptApps = new List<OneDragonApplicationConfigItem>();
		List<string> migratedAppIds = new List<string>();
		bool removedDisabledApp = false;
		foreach (OneDragonApplicationConfigItem item in savedApps)
		{
			OneDragonApplicationConfigItem copy = new OneDragonApplicationConfigItem(item.AppId, item.Enabled);
			if (defaultSet.Contains(item.AppId))
			{
				keptApps.Add(copy);
				continue;
			}
			if (isAppRegistered(item.AppId))
			{
				// 注册但非默认组：启用的保留显示并标记迁移，禁用的从配置清除
				if (item.Enabled)
				{
					keptApps.Add(copy);
					migratedAppIds.Add(item.AppId);
				}
				else
				{
					removedDisabledApp = true;
				}
			}
			else
			{
				// 完全未注册：原位保留（不可见不删除）
				keptApps.Add(copy);
			}
		}
		HashSet<string> persistedIds = keptApps.Select((OneDragonApplicationConfigItem item) => item.AppId).ToHashSet<string>(StringComparer.Ordinal);
		List<OneDragonApplicationConfigItem> transientItems = defaultGroupAppIds
			.Where((string appId) => !persistedIds.Contains(appId))
			.Select((string appId) => new OneDragonApplicationConfigItem(appId, enabled: false)
			{
				IsPersisted = false,
			})
			.ToList();
		HashSet<string> visibleSet = defaultSet.Concat(migratedAppIds).ToHashSet<string>(StringComparer.Ordinal);
		List<OneDragonApplicationConfigItem> visibleApps = transientItems
			.Concat(keptApps.Where((OneDragonApplicationConfigItem item) => visibleSet.Contains(item.AppId)))
			.ToList();
		return new ZzzOneDragonAppMergeResult(
			keptApps,
			visibleApps,
			migratedAppIds,
			transientItems.Select((OneDragonApplicationConfigItem item) => item.AppId).ToArray(),
			removedDisabledApp);
	}

	/// <summary>
	/// 把前端提交的可见顺序写回持久化列表：未触碰的临时项不写入保存顺序，
	/// 已迁移项关闭即从配置永久移除，未注册项保持原位。
	/// </summary>
	public static IReadOnlyList<OneDragonApplicationConfigItem> ApplyVisibleOrder(
		ZzzOneDragonAppMergeResult mergeResult,
		IReadOnlyList<ZzzOneDragonAppUpdateDto> visibleApps)
	{
		HashSet<string> expectedIds = mergeResult.VisibleApps.Select((OneDragonApplicationConfigItem item) => item.AppId).ToHashSet<string>(StringComparer.Ordinal);
		if (visibleApps.Count != expectedIds.Count
			|| visibleApps.Any((ZzzOneDragonAppUpdateDto item) => !expectedIds.Contains(item.AppId))
			|| visibleApps.Select((ZzzOneDragonAppUpdateDto item) => item.AppId).Distinct<string>(StringComparer.Ordinal).Count() != visibleApps.Count)
		{
			throw new ArgumentException("一条龙应用列表与真实注册表不一致，请刷新后重试。", "visibleApps");
		}
		HashSet<string> transientSet = mergeResult.TransientAppIds.ToHashSet<string>(StringComparer.Ordinal);
		HashSet<string> migratedSet = mergeResult.MigratedAppIds.ToHashSet<string>(StringComparer.Ordinal);
		Dictionary<string, int> defaultIndexMap = mergeResult.VisibleApps
			.Select((OneDragonApplicationConfigItem item, int index) => (item.AppId, index))
			.ToDictionary(pair => pair.AppId, pair => pair.index, StringComparer.Ordinal);
		// 仍在默认位置且未启用的临时项视为未被用户触碰，不写入保存顺序
		HashSet<string> untouchedTransientIds = visibleApps
			.Select((ZzzOneDragonAppUpdateDto dto, int index) => (dto, index))
			.Where(pair => transientSet.Contains(pair.dto.AppId) && !pair.dto.Enabled && defaultIndexMap[pair.dto.AppId] == pair.index)
			.Select(pair => pair.dto.AppId)
			.ToHashSet<string>(StringComparer.Ordinal);
		// 关闭的已迁移项从配置永久移除
		HashSet<string> removedMigratedIds = visibleApps
			.Where((ZzzOneDragonAppUpdateDto dto) => migratedSet.Contains(dto.AppId) && !dto.Enabled)
			.Select((ZzzOneDragonAppUpdateDto dto) => dto.AppId)
			.ToHashSet<string>(StringComparer.Ordinal);
		List<OneDragonApplicationConfigItem> allApps = mergeResult.AllApps
			.Where((OneDragonApplicationConfigItem item) => !removedMigratedIds.Contains(item.AppId))
			.Select((OneDragonApplicationConfigItem item) => new OneDragonApplicationConfigItem(item.AppId, item.Enabled))
			.ToList();
		List<ZzzOneDragonAppUpdateDto> persistOrder = visibleApps
			.Where((ZzzOneDragonAppUpdateDto dto) => !untouchedTransientIds.Contains(dto.AppId) && !removedMigratedIds.Contains(dto.AppId))
			.ToList();
		HashSet<string> allIds = allApps.Select((OneDragonApplicationConfigItem item) => item.AppId).ToHashSet<string>(StringComparer.Ordinal);
		foreach (ZzzOneDragonAppUpdateDto dto in persistOrder)
		{
			// 新转正的临时项补进保存列表，位置由下方的顺序回写决定
			if (allIds.Add(dto.AppId))
			{
				allApps.Add(new OneDragonApplicationConfigItem(dto.AppId, dto.Enabled));
			}
		}
		HashSet<string> persistIds = persistOrder.Select((ZzzOneDragonAppUpdateDto dto) => dto.AppId).ToHashSet<string>(StringComparer.Ordinal);
		List<int> activeIndices = (from pair in allApps.Select((OneDragonApplicationConfigItem item, int index) => (item: item, index: index))
			where persistIds.Contains(pair.item.AppId)
			select pair.index).ToList();
		for (int num = 0; num < activeIndices.Count; num++)
		{
			ZzzOneDragonAppUpdateDto dto = persistOrder[num];
			allApps[activeIndices[num]] = new OneDragonApplicationConfigItem(dto.AppId, dto.Enabled);
		}
		return allApps;
	}
}
