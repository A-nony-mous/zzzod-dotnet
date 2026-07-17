using System;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 使用生产 AutoBattleDodgeContext 执行闪避检测。
/// </summary>
public sealed class ZContextScreenshotHelperDodgeDetector : IScreenshotHelperDodgeDetector
{
	private readonly ZContext _context;

	/// <summary>
	/// 初始化生产闪避检测器。
	/// </summary>
	public ZContextScreenshotHelperDodgeDetector(ZContext context)
	{
		_context = context;
	}

	/// <inheritdoc />
	public bool CheckDodgeFlash(Mat screen, DateTimeOffset captureTimeUtc)
	{
		return _context.AutoBattleContext.DodgeContext.CheckDodgeFlash(screen, (double)captureTimeUtc.ToUnixTimeMilliseconds() / 1000.0);
	}

	/// <inheritdoc />
	public bool CheckDodgeAudio(DateTimeOffset captureTimeUtc)
	{
		return _context.AutoBattleContext.DodgeContext.CheckDodgeAudio((double)captureTimeUtc.ToUnixTimeMilliseconds() / 1000.0);
	}
}
