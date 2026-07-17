namespace ZzzOd.AppHost.Resources;

/// <summary>
/// 资源下载状态。
/// </summary>
public sealed record ZzzResourceDownloadStatusDto(string ResourceId, string ModelId, bool IsInstalled, bool IsRunning, bool IsCancelling, double? ProgressPercent, string Message, string? Error = null);
