namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码运行记录中的兑换码条目。
/// </summary>
public sealed record RedemptionCodeEntry(string Code, string EndDt);
