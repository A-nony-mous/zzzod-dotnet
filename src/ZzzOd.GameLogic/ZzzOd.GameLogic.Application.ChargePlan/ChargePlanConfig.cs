using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划配置。
/// </summary>
public sealed class ChargePlanConfig : IApplicationConfig
{
	private Action? _save;

	[YamlMember(Alias = "plan_list", ApplyNamingConventions = false)]
	public List<ChargePlanItem> PlanList { get; set; } = new List<ChargePlanItem>();

	[YamlMember(Alias = "restore_charge", ApplyNamingConventions = false)]
	public string RestoreCharge { get; set; } = RestoreChargeMode.None.DisplayName;

	[YamlMember(Alias = "history_list", ApplyNamingConventions = false)]
	public List<ChargePlanItem> HistoryList { get; set; } = new List<ChargePlanItem>();

	[YamlMember(Alias = "loop", ApplyNamingConventions = false)]
	public bool Loop { get; set; } = true;

	[YamlMember(Alias = "daily_reset_plan_times", ApplyNamingConventions = false)]
	public bool DailyResetPlanTimes { get; set; }

	[YamlMember(Alias = "last_daily_reset_dt", ApplyNamingConventions = false)]
	public string LastDailyResetDt { get; set; } = string.Empty;

	[YamlMember(Alias = "skip_plan", ApplyNamingConventions = false)]
	public bool SkipPlan { get; set; }

	[YamlMember(Alias = "double_reward", ApplyNamingConventions = false)]
	public bool DoubleReward { get; set; }

	[YamlMember(Alias = "combat_simulation_double_reward_config", ApplyNamingConventions = false)]
	public ChargePlanItem CombatSimulationDoubleRewardConfig { get; set; } = new ChargePlanItem();

	[YamlIgnore]
	public bool IsRestoreChargeEnabled => RestoreChargeMode.FromDisplayName(RestoreCharge) != RestoreChargeMode.None;

	public bool TryResetPlanTimesByDt(string currentDt)
	{
		if (!DailyResetPlanTimes || string.Equals(LastDailyResetDt, currentDt, StringComparison.Ordinal))
		{
			return false;
		}
		foreach (ChargePlanItem plan in PlanList)
		{
			plan.RunTimes = 0;
		}
		LastDailyResetDt = currentDt;
		Save();
		return true;
	}

	public bool AllPlanFinished()
	{
		return PlanList.All((ChargePlanItem plan) => plan.Skipped || plan.RunTimes >= plan.PlanTimes);
	}

	public ChargePlanItem? GetNextPlan(ChargePlanItem? lastTriedPlan = null)
	{
		if (PlanList.Count == 0)
		{
			return null;
		}
		int num = 0;
		if (lastTriedPlan != null)
		{
			int num2 = PlanList.FindIndex((ChargePlanItem plan) => IsSamePlan(plan, lastTriedPlan));
			if (num2 >= 0)
			{
				num = num2 + 1;
				if (num >= PlanList.Count)
				{
					return null;
				}
			}
		}
		for (int num3 = num; num3 < PlanList.Count; num3++)
		{
			ChargePlanItem chargePlanItem = PlanList[num3];
			if (!chargePlanItem.Skipped && chargePlanItem.RunTimes < chargePlanItem.PlanTimes)
			{
				return chargePlanItem;
			}
		}
		return null;
	}

	public void ResetPlans()
	{
		List<ChargePlanItem> list = PlanList.Where((ChargePlanItem plan) => !plan.Skipped && plan.PlanTimes > 0).ToList();
		if (list.Count == 0)
		{
			return;
		}
		while (!list.Any((ChargePlanItem plan) => plan.RunTimes < plan.PlanTimes))
		{
			foreach (ChargePlanItem item in list)
			{
				item.RunTimes -= item.PlanTimes;
			}
		}
		Save();
	}

	public void AddPlanRunTimes(ChargePlanItem toAdd)
	{
		ChargePlanItem chargePlanItem = PlanList.FirstOrDefault((ChargePlanItem plan) => IsSamePlan(plan, toAdd) && plan.RunTimes < plan.PlanTimes);
		if (chargePlanItem != null)
		{
			chargePlanItem.RunTimes++;
			Save();
			return;
		}
		ChargePlanItem chargePlanItem2 = PlanList.FirstOrDefault((ChargePlanItem plan) => IsSamePlan(plan, toAdd));
		if (chargePlanItem2 != null)
		{
			chargePlanItem2.RunTimes++;
			Save();
		}
	}

	public static ChargePlanConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<ChargePlanConfig> yamlConfig = new YamlConfig<ChargePlanConfig>(environment, "charge_plan", null, instanceIndex, new string[] { groupId });
		ChargePlanConfig current = yamlConfig.Current;
		current.ConfigurePersistence(delegate
		{
			yamlConfig.Save();
		});
		return current;
	}

	public ChargePlanItem? GetHistoryByUid(ChargePlanItem plan)
	{
		return HistoryList.FirstOrDefault((ChargePlanItem history) => IsSamePlan(history, plan, comparePlanId: false));
	}

	private static bool IsSamePlan(ChargePlanItem x, ChargePlanItem y, bool comparePlanId = true)
	{
		if (comparePlanId && !string.IsNullOrWhiteSpace(x.PlanId) && !string.IsNullOrWhiteSpace(y.PlanId))
		{
			return string.Equals(x.PlanId, y.PlanId, StringComparison.Ordinal);
		}
		return string.Equals(x.TabName, y.TabName, StringComparison.Ordinal) && string.Equals(x.CategoryName, y.CategoryName, StringComparison.Ordinal) && string.Equals(x.MissionTypeName, y.MissionTypeName, StringComparison.Ordinal) && string.Equals(x.MissionName, y.MissionName, StringComparison.Ordinal) && string.Equals(x.Level, y.Level, StringComparison.Ordinal) && string.Equals(x.AutoBattleConfig, y.AutoBattleConfig, StringComparison.Ordinal) && x.RunTimes == y.RunTimes && x.PlanTimes == y.PlanTimes && string.Equals(x.CardNum, y.CardNum, StringComparison.Ordinal) && x.PredefinedTeamIndex == y.PredefinedTeamIndex && x.NotoriousHuntBuffNum == y.NotoriousHuntBuffNum && x.Skipped == y.Skipped;
	}

	private void ConfigurePersistence(Action save)
	{
		_save = save;
	}

	private void Save()
	{
		_save?.Invoke();
	}
}
