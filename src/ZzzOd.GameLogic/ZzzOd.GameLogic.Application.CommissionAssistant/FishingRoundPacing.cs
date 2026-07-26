using System;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 钓鱼轮次节拍标记，放在 <c>OperationResult.Data</c> 里回传给 Operation 层。
/// </summary>
/// <remarks>
/// 参考实现钓鱼分支分两种节拍：
/// 抛竿 / 等待上鱼 / 时机命中用 <c>wait=0.1</c>（固定睡眠），
/// 时机未命中（:534）、连点（:547）、长按（:566）用 <c>wait_round_time=0.1</c>（补足制，注释写明"这个要尽快按"）。
/// .NET 把这些分支收进了 <c>HandleFishing</c> 服务方法，返回值里没有通道信息，
/// 于是本类型显式携带它；<c>Data</c> 为空时按固定睡眠处理。
/// </remarks>
/// <param name="Duration">节拍时长。</param>
internal sealed record FishingRoundPacing(TimeSpan Duration);
