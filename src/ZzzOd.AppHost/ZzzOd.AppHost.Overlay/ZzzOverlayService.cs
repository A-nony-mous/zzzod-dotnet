using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Events;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Events;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// 将运行时调试事件聚合为 GUI 可读取的 Overlay 快照。
/// </summary>
public sealed class ZzzOverlayService : IZzzOverlayService, IDisposable
{
	private const int LogCapacity = 500;

	private const int BackendEventDrainBatchSize = 128;

	private static readonly string[] CoreMetricOrder = ["ocr_ms", "yolo_ms", "cv_pipeline_ms", "operation_round_ms", "overlay_refresh_ms"];

	private readonly Lock _lock = new();

	private readonly Lock _backendDrainLock = new();

	private readonly ZzzRuntimeManager? _runtime;

	private readonly ZzzBackendEventBus? _backendEventBus;

	private readonly ZzzLogFanOutLoggerProvider? _logProvider;

	private readonly Dictionary<string, ZzzOverlayPerformanceSampleDto> _latestPerformance = new(StringComparer.Ordinal);

	private readonly List<ZzzOverlayLogEntryDto> _logs = [];

	private readonly List<IDisposable> _contextSubscriptions = [];

	private ZzzOverlayDisplayOptionsDto _displayOptions = new();

	private ChannelReader<ZzzBackendEvent>? _backendEventReader;

	private ZContext? _attachedContext;

	private DateTimeOffset _runStateUpdatedAt;

	private bool _enabled;

	private bool _disposed;

	/// <summary>
	/// 初始化独立 Overlay 服务。
	/// </summary>
	public ZzzOverlayService()
		: this(null, null, null, initialize: true)
	{
	}

	/// <summary>
	/// 初始化绑定生产运行时的 Overlay 服务。
	/// </summary>
	public ZzzOverlayService(ZzzRuntimeManager runtime)
		: this(runtime ?? throw new ArgumentNullException(nameof(runtime)), null, null, initialize: true)
	{
	}

	/// <summary>
	/// 初始化绑定生产运行时、后端事件和日志 provider 的 Overlay 服务。
	/// </summary>
	public ZzzOverlayService(
		ZzzRuntimeManager runtime,
		ZzzBackendEventBus backendEventBus,
		ZzzLogFanOutLoggerProvider logProvider)
		: this(
			runtime ?? throw new ArgumentNullException(nameof(runtime)),
			backendEventBus ?? throw new ArgumentNullException(nameof(backendEventBus)),
			logProvider ?? throw new ArgumentNullException(nameof(logProvider)),
			initialize: true)
	{
	}

	/// <inheritdoc />
	public ZzzOverlayStatusDto GetStatus()
	{
		ZzzOverlaySnapshotDto snapshot = GetSnapshot();
		return new ZzzOverlayStatusDto(snapshot.Enabled, snapshot.VisionFrame?.Timestamp, snapshot.VisionFrame?.Items.Count ?? 0);
	}

	/// <inheritdoc />
	public void SetEnabled(bool enabled)
	{
		using (_lock.EnterScope())
		{
			if (!_disposed)
			{
				_enabled = enabled;
			}
		}
	}

	/// <inheritdoc />
	public void ConfigureDisplay(ZzzOverlayDisplayOptionsDto options)
	{
		ArgumentNullException.ThrowIfNull(options);
		using (_lock.EnterScope())
		{
			if (!_disposed)
			{
				_displayOptions = CopyOptions(options);
			}
		}
	}

	/// <inheritdoc />
	public ZzzOverlayFrameDto? GetLastFrame() => GetSnapshot().VisionFrame;

	/// <inheritdoc />
	public void SubmitPerformanceSample(ZzzOverlayPerformanceSampleDto sample)
	{
		ArgumentNullException.ThrowIfNull(sample);
		StorePerformanceSample(sample);
	}

	/// <inheritdoc />
	public IReadOnlyList<ZzzOverlayPerformanceSampleDto> GetPerformanceSamples() => GetSnapshot().Performance;

	/// <inheritdoc />
	public ZzzOverlaySnapshotDto GetSnapshot()
	{
		for (int attempt = 0; attempt < 2; attempt++)
		{
			EnsureRuntimeAttached();
			DrainBackendEvents();
			DateTimeOffset now = DateTimeOffset.UtcNow;
			ZContext? context;
			ZzzOverlayDisplayOptionsDto options;
			ImmutableArray<ZzzOverlayLogEntryDto> logs;
			bool enabled;
			DateTimeOffset runStateUpdatedAt;
			using (_lock.EnterScope())
			{
				context = _attachedContext;
				options = _displayOptions;
				logs = GetVisibleLogsUnsafe(now, options);
				enabled = _enabled;
				runStateUpdatedAt = _runStateUpdatedAt;
			}

			if (!IsCurrentRuntimeContext(context))
			{
				ReleaseContextIfCurrent(context);
				continue;
			}

			OverlayDebugSnapshot debugSnapshot = context?.OverlayDebugBus.Snapshot(now) ?? EmptyDebugSnapshot();
			ZzzOverlaySnapshotDto snapshot = BuildSnapshot(context, debugSnapshot, enabled, options, logs, runStateUpdatedAt, now);
			using (_lock.EnterScope())
			{
				if (ReferenceEquals(context, _attachedContext) && IsCurrentRuntimeContext(context))
				{
					return snapshot;
				}
			}
		}

		using (_lock.EnterScope())
		{
			return new ZzzOverlaySnapshotDto(
				DateTimeOffset.UtcNow,
				_enabled,
				null,
				null,
				ImmutableArray<ZzzOverlayOperationDto>.Empty,
				ImmutableArray<ZzzOverlayDecisionDto>.Empty,
				ImmutableArray<ZzzOverlayTimelineItemDto>.Empty,
				ImmutableArray<ZzzOverlayPerformanceSampleDto>.Empty,
				ImmutableArray<ZzzOverlayLogEntryDto>.Empty);
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		ChannelReader<ZzzBackendEvent>? eventReader;
		using (_lock.EnterScope())
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			ReleaseAttachedContextUnsafe();
			_latestPerformance.Clear();
			_logs.Clear();
			eventReader = _backendEventReader;
			_backendEventReader = null;
		}

		if (eventReader is not null)
		{
			_backendEventBus?.Unsubscribe(eventReader);
		}

		if (_runtime is not null)
		{
			_runtime.ContextChanged -= OnRuntimeContextChanged;
		}
	}

	private ZzzOverlayService(
		ZzzRuntimeManager? runtime,
		ZzzBackendEventBus? backendEventBus,
		ZzzLogFanOutLoggerProvider? logProvider,
		bool initialize)
	{
		_runtime = runtime;
		_backendEventBus = backendEventBus;
		_logProvider = logProvider;
		if (runtime is not null)
		{
			runtime.ContextChanged += OnRuntimeContextChanged;
		}
		if (backendEventBus is not null)
		{
			_backendEventReader = backendEventBus.Subscribe("log.appended", LogCapacity);
		}

		if (logProvider is not null)
		{
			AppendLogs(logProvider.GetRecent(LogCapacity));
		}
	}

	private void EnsureRuntimeAttached()
	{
		for (int attempt = 0; attempt < 2; attempt++)
		{
			ZContext? context = _runtime?.TryGetContext();
			using (_lock.EnterScope())
			{
				if (_disposed || ReferenceEquals(context, _attachedContext))
				{
					return;
				}

				bool replacingAttachedContext = _attachedContext is not null;
				ReleaseAttachedContextUnsafe(clearLogs: replacingAttachedContext);
				_attachedContext = context;
				_runStateUpdatedAt = DateTimeOffset.UtcNow;
				if (context is not null)
				{
					AttachContextUnsafe(context);
				}
			}

			if (_runtime is null || ReferenceEquals(context, _runtime.TryGetContext()))
			{
				return;
			}

			using (_lock.EnterScope())
			{
				if (ReferenceEquals(context, _attachedContext))
				{
					ReleaseAttachedContextUnsafe(clearLogs: true);
				}
			}
		}
	}

	private void OnRuntimeContextChanged(ZContext? _, ZContext? __)
	{
		ZContext? activeContext = _runtime?.TryGetContext();
		using (_lock.EnterScope())
		{
			if (!_disposed && !ReferenceEquals(_attachedContext, activeContext))
			{
				ReleaseAttachedContextUnsafe(clearLogs: _attachedContext is not null);
			}
		}
	}

	private bool IsCurrentRuntimeContext(ZContext? context) =>
		_runtime is null || ReferenceEquals(context, _runtime.TryGetContext());

	private void ReleaseContextIfCurrent(ZContext? context)
	{
		using (_lock.EnterScope())
		{
			if (ReferenceEquals(context, _attachedContext))
			{
				ReleaseAttachedContextUnsafe(clearLogs: _attachedContext is not null);
			}
		}
	}

	private void AttachContextUnsafe(ZContext context)
	{
		TryAddSubscription(() => context.EventBus.Subscribe<VisionDrawEventPayload>("Overlay.Vision", envelope => OnVisionEvent(context, envelope), this));
		TryAddSubscription(() => context.RunContext.EventBus.Subscribe<ApplicationRunContextStateEvent>(nameof(ApplicationRunContextStateEvent), envelope => OnRunStateEvent(context, envelope), this));
		TryAddSubscription(() => context.RunContext.EventBus.Subscribe<ApplicationRunContextSnapshot>(ApplicationRunContextEventIds.SnapshotChanged, envelope => OnRunContextSnapshotEvent(context, envelope), this));
	}

	private void TryAddSubscription(Func<IDisposable> subscribe)
	{
		try
		{
			_contextSubscriptions.Add(subscribe());
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void ReleaseAttachedContextUnsafe(bool clearLogs = false)
	{
		foreach (IDisposable subscription in _contextSubscriptions)
		{
			subscription.Dispose();
		}

		_contextSubscriptions.Clear();
		_attachedContext = null;
		_runStateUpdatedAt = default;
		_latestPerformance.Clear();
		if (clearLogs)
		{
			_logs.Clear();
		}
	}

	private void OnVisionEvent(ZContext sourceContext, ContextEvent<VisionDrawEventPayload> envelope)
	{
		if (!IsAttached(sourceContext))
		{
			return;
		}
		if (envelope.Payload.AlreadyPublishedToDebugBus)
		{
			return;
		}

		foreach (VisionDrawItem item in envelope.Payload.Items)
		{
			sourceContext.OverlayDebugBus.PublishVision(item);
		}
	}

	private void OnRunStateEvent(ZContext sourceContext, ContextEvent<ApplicationRunContextStateEvent> _)
	{
		using (_lock.EnterScope())
		{
			if (ReferenceEquals(sourceContext, _attachedContext))
			{
				_runStateUpdatedAt = DateTimeOffset.UtcNow;
			}
		}
	}

	private void OnRunContextSnapshotEvent(ZContext sourceContext, ContextEvent<ApplicationRunContextSnapshot> envelope)
	{
		using (_lock.EnterScope())
		{
			if (ReferenceEquals(sourceContext, _attachedContext))
			{
				_runStateUpdatedAt = envelope.Payload.UpdatedAt;
			}
		}
	}

	private bool IsAttached(ZContext context)
	{
		using (_lock.EnterScope())
		{
			return !_disposed && ReferenceEquals(context, _attachedContext);
		}
	}

	private void StorePerformanceSample(ZzzOverlayPerformanceSampleDto sample)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sample.Metric);
		ArgumentException.ThrowIfNullOrWhiteSpace(sample.Unit);
		if (!double.IsFinite(sample.Value))
		{
			return;
		}

		ZzzOverlayPerformanceSampleDto normalized = sample with
		{
			CreatedAt = sample.CreatedAt == default ? DateTimeOffset.UtcNow : sample.CreatedAt,
			TtlSeconds = NormalizePositiveFinite(sample.TtlSeconds, 20d)
		};
		using (_lock.EnterScope())
		{
			if (_disposed)
			{
				return;
			}

			_latestPerformance[normalized.Metric] = normalized;
		}
	}

	private void DrainBackendEvents()
	{
		using (_backendDrainLock.EnterScope())
		{
			ChannelReader<ZzzBackendEvent>? reader;
			using (_lock.EnterScope())
			{
				reader = _backendEventReader;
			}

			if (reader is null)
			{
				return;
			}

			for (int count = 0; count < BackendEventDrainBatchSize && reader.TryRead(out ZzzBackendEvent? backendEvent); count++)
			{
				if (string.Equals(backendEvent.Type, "log.appended", StringComparison.Ordinal) && backendEvent.Data is ZzzLogEntryDto entry)
				{
					AppendLog(new ZzzOverlayLogEntryDto(entry.Timestamp, entry.Level, entry.Category, entry.Message, entry.Exception));
				}
			}
		}
	}

	private void AppendLogs(IEnumerable<ZzzLogEntryDto> entries)
	{
		foreach (ZzzLogEntryDto entry in entries)
		{
			AppendLog(new ZzzOverlayLogEntryDto(entry.Timestamp, entry.Level, entry.Category, entry.Message, entry.Exception));
		}
	}

	private void AppendLog(ZzzOverlayLogEntryDto entry)
	{
		using (_lock.EnterScope())
		{
			if (_disposed || _logs.Contains(entry))
			{
				return;
			}

			_logs.Add(entry);
			int removeCount = _logs.Count - LogCapacity;
			if (removeCount > 0)
			{
				_logs.RemoveRange(0, removeCount);
			}
		}
	}

	private ZzzOverlaySnapshotDto BuildSnapshot(
		ZContext? context,
		OverlayDebugSnapshot debugSnapshot,
		bool enabled,
		ZzzOverlayDisplayOptionsDto options,
		ImmutableArray<ZzzOverlayLogEntryDto> logs,
		DateTimeOffset runStateUpdatedAt,
		DateTimeOffset now)
	{
		ImmutableArray<ZzzOverlayDrawItemDto> visionItems = options.VisionLayerEnabled
			? DeduplicateVision(debugSnapshot.VisionItems, options.YoloDedupIouThreshold)
				.Where(item => IsVisionSourceEnabled(item.Source, options))
				.Select(MapVision)
				.ToImmutableArray()
			: ImmutableArray<ZzzOverlayDrawItemDto>.Empty;
		ZzzOverlayFrameDto? visionFrame = visionItems.IsDefaultOrEmpty ? null : new ZzzOverlayFrameDto(now, visionItems);

		ZzzOverlayRunStateDto? state = IsPanelEnabled(options, "state") && context is not null
			? MapRunState(context, debugSnapshot.RunContext, debugSnapshot.OperationItems, runStateUpdatedAt == default ? now : runStateUpdatedAt)
			: null;
		ImmutableArray<ZzzOverlayOperationDto> operations = IsPanelEnabled(options, "state")
			? debugSnapshot.OperationItems.Select(MapOperation).ToImmutableArray()
			: ImmutableArray<ZzzOverlayOperationDto>.Empty;
		ImmutableArray<ZzzOverlayDecisionDto> decisions = IsPanelEnabled(options, "decision")
			? debugSnapshot.DecisionItems.Select(MapDecision).ToImmutableArray()
			: ImmutableArray<ZzzOverlayDecisionDto>.Empty;
		ImmutableArray<ZzzOverlayTimelineItemDto> timeline = IsPanelEnabled(options, "timeline")
			? debugSnapshot.TimelineItems.Select(MapTimeline).ToImmutableArray()
			: ImmutableArray<ZzzOverlayTimelineItemDto>.Empty;
		ImmutableArray<ZzzOverlayPerformanceSampleDto> performance = IsPanelEnabled(options, "performance")
			? CollectPerformance(debugSnapshot.PerformanceItems, now, options)
			: ImmutableArray<ZzzOverlayPerformanceSampleDto>.Empty;
		ImmutableArray<ZzzOverlayLogEntryDto> visibleLogs = IsPanelEnabled(options, "log")
			? logs
			: ImmutableArray<ZzzOverlayLogEntryDto>.Empty;

		return new ZzzOverlaySnapshotDto(now, enabled, visionFrame, state, operations, decisions, timeline, performance, visibleLogs);
	}

	private ImmutableArray<ZzzOverlayPerformanceSampleDto> CollectPerformance(
		IReadOnlyList<PerformanceMetricSample> debugSamples,
		DateTimeOffset now,
		ZzzOverlayDisplayOptionsDto options)
	{
		Dictionary<string, ZzzOverlayPerformanceSampleDto> samples = new(StringComparer.Ordinal);
		foreach (PerformanceMetricSample sample in debugSamples)
		{
			if (!double.IsFinite(sample.Value) || IsExpired(sample.CreatedAt, sample.TtlSeconds, now))
			{
				continue;
			}

			ZzzOverlayPerformanceSampleDto mapped = new(sample.Metric, sample.Value, sample.Unit, sample.CreatedAt, NormalizePositiveFinite(sample.TtlSeconds, 20d));
			if (!samples.TryGetValue(mapped.Metric, out ZzzOverlayPerformanceSampleDto? current) || mapped.CreatedAt >= current.CreatedAt)
			{
				samples[mapped.Metric] = mapped;
			}
		}

		using (_lock.EnterScope())
		{
			foreach ((string metric, ZzzOverlayPerformanceSampleDto sample) in _latestPerformance.ToArray())
			{
				if (IsExpired(sample.CreatedAt, sample.TtlSeconds, now))
				{
					_latestPerformance.Remove(metric);
					continue;
				}

				if (!samples.TryGetValue(metric, out ZzzOverlayPerformanceSampleDto? current) || sample.CreatedAt >= current.CreatedAt)
				{
					samples[metric] = sample;
				}
			}
		}

		Dictionary<string, int> order = CoreMetricOrder
			.Select((metric, index) => (metric, index))
			.ToDictionary(item => item.metric, item => item.index, StringComparer.Ordinal);
		return samples.Values
			.Where(sample => IsMetricEnabled(options, sample.Metric))
			.OrderBy(sample => order.TryGetValue(sample.Metric, out int index) ? index : int.MaxValue)
			.ThenBy(sample => sample.Metric, StringComparer.Ordinal)
			.ToImmutableArray();
	}

	private ImmutableArray<ZzzOverlayLogEntryDto> GetVisibleLogsUnsafe(DateTimeOffset now, ZzzOverlayDisplayOptionsDto options)
	{
		double fadeSeconds = NormalizePositiveFinite(options.LogFadeSeconds, 12d);
		int maxLines = Math.Clamp(options.LogMaxLines, 1, LogCapacity);
		return _logs
			.Where(entry => (now - entry.Timestamp).TotalSeconds <= fadeSeconds)
			.TakeLast(maxLines)
			.ToImmutableArray();
	}

	private static ZzzOverlayRunStateDto MapRunState(
		ZContext context,
		ApplicationRunContextSnapshot? runContextSnapshot,
		IReadOnlyList<OperationTraceItem> operations,
		DateTimeOffset updatedAt)
	{
		ApplicationRunContextSnapshot effectiveSnapshot = runContextSnapshot ?? new ApplicationRunContextSnapshot(
			context.RunContext.State,
			context.RunContext.CurrentAppId,
			context.RunContext.CurrentInstanceIndex,
			context.RunContext.CurrentGroupId,
			updatedAt);
		string? currentAppId = effectiveSnapshot.AppId;
		string? currentApp = currentAppId;
		if (!string.IsNullOrWhiteSpace(currentAppId) && context.RunContext.IsAppRegistered(currentAppId))
		{
			try
			{
				currentApp = context.RunContext.GetApplicationName(currentAppId);
			}
			catch (InvalidOperationException)
			{
			}
		}

		OperationTraceItem? operation = effectiveSnapshot.State is ApplicationRunContextState.Running or ApplicationRunContextState.Pause
			? operations
				.Where(item => string.IsNullOrWhiteSpace(currentAppId) || string.Equals(item.AppId, currentAppId, StringComparison.Ordinal))
				.OrderByDescending(item => item.CreatedAt)
				.FirstOrDefault()
			: null;
		return new ZzzOverlayRunStateDto(
			effectiveSnapshot.State.ToString(),
			currentAppId,
			currentApp,
			operation?.CurrentNode,
			operation?.PreviousNode,
			operation is null ? null : operation.RetryCount,
			effectiveSnapshot.GroupId,
			effectiveSnapshot.InstanceIndex,
			effectiveSnapshot.UpdatedAt == default ? updatedAt : effectiveSnapshot.UpdatedAt,
			MapAutoBattleState(context.TryGetAutoBattleOverlayStatus()));
	}

	private static ZzzOverlayAutoBattleStateDto? MapAutoBattleState(AutoBattleOverlayStatusSnapshot? snapshot) =>
		snapshot is null
			? null
			: new ZzzOverlayAutoBattleStateDto(
				snapshot.IsRunning,
				snapshot.FrontAgentName,
				snapshot.FrontSpecialReady,
				snapshot.FrontUltimateReady,
				snapshot.LatestDodgeState,
				snapshot.ChainReady,
				snapshot.LatestQuickAssistAgent,
				snapshot.DistanceMeters);

	private static ZzzOverlayDrawItemDto MapVision(VisionDrawItem item)
	{
		string? text = string.IsNullOrWhiteSpace(item.Label)
			? null
			: item.Score.HasValue
				? $"{item.Label} {item.Score.Value:F2}"
				: item.Label;
		return new ZzzOverlayDrawItemDto(
			ZzzOverlayDrawItemKind.VisionDrawItem,
			$"{item.Source}:{item.Label}",
			new ZzzOverlayRectDto(item.X1, item.Y1, Math.Max(0, item.X2 - item.X1), Math.Max(0, item.Y2 - item.Y1)),
			text,
			item.Color,
			CopyMetadata(item.Metadata));
	}

	private static ZzzOverlayOperationDto MapOperation(OperationTraceItem item) => new(
		item.AppId,
		item.Operation,
		item.CurrentNode,
		item.PreviousNode,
		item.NextNode,
		item.RetryCount,
		item.ResultKind,
		item.Status,
		item.CreatedAt);

	private static ZzzOverlayDecisionDto MapDecision(DecisionTraceItem item) => new(
		item.Source,
		item.Trigger,
		item.Expression,
		item.Operation,
		item.Status,
		item.CreatedAt,
		CopyMetadata(item.Metadata));

	private static ZzzOverlayTimelineItemDto MapTimeline(TimelineItem item) => new(
		item.Category,
		item.Title,
		item.Detail,
		item.Level,
		item.CreatedAt,
		CopyMetadata(item.Metadata));

	private static IEnumerable<VisionDrawItem> DeduplicateVision(
		IReadOnlyList<VisionDrawItem> items,
		double yoloDedupIouThreshold)
	{
		List<VisionDrawItem> result = [];
		foreach (IGrouping<string, VisionDrawItem> sourceGroup in items.GroupBy(item => item.Source, StringComparer.Ordinal))
		{
			if (!IsYoloSource(sourceGroup.Key))
			{
				HashSet<string> exactItems = new(StringComparer.Ordinal);
				foreach (VisionDrawItem item in sourceGroup)
				{
					string key = $"{item.Label}|{item.X1}|{item.Y1}|{item.X2}|{item.Y2}|{item.Score?.ToString("R", CultureInfo.InvariantCulture)}|{item.Created.ToString("R", CultureInfo.InvariantCulture)}";
					if (exactItems.Add(key))
					{
						result.Add(item);
					}
				}

				continue;
			}

			foreach (IGrouping<string, VisionDrawItem> labelGroup in sourceGroup.GroupBy(item => item.Label, StringComparer.Ordinal))
			{
				List<VisionDrawItem> retained = [];
				foreach (VisionDrawItem item in labelGroup
					.Select((item, index) => (item, index))
					.OrderByDescending(pair => pair.item.Created)
					.ThenByDescending(pair => pair.index)
					.Select(pair => pair.item))
				{
					if (retained.All(existing => CalculateIou(existing, item) < yoloDedupIouThreshold))
					{
						retained.Add(item);
					}
				}

				result.AddRange(retained.OrderBy(item => item.Created));
			}
		}

		return result.OrderBy(item => item.Created);
	}

	private static bool IsVisionSourceEnabled(string source, ZzzOverlayDisplayOptionsDto options) =>
		!IsYoloSource(source) ? source.Contains("ocr", StringComparison.OrdinalIgnoreCase) ? options.ShowOcr : source.Contains("template", StringComparison.OrdinalIgnoreCase) ? options.ShowTemplate : source.Contains("cv", StringComparison.OrdinalIgnoreCase) ? options.ShowCv : true : options.ShowYolo;

	private static bool IsYoloSource(string source) => source.Contains("yolo", StringComparison.OrdinalIgnoreCase);

	private static bool IsPanelEnabled(ZzzOverlayDisplayOptionsDto options, string panelId) =>
		!options.PanelEnabledMap.TryGetValue(panelId, out bool enabled) || enabled;

	private static bool IsMetricEnabled(ZzzOverlayDisplayOptionsDto options, string metric) =>
		!options.PerformanceMetricEnabledMap.TryGetValue(metric, out bool enabled) || enabled;

	private static bool IsExpired(DateTimeOffset createdAt, double ttlSeconds, DateTimeOffset now) =>
		createdAt == default || (now - createdAt).TotalSeconds > NormalizePositiveFinite(ttlSeconds, 20d);

	private static double CalculateIou(VisionDrawItem left, VisionDrawItem right)
	{
		double intersectionLeft = Math.Max(left.X1, right.X1);
		double intersectionTop = Math.Max(left.Y1, right.Y1);
		double intersectionRight = Math.Min(left.X2, right.X2);
		double intersectionBottom = Math.Min(left.Y2, right.Y2);
		double intersectionWidth = Math.Max(0d, intersectionRight - intersectionLeft);
		double intersectionHeight = Math.Max(0d, intersectionBottom - intersectionTop);
		double intersection = intersectionWidth * intersectionHeight;
		double leftArea = Math.Max(0d, left.X2 - left.X1) * Math.Max(0d, left.Y2 - left.Y1);
		double rightArea = Math.Max(0d, right.X2 - right.X1) * Math.Max(0d, right.Y2 - right.Y1);
		double union = leftArea + rightArea - intersection;
		return union <= 0d ? 0d : intersection / union;
	}

	private static ImmutableDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, object?>? metadata) =>
		metadata is null
			? ImmutableDictionary<string, string>.Empty
			: metadata.ToImmutableDictionary(
				item => item.Key,
				item => FormatMetadataValue(item.Key, item.Value),
				StringComparer.Ordinal);

	private static string FormatMetadataValue(string key, object? value)
	{
		if (string.Equals(key, "path_points", StringComparison.Ordinal) && value is IEnumerable<Point> points)
		{
			return string.Join(";", points.Select(point => string.Concat(
				point.X.ToString(CultureInfo.InvariantCulture),
				",",
				point.Y.ToString(CultureInfo.InvariantCulture))));
		}

		return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
	}

	private static OverlayDebugSnapshot EmptyDebugSnapshot() => new(
		Array.Empty<VisionDrawItem>(),
		Array.Empty<OperationTraceItem>(),
		Array.Empty<DecisionTraceItem>(),
		Array.Empty<TimelineItem>(),
		Array.Empty<PerformanceMetricSample>());

	private static ZzzOverlayDisplayOptionsDto CopyOptions(ZzzOverlayDisplayOptionsDto options) => options with
	{
		PanelEnabledMap = CopyBoolMap(options.PanelEnabledMap),
		PerformanceMetricEnabledMap = CopyBoolMap(options.PerformanceMetricEnabledMap),
		LogMaxLines = Math.Clamp(options.LogMaxLines, 1, LogCapacity),
		LogFadeSeconds = NormalizePositiveFinite(options.LogFadeSeconds, 12d),
		YoloDedupIouThreshold = NormalizeYoloDedupIouThreshold(options.YoloDedupIouThreshold)
	};

	private static double NormalizeYoloDedupIouThreshold(double value) =>
		double.IsFinite(value)
			? Math.Clamp(value, 0.01d, 1d)
			: ZzzOverlayDisplayOptionsDto.DefaultYoloDedupIouThreshold;

	private static double NormalizePositiveFinite(double value, double fallback) =>
		double.IsFinite(value) ? Math.Max(0.1d, value) : fallback;

	private static IReadOnlyDictionary<string, bool> CopyBoolMap(IReadOnlyDictionary<string, bool>? source) =>
		source is null
			? new Dictionary<string, bool>(StringComparer.Ordinal)
			: source.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
}
