using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 通知应用运行记录。
/// </summary>
public sealed class NotifyRunRecord : ZApplicationRunRecord
{
	/// <summary>
	/// 初始化通知运行记录。
	/// </summary>
	public NotifyRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
		: base("notify", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
	}

	/// <inheritdoc />
	public override void CheckAndUpdateStatus()
	{
		ResetRecord();
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static NotifyRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "notify", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		return new NotifyRunRecord(gameRefreshHourOffset, now)
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus
		};
	}
}
