using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Operation;
using Serilog;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.AutoBattle;

public class AutoBattleDodgeContext
{
	private readonly ZContext _ctx;

	private readonly IAutoBattleFlashClassifier _flashClassifier;

	private readonly IAutoBattleDodgeAudioDetector _audioDetector;

	private readonly object _checkDodgeFlashLock = new object();

	private readonly SemaphoreSlim _dodgeFlashCheckGate = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim _dodgeAudioCheckGate = new SemaphoreSlim(1, 1);

	private readonly object _runGenerationLock = new object();

	private long _runGeneration;

	private long _droppedFlashBecauseBusy;

	private long _droppedAudioBecauseBusy;

	private long _reportedDroppedFlashBecauseBusy;

	private long _reportedDroppedAudioBecauseBusy;

	private long _lastFlashDiagnosticAtMilliseconds;

	private long _lastAudioDiagnosticAtMilliseconds;

	private long _lastFlashBusyDiagnosticAtMilliseconds;

	private long _lastAudioBusyDiagnosticAtMilliseconds;

	private AutoBattleInterval _checkDodgeInterval = new AutoBattleInterval(0.02f, 0.02f);

	private double _lastCheckDodgeTime;

	public AutoBattleInterval CheckDodgeInterval => _checkDodgeInterval;

	public double LastCheckDodgeTime => _lastCheckDodgeTime;

	public long DroppedFlashBecauseBusy => Interlocked.Read(in _droppedFlashBecauseBusy);

	public long DroppedAudioBecauseBusy => Interlocked.Read(in _droppedAudioBecauseBusy);

	private long CurrentRunGeneration
	{
		get
		{
			lock (_runGenerationLock)
			{
				return _runGeneration;
			}
		}
	}

	public AutoBattleDodgeContext(ZContext ctx, IAutoBattleFlashClassifier? flashClassifier = null, IAutoBattleDodgeAudioDetector? audioDetector = null)
	{
		_ctx = ctx;
		_flashClassifier = flashClassifier ?? new ZzzFlashClassifierAdapter(ctx);
		_audioDetector = audioDetector ?? new AutoBattleDodgeAudioDetector();
	}

	public bool TryScheduleDodgeFlashCheck(out long runGeneration)
	{
		if (!_dodgeFlashCheckGate.Wait(0))
		{
			runGeneration = CurrentRunGeneration;
			RecordBusyDrop("闪光", ref _droppedFlashBecauseBusy, ref _reportedDroppedFlashBecauseBusy, ref _lastFlashBusyDiagnosticAtMilliseconds);
			return false;
		}
		runGeneration = CurrentRunGeneration;
		return true;
	}

	public void CompleteDodgeFlashCheck()
	{
		_dodgeFlashCheckGate.Release();
	}

	public bool TryScheduleDodgeAudioCheck(out long runGeneration)
	{
		if (!_dodgeAudioCheckGate.Wait(0))
		{
			runGeneration = CurrentRunGeneration;
			RecordBusyDrop("声音", ref _droppedAudioBecauseBusy, ref _reportedDroppedAudioBecauseBusy, ref _lastAudioBusyDiagnosticAtMilliseconds);
			return false;
		}
		runGeneration = CurrentRunGeneration;
		return true;
	}

	public void CompleteDodgeAudioCheck()
	{
		_dodgeAudioCheckGate.Release();
	}

	public void InitAutoOp(AutoBattleOperator autoOp)
	{
		_checkDodgeInterval = autoOp.CheckDodgeInterval;
		if (_flashClassifier is ZzzFlashClassifierAdapter zzzFlashClassifierAdapter && !zzzFlashClassifierAdapter.InitModel())
		{
			throw new InvalidOperationException("闪光识别模型初始化失败");
		}
	}

	public void InitBattleDodgeContext()
	{
		_lastCheckDodgeTime = 0.0;
		_audioDetector.ResetBattle();
		Interlocked.Exchange(ref _droppedFlashBecauseBusy, 0L);
		Interlocked.Exchange(ref _droppedAudioBecauseBusy, 0L);
		Interlocked.Exchange(ref _reportedDroppedFlashBecauseBusy, 0L);
		Interlocked.Exchange(ref _reportedDroppedAudioBecauseBusy, 0L);
		AdvanceRunGeneration();
	}

	public bool CheckDodgeFlash(object? screen, double screenshotTime, Task<bool>? audioTask = null, long? runGeneration = null, double queueDelayMilliseconds = 0.0)
	{
		AutoBattleFlashCheckResult autoBattleFlashCheckResult = CheckDodgeFlashVisual(screen, screenshotTime, runGeneration, queueDelayMilliseconds);
		if (autoBattleFlashCheckResult.VisualDetected)
		{
			return true;
		}
		if (!autoBattleFlashCheckResult.ShouldConsumeAudio || audioTask == null)
		{
			return false;
		}
		try
		{
			return PublishDodgeAudioResult(audioTask.GetAwaiter().GetResult(), screenshotTime, runGeneration, queueDelayMilliseconds);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "读取声音闪避检测结果失败");
			return false;
		}
	}

	public AutoBattleFlashCheckResult CheckDodgeFlashVisual(object? screen, double screenshotTime, long? runGeneration = null, double queueDelayMilliseconds = 0.0, string source = "unknown")
	{
		if (!IsRunGenerationCurrent(runGeneration) || !Monitor.TryEnter(_checkDodgeFlashLock))
		{
			return new AutoBattleFlashCheckResult(VisualDetected: false, ShouldConsumeAudio: false);
		}
		try
		{
			if (screenshotTime - _lastCheckDodgeTime < (double)_checkDodgeInterval.NextValue())
			{
				return new AutoBattleFlashCheckResult(VisualDetected: false, ShouldConsumeAudio: false);
			}
			_lastCheckDodgeTime = screenshotTime;
			AutoBattleFlashClassification autoBattleFlashClassification = _flashClassifier.Classify(screen);
			int classIndex = autoBattleFlashClassification.ClassIndex;
			if (1 == 0)
			{
			}
			string text = classIndex switch
			{
				1 => YoloStateEventEnum.DODGE_RED.GetDescription(), 
				2 => YoloStateEventEnum.DODGE_YELLOW.GetDescription(), 
				_ => null, 
			};
			if (1 == 0)
			{
			}
			string text2 = text;
			if (text2 == null)
			{
				LogFlashDiagnostic(autoBattleFlashClassification, screenshotTime, queueDelayMilliseconds);
				return new AutoBattleFlashCheckResult(VisualDetected: false, ShouldConsumeAudio: true);
			}
			bool flag = PublishDodgeState(text2, screenshotTime, runGeneration, queueDelayMilliseconds);
			LogFlashDiagnostic(autoBattleFlashClassification, screenshotTime, queueDelayMilliseconds);
			return flag ? new AutoBattleFlashCheckResult(VisualDetected: true, ShouldConsumeAudio: false) : new AutoBattleFlashCheckResult(VisualDetected: false, ShouldConsumeAudio: false);
		}
		catch (Exception exception)
		{
			AutoBattleDiagnosticLogger.LogFailure(_ctx.Logger, exception, "识别画面闪光失败", "Flash", source, screenshotTime, runGeneration, queueDelayMilliseconds);
			return new AutoBattleFlashCheckResult(VisualDetected: false, ShouldConsumeAudio: false);
		}
		finally
		{
			Monitor.Exit(_checkDodgeFlashLock);
		}
	}

	public bool PublishDodgeAudioResult(bool audioDetected, double screenshotTime, long? runGeneration = null, double queueDelayMilliseconds = 0.0)
	{
		return audioDetected && PublishDodgeState(YoloStateEventEnum.DODGE_AUDIO.GetDescription(), screenshotTime, runGeneration, queueDelayMilliseconds);
	}

	public bool CheckDodgeAudio(double screenshotTime, long? runGeneration = null, double queueDelayMilliseconds = 0.0, string source = "unknown")
	{
		if (!IsRunGenerationCurrent(runGeneration))
		{
			return false;
		}
		long timestamp = Stopwatch.GetTimestamp();
		try
		{
			bool flag = _audioDetector.CheckAudio(screenshotTime);
			if (!IsRunGenerationCurrent(runGeneration))
			{
				return false;
			}
			LogAudioDiagnostic(flag, screenshotTime, queueDelayMilliseconds, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
			return flag;
		}
		catch (Exception exception)
		{
			AutoBattleDiagnosticLogger.LogFailure(_ctx.Logger, exception, "识别闪避音频失败", "Audio", source, screenshotTime, runGeneration, queueDelayMilliseconds);
			return false;
		}
	}

	public bool ShouldInterruptForDodge(double now, double withinSeconds = 0.2)
	{
		YoloStateEventEnum[] values = Enum.GetValues<YoloStateEventEnum>();
		foreach (YoloStateEventEnum value in values)
		{
			StateRecorder stateRecorder = _ctx.AutoBattleContext.StateRecordService.GetStateRecorder(value.GetDescription());
			if (stateRecorder != null && stateRecorder.LastRecordTime > 0.0 && now - stateRecorder.LastRecordTime <= withinSeconds)
			{
				return true;
			}
		}
		return false;
	}

	public void StartContextAsync()
	{
		AdvanceRunGeneration();
		_audioDetector.Start();
	}

	public void StopContext()
	{
		AdvanceRunGeneration();
		_audioDetector.Stop();
	}

	public void AfterAppShutdown()
	{
		StopContext();
	}

	private void AdvanceRunGeneration()
	{
		lock (_runGenerationLock)
		{
			_runGeneration++;
		}
	}

	private bool IsRunGenerationCurrent(long? runGeneration)
	{
		if (!runGeneration.HasValue)
		{
			return true;
		}
		lock (_runGenerationLock)
		{
			return _runGeneration == runGeneration.Value;
		}
	}

	private bool TryPublishState(string stateName, double screenshotTime, long? runGeneration)
	{
		lock (_runGenerationLock)
		{
			if (runGeneration.HasValue && _runGeneration != runGeneration.Value)
			{
				return false;
			}
			_ctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord(stateName, screenshotTime));
			return true;
		}
	}

	private bool PublishDodgeState(string stateName, double screenshotTime, long? runGeneration, double queueDelayMilliseconds)
	{
		if (!TryPublishState(stateName, screenshotTime, runGeneration))
		{
			return false;
		}
		_ctx.Logger.Information("识别到闪避提示: State={State}, ScreenshotTime={ScreenshotTime}, FrameAgeMilliseconds={FrameAgeMilliseconds}, QueueDelayMilliseconds={QueueDelayMilliseconds}", stateName, screenshotTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)(screenshotTime * 1000.0), queueDelayMilliseconds);
		return true;
	}

	private void RecordBusyDrop(string detector, ref long counter, ref long reportedCounter, ref long lastDiagnosticAtMilliseconds)
	{
		long num = Interlocked.Increment(ref counter);
		long num2 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long num3 = Interlocked.Read(in lastDiagnosticAtMilliseconds);
		if (num2 - num3 >= 500 && Interlocked.CompareExchange(ref lastDiagnosticAtMilliseconds, num2, num3) == num3)
		{
			long num4 = Interlocked.Exchange(ref reportedCounter, num);
			_ctx.Logger.Information("自动战斗{Detector}检测繁忙: DroppedBecauseBusy={DroppedBecauseBusy}, WindowMilliseconds={WindowMilliseconds}", detector, num - num4, (num3 == 0L) ? 500 : (num2 - num3));
		}
	}

	private void LogFlashDiagnostic(AutoBattleFlashClassification classification, double screenshotTime, double queueDelayMilliseconds)
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long num2 = Interlocked.Read(in _lastFlashDiagnosticAtMilliseconds);
		int classIndex = classification.ClassIndex;
		bool flag = (uint)(classIndex - 1) <= 1u;
		if (flag || (num - num2 >= 1000 && Interlocked.CompareExchange(ref _lastFlashDiagnosticAtMilliseconds, num, num2) == num2))
		{
			Interlocked.Exchange(ref _lastFlashDiagnosticAtMilliseconds, num);
			_ctx.Logger.Information("自动战斗闪光检测: ClassIndex={ClassIndex}, CaptureAgeMilliseconds={CaptureAgeMilliseconds}, QueueDelayMilliseconds={QueueDelayMilliseconds}, ColorConversionElapsedMilliseconds={ColorConversionElapsedMilliseconds:F2}, PreprocessElapsedMilliseconds={PreprocessElapsedMilliseconds:F2}, InferenceElapsedMilliseconds={InferenceElapsedMilliseconds:F2}, PostprocessElapsedMilliseconds={PostprocessElapsedMilliseconds:F2}, TotalElapsedMilliseconds={TotalElapsedMilliseconds:F2}, DroppedBecauseBusy={DroppedBecauseBusy}", classification.ClassIndex, num - (long)(screenshotTime * 1000.0), queueDelayMilliseconds, classification.ColorConversionElapsedMilliseconds, classification.PreprocessElapsedMilliseconds, classification.InferenceElapsedMilliseconds, classification.PostprocessElapsedMilliseconds, classification.TotalElapsedMilliseconds, DroppedFlashBecauseBusy);
		}
	}

	private void LogAudioDiagnostic(bool detected, double screenshotTime, double queueDelayMilliseconds, double totalElapsedMilliseconds)
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long num2 = Interlocked.Read(in _lastAudioDiagnosticAtMilliseconds);
		if (detected || (num - num2 >= 1000 && Interlocked.CompareExchange(ref _lastAudioDiagnosticAtMilliseconds, num, num2) == num2))
		{
			Interlocked.Exchange(ref _lastAudioDiagnosticAtMilliseconds, num);
			_ctx.Logger.Information("自动战斗声音检测: Detected={Detected}, CaptureAgeMilliseconds={CaptureAgeMilliseconds}, QueueDelayMilliseconds={QueueDelayMilliseconds}, TotalElapsedMilliseconds={TotalElapsedMilliseconds:F2}, DroppedBecauseBusy={DroppedBecauseBusy}", detected, num - (long)(screenshotTime * 1000.0), queueDelayMilliseconds, totalElapsedMilliseconds, DroppedAudioBecauseBusy);
		}
	}
}
