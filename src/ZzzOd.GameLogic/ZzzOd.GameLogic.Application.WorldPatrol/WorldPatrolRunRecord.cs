using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地运行记录。
/// </summary>
public sealed class WorldPatrolRunRecord : ZApplicationRunRecord
{
	private readonly Action<WorldPatrolRunRecordData>? _saveData;

	/// <summary>当日已完成路线。</summary>
	[YamlMember(Alias = "finished", ApplyNamingConventions = false)]
	public List<string> Finished { get; set; } = new List<string>();

	/// <summary>路线耗时缓存，BaselineParity 当前不持久化该字段。</summary>
	[YamlIgnore]
	public Dictionary<string, List<double>> TimeCost { get; } = new Dictionary<string, List<double>>(StringComparer.Ordinal);

	/// <summary>当日已完成轮数。</summary>
	[YamlMember(Alias = "completed_rounds", ApplyNamingConventions = false)]
	public int CompletedRounds { get; set; }

	/// <summary>本次任务每轮路线数。</summary>
	[YamlMember(Alias = "routes_per_round", ApplyNamingConventions = false)]
	public int RoutesPerRound { get; set; }

	/// <summary>当前轮次，运行时字段。</summary>
	[YamlIgnore]
	public int CurrentRound { get; set; } = 1;

	/// <summary>总轮次，运行时字段。</summary>
	[YamlIgnore]
	public int TotalRounds { get; set; } = 1;

	/// <summary>本轮开始时间戳，运行时字段。</summary>
	[YamlIgnore]
	public double? RoundStartTime { get; set; }

	/// <summary>本轮累计等待秒数，运行时字段。</summary>
	[YamlIgnore]
	public double RoundWaitSeconds { get; set; }

	/// <summary>本轮等待开始时间戳，运行时字段。</summary>
	[YamlIgnore]
	public double? RoundWaitStartTime { get; set; }

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public WorldPatrolRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<WorldPatrolRunRecordData>? saveData = null)
		: base("world_patrol", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
		_saveData = saveData;
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static WorldPatrolRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		YamlConfig<WorldPatrolRunRecordData> yamlConfig = new YamlConfig<WorldPatrolRunRecordData>(environment, "world_patrol", null, instanceIndex, new string[] { "app_run_record" });
		WorldPatrolRunRecordData current = yamlConfig.Current;
		WorldPatrolRunRecord worldPatrolRunRecord = new WorldPatrolRunRecord(gameRefreshHourOffset, now, delegate(WorldPatrolRunRecordData updated)
		{
			yamlConfig.Current.Dt = updated.Dt;
			yamlConfig.Current.RunTime = updated.RunTime;
			yamlConfig.Current.RunTimeFloat = updated.RunTimeFloat;
			yamlConfig.Current.RunStatus = updated.RunStatus;
			yamlConfig.Current.Finished = updated.Finished.ToList();
			yamlConfig.Current.CompletedRounds = updated.CompletedRounds;
			yamlConfig.Current.RoutesPerRound = updated.RoutesPerRound;
			yamlConfig.Save();
		})
		{
			Dt = current.Dt,
			RunTime = (string.IsNullOrWhiteSpace(current.RunTime) ? "-" : current.RunTime),
			RunTimeFloat = current.RunTimeFloat,
			RunStatus = current.RunStatus,
			Finished = current.Finished.ToList(),
			CompletedRounds = current.CompletedRounds,
			RoutesPerRound = current.RoutesPerRound
		};
		if (string.IsNullOrWhiteSpace(worldPatrolRunRecord.Dt))
		{
			worldPatrolRunRecord.Dt = new WorldPatrolRunRecord(gameRefreshHourOffset, now).Dt;
		}
		return worldPatrolRunRecord;
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
		Finished = new List<string>();
		CompletedRounds = 0;
		SaveData();
	}

	/// <summary>
	/// 重置本轮计时字段。
	/// </summary>
	public void ResetRoundTiming()
	{
		RoundStartTime = null;
		RoundWaitSeconds = 0.0;
		RoundWaitStartTime = null;
	}

	/// <summary>
	/// 清空当日已完成路线。
	/// </summary>
	public void ResetFinished()
	{
		Finished = new List<string>();
		SaveData();
	}

	/// <summary>
	/// 当日已完成轮数加一。
	/// </summary>
	public void IncCompletedRounds()
	{
		CompletedRounds++;
		SaveData();
	}

	/// <summary>
	/// 记录本次任务每轮路线数。
	/// </summary>
	public void SetRoutesPerRound(int count)
	{
		RoutesPerRound = count;
		SaveData();
	}

	/// <summary>
	/// 记录已完成路线。
	/// </summary>
	public void AddRecord(string routeId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(routeId, "routeId");
		Finished.Add(routeId);
		if (!TimeCost.ContainsKey(routeId))
		{
			TimeCost[routeId] = new List<double>();
		}
		while (TimeCost[routeId].Count > 3)
		{
			TimeCost[routeId].RemoveAt(0);
		}
		SaveData();
	}

	private void SaveData()
	{
		_saveData?.Invoke(new WorldPatrolRunRecordData
		{
			Dt = base.Dt,
			RunTime = base.RunTime,
			RunTimeFloat = base.RunTimeFloat,
			RunStatus = base.RunStatus,
			Finished = Finished.ToList(),
			CompletedRounds = CompletedRounds,
			RoutesPerRound = RoutesPerRound
		});
	}
}
