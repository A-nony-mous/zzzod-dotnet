using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 闪避截图工具业务核心。
/// </summary>
public sealed class ScreenshotHelperService : IDisposable
{
	private readonly ScreenshotHelperConfig _config;

	private readonly IScreenshotHelperCaptureSource _captureSource;

	private readonly IScreenshotHelperImageStore _imageStore;

	private readonly IScreenshotHelperDodgeDetector _dodgeDetector;

	private readonly IScreenshotHelperMiniMapAngleDetector _miniMapAngleDetector;

	private readonly Func<DateTimeOffset> _clock;

	private readonly Func<bool> _canAcceptKey;

	private readonly Queue<ScreenshotHelperFrame> _screenshotCache = new Queue<ScreenshotHelperFrame>();

	private bool _toSaveScreenshot;

	private bool _isSavingAfterKey;

	private DateTimeOffset _lastSaveScreenshotTime = DateTimeOffset.MinValue;

	private bool _disposed;

	/// <summary>
	/// 当前缓存数量。
	/// </summary>
	public int CachedFrameCount => _screenshotCache.Count;

	/// <summary>
	/// 初始化截图助手服务。
	/// </summary>
	public ScreenshotHelperService(ScreenshotHelperConfig config, IScreenshotHelperCaptureSource captureSource, IScreenshotHelperImageStore imageStore, IScreenshotHelperDodgeDetector dodgeDetector, IScreenshotHelperMiniMapAngleDetector miniMapAngleDetector, Func<DateTimeOffset>? clock = null, Func<bool>? canAcceptKey = null)
	{
		_config = config;
		_captureSource = captureSource;
		_imageStore = imageStore;
		_dodgeDetector = dodgeDetector;
		_miniMapAngleDetector = miniMapAngleDetector;
		_clock = clock ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.UtcNow));
		_canAcceptKey = canAcceptKey ?? (() => true);
	}

	/// <summary>
	/// 处理一次按键事件。
	/// </summary>
	public bool HandleKeyPress(string? key)
	{
		if (!_canAcceptKey())
		{
			return false;
		}
		if (_toSaveScreenshot)
		{
			return false;
		}
		DateTimeOffset dateTimeOffset = _clock();
		if (dateTimeOffset - _lastSaveScreenshotTime <= TimeSpan.FromSeconds(1L))
		{
			return false;
		}
		if (!string.Equals(key, _config.KeySave, StringComparison.Ordinal))
		{
			return false;
		}
		_toSaveScreenshot = true;
		return true;
	}

	/// <summary>
	/// 截图并执行缓存、检测和保存。
	/// </summary>
	public ScreenshotHelperTickResult CaptureAndProcess()
	{
		ThrowIfDisposed();
		using ScreenshotHelperFrame screenshotHelperFrame = _captureSource.Capture();
		if ((object)screenshotHelperFrame == null)
		{
			return new ScreenshotHelperTickResult(Captured: false, Array.Empty<ScreenshotHelperSavedImage>(), _config.Frequency, _toSaveScreenshot, _isSavingAfterKey);
		}
		if (!_config.ScreenshotBeforeKey && _toSaveScreenshot && _isSavingAfterKey)
		{
			ClearCache();
			return new ScreenshotHelperTickResult(Captured: true, Array.Empty<ScreenshotHelperSavedImage>(), _config.Frequency, _toSaveScreenshot, _isSavingAfterKey);
		}
		CacheFrame(screenshotHelperFrame);
		List<ScreenshotHelperSavedImage> list = new List<ScreenshotHelperSavedImage>();
		if (_config.MiniMapAngleDetect && _miniMapAngleDetector.ShouldSaveForMissingAngle(screenshotHelperFrame.Image))
		{
			list.Add(_imageStore.Save(screenshotHelperFrame.Image, "mini_map_angle", screenshotHelperFrame.CaptureTimeUtc));
		}
		if (_config.DodgeDetect && (_dodgeDetector.CheckDodgeFlash(screenshotHelperFrame.Image, screenshotHelperFrame.CaptureTimeUtc) || _dodgeDetector.CheckDodgeAudio(screenshotHelperFrame.CaptureTimeUtc)))
		{
			list.Add(_imageStore.Save(screenshotHelperFrame.Image, "dodge", screenshotHelperFrame.CaptureTimeUtc));
		}
		if (_toSaveScreenshot)
		{
			if (_config.ScreenshotBeforeKey)
			{
				list.AddRange(SaveCachedFrames("switch"));
				ClearCache();
				_toSaveScreenshot = false;
				_isSavingAfterKey = false;
				_lastSaveScreenshotTime = _clock();
				return new ScreenshotHelperTickResult(Captured: true, list, TimeSpan.Zero, _toSaveScreenshot, _isSavingAfterKey);
			}
			_isSavingAfterKey = true;
			ClearCache();
			return new ScreenshotHelperTickResult(Captured: true, list, _config.Frequency, _toSaveScreenshot, _isSavingAfterKey);
		}
		TimeSpan timeSpan = _config.Frequency - (_clock() - screenshotHelperFrame.CaptureTimeUtc);
		if (timeSpan < TimeSpan.FromMilliseconds(10L))
		{
			timeSpan = TimeSpan.FromMilliseconds(10L);
		}
		return new ScreenshotHelperTickResult(Captured: true, list, timeSpan, _toSaveScreenshot, _isSavingAfterKey);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			ClearCache();
		}
	}

	private void CacheFrame(ScreenshotHelperFrame frame)
	{
		_screenshotCache.Enqueue(new ScreenshotHelperFrame(frame.CaptureTimeUtc, frame.Image.Clone()));
		while (_screenshotCache.Count > _config.CacheMaxCount)
		{
			_screenshotCache.Dequeue().Dispose();
		}
	}

	private IReadOnlyList<ScreenshotHelperSavedImage> SaveCachedFrames(string prefix)
	{
		List<ScreenshotHelperSavedImage> list = new List<ScreenshotHelperSavedImage>();
		foreach (ScreenshotHelperFrame item in _screenshotCache)
		{
			list.Add(_imageStore.Save(item.Image, prefix, item.CaptureTimeUtc));
		}
		return list;
	}

	private void ClearCache()
	{
		while (_screenshotCache.Count > 0)
		{
			_screenshotCache.Dequeue().Dispose();
		}
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}
}
