using System;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// Overlay 运行期性能采样。
/// </summary>
public sealed record ZzzOverlayPerformanceSampleDto(string Metric, double Value, string Unit, DateTimeOffset CreatedAt, double TtlSeconds = 20.0);
