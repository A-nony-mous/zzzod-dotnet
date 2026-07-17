using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 战斗助手运行快照。
/// </summary>
/// <param name="IsRunning">自动战斗指令是否运行。</param>
/// <param name="TriggerDisplay">当前触发器。</param>
/// <param name="ExpressionDisplay">当前条件集。</param>
/// <param name="ExecutionDurationSeconds">当前执行持续时间。</param>
/// <param name="States">当前使用状态。</param>
public sealed record ZzzBattleAssistantRuntimeDto(bool IsRunning, string? TriggerDisplay, string? ExpressionDisplay, double? ExecutionDurationSeconds, IReadOnlyList<ZzzBattleAssistantStateDto> States);
