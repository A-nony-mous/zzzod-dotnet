using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码应用运行记录。
/// </summary>
public sealed class RedemptionCodeRunRecord : ZApplicationRunRecord
{
	private sealed class RedemptionCodeRunRecordData
	{
		[YamlMember(Alias = "used_code_list", ApplyNamingConventions = false)]
		public List<string> UsedCodeList { get; set; } = new List<string>();
	}

	private readonly Func<RedemptionCodeConfig> _configFactory;

	private readonly Func<DateTimeOffset> _now;

	private readonly Action<List<string>>? _saveUsedCodeList;

	/// <summary>
	/// 有效兑换码列表。
	/// </summary>
	[YamlIgnore]
	public IReadOnlyList<RedemptionCodeEntry> ValidCodeList { get; private set; }

	/// <summary>
	/// 已使用的兑换码。
	/// </summary>
	[YamlMember(Alias = "used_code_list", ApplyNamingConventions = false)]
	public List<string> UsedCodeList { get; set; } = new List<string>();

	/// <summary>
	/// 当前日期下的运行状态。
	/// </summary>
	[YamlIgnore]
	public new int RunStatusUnderNow => (GetUnusedCodeList(GetCurrentDt()).Count <= 0) ? base.RunStatusUnderNow : 0;

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public RedemptionCodeRunRecord(int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Func<RedemptionCodeConfig>? configFactory = null, Action<List<string>>? saveUsedCodeList = null)
		: base("redemption_code", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now)
	{
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		_configFactory = configFactory ?? ((Func<RedemptionCodeConfig>)(() => new RedemptionCodeConfig(new OneDragonEnvironment("test_project", "test_user_id"))));
		_saveUsedCodeList = saveUsedCodeList;
		ValidCodeList = LoadRedemptionCodes();
	}

	/// <inheritdoc />
	public override void CheckAndUpdateStatus()
	{
		if (GetUnusedCodeList(GetCurrentDt()).Count > 0)
		{
			ResetRecord();
		}
		else
		{
			base.CheckAndUpdateStatus();
		}
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static RedemptionCodeRunRecord Load(OneDragonEnvironment environment, int instanceIndex, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, RedemptionCodeConfig? config = null)
	{
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "redemption_code", instanceIndex, gameRefreshHourOffset, ZApplicationRunRecordPeriod.Daily, now);
		YamlConfig<RedemptionCodeRunRecordData> yamlConfig = new YamlConfig<RedemptionCodeRunRecordData>(environment, "redemption_code", null, instanceIndex, new string[] { "app_run_record" });
		return new RedemptionCodeRunRecord(gameRefreshHourOffset, now, () => config ?? RedemptionCodeConfig.Load(environment, instanceIndex, "one_dragon"), delegate(List<string> usedCodeList)
		{
			yamlConfig.Update(delegate(RedemptionCodeRunRecordData data)
			{
				data.UsedCodeList = usedCodeList.ToList();
			});
		})
		{
			Dt = zApplicationRunRecord.Dt,
			RunTime = zApplicationRunRecord.RunTime,
			RunTimeFloat = zApplicationRunRecord.RunTimeFloat,
			RunStatus = zApplicationRunRecord.RunStatus,
			UsedCodeList = yamlConfig.Current.UsedCodeList.ToList()
		};
	}

	/// <summary>
	/// 按日期获取未使用且未过期的兑换码。
	/// </summary>
	public IReadOnlyList<string> GetUnusedCodeList(string dt)
	{
		HashSet<string> used = new HashSet<string>(UsedCodeList, StringComparer.Ordinal);
		return (from item in ValidCodeList
			where string.CompareOrdinal(item.EndDt, dt) >= 0
			select item.Code into code
			where !used.Contains(code)
			select code).ToArray();
	}

	/// <summary>
	/// 添加已使用兑换码。
	/// </summary>
	public void AddUsedCode(string code)
	{
		UsedCodeList.Add(code);
		_saveUsedCodeList?.Invoke(UsedCodeList);
	}

	private IReadOnlyList<RedemptionCodeEntry> LoadRedemptionCodes()
	{
		RedemptionCodeConfig redemptionCodeConfig = _configFactory();
		return redemptionCodeConfig.CodesDict.Select<KeyValuePair<string, int>, RedemptionCodeEntry>((KeyValuePair<string, int> item) => new RedemptionCodeEntry(item.Key, item.Value.ToString(CultureInfo.InvariantCulture))).ToArray();
	}

	private string GetCurrentDt()
	{
		return _now().ToUniversalTime().ToOffset(TimeSpan.FromHours(base.GameRefreshHourOffset)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
	}
}
