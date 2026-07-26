using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle;

/// <summary>
/// 自动战斗当前判定现场：命中的触发器、条件集与本次执行持续时间。
/// </summary>
/// <param name="IsRunning">自动战斗是否正在运行。</param>
/// <param name="TriggerDisplay">当前触发器展示名。</param>
/// <param name="ExpressionDisplay">当前命中的条件集表达式。</param>
/// <param name="DurationSeconds">本次执行持续秒数。</param>
/// <param name="States">当前配置使用到的状态行。</param>
public sealed record AutoBattleExecutionInfo(
	bool IsRunning,
	string? TriggerDisplay,
	string? ExpressionDisplay,
	double? DurationSeconds,
	IReadOnlyList<AutoBattleStateRow> States)
{
	/// <summary>
	/// 未运行时的空现场。
	/// </summary>
	public static AutoBattleExecutionInfo Idle { get; } = new(false, null, null, null, []);
}

/// <summary>
/// 自动战斗状态表的单行。
/// </summary>
/// <param name="StateName">状态名。</param>
/// <param name="LastRecordTime">最近一次触发的记录时间。</param>
/// <param name="SecondsSinceTrigger">距上次触发的秒数。</param>
/// <param name="Value">状态值。</param>
/// <param name="Revision">状态版本号。</param>
public sealed record AutoBattleStateRow(
	string StateName,
	double LastRecordTime,
	double SecondsSinceTrigger,
	int? Value,
	long Revision);

/// <summary>
/// 自动战斗判定现场取数。游戏助手战斗页与 Overlay battle 面板共用这一份实现，不得各写一份。
/// </summary>
public static class AutoBattleExecutionInfoReader
{
	private const double MaxTriggerSeconds = 999d;
	private const string ForegroundStatePrefix = "前台-";
	private const string BackgroundStatePrefix = "后台-";

	/// <summary>
	/// 读取当前判定现场。
	/// </summary>
	/// <param name="autoOp">自动战斗执行器。</param>
	/// <param name="stateSnapshots">状态记录快照。</param>
	/// <param name="now">当前时刻。</param>
	/// <returns>判定现场；未运行时返回空现场。</returns>
	public static AutoBattleExecutionInfo Read(
		AutoBattleOperator? autoOp,
		IReadOnlyDictionary<string, StateRecorderSnapshot> stateSnapshots,
		DateTimeOffset now) =>
		autoOp is null
			? AutoBattleExecutionInfo.Idle
			: Read(autoOp.GetRuntimeSnapshot(), stateSnapshots, now);

	/// <summary>
	/// 读取当前判定现场。
	/// </summary>
	/// <param name="runtime">执行器运行快照。</param>
	/// <param name="stateSnapshots">状态记录快照。</param>
	/// <param name="now">当前时刻。</param>
	/// <returns>判定现场；未运行时返回空现场。</returns>
	public static AutoBattleExecutionInfo Read(
		AutoBattleOperatorRuntimeSnapshot runtime,
		IReadOnlyDictionary<string, StateRecorderSnapshot> stateSnapshots,
		DateTimeOffset now)
	{
		ArgumentNullException.ThrowIfNull(runtime);
		ArgumentNullException.ThrowIfNull(stateSnapshots);
		if (!runtime.IsRunning)
		{
			return AutoBattleExecutionInfo.Idle;
		}

		List<AutoBattleStateRow> rows = [];
		foreach (string stateName in runtime.UsageStates)
		{
			if (!stateSnapshots.TryGetValue(stateName, out StateRecorderSnapshot? recorder) ||
				recorder is null ||
				recorder.LastRecordTime == -1d)
			{
				continue;
			}

			if (recorder.LastRecordTime == 0d &&
				(recorder.StateName.StartsWith(ForegroundStatePrefix, StringComparison.Ordinal) ||
					recorder.StateName.StartsWith(BackgroundStatePrefix, StringComparison.Ordinal)))
			{
				continue;
			}

			rows.Add(new AutoBattleStateRow(
				recorder.StateName,
				recorder.LastRecordTime,
				GetTriggerSeconds(now, recorder),
				recorder.LastValue,
				recorder.Revision));
		}

		double? durationSeconds = runtime.ExecutionStartedAtUtc is DateTimeOffset startedAt
			? Math.Max(0d, (now - startedAt).TotalSeconds)
			: null;
		return new AutoBattleExecutionInfo(true, runtime.TriggerDisplay, runtime.ExpressionDisplay, durationSeconds, rows);
	}

	/// <summary>
	/// 按 Overlay battle 面板的取样规则筛选状态行：只留已触发过的、按关键词过滤、按最近触发降序、截断到上限。
	/// </summary>
	/// <param name="rows">全部状态行。</param>
	/// <param name="filter">空白分隔的过滤关键词；为空表示不过滤。</param>
	/// <param name="maxCount">保留条数上限。</param>
	/// <returns>取样后的状态行。</returns>
	public static IReadOnlyList<AutoBattleStateRow> Sample(
		IReadOnlyList<AutoBattleStateRow> rows,
		string? filter,
		int maxCount)
	{
		ArgumentNullException.ThrowIfNull(rows);
		if (maxCount <= 0)
		{
			return [];
		}

		string[] keywords = (filter ?? string.Empty).Split(
			(char[]?)null,
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return
		[
			.. rows
				.Where(row => row.LastRecordTime > 0d)
				.Where(row => keywords.Length == 0 ||
					keywords.Any(keyword => row.StateName.Contains(keyword, StringComparison.Ordinal)))
				.OrderByDescending(row => row.LastRecordTime)
				.Take(maxCount),
		];
	}

	/// <summary>
	/// 计算距上次触发的秒数。从未触发或缺时间戳时返回上限值。
	/// </summary>
	/// <param name="now">当前时刻。</param>
	/// <param name="recorder">状态记录快照。</param>
	/// <returns>距上次触发的秒数。</returns>
	public static double GetTriggerSeconds(DateTimeOffset now, StateRecorderSnapshot recorder)
	{
		ArgumentNullException.ThrowIfNull(recorder);
		return recorder.LastRecordTime == 0d || !recorder.LastRecordTimestampUtc.HasValue
			? MaxTriggerSeconds
			: Math.Clamp((now - recorder.LastRecordTimestampUtc.Value).TotalSeconds, 0d, MaxTriggerSeconds);
	}
}
