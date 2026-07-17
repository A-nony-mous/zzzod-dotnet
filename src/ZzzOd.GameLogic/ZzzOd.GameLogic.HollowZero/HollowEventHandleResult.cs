namespace ZzzOd.GameLogic.HollowZero;

public sealed record HollowEventHandleResult(string EventName, HollowEventOutcomeKind Outcome, bool Success, string? Message = null);
