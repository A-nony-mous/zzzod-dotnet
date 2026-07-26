using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.DailySignIn;

/// <summary>
/// 每日签到运行记录。
/// </summary>
public sealed class DailySignInRunRecord : ZApplicationRunRecord
{
	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public DailySignInRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
		: base("daily_signin", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static DailySignInRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "daily_signin", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		return new DailySignInRunRecord(gameRefreshHourOffset, now)
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus
		};
	}
}
