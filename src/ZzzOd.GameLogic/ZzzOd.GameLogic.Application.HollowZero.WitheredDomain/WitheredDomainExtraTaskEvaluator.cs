using System;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 按 BaselineParity HollowRunner 的规则判断额外任务是否应离开空洞。
/// </summary>
public static class WitheredDomainExtraTaskEvaluator
{
	/// <summary>
	/// 判断当前地图是否满足额外任务退出条件。
	/// </summary>
	public static bool ShouldLeave(WitheredDomainConfig config, WitheredDomainRunRecord runRecord, WitheredDomainContext context, HollowZeroMap currentMap)
	{
		ArgumentNullException.ThrowIfNull(config, "config");
		ArgumentNullException.ThrowIfNull(runRecord, "runRecord");
		ArgumentNullException.ThrowIfNull(context, "context");
		ArgumentNullException.ThrowIfNull(currentMap, "currentMap");
		if (runRecord.IsFinishedByDay())
		{
			return true;
		}
		if (!runRecord.IsFinishedByWeeklyTimes() || string.Equals(config.ExtraTask, "不进行", StringComparison.Ordinal))
		{
			return false;
		}
		bool flag = currentMap.ContainsEntry("业绩考察点空");
		if (flag)
		{
			runRecord.SetNoEvalPoint(complete: true);
		}
		string extraExit = config.ExtraExit;
		if (1 == 0)
		{
		}
		bool flag2 = ((extraExit == "2层业绩后退出") ? IsExitByLevel(context, currentMap, flag, 2) : (extraExit == "3层业绩后退出" && IsExitByLevel(context, currentMap, flag, 3)));
		if (1 == 0)
		{
		}
		bool flag3 = flag2;
		string extraTask = config.ExtraTask;
		flag2 = ((extraTask == "刷满业绩点" || extraTask == "刷满周期奖励") ? true : false);
		return flag2 && !string.Equals(config.ExtraExit, "通关", StringComparison.Ordinal) && flag3;
	}

	private static bool IsExitByLevel(WitheredDomainContext context, HollowZeroMap currentMap, bool emptyEvaPoint, int targetLevel)
	{
		HollowLevelInfo levelInfo = context.LevelInfo;
		if (targetLevel == 2 && (levelInfo.Level > 2 || (levelInfo.Level == 2 && levelInfo.Phase > 1)))
		{
			return true;
		}
		if (targetLevel == 3 && levelInfo.Level == 3 && levelInfo.Phase > 1)
		{
			return true;
		}
		return levelInfo.Level == targetLevel && levelInfo.Phase == 1 && (emptyEvaPoint || (context.HadBeenEntry("业绩考察点") && !currentMap.ContainsEntry("业绩考察点")));
	}
}
