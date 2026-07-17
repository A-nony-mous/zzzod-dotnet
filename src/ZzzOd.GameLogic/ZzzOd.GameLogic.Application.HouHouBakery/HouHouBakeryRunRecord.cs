using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 吼吼饼铺运行记录。
/// </summary>
public sealed class HouHouBakeryRunRecord : ZApplicationRunRecord
{
	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public HouHouBakeryRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
		: base("hou_hou_bakery", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static HouHouBakeryRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "hou_hou_bakery", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		return new HouHouBakeryRunRecord(gameRefreshHourOffset, now)
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus
		};
	}
}
