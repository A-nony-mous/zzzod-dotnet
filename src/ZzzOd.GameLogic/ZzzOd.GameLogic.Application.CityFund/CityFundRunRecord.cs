using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.CityFund;

/// <summary>
/// 丽都城募应用运行记录。
/// </summary>
public sealed class CityFundRunRecord : ZApplicationRunRecord
{
	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public CityFundRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
		: base("city_fund", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static CityFundRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "city_fund", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		return new CityFundRunRecord(gameRefreshHourOffset, now)
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus
		};
	}
}
