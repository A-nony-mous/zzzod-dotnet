namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 日志事件。
/// </summary>
/// <param name="Level">日志级别。</param>
/// <param name="Message">日志文本。</param>
public sealed record ZzzLogEvent(string Level, string Message);
