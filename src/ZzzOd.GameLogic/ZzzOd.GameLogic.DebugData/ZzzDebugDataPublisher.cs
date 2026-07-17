using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Events;

namespace ZzzOd.GameLogic.DebugData;

/// <summary>
/// 发布 ZZZ 业务调试数据。
/// </summary>
public sealed class ZzzDebugDataPublisher
{
	private readonly ContextEventBus _eventBus;

	/// <summary>
	/// 初始化调试数据发布器。
	/// </summary>
	public ZzzDebugDataPublisher(ContextEventBus eventBus)
	{
		_eventBus = eventBus ?? throw new ArgumentNullException("eventBus");
	}

	/// <summary>
	/// 发布单条调试数据。
	/// </summary>
	public void Publish(ZzzDebugDataItem item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		PublishMany(new ZzzDebugDataItem[] { item });
	}

	/// <summary>
	/// 批量发布调试数据。
	/// </summary>
	public void PublishMany(IEnumerable<ZzzDebugDataItem> items)
	{
		ArgumentNullException.ThrowIfNull(items, "items");
		ZzzDebugDataItem[] array = items.ToArray();
		if (array.Length == 0)
		{
			return;
		}
		_eventBus.Publish("Zzz.Debug.Data", new ZzzDebugDataEventPayload(array));
		foreach (IGrouping<ZzzDebugDataKind, ZzzDebugDataItem> item in from item in array
			group item by item.Kind)
		{
			_eventBus.Publish(ZzzDebugEventIds.ForKind(item.Key), new ZzzDebugDataEventPayload(item.ToArray()));
		}
	}
}
