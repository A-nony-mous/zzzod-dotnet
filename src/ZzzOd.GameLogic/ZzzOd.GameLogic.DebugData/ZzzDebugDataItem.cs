using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;

namespace ZzzOd.GameLogic.DebugData;

/// <summary>
/// GUI-free ZZZ 业务调试数据项。
/// </summary>
public sealed class ZzzDebugDataItem
{
	/// <summary>调试数据类型。</summary>
	public ZzzDebugDataKind Kind { get; }

	/// <summary>来源标识。</summary>
	public string Source { get; }

	/// <summary>展示标签或主文本。</summary>
	public string Label { get; }

	/// <summary>关联画面区域。</summary>
	public Rect? Region { get; }

	/// <summary>置信度。</summary>
	public double? Confidence { get; }

	/// <summary>业务状态。</summary>
	public string? State { get; }

	/// <summary>耗时，单位毫秒。</summary>
	public double? ElapsedMilliseconds { get; }

	/// <summary>路径点。</summary>
	public IReadOnlyList<Point> PathPoints { get; }

	/// <summary>扩展元数据。</summary>
	public IReadOnlyDictionary<string, object?> Metadata { get; }

	/// <summary>创建时间。</summary>
	public DateTimeOffset CreatedAt { get; }

	/// <summary>建议保留秒数。</summary>
	public double TtlSeconds { get; }

	/// <summary>
	/// 初始化调试数据项。
	/// </summary>
	public ZzzDebugDataItem(ZzzDebugDataKind kind, string source, string label, Rect? region = null, double? confidence = null, string? state = null, double? elapsedMilliseconds = null, IReadOnlyList<Point>? pathPoints = null, IReadOnlyDictionary<string, object?>? metadata = null, DateTimeOffset? createdAt = null, double ttlSeconds = 30.0)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source, "source");
		ArgumentException.ThrowIfNullOrWhiteSpace(label, "label");
		Kind = kind;
		Source = source;
		Label = label;
		Region = region;
		Confidence = confidence;
		State = state;
		ElapsedMilliseconds = elapsedMilliseconds;
		PathPoints = ((pathPoints == null) ? Array.Empty<Point>() : pathPoints.ToArray());
		Metadata = ((metadata == null) ? new ReadOnlyDictionary<string, object>(new Dictionary<string, object>()) : new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(metadata, StringComparer.Ordinal)));
		CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
		TtlSeconds = ttlSeconds;
	}

	/// <summary>
	/// 创建 OCR 调试数据。
	/// </summary>
	public static ZzzDebugDataItem Ocr(string source, string text, Rect region, double? confidence = null, string? state = null, double? elapsedMilliseconds = null, IReadOnlyDictionary<string, object?>? metadata = null, double ttlSeconds = 30.0)
	{
		return new ZzzDebugDataItem(ZzzDebugDataKind.Ocr, source, text, region, confidence, state, elapsedMilliseconds, null, metadata, null, ttlSeconds);
	}

	/// <summary>
	/// 创建 YOLO 调试数据。
	/// </summary>
	public static ZzzDebugDataItem Yolo(string source, string label, Rect region, double? confidence = null, string? state = null, double? elapsedMilliseconds = null, IReadOnlyDictionary<string, object?>? metadata = null, double ttlSeconds = 30.0)
	{
		return new ZzzDebugDataItem(ZzzDebugDataKind.Yolo, source, label, region, confidence, state, elapsedMilliseconds, null, metadata, null, ttlSeconds);
	}

	/// <summary>
	/// 创建路径调试数据。
	/// </summary>
	public static ZzzDebugDataItem Path(string source, string label, IReadOnlyList<Point> points, string? state = null, Rect? region = null, double? elapsedMilliseconds = null, IReadOnlyDictionary<string, object?>? metadata = null, double ttlSeconds = 30.0)
	{
		return new ZzzDebugDataItem(ZzzDebugDataKind.Path, source, label, region, null, state, elapsedMilliseconds, points, metadata, null, ttlSeconds);
	}

	/// <summary>
	/// 创建动作决策调试数据。
	/// </summary>
	public static ZzzDebugDataItem ActionDecision(string source, string trigger, string operation, string status, string? expression = null, IReadOnlyDictionary<string, object?>? metadata = null, double ttlSeconds = 30.0)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["trigger"] = trigger,
			["operation"] = operation,
			["expression"] = expression ?? string.Empty
		};
		MergeMetadata(dictionary, metadata);
		IReadOnlyDictionary<string, object> metadata2 = dictionary;
		return new ZzzDebugDataItem(ZzzDebugDataKind.ActionDecision, source, operation, null, null, status, null, null, metadata2, null, ttlSeconds);
	}

	/// <summary>
	/// 创建性能采样调试数据。
	/// </summary>
	public static ZzzDebugDataItem Performance(string source, string metric, double value, string unit, IReadOnlyDictionary<string, object?>? metadata = null, double ttlSeconds = 30.0)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["metric"] = metric,
			["unit"] = unit,
			["value"] = value,
		};
		MergeMetadata(dictionary, metadata);
		double? elapsedMilliseconds = (string.Equals(unit, "ms", StringComparison.OrdinalIgnoreCase) ? new double?(value) : ((double?)null));
		IReadOnlyDictionary<string, object> metadata2 = dictionary;
		return new ZzzDebugDataItem(ZzzDebugDataKind.Performance, source, metric, null, null, unit, elapsedMilliseconds, null, metadata2, null, ttlSeconds);
	}

	private static void MergeMetadata(Dictionary<string, object?> target, IReadOnlyDictionary<string, object?>? source)
	{
		if (source == null)
		{
			return;
		}
		foreach (KeyValuePair<string, object> item in source)
		{
			item.Deconstruct(out var key, out var value);
			string key2 = key;
			object value2 = value;
			target[key2] = value2;
		}
	}
}
