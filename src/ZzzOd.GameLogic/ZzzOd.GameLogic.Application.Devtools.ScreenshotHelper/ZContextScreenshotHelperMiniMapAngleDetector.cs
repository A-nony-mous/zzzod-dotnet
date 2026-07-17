using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 使用生产 WorldPatrolService 执行小地图角度检测。
/// </summary>
public sealed class ZContextScreenshotHelperMiniMapAngleDetector : IScreenshotHelperMiniMapAngleDetector
{
	private readonly ZContext _context;

	/// <summary>
	/// 初始化生产小地图角度检测器。
	/// </summary>
	public ZContextScreenshotHelperMiniMapAngleDetector(ZContext context)
	{
		_context = context;
	}

	/// <inheritdoc />
	public bool ShouldSaveForMissingAngle(Mat screen)
	{
		return !_context.WorldPatrolService.CutMiniMap(_context, screen).ViewAngle.HasValue;
	}
}
