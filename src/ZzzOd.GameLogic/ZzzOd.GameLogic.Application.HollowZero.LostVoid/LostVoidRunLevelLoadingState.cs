namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed record LostVoidRunLevelLoadingState(bool InNormalWorld, bool IsChoosingReward = false, bool ChallengeConfirmAvailable = false, string? TalkStatus = null);
