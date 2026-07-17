using System;
using System.Collections.Generic;
using System.Globalization;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地运行记录。
/// </summary>
public sealed class LostVoidRunRecord : ZApplicationRunRecord
{
	private readonly LostVoidConfig _config;

	private readonly Func<DateTimeOffset> _now;

	private readonly Action<LostVoidRunRecordData>? _saveData;

	private bool _suppressSave;

	private int _dailyRunTimes;

	private int _weeklyRunTimes;

	private bool _bountyCommissionComplete;

	private bool _evalPointComplete;

	private bool _periodRewardComplete;

	private bool _completeTaskForceWithUp;

	/// <summary>今日完成次数。</summary>
	[YamlMember(Alias = "daily_run_times", ApplyNamingConventions = false)]
	public int DailyRunTimes
	{
		get
		{
			return _dailyRunTimes;
		}
		set
		{
			SetAndSave(ref _dailyRunTimes, value);
		}
	}

	/// <summary>本周完成次数。</summary>
	[YamlMember(Alias = "weekly_run_times", ApplyNamingConventions = false)]
	public int WeeklyRunTimes
	{
		get
		{
			return _weeklyRunTimes;
		}
		set
		{
			SetAndSave(ref _weeklyRunTimes, value);
		}
	}

	/// <summary>悬赏委托已完成。</summary>
	[YamlMember(Alias = "bounty_commission_complete", ApplyNamingConventions = false)]
	public bool BountyCommissionComplete
	{
		get
		{
			return _bountyCommissionComplete;
		}
		set
		{
			SetAndSave(ref _bountyCommissionComplete, value);
		}
	}

	/// <summary>业绩点已刷满。</summary>
	[YamlMember(Alias = "eval_point_complete", ApplyNamingConventions = false)]
	public bool EvalPointComplete
	{
		get
		{
			return _evalPointComplete;
		}
		set
		{
			SetAndSave(ref _evalPointComplete, value);
		}
	}

	/// <summary>周期奖励已刷满。</summary>
	[YamlMember(Alias = "period_reward_complete", ApplyNamingConventions = false)]
	public bool PeriodRewardComplete
	{
		get
		{
			return _periodRewardComplete;
		}
		set
		{
			SetAndSave(ref _periodRewardComplete, value);
		}
	}

	/// <summary>已使用 UP 代理人完成特遣调查。</summary>
	[YamlMember(Alias = "complete_task_force_with_up", ApplyNamingConventions = false)]
	public bool CompleteTaskForceWithUp
	{
		get
		{
			return _completeTaskForceWithUp;
		}
		set
		{
			SetAndSave(ref _completeTaskForceWithUp, value);
		}
	}

	/// <summary>按 BaselineParity 当周、当天完成条件计算当前展示状态。</summary>
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
			if (!string.Equals(base.Dt, currentDt, StringComparison.Ordinal))
			{
				return IsFinishedByWeek() ? 1 : 0;
			}
			return IsFinishedByDay() ? 1 : 0;
		}
	}

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public LostVoidRunRecord(LostVoidConfig config, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<LostVoidRunRecordData>? saveData = null)
		: base("lost_void", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Weekly, now)
	{
		_config = config;
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		_saveData = saveData;
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static LostVoidRunRecord Load(OneDragonEnvironment environment, LostVoidConfig config, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		YamlConfig<LostVoidRunRecordData> yamlConfig = new YamlConfig<LostVoidRunRecordData>(environment, "lost_void", null, instanceIndex, new string[] { "app_run_record" });
		LostVoidRunRecordData current = yamlConfig.Current;
		LostVoidRunRecord lostVoidRunRecord = new LostVoidRunRecord(config, gameRefreshHourOffset, now, delegate(LostVoidRunRecordData updated)
		{
			yamlConfig.Current.Dt = updated.Dt;
			yamlConfig.Current.RunTime = updated.RunTime;
			yamlConfig.Current.RunTimeFloat = updated.RunTimeFloat;
			yamlConfig.Current.RunStatus = updated.RunStatus;
			yamlConfig.Current.DailyRunTimes = updated.DailyRunTimes;
			yamlConfig.Current.WeeklyRunTimes = updated.WeeklyRunTimes;
			yamlConfig.Current.BountyCommissionComplete = updated.BountyCommissionComplete;
			yamlConfig.Current.EvalPointComplete = updated.EvalPointComplete;
			yamlConfig.Current.PeriodRewardComplete = updated.PeriodRewardComplete;
			yamlConfig.Current.CompleteTaskForceWithUp = updated.CompleteTaskForceWithUp;
			yamlConfig.Save();
		});
		lostVoidRunRecord._suppressSave = true;
		try
		{
			lostVoidRunRecord.Dt = current.Dt;
			lostVoidRunRecord.RunTime = (string.IsNullOrWhiteSpace(current.RunTime) ? "-" : current.RunTime);
			lostVoidRunRecord.RunTimeFloat = current.RunTimeFloat;
			lostVoidRunRecord.RunStatus = current.RunStatus;
			lostVoidRunRecord.DailyRunTimes = current.DailyRunTimes;
			lostVoidRunRecord.WeeklyRunTimes = current.WeeklyRunTimes;
			lostVoidRunRecord.BountyCommissionComplete = current.BountyCommissionComplete;
			lostVoidRunRecord.EvalPointComplete = current.EvalPointComplete;
			lostVoidRunRecord.PeriodRewardComplete = current.PeriodRewardComplete;
			lostVoidRunRecord.CompleteTaskForceWithUp = current.CompleteTaskForceWithUp;
		}
		finally
		{
			lostVoidRunRecord._suppressSave = false;
		}
		if (string.IsNullOrWhiteSpace(lostVoidRunRecord.Dt))
		{
			lostVoidRunRecord.Dt = lostVoidRunRecord.GetCurrentDt();
		}
		return lostVoidRunRecord;
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
		BountyCommissionComplete = false;
		EvalPointComplete = false;
		PeriodRewardComplete = false;
		CompleteTaskForceWithUp = false;
		SaveData();
	}

	/// <summary>
	/// 增加一次完成次数。
	/// </summary>
	public void AddCompleteTimes()
	{
		DailyRunTimes++;
		WeeklyRunTimes++;
		SaveData();
	}

	/// <summary>
	/// 按周是否完成。
	/// </summary>
	public bool IsFinishedByWeek()
	{
		string extraTask = _config.ExtraTask;
		if (1 == 0)
		{
		}
		bool result = extraTask switch
		{
			"完成悬赏委托" => BountyCommissionComplete, 
			"刷满业绩点" => EvalPointComplete, 
			"刷满周期奖励" => PeriodRewardComplete, 
			"完成周计划次数" => WeeklyRunTimes >= _config.WeeklyPlanTimes, 
			_ => false, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	/// <summary>
	/// 按天是否完成。
	/// </summary>
	public bool IsFinishedByDay()
	{
		return IsFinishedByWeek() || DailyRunTimes >= _config.DailyPlanTimes;
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
		if (!_suppressSave)
		{
			_saveData?.Invoke(new LostVoidRunRecordData
			{
				Dt = base.Dt,
				RunTime = base.RunTime,
				RunTimeFloat = base.RunTimeFloat,
				RunStatus = base.RunStatus,
				DailyRunTimes = DailyRunTimes,
				WeeklyRunTimes = WeeklyRunTimes,
				BountyCommissionComplete = BountyCommissionComplete,
				EvalPointComplete = EvalPointComplete,
				PeriodRewardComplete = PeriodRewardComplete,
				CompleteTaskForceWithUp = CompleteTaskForceWithUp
			});
		}
	}

	private void SetAndSave<T>(ref T field, T value)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			SaveData();
		}
	}
}
