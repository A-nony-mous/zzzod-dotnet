using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 后端事件总线。
/// </summary>
public sealed class ZzzBackendEventBus
{
	private sealed record Subscription(Channel<ZzzBackendEvent> Channel, string? EventType);

	private readonly Lock _lock = new Lock();

	private readonly List<Subscription> _subscribers = new List<Subscription>();

	private bool _completed;

	/// <summary>
	/// 发布事件。
	/// </summary>
	/// <param name="type">事件类型。</param>
	/// <param name="data">事件数据。</param>
	public void Publish(string type, object data)
	{
		ZzzBackendEvent item = new ZzzBackendEvent(type, DateTimeOffset.UtcNow, data);
		Subscription[] subscribers;
		using (_lock.EnterScope())
		{
			if (_completed)
			{
				return;
			}
			subscribers = _subscribers.ToArray();
		}
		foreach (Subscription subscription in subscribers)
		{
			if (subscription.EventType is null || string.Equals(subscription.EventType, type, StringComparison.Ordinal))
			{
				subscription.Channel.Writer.TryWrite(item);
			}
		}
	}

	/// <summary>
	/// 订阅全部事件。
	/// </summary>
	/// <param name="capacity">指定正数时使用有界队列并丢弃最旧事件。</param>
	/// <returns>事件读取器。</returns>
	public ChannelReader<ZzzBackendEvent> Subscribe(int? capacity = null) => SubscribeCore(null, capacity);

	/// <summary>
	/// 订阅指定类型的事件。
	/// </summary>
	/// <param name="eventType">事件类型。</param>
	/// <param name="capacity">有界队列容量。</param>
	/// <returns>事件读取器。</returns>
	public ChannelReader<ZzzBackendEvent> Subscribe(string eventType, int capacity)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
		if (capacity <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(capacity));
		}

		return SubscribeCore(eventType, capacity);
	}

	private ChannelReader<ZzzBackendEvent> SubscribeCore(string? eventType, int? capacity)
	{
		Channel<ZzzBackendEvent> channel = capacity is > 0
			? Channel.CreateBounded<ZzzBackendEvent>(new BoundedChannelOptions(capacity.Value)
			{
				SingleReader = true,
				SingleWriter = false,
				FullMode = BoundedChannelFullMode.DropOldest,
			})
			: Channel.CreateUnbounded<ZzzBackendEvent>(new UnboundedChannelOptions
			{
				SingleReader = true,
				SingleWriter = false,
			});
		using (_lock.EnterScope())
		{
			if (_completed)
			{
				channel.Writer.TryComplete();
				return channel.Reader;
			}
			_subscribers.Add(new Subscription(channel, eventType));
		}
		return channel.Reader;
	}

	/// <summary>
	/// 取消订阅。
	/// </summary>
	/// <param name="reader">事件读取器。</param>
	public void Unsubscribe(ChannelReader<ZzzBackendEvent> reader)
	{
		using (_lock.EnterScope())
		{
			for (int num = _subscribers.Count - 1; num >= 0; num--)
			{
				if (_subscribers[num].Channel.Reader == reader)
				{
					_subscribers[num].Channel.Writer.TryComplete();
					_subscribers.RemoveAt(num);
					break;
				}
			}
		}
	}

	/// <summary>
	/// 结束所有事件订阅。
	/// </summary>
	public void Complete()
	{
		Subscription[] subscribers;
		using (_lock.EnterScope())
		{
			if (_completed)
			{
				return;
			}
			_completed = true;
			subscribers = _subscribers.ToArray();
			_subscribers.Clear();
		}
		foreach (Subscription subscription in subscribers)
		{
			subscription.Channel.Writer.TryComplete();
		}
	}
}
