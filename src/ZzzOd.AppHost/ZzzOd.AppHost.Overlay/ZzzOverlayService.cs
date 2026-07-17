using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Events;
using OneDragon.Core.Events;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// ZZZ Overlay 服务。
/// </summary>
public sealed class ZzzOverlayService : IZzzOverlayService, IDisposable
{
	private static readonly string[] CoreMetricOrder = new string[5] { "ocr_ms", "yolo_ms", "cv_pipeline_ms", "operation_round_ms", "overlay_refresh_ms" };

	private readonly Lock _lock = new Lock();

	private readonly ZzzRuntimeManager? _runtime;

	private readonly Dictionary<string, ZzzOverlayPerformanceSampleDto> _latestPerformance = new Dictionary<string, ZzzOverlayPerformanceSampleDto>(StringComparer.Ordinal);

	private bool _enabled;

	private ZzzOverlayFrameDto? _lastFrame;

	private ZContext? _attachedContext;

	private IDisposable? _performanceSubscription;

	/// <summary>
	/// 初始化独立 Overlay 服务。
	/// </summary>
	public ZzzOverlayService()
	{
	}

	/// <summary>
	/// 初始化绑定生产运行时的 Overlay 服务。
	/// </summary>
	public ZzzOverlayService(ZzzRuntimeManager runtime)
	{
		_runtime = runtime ?? throw new ArgumentNullException("runtime");
	}

	/// <inheritdoc />
	public ZzzOverlayStatusDto GetStatus()
	{
		EnsureRuntimeAttached();
		using (_lock.EnterScope())
		{
			return new ZzzOverlayStatusDto(_enabled, _lastFrame?.Timestamp, _lastFrame?.Items.Count ?? 0);
		}
	}

	/// <inheritdoc />
	public void SetEnabled(bool enabled)
	{
		using (_lock.EnterScope())
		{
			_enabled = enabled;
		}
	}

	/// <inheritdoc />
	public void SubmitFrame(ZzzOverlayFrameDto frame)
	{
		ArgumentNullException.ThrowIfNull(frame, "frame");
		using (_lock.EnterScope())
		{
			_lastFrame = frame;
		}
	}

	/// <inheritdoc />
	public ZzzOverlayFrameDto? GetLastFrame()
	{
		EnsureRuntimeAttached();
		using (_lock.EnterScope())
		{
			return _lastFrame;
		}
	}

	/// <inheritdoc />
	public void SubmitPerformanceSample(ZzzOverlayPerformanceSampleDto sample)
	{
		ArgumentNullException.ThrowIfNull(sample, "sample");
		ArgumentException.ThrowIfNullOrWhiteSpace(sample.Metric, "sample.Metric");
		ArgumentException.ThrowIfNullOrWhiteSpace(sample.Unit, "sample.Unit");
		if (!double.IsFinite(sample.Value))
		{
			return;
		}
		ZzzOverlayPerformanceSampleDto zzzOverlayPerformanceSampleDto = sample with
		{
			CreatedAt = ((sample.CreatedAt == default(DateTimeOffset)) ? DateTimeOffset.UtcNow : sample.CreatedAt),
			TtlSeconds = Math.Max(0.1, sample.TtlSeconds)
		};
		using (_lock.EnterScope())
		{
			_latestPerformance[zzzOverlayPerformanceSampleDto.Metric] = zzzOverlayPerformanceSampleDto;
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<ZzzOverlayPerformanceSampleDto> GetPerformanceSamples()
	{
		EnsureRuntimeAttached();
		using (_lock.EnterScope())
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			string[] array = (from item in _latestPerformance
				where now - item.Value.CreatedAt > TimeSpan.FromSeconds(item.Value.TtlSeconds)
				select item.Key).ToArray();
			foreach (string key in array)
			{
				_latestPerformance.Remove(key);
			}
			Dictionary<string, int> order = CoreMetricOrder.Select((string metric, int index) => (metric: metric, index: index)).ToDictionary<(string, int), string, int>(((string metric, int index) item) => item.metric, ((string metric, int index) item) => item.index, StringComparer.Ordinal);
			return _latestPerformance.Values.OrderBy((ZzzOverlayPerformanceSampleDto item) => order.TryGetValue(item.Metric, out var value) ? value : int.MaxValue).ThenBy<ZzzOverlayPerformanceSampleDto, string>((ZzzOverlayPerformanceSampleDto item) => item.Metric, StringComparer.Ordinal).ToArray();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		using (_lock.EnterScope())
		{
			_performanceSubscription?.Dispose();
			_performanceSubscription = null;
			_attachedContext = null;
			_latestPerformance.Clear();
		}
	}

	private void EnsureRuntimeAttached()
	{
		ZContext zContext = _runtime?.TryGetContext();
		if (zContext == _attachedContext)
		{
			return;
		}
		using (_lock.EnterScope())
		{
			if (zContext == _attachedContext)
			{
				return;
			}
			_performanceSubscription?.Dispose();
			_performanceSubscription = null;
			_attachedContext = zContext;
			_latestPerformance.Clear();
			if (zContext != null)
			{
				_performanceSubscription = zContext.EventBus.Subscribe("Overlay.Performance", delegate(ContextEvent<PerformanceMetricEventPayload> envelope)
				{
					SubmitPerformanceSample(new ZzzOverlayPerformanceSampleDto(envelope.Payload.Sample.Metric, envelope.Payload.Sample.Value, envelope.Payload.Sample.Unit, envelope.Payload.Sample.CreatedAt, envelope.Payload.Sample.TtlSeconds));
				}, this);
			}
		}
	}
}
