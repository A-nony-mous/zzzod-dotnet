using System.Collections.Generic;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战队伍工具。
/// </summary>
public static class ShiyuDefenseTeamUtils
{
	/// <summary>
	/// 按弱点和抗性计算最佳队伍。
	/// </summary>
	public static IReadOnlyList<DefensePhaseTeamInfo> CalculateTeams(ShiyuDefenseConfig config, IReadOnlyList<PredefinedTeamInfo> predefinedTeamList, IReadOnlyList<DefensePhaseTeamInfo> detectedPhaseList)
	{
		return new DefenseTeamSearcher(config, predefinedTeamList, detectedPhaseList).Search();
	}
}
