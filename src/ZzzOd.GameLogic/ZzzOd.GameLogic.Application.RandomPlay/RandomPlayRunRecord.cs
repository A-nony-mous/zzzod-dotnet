using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业运行记录。
/// </summary>
public sealed class RandomPlayRunRecord : ZApplicationRunRecord
{
	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public RandomPlayRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
		: base("random_play", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static RandomPlayRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "random_play", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		return new RandomPlayRunRecord(gameRefreshHourOffset, now)
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus
		};
	}
}
