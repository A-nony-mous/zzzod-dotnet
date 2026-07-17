namespace ZzzOd.AppHost.Backend;

/// <summary>迷失之地运行记录。</summary>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="DailyRunTimes">本日次数。</param>
/// <param name="WeeklyRunTimes">本周次数。</param>
/// <param name="BountyCommissionComplete">悬赏委托是否完成。</param>
/// <param name="EvalPointComplete">业绩点是否完成。</param>
/// <param name="PeriodRewardComplete">周期奖励是否完成。</param>
public sealed record ZzzLostVoidRunRecordDto(int InstanceIndex, int DailyRunTimes, int WeeklyRunTimes, bool BountyCommissionComplete, bool EvalPointComplete, bool PeriodRewardComplete);
