using System.Collections.Generic;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// Overlay 快照展示筛选配置。
/// </summary>
public sealed record ZzzOverlayDisplayOptionsDto
{
	public const double DefaultYoloDedupIouThreshold = 0.3d;

	/// <summary>
	/// 是否输出视觉绘制项。
	/// </summary>
	public bool VisionLayerEnabled { get; init; } = true;

	/// <summary>
	/// 是否输出 YOLO 绘制项。
	/// </summary>
	public bool ShowYolo { get; init; } = true;

	/// <summary>
	/// 连续 YOLO 绘制项按标签去重时使用的 IoU 阈值。
	/// </summary>
	public double YoloDedupIouThreshold { get; init; } = DefaultYoloDedupIouThreshold;

	/// <summary>
	/// 是否输出 OCR 绘制项。
	/// </summary>
	public bool ShowOcr { get; init; } = true;

	/// <summary>
	/// 是否输出 Template 绘制项。
	/// </summary>
	public bool ShowTemplate { get; init; } = true;

	/// <summary>
	/// 是否输出 CV 绘制项。
	/// </summary>
	public bool ShowCv { get; init; } = true;

	/// <summary>
	/// 信息面板启用映射，键为 log、state、decision、timeline、performance。
	/// </summary>
	public IReadOnlyDictionary<string, bool> PanelEnabledMap { get; init; } = new Dictionary<string, bool>(StringComparer.Ordinal)
	{
		["log"] = true,
		["state"] = true,
		["decision"] = true,
		["timeline"] = true,
		["performance"] = true
	};

	/// <summary>
	/// 性能指标启用映射。
	/// </summary>
	public IReadOnlyDictionary<string, bool> PerformanceMetricEnabledMap { get; init; } = new Dictionary<string, bool>(StringComparer.Ordinal);

	/// <summary>
	/// 日志最大保留条数。
	/// </summary>
	public int LogMaxLines { get; init; } = 120;

	/// <summary>
	/// 日志淡出后不再输出的秒数。
	/// </summary>
	public double LogFadeSeconds { get; init; } = 12d;
}
