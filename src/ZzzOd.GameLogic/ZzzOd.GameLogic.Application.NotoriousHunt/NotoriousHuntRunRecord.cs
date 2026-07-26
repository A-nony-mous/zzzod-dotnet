using System;
using System.Globalization;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎运行记录。
/// </summary>
public sealed class NotoriousHuntRunRecord : ZApplicationRunRecord
{
	private sealed class NotoriousHuntRunRecordData
	{
		[YamlMember(Alias = "left_times", ApplyNamingConventions = false)]
		public int LeftTimes { get; set; } = 3;
	}

	private readonly NotoriousHuntConfig? _config;

	private readonly Func<DateTimeOffset> _now;

	private readonly Action<int>? _saveLeftTimes;

	/// <summary>
	/// 剩余奖励次数。
	/// </summary>
	[YamlMember(Alias = "left_times", ApplyNamingConventions = false)]
	public int LeftTimes { get; set; } = 3;

	/// <summary>
	/// 当前星期，星期一为 1，星期日为 7。
	/// </summary>
	[YamlIgnore]
	public int CurrentWeekday
	{
		get
		{
			int dayOfWeek = (int)_now().ToUniversalTime().ToOffset(TimeSpan.FromHours(base.GameRefreshHourOffset)).DayOfWeek;
			return (dayOfWeek == 0) ? 7 : dayOfWeek;
		}
	}

	/// <summary>
	/// 今日是否允许自动运行。
	/// </summary>
	[YamlIgnore]
	public bool IsAutoRunAllowedToday => _config == null || CurrentWeekday >= _config.WeeklyChallengeStartWeekday;

	/// <summary>
	/// 是否按本周完成。
	/// </summary>
	[YamlIgnore]
	public bool IsFinishedByWeek => !ShouldResetByCurrentWeek() && LeftTimes <= 0;

	/// <summary>
	/// 当前日期下的运行状态。
	/// </summary>
	[YamlIgnore]
	public override int RunStatusUnderNow
	{
		get
		{
			if (ShouldResetByCurrentWeek())
			{
				return 0;
			}
			return (LeftTimes <= 0) ? 1 : base.RunStatus;
		}
	}

	/// <summary>
	/// 记录是否完成。
	/// </summary>
	[YamlIgnore]
	public override bool IsDone => IsFinishedByWeek || !IsAutoRunAllowedToday;

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public NotoriousHuntRunRecord(NotoriousHuntConfig? config = null, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<int>? saveLeftTimes = null)
		: base("notorious_hunt", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Weekly, now)
	{
		_config = config;
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		_saveLeftTimes = saveLeftTimes;
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static NotoriousHuntRunRecord Load(OneDragonEnvironment environment, int instanceIndex, NotoriousHuntConfig config, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "notorious_hunt", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Weekly, now);
		YamlConfig<NotoriousHuntRunRecordData> yamlConfig = new YamlConfig<NotoriousHuntRunRecordData>(environment, "notorious_hunt", null, instanceIndex, new string[] { "app_run_record" });
		return new NotoriousHuntRunRecord(config, gameRefreshHourOffset, now, delegate(int leftTimes)
		{
			yamlConfig.Update(delegate(NotoriousHuntRunRecordData data)
			{
				data.LeftTimes = leftTimes;
			});
		})
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus,
			LeftTimes = yamlConfig.Current.LeftTimes
		};
	}

	/// <inheritdoc />
	public override void ResetRecord()
	{
		base.ResetRecord();
		LeftTimes = 3;
		_saveLeftTimes?.Invoke(LeftTimes);
	}

	/// <summary>
	/// 更新剩余奖励次数。
	/// </summary>
	public void UpdateLeftTimes(int leftTimes)
	{
		LeftTimes = leftTimes;
		_saveLeftTimes?.Invoke(LeftTimes);
	}

	private bool ShouldResetByCurrentWeek()
	{
		return !string.Equals(GetSundayDt(base.Dt), GetSundayDt(GetCurrentDt()), StringComparison.Ordinal);
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
}
