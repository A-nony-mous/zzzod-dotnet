using System;
using System.Globalization;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板运行记录。
/// </summary>
public sealed class IntelBoardRunRecord : ZApplicationRunRecord
{
	/// <summary>每次恶名狩猎经验。</summary>
	public const int ExpPerNotoriousHunt = 500;

	/// <summary>每次专业挑战室经验。</summary>
	public const int ExpPerExpertChallenge = 250;

	/// <summary>每周目标经验。</summary>
	public const int ExpTarget = 5000;

	private readonly IntelBoardConfig _config;

	private readonly Action<IntelBoardRunRecordData>? _saveData;

	private readonly Func<DateTimeOffset> _now;

	private bool _suppressSave;

	private bool _progressComplete;

	private int _notoriousHuntCount;

	private int _expertChallengeCount;

	private int _baseExp;

	/// <summary>
	/// 本周期进度是否已满。
	/// </summary>
	[YamlMember(Alias = "progress_complete", ApplyNamingConventions = false)]
	public bool ProgressComplete
	{
		get
		{
			return _progressComplete;
		}
		set
		{
			_progressComplete = value;
			SaveData();
		}
	}

	/// <summary>
	/// 本周期恶名狩猎完成次数。
	/// </summary>
	[YamlMember(Alias = "notorious_hunt_count", ApplyNamingConventions = false)]
	public int NotoriousHuntCount
	{
		get
		{
			return _notoriousHuntCount;
		}
		set
		{
			_notoriousHuntCount = value;
			SaveData();
		}
	}

	/// <summary>
	/// 本周期专业挑战室完成次数。
	/// </summary>
	[YamlMember(Alias = "expert_challenge_count", ApplyNamingConventions = false)]
	public int ExpertChallengeCount
	{
		get
		{
			return _expertChallengeCount;
		}
		set
		{
			_expertChallengeCount = value;
			SaveData();
		}
	}

	/// <summary>
	/// 根据 OCR 进度估算的基础经验值。
	/// </summary>
	[YamlMember(Alias = "base_exp", ApplyNamingConventions = false)]
	public int BaseExp
	{
		get
		{
			return _baseExp;
		}
		set
		{
			_baseExp = value;
			SaveData();
		}
	}

	/// <summary>
	/// 本周期累计经验。
	/// </summary>
	[YamlIgnore]
	public int TotalExp => BaseExp + NotoriousHuntCount * 500 + ExpertChallengeCount * 250;

	/// <summary>
	/// 经验是否刷满。
	/// </summary>
	[YamlIgnore]
	public bool ExpComplete => TotalExp >= 5000;

	/// <summary>
	/// 按周是否已完成。
	/// </summary>
	[YamlIgnore]
	public bool IsFinishedByWeek => _config.ExpGrindMode ? ExpComplete : ProgressComplete;

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
			return IsFinishedByWeek ? 1 : 0;
		}
	}

	/// <summary>
	/// 当前记录是否已完成。
	/// </summary>
	[YamlIgnore]
	public override bool IsDone => !ShouldResetByCurrentWeek() && IsFinishedByWeek;

	/// <summary>
	/// 初始化运行记录。
	/// </summary>
	public IntelBoardRunRecord(IntelBoardConfig? config = null, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null, Action<IntelBoardRunRecordData>? saveData = null)
		: base("intel_board", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Weekly, now)
	{
		_config = config ?? new IntelBoardConfig();
		_saveData = saveData;
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
	}

	/// <summary>
	/// 加载运行记录。
	/// </summary>
	public static IntelBoardRunRecord Load(OneDragonEnvironment environment, int instanceIndex, IntelBoardConfig config, int gameRefreshHourOffset = 0, Func<DateTimeOffset>? now = null)
	{
		YamlConfig<IntelBoardRunRecordData> yamlConfig = new YamlConfig<IntelBoardRunRecordData>(environment, "intel_board", null, instanceIndex, new string[] { "app_run_record" });
		IntelBoardRunRecordData current = yamlConfig.Current;
		IntelBoardRunRecord intelBoardRunRecord = new IntelBoardRunRecord(config, gameRefreshHourOffset, now, delegate(IntelBoardRunRecordData updated)
		{
			yamlConfig.Current.Dt = updated.Dt;
			yamlConfig.Current.RunTime = updated.RunTime;
			yamlConfig.Current.RunTimeFloat = updated.RunTimeFloat;
			yamlConfig.Current.RunStatus = updated.RunStatus;
			yamlConfig.Current.ProgressComplete = updated.ProgressComplete;
			yamlConfig.Current.NotoriousHuntCount = updated.NotoriousHuntCount;
			yamlConfig.Current.ExpertChallengeCount = updated.ExpertChallengeCount;
			yamlConfig.Current.BaseExp = updated.BaseExp;
			yamlConfig.Save();
		});
		intelBoardRunRecord._suppressSave = true;
		try
		{
			intelBoardRunRecord.RunTime = current.RunTime;
			intelBoardRunRecord.RunTimeFloat = current.RunTimeFloat;
			intelBoardRunRecord.RunStatus = current.RunStatus;
			intelBoardRunRecord.ProgressComplete = current.ProgressComplete;
			intelBoardRunRecord.NotoriousHuntCount = current.NotoriousHuntCount;
			intelBoardRunRecord.ExpertChallengeCount = current.ExpertChallengeCount;
			intelBoardRunRecord.BaseExp = current.BaseExp;
			if (!string.IsNullOrWhiteSpace(current.Dt))
			{
				intelBoardRunRecord.Dt = current.Dt;
			}
		}
		finally
		{
			intelBoardRunRecord._suppressSave = false;
		}
		intelBoardRunRecord.ConfigureRuntime("intel_board", gameRefreshHourOffset, ZApplicationRunRecordPeriod.Weekly, now, intelBoardRunRecord.SaveData);
		return intelBoardRunRecord;
	}

	/// <inheritdoc />
	public override void ResetRecord()
	{
		_suppressSave = true;
		try
		{
			base.ResetRecord();
			ProgressComplete = false;
			NotoriousHuntCount = 0;
			ExpertChallengeCount = 0;
			BaseExp = 0;
		}
		finally
		{
			_suppressSave = false;
		}
		SaveData();
	}

	/// <inheritdoc />
	public override void CheckAndUpdateStatus()
	{
		if (ShouldResetByCurrentWeek())
		{
			ResetRecord();
		}
		else if (!IsFinishedByWeek)
		{
			base.ResetRecord();
		}
	}

	/// <summary>
	/// 标记普通进度完成。
	/// </summary>
	public void MarkProgressComplete()
	{
		ProgressComplete = true;
	}

	/// <summary>
	/// 记录一次委托完成。
	/// </summary>
	public void AddCommission(IntelBoardCommissionType commissionType)
	{
		if (commissionType == IntelBoardCommissionType.NotoriousHunt)
		{
			NotoriousHuntCount++;
		}
		else
		{
			ExpertChallengeCount++;
		}
	}

	/// <summary>
	/// 更新基础经验估算值。
	/// </summary>
	public void UpdateBaseExp(int baseExp)
	{
		BaseExp = baseExp;
	}

	private void SaveData()
	{
		if (!_suppressSave)
		{
			_saveData?.Invoke(new IntelBoardRunRecordData
			{
				Dt = base.Dt,
				RunTime = base.RunTime,
				RunTimeFloat = base.RunTimeFloat,
				RunStatus = base.RunStatus,
				ProgressComplete = ProgressComplete,
				NotoriousHuntCount = NotoriousHuntCount,
				ExpertChallengeCount = ExpertChallengeCount,
				BaseExp = BaseExp
			});
		}
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
