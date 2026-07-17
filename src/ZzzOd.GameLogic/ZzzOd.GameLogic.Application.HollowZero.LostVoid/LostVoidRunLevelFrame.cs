using OneDragon.Core.Yolo;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed record LostVoidRunLevelFrame(bool InNormalWorld, bool ChallengeConfirmAvailable = false, bool BossBattleStarted = false, bool BossInteractAvailable = false, YoloDetectFrameResult? DetectResult = null, bool BattleEncountered = false);
