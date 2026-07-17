using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.DebugData;

/// <summary>
/// ZZZ 业务调试数据事件载荷。
/// </summary>
public sealed class ZzzDebugDataEventPayload
{
	/// <summary>事件内的数据项。</summary>
	public IReadOnlyList<ZzzDebugDataItem> Items { get; }

	/// <summary>事件创建时间。</summary>
	public DateTimeOffset CreatedAt { get; }

	/// <summary>
	/// 初始化事件载荷。
	/// </summary>
	public ZzzDebugDataEventPayload(IReadOnlyList<ZzzDebugDataItem> items)
	{
		Items = items ?? throw new ArgumentNullException("items");
		CreatedAt = DateTimeOffset.UtcNow;
	}
}
