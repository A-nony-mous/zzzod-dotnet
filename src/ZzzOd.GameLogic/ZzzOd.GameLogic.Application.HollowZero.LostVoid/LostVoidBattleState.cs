namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed record LostVoidBattleState(bool CurrentFrameInBattle, bool NextRegionHint = false, bool NoLongerInBattleByDetection = false, bool InInteractScreen = false, bool BattleFailed = false, bool TransitionCheckPerformed = true, bool DetectorChecked = false, bool FinishScreenChecked = false, bool FrameValid = true, bool AgentDead = false);
