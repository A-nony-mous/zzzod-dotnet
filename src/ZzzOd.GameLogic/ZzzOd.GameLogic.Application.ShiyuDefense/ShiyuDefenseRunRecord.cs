using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战运行记录。
/// </summary>
public sealed class ShiyuDefenseRunRecord : ZApplicationRunRecord
{
	private readonly ShiyuDefenseConfig _config;

	private readonly Action<ShiyuDefenseRunRecordData>? _saveData;

	/// <summary>
	/// 已完成剧变节点。
	/// </summary>
	[YamlMember(Alias = "critical_history", ApplyNamingConventions = false)]
	public List<int> CriticalHistory { get; set; } = new List<int>();

	/// <summary>
	/// 当前日期下的运行状态。
	/// </summary>
	[YamlIgnore]
	public override int RunStatusUnderNow
	{
		get
		{
			if (!NextNodeIndex().HasValue)
			{
				return 1;
			}
			return base.RunStatusUnderNow;
		}
	}

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public ShiyuDefenseRunRecord(ShiyuDefenseConfig config, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<ShiyuDefenseRunRecordData>? saveData = null)
		: base("shiyu_defense", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
		_config = config;
		_saveData = saveData;
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static ShiyuDefenseRunRecord Load(OneDragonEnvironment environment, int instanceIndex, ShiyuDefenseConfig config, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		YamlConfig<ShiyuDefenseRunRecordData> yamlConfig = new YamlConfig<ShiyuDefenseRunRecordData>(environment, "shiyu_defense", null, instanceIndex, new string[] { "app_run_record" });
		ShiyuDefenseRunRecordData current = yamlConfig.Current;
		ShiyuDefenseRunRecord shiyuDefenseRunRecord = new ShiyuDefenseRunRecord(config, gameRefreshHourOffset, now, delegate(ShiyuDefenseRunRecordData updated)
		{
			yamlConfig.Current.Dt = updated.Dt;
			yamlConfig.Current.RunTime = updated.RunTime;
			yamlConfig.Current.RunTimeFloat = updated.RunTimeFloat;
			yamlConfig.Current.RunStatus = updated.RunStatus;
			yamlConfig.Current.CriticalHistory = updated.CriticalHistory.ToList();
			yamlConfig.Save();
		})
		{
			Dt = current.Dt,
			RunTime = (string.IsNullOrWhiteSpace(current.RunTime) ? "-" : current.RunTime),
			RunTimeFloat = current.RunTimeFloat,
			RunStatus = current.RunStatus,
			CriticalHistory = current.CriticalHistory.ToList()
		};
		if (string.IsNullOrWhiteSpace(shiyuDefenseRunRecord.Dt))
		{
			shiyuDefenseRunRecord.Dt = (now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now)))().ToUniversalTime().ToOffset(TimeSpan.FromHours(gameRefreshHourOffset)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		}
		return shiyuDefenseRunRecord;
	}

	/// <summary>
	/// 获取下一个需要挑战的节点下标。
	/// </summary>
	public int? NextNodeIndex()
	{
		HashSet<int> hashSet = CriticalHistory.ToHashSet();
		for (int i = 1; i <= _config.CriticalMaxNodeIndex; i++)
		{
			if (!hashSet.Contains(i))
			{
				return i;
			}
		}
		return null;
	}

	/// <summary>
	/// 记录已完成节点。
	/// </summary>
	public void AddNodeFinished(int nodeIndex)
	{
		if (!CriticalHistory.Contains(nodeIndex))
		{
			CriticalHistory.Add(nodeIndex);
			SaveData();
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
		base.ResetRecord();
		CriticalHistory = new List<int>();
		SaveData();
	}

	private void SaveData()
	{
		_saveData?.Invoke(new ShiyuDefenseRunRecordData
		{
			Dt = base.Dt,
			RunTime = base.RunTime,
			RunTimeFloat = base.RunTimeFloat,
			RunStatus = base.RunStatus,
			CriticalHistory = CriticalHistory.ToList()
		});
	}
}
