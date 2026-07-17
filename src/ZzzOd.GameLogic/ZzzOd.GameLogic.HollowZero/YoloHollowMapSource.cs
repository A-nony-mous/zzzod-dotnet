using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

/// <summary>
/// 从当前游戏截图调用空洞 YOLO 模型建图。
/// </summary>
public sealed class YoloHollowMapSource : IHollowMapSource
{
	private readonly ZContext _context;

	/// <summary>
	/// 初始化真实截图地图源。
	/// </summary>
	public YoloHollowMapSource(ZContext context)
	{
		_context = context ?? throw new ArgumentNullException("context");
	}

	/// <inheritdoc />
	public Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		DateTimeOffset screenshotTimeUtc = detection?.CaptureTimeUtc ?? DateTimeOffset.UtcNow;
		Mat mat = detection?.Screen;
		if (mat == null)
		{
			return Task.FromResult<HollowZeroMap>(null);
		}
		return Task.FromResult(HollowYoloMapService.CalculateCurrentMap(_context, mat, screenshotTimeUtc));
	}
}
