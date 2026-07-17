namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 战斗检测结果。
/// </summary>
public sealed record WorldPatrolBattleCheckResult(bool InBattle, string? BattleEndStatus = null, bool FrameValid = true);
