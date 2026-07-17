using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战队伍配置。
/// </summary>
public sealed class ShiyuDefenseTeamConfig
{
	[YamlMember(Alias = "team_idx", ApplyNamingConventions = false)]
	public int TeamIndex { get; set; } = -1;

	[YamlMember(Alias = "for_critical", ApplyNamingConventions = false)]
	public bool ForCritical { get; set; }

	[YamlMember(Alias = "weakness_list", ApplyNamingConventions = false)]
	public List<string> WeaknessListRaw { get; set; } = new List<string>();

	[YamlIgnore]
	public List<DmgTypeEnum> WeaknessList
	{
		get
		{
			return WeaknessListRaw.Select(ShiyuDefenseDamageType.Parse).ToList();
		}
		set
		{
			WeaknessListRaw = value.Select(ShiyuDefenseDamageType.ToPythonName).ToList();
		}
	}
}
