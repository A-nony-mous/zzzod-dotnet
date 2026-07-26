using System;
using System.Threading.Tasks;
using OneDragon.Core.Utils;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地战斗中的识别证据（YOLO 交互/距离/入口 + 前往下一个区域 OCR）。
/// </summary>
internal sealed record LostVoidInBattleProbeResult(
	DateTimeOffset FrameTimeUtc,
	bool NoLongerInBattleByDetection,
	bool NextRegionHint,
	string Detect,
	double DetectorElapsedMilliseconds,
	bool DetectorRan);

/// <summary>
/// 迷失之地战斗轮次的异步识别探测器。
/// 把耗时的 YOLO 与 OCR 移出战斗轮关键路径：轮次只负责调度与消费缓存结果，
/// 从而让同一轮里提交的角色状态识别保持高频供帧。
/// 语义约束：同一时刻只有一个探测在飞行；结果按帧序消费且只消费一次。
/// </summary>
internal sealed class LostVoidInBattleProbe
{
	private static readonly DedicatedTaskScheduler ProbeScheduler = new DedicatedTaskScheduler("zzz-lost-void-probe", 1);

	private static readonly TaskFactory ProbeExecutor = new TaskFactory(ProbeScheduler);

	private readonly TimeSpan _minInterval;

	private readonly Action<Action>? _dispatchOverride;

	private readonly object _lock = new object();

	private bool _inFlight;

	private DateTimeOffset? _lastScheduledFrameUtc;

	private DateTimeOffset? _lastConsumedFrameUtc;

	private LostVoidInBattleProbeResult? _pending;

	/// <param name="minInterval">两次探测之间的最小间隔，对齐参考实现迷失之地战斗中 0.8 秒的检测节流。</param>
	/// <param name="dispatchOverride">测试用的同步执行入口；为空时投递到专用调度线程。</param>
	internal LostVoidInBattleProbe(TimeSpan? minInterval = null, Action<Action>? dispatchOverride = null)
	{
		_minInterval = minInterval ?? TimeSpan.FromMilliseconds(800L);
		_dispatchOverride = dispatchOverride;
	}

	/// <summary>
	/// 距离上次调度已超过节流间隔且没有在飞行的探测时，调度一次新探测。
	/// </summary>
	/// <returns>本次是否调度了探测。</returns>
	internal bool TrySchedule(DateTimeOffset frameTimeUtc, Func<LostVoidInBattleProbeResult?> work, TimeSpan? minIntervalOverride = null)
	{
		ArgumentNullException.ThrowIfNull(work, "work");
		TimeSpan minInterval = minIntervalOverride ?? _minInterval;
		lock (_lock)
		{
			if (_inFlight)
			{
				return false;
			}
			if (_lastScheduledFrameUtc.HasValue && frameTimeUtc - _lastScheduledFrameUtc.Value < minInterval)
			{
				return false;
			}
			_inFlight = true;
			_lastScheduledFrameUtc = frameTimeUtc;
		}
		Action run = delegate
		{
			LostVoidInBattleProbeResult? result = null;
			try
			{
				result = work();
			}
			finally
			{
				Publish(result);
			}
		};
		if (_dispatchOverride != null)
		{
			_dispatchOverride(run);
		}
		else
		{
			ProbeExecutor.StartNew(run);
		}
		return true;
	}

	/// <summary>
	/// 取出尚未消费的探测结果；同一结果不会被消费两次，早于已消费帧的结果直接丢弃。
	/// </summary>
	internal bool TryConsume(out LostVoidInBattleProbeResult result)
	{
		lock (_lock)
		{
			LostVoidInBattleProbeResult? pending = _pending;
			_pending = null;
			if (pending == null)
			{
				result = null!;
				return false;
			}
			if (_lastConsumedFrameUtc.HasValue && pending.FrameTimeUtc <= _lastConsumedFrameUtc.Value)
			{
				result = null!;
				return false;
			}
			_lastConsumedFrameUtc = pending.FrameTimeUtc;
			result = pending;
			return true;
		}
	}

	/// <summary>
	/// 战斗开始/结束时清空探测状态，避免上一场战斗的结果泄漏到下一场。
	/// </summary>
	internal void Reset()
	{
		lock (_lock)
		{
			_pending = null;
			_lastScheduledFrameUtc = null;
			_lastConsumedFrameUtc = null;
		}
	}

	private void Publish(LostVoidInBattleProbeResult? result)
	{
		lock (_lock)
		{
			_inFlight = false;
			if (result != null && (!_lastConsumedFrameUtc.HasValue || result.FrameTimeUtc > _lastConsumedFrameUtc.Value))
			{
				_pending = result;
			}
		}
	}
}
