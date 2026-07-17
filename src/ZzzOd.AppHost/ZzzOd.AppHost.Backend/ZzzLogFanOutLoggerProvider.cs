using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 将宿主日志写入文件、事件总线和内存最近日志。
/// </summary>
public sealed class ZzzLogFanOutLoggerProvider : ILoggerProvider, IDisposable
{
	private sealed class ZzzLogFanOutLogger : ILogger
	{
		private readonly string _category;

		private readonly Action<ZzzLogEntryDto> _write;

		public ZzzLogFanOutLogger(string category, Action<ZzzLogEntryDto> write)
		{
			_category = category;
			_write = write;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return null;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel != LogLevel.None;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			ArgumentNullException.ThrowIfNull(formatter, "formatter");
			if (IsEnabled(logLevel))
			{
				string text = formatter(state, exception);
				if (!string.IsNullOrWhiteSpace(text) || exception != null)
				{
					_write(new ZzzLogEntryDto(DateTimeOffset.UtcNow, logLevel.ToString(), _category, text, exception?.ToString()));
				}
			}
		}
	}

	private const int RecentLimit = 500;

	private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private readonly ConcurrentDictionary<string, ZzzLogFanOutLogger> _loggers = new ConcurrentDictionary<string, ZzzLogFanOutLogger>(StringComparer.Ordinal);

	private readonly Lock _fileLock = new Lock();

	private readonly Lock _recentLock = new Lock();

	private readonly Queue<ZzzLogEntryDto> _recent = new Queue<ZzzLogEntryDto>();

	private readonly ZzzBackendEventBus _eventBus;

	private readonly string _logFilePath;

	private bool _disposed;

	/// <summary>
	/// 初始化日志广播 provider。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <param name="eventBus">事件总线。</param>
	public ZzzLogFanOutLoggerProvider(ZzzRunRoot runRoot, ZzzBackendEventBus eventBus)
	{
		_eventBus = eventBus;
		_logFilePath = Path.Combine(runRoot.Path, ".log", "zzz-app-host.log");
		Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
	}

	/// <inheritdoc />
	public ILogger CreateLogger(string categoryName)
	{
		return _loggers.GetOrAdd(categoryName, (string category) => new ZzzLogFanOutLogger(category, Write));
	}

	/// <summary>
	/// 获取最近日志。
	/// </summary>
	/// <param name="limit">最大条数。</param>
	/// <returns>最近日志。</returns>
	public IReadOnlyList<ZzzLogEntryDto> GetRecent(int limit)
	{
		int count = Math.Clamp(limit, 1, 500);
		using (_recentLock.EnterScope())
		{
			return _recent.TakeLast(count).ToArray();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_disposed = true;
		_loggers.Clear();
	}

	private void Write(ZzzLogEntryDto entry)
	{
		if (_disposed)
		{
			return;
		}
		string text = FormatLine(entry);
		using (_recentLock.EnterScope())
		{
			_recent.Enqueue(entry);
			while (_recent.Count > 500)
			{
				_recent.Dequeue();
			}
		}
		using (_fileLock.EnterScope())
		{
			File.AppendAllText(_logFilePath, text + Environment.NewLine, Utf8WithoutBom);
		}
		_eventBus.Publish("log.appended", entry);
	}

	private static string FormatLine(ZzzLogEntryDto entry)
	{
		string value = (string.IsNullOrWhiteSpace(entry.Exception) ? string.Empty : (" " + entry.Exception));
		return $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] [{entry.Category}] {entry.Message}{value}";
	}
}
