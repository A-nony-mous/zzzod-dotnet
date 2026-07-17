using System;
using System.Globalization;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都运行记录。
/// </summary>
public sealed class WitheredDomainRunRecord : ZApplicationRunRecord
{
	private readonly WitheredDomainConfig _config;

	private readonly Func<DateTimeOffset> _now;

	private readonly Action<WitheredDomainRunRecordData>? _saveData;

	/// <summary>本周运行次数。</summary>
	[YamlMember(Alias = "weekly_run_times", ApplyNamingConventions = false)]
	public int WeeklyRunTimes { get; set; }

	/// <summary>今日进入次数。</summary>
	[YamlMember(Alias = "daily_run_times", ApplyNamingConventions = false)]
	public int DailyRunTimes { get; set; }

	/// <summary>业绩点已空。</summary>
	[YamlMember(Alias = "no_eval_point", ApplyNamingConventions = false)]
	public bool NoEvalPoint { get; set; }

	/// <summary>周期奖励已满。</summary>
	[YamlMember(Alias = "period_reward_complete", ApplyNamingConventions = false)]
	public bool PeriodRewardComplete { get; set; }

	/// <summary>
	/// 基于当前日期和周计划计算的运行状态。
	/// </summary>
	[YamlIgnore]
	public new int RunStatusUnderNow
	{
		get
		{
			string currentDt = GetCurrentDt();
			if (!string.Equals(GetSundayDt(base.Dt), GetSundayDt(currentDt), StringComparison.Ordinal))
			{
				return 0;
			}
			return IsFinishedByDay() ? 1 : 0;
		}
	}

	/// <summary>
	/// 当前日期和周计划下是否完成。
	/// </summary>
	[YamlIgnore]
	public new bool IsDone
	{
		get
		{
			string currentDt = GetCurrentDt();
			return string.Equals(GetSundayDt(base.Dt), GetSundayDt(currentDt), StringComparison.Ordinal) && (string.Equals(base.Dt, currentDt, StringComparison.Ordinal) ? IsFinishedByDay() : IsFinishedByWeek());
		}
	}

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public WitheredDomainRunRecord(WitheredDomainConfig config, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<WitheredDomainRunRecordData>? saveData = null)
		: base("withered_domain", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Weekly, now)
	{
		_config = config;
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		_saveData = saveData;
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static WitheredDomainRunRecord Load(OneDragonEnvironment environment, WitheredDomainConfig config, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		YamlConfig<WitheredDomainRunRecordData> yamlConfig = new YamlConfig<WitheredDomainRunRecordData>(environment, "withered_domain", null, instanceIndex, new string[] { "app_run_record" });
		WitheredDomainRunRecordData current = yamlConfig.Current;
		WitheredDomainRunRecord witheredDomainRunRecord = new WitheredDomainRunRecord(config, gameRefreshHourOffset, now, delegate(WitheredDomainRunRecordData updated)
		{
			yamlConfig.Current.Dt = updated.Dt;
			yamlConfig.Current.RunTime = updated.RunTime;
			yamlConfig.Current.RunTimeFloat = updated.RunTimeFloat;
			yamlConfig.Current.RunStatus = updated.RunStatus;
			yamlConfig.Current.WeeklyRunTimes = updated.WeeklyRunTimes;
			yamlConfig.Current.DailyRunTimes = updated.DailyRunTimes;
			yamlConfig.Current.NoEvalPoint = updated.NoEvalPoint;
			yamlConfig.Current.PeriodRewardComplete = updated.PeriodRewardComplete;
			yamlConfig.Save();
		})
		{
			Dt = current.Dt,
			RunTime = (string.IsNullOrWhiteSpace(current.RunTime) ? "-" : current.RunTime),
			RunTimeFloat = current.RunTimeFloat,
			RunStatus = current.RunStatus,
			WeeklyRunTimes = current.WeeklyRunTimes,
			DailyRunTimes = current.DailyRunTimes,
			NoEvalPoint = current.NoEvalPoint,
			PeriodRewardComplete = current.PeriodRewardComplete
		};
		if (string.IsNullOrWhiteSpace(witheredDomainRunRecord.Dt))
		{
			witheredDomainRunRecord.Dt = witheredDomainRunRecord.GetCurrentDt();
		}
		return witheredDomainRunRecord;
	}

	/// <inheritdoc />
	public override void CheckAndUpdateStatus()
	{
		string currentDt = GetCurrentDt();
		if (!string.Equals(GetSundayDt(base.Dt), GetSundayDt(currentDt), StringComparison.Ordinal))
		{
			ResetRecord();
			ResetForWeekly();
		}
		else if (!string.Equals(base.Dt, currentDt, StringComparison.Ordinal))
		{
			ResetRecord();
			DailyRunTimes = 0;
			SaveData();
		}
		else if (!IsFinishedByWeek() && !IsFinishedByDay())
		{
			ResetRecord();
		}
	}

	/// <inheritdoc />
	public override void UpdateStatus(int newStatus, bool onlyStatus = false)
	{
		base.UpdateStatus(newStatus, onlyStatus);
		SaveData();
	}

	/// <inheritdoc />
	public override void ResetRecord()
	{
		UpdateStatus(0, onlyStatus: true);
	}

	/// <summary>
	/// 重置每周记录。
	/// </summary>
	public void ResetForWeekly()
	{
		WeeklyRunTimes = 0;
		DailyRunTimes = 0;
		NoEvalPoint = false;
		PeriodRewardComplete = false;
		SaveData();
	}

	/// <summary>
	/// 增加通关次数。
	/// </summary>
	public void AddTimes()
	{
		WeeklyRunTimes++;
		SaveData();
	}

	/// <summary>
	/// 增加今日进入次数。
	/// </summary>
	public void AddDailyTimes()
	{
		DailyRunTimes++;
		SaveData();
	}

	/// <summary>
	/// 写入周期奖励领取状态。
	/// </summary>
	public void SetPeriodRewardComplete(bool complete)
	{
		PeriodRewardComplete = complete;
		SaveData();
	}

	/// <summary>
	/// 写入业绩考察点已空状态。
	/// </summary>
	public void SetNoEvalPoint(bool complete)
	{
		NoEvalPoint = complete;
		SaveData();
	}

	/// <summary>
	/// 运行次数是否达到计划。
	/// </summary>
	public bool IsFinishedByTimes()
	{
		return WeeklyRunTimes >= _config.WeeklyPlanTimes || DailyRunTimes >= _config.DailyPlanTimes;
	}

	/// <summary>
	/// 每周次数是否达到计划。
	/// </summary>
	public bool IsFinishedByWeeklyTimes()
	{
		return WeeklyRunTimes >= _config.WeeklyPlanTimes;
	}

	/// <summary>
	/// 今日是否完成。
	/// </summary>
	public bool IsFinishedByDay()
	{
		return IsFinishedByWeek() || DailyRunTimes >= _config.DailyPlanTimes;
	}

	/// <summary>
	/// 本周是否完成。
	/// </summary>
	public bool IsFinishedByWeek()
	{
		if (WeeklyRunTimes < _config.WeeklyPlanTimes)
		{
			return false;
		}
		string extraTask = _config.ExtraTask;
		if (1 == 0)
		{
		}
		bool result = extraTask switch
		{
			"不进行" => true, 
			"刷满业绩点" => NoEvalPoint, 
			"刷满周期奖励" => PeriodRewardComplete, 
			_ => false, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private string GetCurrentDt()
	{
		return _now().ToUniversalTime().ToOffset(TimeSpan.FromHours(base.GameRefreshHourOffset)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
	}

	private static string GetSundayDt(string dt)
	{
		if (!DateTime.TryParseExact(dt, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return string.Empty;
		}
		int num = (int)(result.DayOfWeek + 6) % 7;
		return result.AddDays(6 - num).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
	}

	private void SaveData()
	{
		_saveData?.Invoke(new WitheredDomainRunRecordData
		{
			Dt = base.Dt,
			RunTime = base.RunTime,
			RunTimeFloat = base.RunTimeFloat,
			RunStatus = base.RunStatus,
			WeeklyRunTimes = WeeklyRunTimes,
			DailyRunTimes = DailyRunTimes,
			NoEvalPoint = NoEvalPoint,
			PeriodRewardComplete = PeriodRewardComplete
		});
	}
}
