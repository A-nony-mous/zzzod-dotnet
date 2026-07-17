using System.Collections.Generic;
using System.Linq;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战阶段配队信息。
/// </summary>
public sealed class DefensePhaseTeamInfo
{
	/// <summary>阶段弱点。</summary>
	public List<DmgTypeEnum> PhaseWeakness { get; }

	/// <summary>阶段抗性。</summary>
	public List<DmgTypeEnum> PhaseResistance { get; }

	/// <summary>选中的预备编队下标。</summary>
	public int TeamIndex { get; set; } = -1;

	/// <summary>是否命中弱点。</summary>
	public int SameAsWeakness { get; set; }

	/// <summary>是否命中抗性。</summary>
	public int SameAsResistance { get; set; }

	/// <summary>阶段评分。</summary>
	public int Score => SameAsWeakness - SameAsResistance;

	/// <summary>
	/// 初始化阶段配队信息。
	/// </summary>
	public DefensePhaseTeamInfo(IReadOnlyList<DmgTypeEnum> phaseWeakness, IReadOnlyList<DmgTypeEnum> phaseResistance)
	{
		PhaseWeakness = phaseWeakness.ToList();
		PhaseResistance = phaseResistance.ToList();
	}

	/// <summary>
	/// 计算该阶段使用某队伍配置时的评分。
	/// </summary>
	public void CalculateScore(ShiyuDefenseTeamConfig? defenseTeamConfig)
	{
		if (defenseTeamConfig == null)
		{
			SameAsWeakness = 0;
			SameAsResistance = 1;
		}
		else
		{
			SameAsWeakness = (defenseTeamConfig.WeaknessList.Any(PhaseWeakness.Contains) ? 1 : 0);
			SameAsResistance = (defenseTeamConfig.WeaknessList.Any(PhaseResistance.Contains) ? 1 : 0);
		}
	}

	/// <summary>
	/// 克隆阶段配队信息。
	/// </summary>
	public DefensePhaseTeamInfo Clone()
	{
		return new DefensePhaseTeamInfo(PhaseWeakness, PhaseResistance)
		{
			TeamIndex = TeamIndex,
			SameAsWeakness = SameAsWeakness,
			SameAsResistance = SameAsResistance
		};
	}
}
