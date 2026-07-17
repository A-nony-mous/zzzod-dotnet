using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Yolo;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.HollowZero;

public sealed class YoloHollowEventSource : IHollowEventSource
{
	private readonly ZContext _context;

	private readonly float _confidence;

	private readonly float _iou;

	public YoloHollowEventSource(ZContext context, float confidence = 0.6f, float iou = 0.5f)
	{
		_context = context ?? throw new ArgumentNullException("context");
		_confidence = confidence;
		_iou = iou;
	}

	public Task<HollowEventDetection?> DetectAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var (captureTimeUtc, mat) = _context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null);
		if (mat == null)
		{
			return Task.FromResult<HollowEventDetection>(null);
		}
		try
		{
			double num = (double)captureTimeUtc.ToUnixTimeMilliseconds() / 1000.0;
			YoloDetectObjectResult yoloDetectObjectResult = _context.HollowEventDetector.CoreDetector.Run(mat, _confidence, _iou, num).Results.OrderByDescending((YoloDetectObjectResult result) => result.Score).FirstOrDefault();
			if (yoloDetectObjectResult == null)
			{
				mat.Dispose();
				return Task.FromResult<HollowEventDetection>(null);
			}
			return Task.FromResult(new HollowEventDetection(yoloDetectObjectResult.DetectClass.ClassName, yoloDetectObjectResult.Score, captureTimeUtc, num, mat));
		}
		catch
		{
			mat.Dispose();
			throw;
		}
	}
}
