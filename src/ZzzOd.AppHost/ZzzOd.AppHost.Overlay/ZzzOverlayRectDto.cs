namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// Overlay 矩形区域。
/// </summary>
/// <param name="X">横坐标。</param>
/// <param name="Y">纵坐标。</param>
/// <param name="Width">宽度。</param>
/// <param name="Height">高度。</param>
public sealed record ZzzOverlayRectDto(double X, double Y, double Width, double Height);
