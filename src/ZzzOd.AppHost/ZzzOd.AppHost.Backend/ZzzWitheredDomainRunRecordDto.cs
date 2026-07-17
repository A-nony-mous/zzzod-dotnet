namespace ZzzOd.AppHost.Backend;

/// <summary>枯萎之都运行记录。</summary>
public sealed record ZzzWitheredDomainRunRecordDto(int InstanceIndex, int WeeklyRunTimes, int DailyRunTimes, bool NoEvalPoint, bool PeriodRewardComplete);
