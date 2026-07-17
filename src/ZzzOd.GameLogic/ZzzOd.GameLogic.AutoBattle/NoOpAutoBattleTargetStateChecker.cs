using System.Collections.Generic;
using System.Linq;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class NoOpAutoBattleTargetStateChecker : IAutoBattleTargetStateChecker
{
	public IReadOnlyList<TargetStateCheckResult> RunTask(object? screen, DetectionTask task)
	{
		return task.StateDefinitions.Select((TargetStateDef state) => state.ClearOnMiss ? TargetStateCheckResult.Clear(state.StateName) : TargetStateCheckResult.Miss(state.StateName)).ToList();
	}
}
