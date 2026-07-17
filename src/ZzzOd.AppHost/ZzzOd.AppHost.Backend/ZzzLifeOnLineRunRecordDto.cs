namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 生命热线当日运行记录。
/// </summary>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="DailyRunTimes">游戏日内已完成次数。</param>
public sealed record ZzzLifeOnLineRunRecordDto(int InstanceIndex, int DailyRunTimes);
