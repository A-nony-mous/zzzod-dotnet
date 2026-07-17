using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.EngagementReward;

/// <summary>
/// 活跃度奖励运行记录。
/// </summary>
public sealed class EngagementRewardRunRecord : ZApplicationRunRecord
{
	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public EngagementRewardRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
		: base("engagement_reward", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static EngagementRewardRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "engagement_reward", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		return new EngagementRewardRunRecord(gameRefreshHourOffset, now)
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus
		};
	}
}
