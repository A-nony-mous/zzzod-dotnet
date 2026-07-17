using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// GUI 全局键鼠监听与运行中 screenshot helper app 的进程内事件桥。
/// </summary>
public static class ScreenshotHelperGlobalInputSource
{
	private sealed class Subscription(long id) : IDisposable
	{
		private long _id = id;

		public void Dispose()
		{
			long num = Interlocked.Exchange(ref _id, 0L);
			if (num == 0)
			{
				return;
			}
			using (Sync.EnterScope())
			{
				Subscribers.Remove(num);
			}
		}
	}

	private sealed class Suspension : IDisposable
	{
		private int _active = 1;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _active, 0) == 1)
			{
				Interlocked.Decrement(ref _suspensionCount);
			}
		}
	}

	private static readonly Lock Sync = new Lock();

	private static readonly Dictionary<long, Func<string, bool>> Subscribers = new Dictionary<long, Func<string, bool>>();

	private static long _nextId;

	private static int _suspensionCount;

	/// <summary>
	/// 订阅 BaselineParity ContextKeyboardEventEnum.PRESS 等价按键。
	/// </summary>
	public static IDisposable Subscribe(Func<string, bool> handler)
	{
		ArgumentNullException.ThrowIfNull(handler, "handler");
		long num = Interlocked.Increment(ref _nextId);
		using (Sync.EnterScope())
		{
			Subscribers[num] = handler;
		}
		return new Subscription(num);
	}

	/// <summary>
	/// 发布全局按键。
	/// </summary>
	public static void Publish(string key)
	{
		if (Volatile.Read(in _suspensionCount) <= 0 && !string.IsNullOrWhiteSpace(key))
		{
			Func<string, bool>[] array;
			using (Sync.EnterScope())
			{
				array = Subscribers.Values.ToArray();
			}
			Func<string, bool>[] array2 = array;
			foreach (Func<string, bool> func in array2)
			{
				func(key);
			}
		}
	}

	/// <summary>
	/// 录入新按键期间暂停运行中应用接收全局按键。
	/// </summary>
	public static IDisposable Suspend()
	{
		Interlocked.Increment(ref _suspensionCount);
		return new Suspension();
	}
}
