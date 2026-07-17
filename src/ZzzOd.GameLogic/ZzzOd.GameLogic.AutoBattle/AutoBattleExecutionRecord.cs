using System;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed record AutoBattleExecutionRecord(string Event, string Trigger, string OperationSummary, bool Completed, string? ErrorMessage, DateTimeOffset Timestamp);
