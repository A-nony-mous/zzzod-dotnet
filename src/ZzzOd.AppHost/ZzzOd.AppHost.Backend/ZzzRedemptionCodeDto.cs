namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 兑换码显示行。
/// </summary>
public sealed record ZzzRedemptionCodeDto(string Code, int EndDate, bool ReadOnly);
