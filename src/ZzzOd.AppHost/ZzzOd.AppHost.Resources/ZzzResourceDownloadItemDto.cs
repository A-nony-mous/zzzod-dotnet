using System.Collections.Generic;

namespace ZzzOd.AppHost.Resources;

/// <summary>
/// 可下载资源。
/// </summary>
public sealed record ZzzResourceDownloadItemDto(string ResourceId, string Title, IReadOnlyList<ZzzResourceModelOptionDto> Options, string SelectedModelId, bool UseGpu, ZzzResourceDownloadStatusDto Status);
