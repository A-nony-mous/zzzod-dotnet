using System.Collections.Generic;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public interface IAutoBattleTargetStateChecker
{
	IReadOnlyList<TargetStateCheckResult> RunTask(object? screen, DetectionTask task);
}
