using System.Collections.Generic;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// Overlay 绘制项。
/// </summary>
/// <param name="Kind">绘制项类型。</param>
/// <param name="Id">绘制项编号。</param>
/// <param name="Bounds">区域。</param>
/// <param name="Text">文本。</param>
/// <param name="Color">颜色。</param>
/// <param name="Metadata">扩展数据。</param>
public sealed record ZzzOverlayDrawItemDto(ZzzOverlayDrawItemKind Kind, string Id, ZzzOverlayRectDto Bounds, string? Text = null, string? Color = null, IReadOnlyDictionary<string, string>? Metadata = null);
