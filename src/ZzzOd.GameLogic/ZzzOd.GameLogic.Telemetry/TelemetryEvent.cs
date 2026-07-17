using System.Collections.Generic;

namespace ZzzOd.GameLogic.Telemetry;

/// <summary>
/// 遥测事件。
/// </summary>
/// <param name="EventName">事件名。</param>
/// <param name="Properties">属性。</param>
public sealed record TelemetryEvent(string EventName, IReadOnlyDictionary<string, string> Properties);
