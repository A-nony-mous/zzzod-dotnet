using System;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// Overlay 状态。
/// </summary>
/// <param name="Enabled">是否启用。</param>
/// <param name="LastFrameAt">最后绘制帧时间。</param>
/// <param name="ItemCount">绘制项数量。</param>
public sealed record ZzzOverlayStatusDto(bool Enabled, DateTimeOffset? LastFrameAt, int ItemCount);
