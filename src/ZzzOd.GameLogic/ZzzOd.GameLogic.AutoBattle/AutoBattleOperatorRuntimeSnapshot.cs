using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed record AutoBattleOperatorRuntimeSnapshot(bool IsRunning, string? TriggerDisplay, string? ExpressionDisplay, DateTimeOffset? ExecutionStartedAtUtc, IReadOnlyList<string> UsageStates);
