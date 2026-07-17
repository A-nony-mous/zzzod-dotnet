using System;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// WebSocket 消息信封。
/// </summary>
/// <param name="Type">消息类型。</param>
/// <param name="Timestamp">消息时间。</param>
/// <param name="Data">消息数据。</param>
public sealed record ZzzWebSocketEnvelope(string Type, DateTimeOffset Timestamp, object Data);
