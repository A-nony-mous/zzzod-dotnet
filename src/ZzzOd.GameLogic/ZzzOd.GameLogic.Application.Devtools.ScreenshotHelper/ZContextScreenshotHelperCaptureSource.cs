using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 从 ZContext 控制器获取截图。
/// </summary>
public sealed class ZContextScreenshotHelperCaptureSource : IScreenshotHelperCaptureSource
{
	private readonly ZContext _context;

	/// <summary>
	/// 初始化截图来源。
	/// </summary>
	public ZContextScreenshotHelperCaptureSource(ZContext context)
	{
		_context = context;
	}

	/// <inheritdoc />
	public ScreenshotHelperFrame? Capture()
	{
		if (_context.Controller == null)
		{
			return null;
		}
		var (captureTimeUtc, mat) = _context.Controller.Screenshot();
		return (mat == null) ? null : new ScreenshotHelperFrame(captureTimeUtc, mat);
	}
}
