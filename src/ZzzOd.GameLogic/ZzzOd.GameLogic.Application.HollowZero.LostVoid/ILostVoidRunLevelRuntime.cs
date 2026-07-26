using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public interface ILostVoidRunLevelRuntime
{
	Task<LostVoidRunLevelLoadingState> GetLoadingStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken);

	Task<LostVoidRunLevelWorldState> GetNonBattleWorldStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken);

	Task<LostVoidRunLevelFrame> GetNonBattleFrameAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, IReadOnlyList<string> ignoreList, CancellationToken cancellationToken);

	bool CheckBattleEncounterInCurrentFrame(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc);

	bool CheckBattleEncounterInPeriod(LostVoidRunLevel operation, float totalCheckSeconds);

	/// <summary>
	/// 处理挚交会谈进入大世界后的一次性初始化。
	/// Result 非空时调用方应提前返回；Advance 指示调用方是否需要推进 roomInitedTimes 计数
	/// （即使 Result 为空也可能需要推进，避免一次性动作被重复执行）。
	/// </summary>
	(OperationRoundResult? Result, bool Advance) HandleFriendlyTalkInit(LostVoidRunLevel operation, int roomInitedTimes);

	void TurnToFindTarget(LostVoidRunLevel operation);

	Task<OperationResult> MoveByDetectionAsync(LostVoidRunLevel operation, string regionType, string targetType, bool stopWhenInteract, bool stopWhenDisappear, bool allowArrivalByInteractButton, IReadOnlyList<string> ignoreEntries, CancellationToken cancellationToken);

	Task<OperationResult> UpdatePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken);

	Task<OperationResult> AppendAgentTypePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken);

	Task<LostVoidTryInteractResult> TryInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, IReadOnlyList<string> interactedTargetKeys, bool interactAttempted, Mat? screen, CancellationToken cancellationToken);

	Task<LostVoidInteractResult> HandleInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken);

	Task<LostVoidAfterInteractState> GetAfterInteractStateAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken);

	void MoveAfterInteract(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget);

	void StartAutoBattle(LostVoidRunLevel operation);

	void StopAutoBattle(LostVoidRunLevel operation);

	Task<LostVoidBattleState> GetBattleStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken);

	Task<OperationResult> RestartForRetryAsync(LostVoidRunLevel operation, CancellationToken cancellationToken);

	Task<OperationResult> PushErrorAsync(LostVoidRunLevel operation, Mat? screen, string? previousNodeName, string? previousStatus, CancellationToken cancellationToken);

	Task<OperationResult> FailExitAsync(LostVoidRunLevel operation, CancellationToken cancellationToken);
}
