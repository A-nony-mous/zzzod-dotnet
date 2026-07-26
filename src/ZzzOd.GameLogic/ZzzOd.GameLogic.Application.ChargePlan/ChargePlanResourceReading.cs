namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 快捷手册资源栏一次识别得到的三项资源读数。
/// </summary>
/// <param name="BatteryCharge">电量。</param>
/// <param name="BackupBatteryCharge">储蓄电量。</param>
/// <param name="EtherBattery">以太电池数量。</param>
public sealed record ChargePlanResourceReading(int BatteryCharge, int BackupBatteryCharge, int EtherBattery);
