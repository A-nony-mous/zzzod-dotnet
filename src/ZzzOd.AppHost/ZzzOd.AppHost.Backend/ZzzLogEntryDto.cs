using System;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 日志事件。
/// </summary>
/// <param name="Timestamp">时间戳。</param>
/// <param name="Level">日志等级。</param>
/// <param name="Category">日志分类。</param>
/// <param name="Message">日志消息。</param>
/// <param name="Exception">异常文本。</param>
public sealed record ZzzLogEntryDto(DateTimeOffset Timestamp, string Level, string Category, string Message, string? Exception);
