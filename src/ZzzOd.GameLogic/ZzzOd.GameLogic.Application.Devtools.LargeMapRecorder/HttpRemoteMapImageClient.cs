using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// HttpClient 地图图片客户端。
/// </summary>
public sealed class HttpRemoteMapImageClient : IRemoteMapImageClient
{
	private readonly HttpClient _httpClient;

	/// <summary>
	/// 初始化客户端。
	/// </summary>
	public HttpRemoteMapImageClient(HttpClient? httpClient = null)
	{
		_httpClient = httpClient ?? new HttpClient();
	}

	/// <inheritdoc />
	public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken)
	{
		return _httpClient.GetByteArrayAsync(url, cancellationToken);
	}
}
