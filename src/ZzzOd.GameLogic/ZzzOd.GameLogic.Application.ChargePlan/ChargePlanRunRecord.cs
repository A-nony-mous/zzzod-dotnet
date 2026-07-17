using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划运行记录。
/// </summary>
public sealed class ChargePlanRunRecord : ZApplicationRunRecord
{
	/// <summary>最大电量。</summary>
	public const int MaxChargePower = 240;

	private Func<DateTimeOffset> _now;

	private Action<List<int>>? _saveChargePowerSnapshot;

	/// <summary>
	/// 当前电量快照，格式为 [电量, 记录 Unix 秒]。
	/// </summary>
	[YamlMember(Alias = "current_charge_power_snapshot", ApplyNamingConventions = false)]
	public List<int> ChargePowerSnapshot { get; set; }

	/// <summary>
	/// 初始化 YAML 反序列化使用的运行记录。
	/// </summary>
	public ChargePlanRunRecord()
		: this(0, null, null)
	{
	}

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public ChargePlanRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<List<int>>? saveChargePowerSnapshot = null)
		: base("charge_plan", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = 0;
		span[1] = -1;
		ChargePowerSnapshot = list;
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		_saveChargePowerSnapshot = saveChargePowerSnapshot;
	}

	/// <inheritdoc />
	public override void CheckAndUpdateStatus()
	{
		ResetRecord();
	}

	/// <inheritdoc />
	public override void ResetRecord()
	{
		base.ResetRecord();
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = 0;
		span[1] = -1;
		ChargePowerSnapshot = list;
		_saveChargePowerSnapshot?.Invoke(ChargePowerSnapshot);
	}

	/// <summary>
	/// 记录当前电量。
	/// </summary>
	public void RecordCurrentChargePower(int chargePower)
	{
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = chargePower;
		span[1] = (int)_now().ToUnixTimeSeconds();
		ChargePowerSnapshot = list;
		_saveChargePowerSnapshot?.Invoke(ChargePowerSnapshot);
	}

	/// <summary>
	/// 估算当前恢复后的电量。
	/// </summary>
	public int GetEstimatedChargePower()
	{
		if (ChargePowerSnapshot.Count < 2 || ChargePowerSnapshot[1] == -1)
		{
			return -1;
		}
		int num = ChargePowerSnapshot[0];
		int num2 = ChargePowerSnapshot[1];
		int num3 = Math.Max(0, (int)_now().ToUnixTimeSeconds() - num2);
		int num4 = num3 / 360;
		return Math.Min(num + num4, 240);
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static ChargePlanRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		YamlConfig<ChargePlanRunRecord> yamlConfig = new YamlConfig<ChargePlanRunRecord>(environment, "charge_plan", null, instanceIndex, new string[] { "app_run_record" });
		ChargePlanRunRecord current = yamlConfig.Current;
		current.ConfigurePersistence(gameRefreshHourOffset, now, delegate
		{
			yamlConfig.Save();
		});
		return current;
	}

	private void ConfigurePersistence(int gameRefreshHourOffset, Func<DateTimeOffset>? now, Action save)
	{
		ArgumentNullException.ThrowIfNull(save, "save");
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		ConfigureRuntime("charge_plan", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now, save);
		_saveChargePowerSnapshot = delegate
		{
			save();
		};
	}
}
