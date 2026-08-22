using System;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 钓鱼轮次节拍标记，放在 <c>OperationResult.Data</c> 里回传给 Operation 层。
/// </summary>
/// <remarks>
/// 钓鱼分支分两种节拍：
/// 抛竿、等待上鱼和时机命中使用 0.1 秒固定等待；
/// 时机未命中、连点和长按把本轮总时长补足到 0.1 秒，以便尽快按键。
/// .NET 把这些分支收进了 <c>HandleFishing</c> 服务方法，返回值里没有通道信息，
/// 于是本类型显式携带它；<c>Data</c> 为空时按固定睡眠处理。
/// </remarks>
/// <param name="Duration">节拍时长。</param>
internal sealed record FishingRoundPacing(TimeSpan Duration);
