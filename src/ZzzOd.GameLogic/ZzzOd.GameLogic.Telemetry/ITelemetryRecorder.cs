using System.Collections.Generic;

namespace ZzzOd.GameLogic.Telemetry;

/// <summary>
/// 遥测记录器。
/// </summary>
public interface ITelemetryRecorder
{
	/// <summary>
	/// 记录一个遥测事件。
	/// </summary>
	/// <param name="eventName">事件名。</param>
	/// <param name="properties">事件属性。</param>
	void Record(string eventName, IReadOnlyDictionary<string, string> properties);
}
