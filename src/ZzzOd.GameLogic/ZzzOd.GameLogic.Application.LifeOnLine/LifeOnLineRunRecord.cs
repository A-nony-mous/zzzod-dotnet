using System;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线运行记录。
/// </summary>
public sealed class LifeOnLineRunRecord : ZApplicationRunRecord
{
	private readonly LifeOnLineConfig _config;

	private readonly Action<LifeOnLineRunRecordData>? _saveData;

	/// <summary>
	/// 当日完成次数。
	/// </summary>
	[YamlMember(Alias = "daily_run_times", ApplyNamingConventions = false)]
	public int DailyRunTimes { get; set; }

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public LifeOnLineRunRecord(LifeOnLineConfig? config = null, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<LifeOnLineRunRecordData>? saveData = null)
		: base("life_on_line", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
		_config = config ?? new LifeOnLineConfig();
		_saveData = saveData;
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static LifeOnLineRunRecord Load(OneDragonEnvironment environment, int instanceIndex, LifeOnLineConfig config, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "life_on_line", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		YamlConfig<LifeOnLineRunRecordData> yamlConfig = new YamlConfig<LifeOnLineRunRecordData>(environment, "life_on_line", null, instanceIndex, new string[] { "app_run_record" });
		LifeOnLineRunRecordData current = yamlConfig.Current;
		return new LifeOnLineRunRecord(config, gameRefreshHourOffset, now, delegate(LifeOnLineRunRecordData updated)
		{
			yamlConfig.Current.DailyRunTimes = updated.DailyRunTimes;
			yamlConfig.Save();
		})
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus,
			DailyRunTimes = current.DailyRunTimes
		};
	}

	/// <inheritdoc />
	public override void ResetRecord()
	{
		base.ResetRecord();
		DailyRunTimes = 0;
		SaveData();
	}

	/// <summary>
	/// 增加通关次数。
	/// </summary>
	public void AddTimes()
	{
		DailyRunTimes++;
		SaveData();
	}

	/// <summary>
	/// 当前次数是否达到计划。
	/// </summary>
	public bool IsFinishedByTimes()
	{
		return DailyRunTimes >= _config.DailyPlanTimes;
	}

	private void SaveData()
	{
		_saveData?.Invoke(new LifeOnLineRunRecordData
		{
			DailyRunTimes = DailyRunTimes
		});
	}
}
