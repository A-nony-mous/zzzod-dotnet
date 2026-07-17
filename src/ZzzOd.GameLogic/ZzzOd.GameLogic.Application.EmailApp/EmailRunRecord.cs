using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.EmailApp;

/// <summary>
/// 邮件应用运行记录。
/// </summary>
public sealed class EmailRunRecord : ZApplicationRunRecord
{
	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public EmailRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
		: base("email", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static EmailRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "email", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		return new EmailRunRecord(gameRefreshHourOffset, now)
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus
		};
	}
}
