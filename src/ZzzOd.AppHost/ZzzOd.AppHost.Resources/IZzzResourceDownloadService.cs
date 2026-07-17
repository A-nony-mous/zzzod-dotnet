using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZzzOd.AppHost.Resources;

/// <summary>
/// 资源下载服务。
/// </summary>
public interface IZzzResourceDownloadService
{
	/// <summary>资源状态变化。</summary>
	event EventHandler<ZzzResourceDownloadStatusDto>? StatusChanged;

	/// <summary>读取真实资源目录和当前配置。</summary>
	IReadOnlyList<ZzzResourceDownloadItemDto> GetItems();

	/// <summary>下载并安装选中的资源。</summary>
	Task DownloadAsync(string resourceId, string modelId, CancellationToken cancellationToken = default(CancellationToken));

	/// <summary>取消指定资源的当前下载。</summary>
	bool Cancel(string resourceId);
}
