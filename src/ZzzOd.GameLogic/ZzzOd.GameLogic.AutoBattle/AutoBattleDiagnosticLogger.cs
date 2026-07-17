using System;
using Serilog;

namespace ZzzOd.GameLogic.AutoBattle;

internal static class AutoBattleDiagnosticLogger
{
	public static void LogFailure(ILogger logger, Exception exception, string message, string detector, string source, double screenshotTime, long? runGeneration = null, double? queueDelayMilliseconds = null)
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		logger.Error(exception, "{Message}: Detector={Detector}, Source={Source}, ScreenshotTime={ScreenshotTime:F3}, FrameAgeMilliseconds={FrameAgeMilliseconds}, RunGeneration={RunGeneration}, QueueDelayMilliseconds={QueueDelayMilliseconds}", message, detector, source, screenshotTime, num - (long)Math.Round(screenshotTime * 1000.0), runGeneration?.ToString() ?? "无", queueDelayMilliseconds.GetValueOrDefault());
	}
}
