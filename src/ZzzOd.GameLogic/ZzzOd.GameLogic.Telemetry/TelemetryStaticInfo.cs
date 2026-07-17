namespace ZzzOd.GameLogic.Telemetry;

/// <summary>
/// 遥测静态字段。
/// </summary>
/// <param name="UserId">用户标识。</param>
/// <param name="AppVersion">应用版本。</param>
/// <param name="CommitVersion">提交版本。</param>
/// <param name="LauncherVersion">启动器版本。</param>
/// <param name="Platform">平台名称。</param>
/// <param name="MachineId">机器标识。</param>
public sealed record TelemetryStaticInfo(string UserId, string AppVersion, string CommitVersion, string LauncherVersion, string Platform, string MachineId);
