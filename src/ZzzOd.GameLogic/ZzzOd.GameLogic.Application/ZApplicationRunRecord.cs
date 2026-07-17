using System;
using System.Globalization;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用通用运行记录。
/// </summary>
public class ZApplicationRunRecord : IApplicationRunRecord
{
	private Func<DateTimeOffset> _now;

	private Action? _save;

	/// <summary>
	/// 应用 id。
	/// </summary>
	[YamlIgnore]
	public string AppId { get; private set; }

	/// <summary>
	/// 记录日期，格式 yyyyMMdd。
	/// </summary>
	[YamlMember(Alias = "dt", ApplyNamingConventions = false)]
	public string Dt { get; set; } = string.Empty;

	/// <summary>
	/// 运行时间文本，格式 MM-dd HH:mm。
	/// </summary>
	[YamlMember(Alias = "run_time", ApplyNamingConventions = false)]
	public string RunTime { get; set; } = "-";

	/// <summary>
	/// 运行时间戳，单位秒。
	/// </summary>
	[YamlMember(Alias = "run_time_float", ApplyNamingConventions = false)]
	public double RunTimeFloat { get; set; }

	/// <summary>
	/// 运行状态。
	/// </summary>
	[YamlMember(Alias = "run_status", ApplyNamingConventions = false)]
	public int RunStatus { get; set; } = 0;

	/// <summary>
	/// 游戏刷新小时偏移。
	/// </summary>
	[YamlIgnore]
	public int GameRefreshHourOffset { get; private set; }

	/// <summary>
	/// 运行记录周期。
	/// </summary>
	[YamlIgnore]
	public ZApplicationRunRecordPeriod RecordPeriod { get; private set; }

	/// <summary>
	/// 当前日期下的运行状态。
	/// </summary>
	[YamlIgnore]
	public int RunStatusUnderNow => (!ShouldResetByDt()) ? RunStatus : 0;

	/// <summary>
	/// 当前记录是否已完成。
	/// </summary>
	[YamlIgnore]
	public bool IsDone => RunStatus == 1;

	/// <summary>
	/// 初始化 YAML 反序列化使用的运行记录。
	/// </summary>
	public ZApplicationRunRecord()
		: this(string.Empty)
	{
		Dt = string.Empty;
	}

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public ZApplicationRunRecord(string appId, int gameRefreshHourOffset = 0, ZApplicationRunRecordPeriod recordPeriod = ZApplicationRunRecordPeriod.Daily, Func<DateTimeOffset>? now = null)
	{
		AppId = appId;
		GameRefreshHourOffset = gameRefreshHourOffset;
		RecordPeriod = recordPeriod;
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		Dt = GetCurrentDt();
	}

	/// <summary>
	/// 从 YAML 加载运行记录。
	/// </summary>
	public static ZApplicationRunRecord Load(OneDragonEnvironment environment, string appId, int instanceIndex, int gameRefreshHourOffset = 0, ZApplicationRunRecordPeriod recordPeriod = ZApplicationRunRecordPeriod.Daily, Func<DateTimeOffset>? now = null)
	{
		YamlConfig<ZApplicationRunRecord> yamlConfig = new YamlConfig<ZApplicationRunRecord>(environment, appId, null, instanceIndex, new string[] { "app_run_record" });
		ZApplicationRunRecord current = yamlConfig.Current;
		current.ConfigureRuntime(appId, gameRefreshHourOffset, recordPeriod, now, delegate
		{
			yamlConfig.Save();
		});
		if (string.IsNullOrWhiteSpace(current.Dt))
		{
			current.Dt = current.GetCurrentDt();
		}
		return current;
	}

	/// <inheritdoc />
	public virtual void CheckAndUpdateStatus()
	{
		if (ShouldResetByDt())
		{
			ResetRecord();
		}
	}

	/// <summary>
	/// 更新运行状态。
	/// </summary>
	public virtual void UpdateStatus(int newStatus, bool onlyStatus = false)
	{
		RunStatus = newStatus;
		if (!onlyStatus)
		{
			DateTimeOffset current = _now();
			Dt = GetCurrentDt(current);
			RunTime = current.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
			RunTimeFloat = (double)current.ToUnixTimeMilliseconds() / 1000.0;
		}
		_save?.Invoke();
	}

	/// <summary>
	/// 重置运行记录。
	/// </summary>
	public virtual void ResetRecord()
	{
		UpdateStatus(0, onlyStatus: true);
	}

	/// <summary>
	/// 配置派生运行记录的应用、刷新周期、时钟和持久化回调。
	/// </summary>
	protected void ConfigureRuntime(string appId, int gameRefreshHourOffset, ZApplicationRunRecordPeriod recordPeriod, Func<DateTimeOffset>? now, Action save)
	{
		AppId = appId;
		GameRefreshHourOffset = gameRefreshHourOffset;
		RecordPeriod = recordPeriod;
		_save = save;
		if (now != null)
		{
			_now = now;
		}
	}

	private bool ShouldResetByDt()
	{
		string currentDt = GetCurrentDt();
		ZApplicationRunRecordPeriod recordPeriod = RecordPeriod;
		if (1 == 0)
		{
		}
		bool result = recordPeriod switch
		{
			ZApplicationRunRecordPeriod.Daily => !string.Equals(Dt, currentDt, StringComparison.Ordinal), 
			ZApplicationRunRecordPeriod.Weekly => !string.Equals(GetSundayDt(Dt), GetSundayDt(currentDt), StringComparison.Ordinal), 
			_ => true, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private string GetCurrentDt()
	{
		return GetCurrentDt(_now());
	}

	private string GetCurrentDt(DateTimeOffset current)
	{
		return current.ToUniversalTime().ToOffset(TimeSpan.FromHours(GameRefreshHourOffset)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
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
