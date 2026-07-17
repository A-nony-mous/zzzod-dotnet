using System.Threading;
using System.Threading.Tasks;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 远程地图图片客户端。
/// </summary>
public interface IRemoteMapImageClient
{
	/// <summary>
	/// 下载图片字节。
	/// </summary>
	Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken);
}
