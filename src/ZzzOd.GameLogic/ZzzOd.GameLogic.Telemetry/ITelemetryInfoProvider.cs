namespace ZzzOd.GameLogic.Telemetry;

/// <summary>
/// 遥测静态信息提供者。
/// </summary>
public interface ITelemetryInfoProvider
{
	/// <summary>
	/// 获取当前环境的遥测静态信息。
	/// </summary>
	/// <returns>静态信息。</returns>
	TelemetryStaticInfo GetInfo();
}
