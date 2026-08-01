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

	private readonly OverlayDebugBus? _overlayDebugBus;

	/// <summary>
	/// 初始化调试数据发布器。
	/// </summary>
	public ZzzDebugDataPublisher(ContextEventBus eventBus, OverlayDebugBus? overlayDebugBus = null)
	{
		_eventBus = eventBus ?? throw new ArgumentNullException("eventBus");
		_overlayDebugBus = overlayDebugBus;
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
		_eventBus.Publish(ZzzDebugEventIds.All, new ZzzDebugDataEventPayload(array));
		foreach (IGrouping<ZzzDebugDataKind, ZzzDebugDataItem> item in from item in array
			group item by item.Kind)
		{
			_eventBus.Publish(ZzzDebugEventIds.ForKind(item.Key), new ZzzDebugDataEventPayload(item.ToArray()));
		}
		foreach (ZzzDebugDataItem item2 in array)
		{
			PublishOverlayDebugItem(item2);
		}
	}

	/// <summary>
	/// 发布带来源和有效期的业务状态。同一键的新值覆盖旧值。
	/// </summary>
	public bool PublishBusinessState(string key, string value, string source, double ttlSeconds)
	{
		if (_overlayDebugBus == null)
		{
			return false;
		}
		return _overlayDebugBus.PublishBusinessState(new BusinessStateItem(
			key,
			value,
			DateTimeOffset.UtcNow,
			ttlSeconds,
			source));
	}

	private void PublishOverlayDebugItem(ZzzDebugDataItem item)
	{
		if (_overlayDebugBus == null)
		{
			return;
		}

		switch (item.Kind)
		{
		case ZzzDebugDataKind.Ocr:
			PublishRegionVision(item, "ocr", "#64e6a5");
			break;
		case ZzzDebugDataKind.Yolo:
			PublishRegionVision(item, "yolo", "#35d4ff");
			break;
		case ZzzDebugDataKind.Path:
			PublishPathVision(item);
			break;
		case ZzzDebugDataKind.ActionDecision:
			_overlayDebugBus.PublishDecision(new DecisionTraceItem(
				item.Source,
				ReadMetadataString(item.Metadata, "trigger"),
				ReadMetadataString(item.Metadata, "expression"),
				ReadMetadataString(item.Metadata, "operation", item.Label),
				item.State ?? string.Empty,
				item.CreatedAt,
				item.TtlSeconds,
				item.Metadata));
			break;
		case ZzzDebugDataKind.Performance:
			PublishPerformance(item);
			break;
		}
	}

	private void PublishRegionVision(ZzzDebugDataItem item, string source, string color)
	{
		if (!item.Region.HasValue)
		{
			return;
		}

		OneDragon.Core.Abstractions.Geometry.Rect region = item.Region.Value;
		_overlayDebugBus!.PublishVision(new VisionDrawItem(source, item.Label, region.X1, region.Y1, region.X2, region.Y2)
		{
			Score = item.Confidence,
			Color = color,
			Created = item.CreatedAt.ToUnixTimeMilliseconds() / 1000d,
			TtlSeconds = item.TtlSeconds,
			CoordinateSpace = VisionCoordinateSpace.StandardGame,
			Metadata = item.Metadata,
		});
	}

	private void PublishPathVision(ZzzDebugDataItem item)
	{
		if (item.PathPoints.Count == 0 && !item.Region.HasValue)
		{
			return;
		}

		int minX = item.Region?.X1 ?? item.PathPoints.Min(point => point.X);
		int minY = item.Region?.Y1 ?? item.PathPoints.Min(point => point.Y);
		int maxX = item.Region?.X2 ?? item.PathPoints.Max(point => point.X);
		int maxY = item.Region?.Y2 ?? item.PathPoints.Max(point => point.Y);
		Dictionary<string, object?> metadata = new(item.Metadata, StringComparer.Ordinal)
		{
			["path_points"] = item.PathPoints.ToArray(),
		};
		_overlayDebugBus!.PublishVision(new VisionDrawItem("path", item.Label, minX, minY, maxX, maxY)
		{
			Color = "#f5d742",
			Created = item.CreatedAt.ToUnixTimeMilliseconds() / 1000d,
			TtlSeconds = item.TtlSeconds,
			CoordinateSpace = VisionCoordinateSpace.StandardGame,
			Metadata = metadata,
		});
		_overlayDebugBus.PublishTimeline(new TimelineItem(
			"path",
			item.Label,
			item.State ?? string.Empty,
			"information",
			item.CreatedAt,
			item.TtlSeconds,
			metadata));
	}

	private void PublishPerformance(ZzzDebugDataItem item)
	{
		double? value = item.ElapsedMilliseconds ?? ReadMetadataDouble(item.Metadata, "value");
		if (!value.HasValue)
		{
			return;
		}

		_overlayDebugBus!.PublishPerformance(new PerformanceMetricSample(
			ReadMetadataString(item.Metadata, "metric", item.Label),
			value.Value,
			ReadMetadataString(item.Metadata, "unit", item.State ?? string.Empty),
			item.CreatedAt,
			item.TtlSeconds,
			item.Metadata));
	}

	private static string ReadMetadataString(IReadOnlyDictionary<string, object?> metadata, string key, string fallback = "")
	{
		return metadata.TryGetValue(key, out object? value)
			? Convert.ToString(value) ?? fallback
			: fallback;
	}

	private static double? ReadMetadataDouble(IReadOnlyDictionary<string, object?> metadata, string key)
	{
		if (!metadata.TryGetValue(key, out object? value) || value is null)
		{
			return null;
		}

		return value is double number
			? number
			: double.TryParse(Convert.ToString(value), out double parsed) ? parsed : null;
	}
}
