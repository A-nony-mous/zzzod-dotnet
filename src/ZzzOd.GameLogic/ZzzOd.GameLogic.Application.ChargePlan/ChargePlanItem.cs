using System;
using System.Globalization;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划条目。
/// </summary>
public sealed class ChargePlanItem
{
	[YamlMember(Alias = "tab_name", ApplyNamingConventions = false)]
	public string TabName { get; set; } = "训练";

	[YamlMember(Alias = "category_name", ApplyNamingConventions = false)]
	public string CategoryName { get; set; } = "实战模拟室";

	[YamlMember(Alias = "mission_type_name", ApplyNamingConventions = false)]
	public string MissionTypeName { get; set; } = "基础材料";

	[YamlMember(Alias = "mission_name", ApplyNamingConventions = false)]
	public string? MissionName { get; set; } = "调查专项";

	[YamlMember(Alias = "level", ApplyNamingConventions = false)]
	public string Level { get; set; } = "默认等级";

	[YamlMember(Alias = "auto_battle_config", ApplyNamingConventions = false)]
	public string AutoBattleConfig { get; set; } = "全配队通用";

	[YamlMember(Alias = "run_times", ApplyNamingConventions = false)]
	public int RunTimes { get; set; }

	[YamlMember(Alias = "plan_times", ApplyNamingConventions = false)]
	public int PlanTimes { get; set; } = 1;

	[YamlMember(Alias = "card_num", ApplyNamingConventions = false)]
	public string CardNum { get; set; } = "默认数量";

	[YamlMember(Alias = "predefined_team_idx", ApplyNamingConventions = false)]
	public int PredefinedTeamIndex { get; set; } = -1;

	[YamlMember(Alias = "notorious_hunt_buff_num", ApplyNamingConventions = false)]
	public int NotoriousHuntBuffNum { get; set; } = 1;

	[YamlMember(Alias = "plan_id", ApplyNamingConventions = false)]
	public string? PlanId { get; set; } = Guid.NewGuid().ToString("D");

	[YamlIgnore]
	public bool Skipped { get; set; }

	[YamlIgnore]
	public bool IsAgentPlan => string.Equals(MissionTypeName, "代理人方案培养", StringComparison.Ordinal);

	[YamlIgnore]
	public string Uid => $"{TabName ?? string.Empty}_{CategoryName ?? string.Empty}_{MissionTypeName ?? string.Empty}_{MissionName ?? string.Empty}";

	[YamlIgnore]
	public int EstimatedChargePower
	{
		get
		{
			if (CategoryName == "实战模拟室")
			{
				return (CardNum == "默认数量") ? 20 : (int.Parse(CardNum, CultureInfo.InvariantCulture) * 20);
			}
			string categoryName = CategoryName;
			if (1 == 0)
			{
			}
			int result = categoryName switch
			{
				"区域巡防" => 60, 
				"专业挑战室" => 40, 
				"恶名狩猎" => 60, 
				"合成电池" => 60, 
				_ => 0, 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	/// <summary>
	/// 创建一份运行期副本。
	/// </summary>
	public ChargePlanItem Clone()
	{
		return new ChargePlanItem
		{
			TabName = TabName,
			CategoryName = CategoryName,
			MissionTypeName = MissionTypeName,
			MissionName = MissionName,
			Level = Level,
			AutoBattleConfig = AutoBattleConfig,
			RunTimes = RunTimes,
			PlanTimes = PlanTimes,
			CardNum = CardNum,
			PredefinedTeamIndex = PredefinedTeamIndex,
			NotoriousHuntBuffNum = NotoriousHuntBuffNum,
			PlanId = PlanId,
			Skipped = Skipped
		};
	}
}
