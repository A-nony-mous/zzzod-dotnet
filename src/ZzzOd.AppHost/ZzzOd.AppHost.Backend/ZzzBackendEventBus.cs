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
	private readonly Lock _lock = new Lock();

	private readonly List<Channel<ZzzBackendEvent>> _subscribers = new List<Channel<ZzzBackendEvent>>();

	private bool _completed;

	/// <summary>
	/// 发布事件。
	/// </summary>
	/// <param name="type">事件类型。</param>
	/// <param name="data">事件数据。</param>
	public void Publish(string type, object data)
	{
		ZzzBackendEvent item = new ZzzBackendEvent(type, DateTimeOffset.UtcNow, data);
		Channel<ZzzBackendEvent>[] array;
		using (_lock.EnterScope())
		{
			if (_completed)
			{
				return;
			}
			array = _subscribers.ToArray();
		}
		Channel<ZzzBackendEvent>[] array2 = array;
		foreach (Channel<ZzzBackendEvent> channel in array2)
		{
			channel.Writer.TryWrite(item);
		}
	}

	/// <summary>
	/// 订阅事件。
	/// </summary>
	/// <returns>事件读取器。</returns>
	public ChannelReader<ZzzBackendEvent> Subscribe()
	{
		Channel<ZzzBackendEvent> channel = Channel.CreateUnbounded<ZzzBackendEvent>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});
		using (_lock.EnterScope())
		{
			if (_completed)
			{
				channel.Writer.TryComplete();
				return channel.Reader;
			}
			_subscribers.Add(channel);
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
				if (_subscribers[num].Reader == reader)
				{
					_subscribers[num].Writer.TryComplete();
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
		Channel<ZzzBackendEvent>[] array;
		using (_lock.EnterScope())
		{
			if (_completed)
			{
				return;
			}
			_completed = true;
			array = _subscribers.ToArray();
			_subscribers.Clear();
		}
		Channel<ZzzBackendEvent>[] array2 = array;
		foreach (Channel<ZzzBackendEvent> channel in array2)
		{
			channel.Writer.TryComplete();
		}
	}
}
