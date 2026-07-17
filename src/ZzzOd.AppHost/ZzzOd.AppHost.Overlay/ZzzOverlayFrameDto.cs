using System;
using System.Collections.Generic;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// Overlay 绘制帧。
/// </summary>
/// <param name="Timestamp">时间戳。</param>
/// <param name="Items">绘制项。</param>
public sealed record ZzzOverlayFrameDto(DateTimeOffset Timestamp, IReadOnlyList<ZzzOverlayDrawItemDto> Items);
