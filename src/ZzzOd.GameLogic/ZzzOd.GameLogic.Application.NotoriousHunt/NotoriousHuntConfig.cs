using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.Application.ChargePlan;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎应用配置。
/// </summary>
public sealed class NotoriousHuntConfig : ZApplicationConfig, IApplicationConfig
{
	private Action? _save;

	[YamlMember(Alias = "plan_list", ApplyNamingConventions = false)]
	public List<ChargePlanItem> PlanList { get; set; } = new List<ChargePlanItem>();

	[YamlMember(Alias = "weekly_challenge_start_weekday", ApplyNamingConventions = false)]
	public int WeeklyChallengeStartWeekday { get; set; } = 1;

	[YamlMember(Alias = "loop", ApplyNamingConventions = false)]
	public bool Loop { get; set; } = true;

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static NotoriousHuntConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<NotoriousHuntConfig> yamlConfig = new YamlConfig<NotoriousHuntConfig>(environment, "notorious_hunt", null, instanceIndex, new string[2] { "app_config", groupId });
		NotoriousHuntConfig current = yamlConfig.Current;
		current.ConfigureRuntime("notorious_hunt", instanceIndex, groupId);
		current.ConfigurePersistence(delegate
		{
			yamlConfig.Save();
		});
		current.MigrateLegacyConfig();
		return current;
	}

	/// <summary>
	/// 旧配置中恶名狩猎页签迁移到训练。
	/// </summary>
	public void MigrateLegacyConfig()
	{
		bool flag = false;
		foreach (ChargePlanItem plan in PlanList)
		{
			if (plan.TabName == "挑战" || (plan.TabName == "作战" && plan.CategoryName == "恶名狩猎"))
			{
				plan.TabName = "训练";
				flag = true;
			}
		}
		if (flag)
		{
			Save();
		}
	}

	/// <summary>
	/// 全部计划是否已完成。
	/// </summary>
	public bool AllPlanFinished()
	{
		return PlanList.All((ChargePlanItem plan) => plan.Skipped || plan.RunTimes >= plan.PlanTimes);
	}

	/// <summary>
	/// 重置完成计划。
	/// </summary>
	public void ResetPlans()
	{
		List<ChargePlanItem> list = PlanList.Where((ChargePlanItem plan) => !plan.Skipped && plan.PlanTimes > 0).ToList();
		if (list.Count == 0)
		{
			return;
		}
		bool flag = false;
		while (!list.Any((ChargePlanItem plan) => plan.RunTimes < plan.PlanTimes))
		{
			foreach (ChargePlanItem item in list)
			{
				item.RunTimes -= item.PlanTimes;
			}
			flag = true;
		}
		if (flag)
		{
			Save();
		}
	}

	/// <summary>
	/// 获取下一个未完成计划。
	/// </summary>
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

	/// <summary>
	/// 记录一次计划运行。
	/// </summary>
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

	private void ConfigurePersistence(Action save)
	{
		_save = save;
	}

	private void Save()
	{
		_save?.Invoke();
	}

	private static bool IsSamePlan(ChargePlanItem x, ChargePlanItem y)
	{
		if (!string.IsNullOrWhiteSpace(x.PlanId) && !string.IsNullOrWhiteSpace(y.PlanId))
		{
			return string.Equals(x.PlanId, y.PlanId, StringComparison.Ordinal);
		}
		return string.Equals(x.TabName, y.TabName, StringComparison.Ordinal) && string.Equals(x.CategoryName, y.CategoryName, StringComparison.Ordinal) && string.Equals(x.MissionTypeName, y.MissionTypeName, StringComparison.Ordinal) && string.Equals(x.MissionName, y.MissionName, StringComparison.Ordinal);
	}
}
