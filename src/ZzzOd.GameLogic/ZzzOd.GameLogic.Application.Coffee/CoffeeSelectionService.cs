using System;
using System.Collections.Generic;
using System.Linq;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡选择与体力计划匹配逻辑。
/// </summary>
public sealed class CoffeeSelectionService
{
	/// <summary>
	/// 计算本轮优先选择的咖啡名称。
	/// </summary>
	public IReadOnlyList<string> GetCoffeeToChoose(CoffeeConfig config, ChargePlanConfig chargePlanConfig, CompendiumService compendiumService, int day, IReadOnlyCollection<string>? hadCoffeeList = null)
	{
		ArgumentNullException.ThrowIfNull(config, "config");
		ArgumentNullException.ThrowIfNull(chargePlanConfig, "chargePlanConfig");
		ArgumentNullException.ThrowIfNull(compendiumService, "compendiumService");
		HashSet<string> had = ((hadCoffeeList == null) ? new HashSet<string>() : new HashSet<string>(hadCoffeeList, StringComparer.Ordinal));
		List<string> list = (from coffee2 in compendiumService.GetExtraCoffeeList()
			where !had.Contains(coffee2.CoffeeName)
			select coffee2.CoffeeName).ToList();
		if (string.Equals(config.ChooseWay, "优先体力计划", StringComparison.Ordinal))
		{
			IReadOnlyList<ZzzOd.GameLogic.GameData.Coffee> readOnlyList2;
			if (!compendiumService.CoffeeSchedule.TryGetValue(day, out IReadOnlyList<ZzzOd.GameLogic.GameData.Coffee> value))
			{
				IReadOnlyList<ZzzOd.GameLogic.GameData.Coffee> readOnlyList = Array.Empty<ZzzOd.GameLogic.GameData.Coffee>();
				readOnlyList2 = readOnlyList;
			}
			else
			{
				readOnlyList2 = value;
			}
			IReadOnlyList<ZzzOd.GameLogic.GameData.Coffee> readOnlyList3 = readOnlyList2;
			AddUnfinishedPlanPriorityCandidates(list, readOnlyList3, chargePlanConfig.PlanList);
			AddFinishedPlanPriorityCandidates(list, readOnlyList3, chargePlanConfig.PlanList, had);
			if (list.Count == 0)
			{
				ZzzOd.GameLogic.GameData.Coffee coffee = readOnlyList3.FirstOrDefault((ZzzOd.GameLogic.GameData.Coffee coffee2) => coffee2.WithoutBenefit);
				if (coffee != null)
				{
					list.Add(coffee.CoffeeName);
				}
			}
		}
		string text = (string.Equals(config.ChooseWay, "优先体力计划", StringComparison.Ordinal) ? config.GetCoffeeByDay(day) : config.ChooseWay);
		if (!string.IsNullOrWhiteSpace(text) && !had.Contains(text))
		{
			list.Add(text);
		}
		return list;
	}

	/// <summary>
	/// 判断咖啡是否匹配体力计划。
	/// </summary>
	public static bool IsCoffeeForPlan(ZzzOd.GameLogic.GameData.Coffee coffee, ChargePlanItem plan)
	{
		ArgumentNullException.ThrowIfNull(coffee, "coffee");
		ArgumentNullException.ThrowIfNull(plan, "plan");
		// 合成电池计划不喝咖啡加成（对应 Python _is_coffee_for_plan 显式短路）
		if (plan.CategoryName == "合成电池")
		{
			return false;
		}
		if (plan.CategoryName == "实战模拟室" && coffee.CoffeeName == "浓缩咖啡")
		{
			return true;
		}
		if (coffee.WithoutBenefit || coffee.MissionType == null)
		{
			return false;
		}
		if (!string.Equals(coffee.MissionType.MissionTypeName, plan.MissionTypeName, StringComparison.Ordinal))
		{
			return false;
		}
		return coffee.Mission == null || string.Equals(coffee.Mission.MissionName, plan.MissionName, StringComparison.Ordinal);
	}

	private static void AddUnfinishedPlanPriorityCandidates(List<string> candidates, IReadOnlyList<ZzzOd.GameLogic.GameData.Coffee> scheduled, IEnumerable<ChargePlanItem> plans)
	{
		foreach (ChargePlanItem plan in plans)
		{
			if (plan.RunTimes < plan.PlanTimes)
			{
				ZzzOd.GameLogic.GameData.Coffee coffee = scheduled.FirstOrDefault();
				if (coffee != null && IsCoffeeForPlan(coffee, plan))
				{
					candidates.Add(coffee.CoffeeName);
				}
			}
		}
	}

	private static void AddFinishedPlanPriorityCandidates(List<string> candidates, IReadOnlyList<ZzzOd.GameLogic.GameData.Coffee> scheduled, IEnumerable<ChargePlanItem> plans, IReadOnlySet<string> hadCoffeeList)
	{
		foreach (ChargePlanItem plan in plans)
		{
			if (plan.RunTimes < plan.PlanTimes)
			{
				continue;
			}
			foreach (ZzzOd.GameLogic.GameData.Coffee item in scheduled)
			{
				if (hadCoffeeList.Contains(item.CoffeeName))
				{
					continue;
				}
				if (IsCoffeeForPlan(item, plan))
				{
					candidates.Add(item.CoffeeName);
				}
				break;
			}
		}
	}
}
