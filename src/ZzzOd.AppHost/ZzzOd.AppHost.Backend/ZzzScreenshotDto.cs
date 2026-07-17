namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 截图结果。
/// </summary>
/// <param name="ContentType">内容类型。</param>
/// <param name="Bytes">图片字节。</param>
public sealed record ZzzScreenshotDto(string ContentType, byte[] Bytes);
