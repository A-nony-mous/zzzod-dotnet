using System;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 后端事件。
/// </summary>
/// <param name="Type">事件类型。</param>
/// <param name="Timestamp">事件时间。</param>
/// <param name="Data">事件数据。</param>
public sealed record ZzzBackendEvent(string Type, DateTimeOffset Timestamp, object Data);
