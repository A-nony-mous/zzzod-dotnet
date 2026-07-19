using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneDragon.Core.Events;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 将核心 Overlay Operation trace 转成 AppHost backend event。
/// </summary>
public sealed class ZzzOperationTraceBridge : IHostedService, IDisposable
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100L);

	private const int SeenCapacity = 4096;

	private readonly ZzzRuntimeManager _runtime;

	private readonly ZzzBackendEventBus _eventBus;

	private readonly ILogger<ZzzOperationTraceBridge> _logger;

	private readonly Lock _lock = new Lock();

	private readonly HashSet<TraceKey> _seen = new HashSet<TraceKey>();

	private readonly Queue<TraceKey> _seenOrder = new Queue<TraceKey>();

	private CancellationTokenSource? _shutdown;

	private Task? _pollTask;

	private ZContext? _observedContext;

	private bool _disposed;

	/// <summary>
	/// 初始化 bridge。
	/// </summary>
	public ZzzOperationTraceBridge(
		ZzzRuntimeManager runtime,
		ZzzBackendEventBus eventBus,
		ILogger<ZzzOperationTraceBridge> logger)
	{
		_runtime = runtime;
		_eventBus = eventBus;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_pollTask is not null)
		{
			return Task.CompletedTask;
		}

		_shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_pollTask = PollAsync(_shutdown.Token);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		CancellationTokenSource? shutdown = _shutdown;
		Task? pollTask = _pollTask;
		_shutdown = null;
		_pollTask = null;
		shutdown?.Cancel();
		if (pollTask is not null)
		{
			try
			{
				await pollTask.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (shutdown?.IsCancellationRequested == true || cancellationToken.IsCancellationRequested)
			{
			}
		}

		shutdown?.Dispose();
		ResetContext(null);
	}

	/// <summary>
	/// 立即发布当前尚未转发的 trace，供测试和受控刷新使用。
	/// </summary>
	internal int PublishPending()
	{
		ZContext? context = _runtime.TryGetContext();
		if (!ReferenceEquals(context, _observedContext))
		{
			ResetContext(context);
		}

		if (context is null)
		{
			return 0;
		}

		OverlayDebugSnapshot snapshot = context.OverlayDebugBus.Snapshot();
		Dictionary<DateTimeOffset, TimelineItem[]> timelinesByTimestamp = snapshot.TimelineItems
			.Where(item => string.Equals(item.Category, "operation", StringComparison.Ordinal))
			.GroupBy(item => item.CreatedAt)
			.ToDictionary(group => group.Key, group => group.ToArray());

		int published = 0;
		foreach (OperationTraceItem item in snapshot.OperationItems.OrderBy(item => item.CreatedAt))
		{
			TraceKey key = TraceKey.From(item);
			if (!TryMarkSeen(key))
			{
				continue;
			}

			TimelineItem? timeline = timelinesByTimestamp.TryGetValue(item.CreatedAt, out TimelineItem[]? candidates)
				? candidates.FirstOrDefault(candidate => string.Equals(candidate.Title, item.Operation, StringComparison.Ordinal))
				: null;
			string? exceptionType = ReadMetadata(timeline?.Metadata, "exception_type");
			bool isException = item.ResultKind is "exception" or "initialization-failed" or "finalization-failed";
			ZzzOperationTraceDto dto = new ZzzOperationTraceDto(
				item.AppId,
				context.InstanceIndex,
				item.Operation,
				item.CurrentNode,
				item.PreviousNode,
				item.NextNode,
				item.RetryCount,
				item.ResultKind,
				item.Status,
				exceptionType,
				isException ? item.Status : null,
				item.CreatedAt);
			_eventBus.Publish("run.operationTrace", dto);
			published++;
		}

		return published;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_shutdown?.Cancel();
		_shutdown?.Dispose();
		_shutdown = null;
		_pollTask = null;
		ResetContext(null);
	}

	private async Task PollAsync(CancellationToken cancellationToken)
	{
		using PeriodicTimer timer = new PeriodicTimer(PollInterval);
		try
		{
			PublishPending();
			while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				PublishPending();
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "发布 Operation trace 失败。");
		}
	}

	private void ResetContext(ZContext? context)
	{
		using (_lock.EnterScope())
		{
			_observedContext = context;
			_seen.Clear();
			_seenOrder.Clear();
		}
	}

	private bool TryMarkSeen(TraceKey key)
	{
		using (_lock.EnterScope())
		{
			if (!_seen.Add(key))
			{
				return false;
			}

			_seenOrder.Enqueue(key);
			while (_seenOrder.Count > SeenCapacity)
			{
				_seen.Remove(_seenOrder.Dequeue());
			}

			return true;
		}
	}

	private static string? ReadMetadata(IReadOnlyDictionary<string, object?>? metadata, string key) =>
		metadata is not null && metadata.TryGetValue(key, out object? value)
			? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
			: null;

	private readonly record struct TraceKey(
		DateTimeOffset CreatedAt,
		string AppId,
		string Operation,
		string? CurrentNode,
		string? PreviousNode,
		string? NextNode,
		int RetryCount,
		string? ResultKind,
		string? Status)
	{
		public static TraceKey From(OperationTraceItem item) => new TraceKey(
			item.CreatedAt,
			item.AppId,
			item.Operation,
			item.CurrentNode,
			item.PreviousNode,
			item.NextNode,
			item.RetryCount,
			item.ResultKind,
			item.Status);
	}
}
