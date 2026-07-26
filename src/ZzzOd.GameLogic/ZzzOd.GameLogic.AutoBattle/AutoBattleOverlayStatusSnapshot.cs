using OneDragon.Core.Operation;
using OneDragon.Core.Utils;

namespace ZzzOd.GameLogic.AutoBattle;

/// <summary>
/// Overlay 状态面板使用的自动战斗运行快照。
/// </summary>
public sealed record AutoBattleOverlayStatusSnapshot(
	bool IsRunning,
	string? FrontAgentName,
	bool? FrontSpecialReady,
	bool? FrontUltimateReady,
	string? LatestDodgeState,
	bool? ChainReady,
	string? LatestQuickAssistAgent,
	double? DistanceMeters)
{
	/// <summary>
	/// 当前命中的触发器。
	/// </summary>
	public string? CurrentTrigger { get; init; }

	/// <summary>
	/// 当前命中的条件集表达式。
	/// </summary>
	public string? CurrentExpression { get; init; }

	/// <summary>
	/// 本次执行的持续秒数。
	/// </summary>
	public double? CurrentDurationSeconds { get; init; }

	/// <summary>
	/// 取样后的状态行，按最近触发降序。
	/// </summary>
	public IReadOnlyList<AutoBattleStateRow> StateRows { get; init; } = [];
}

internal static class AutoBattleOverlayStatusSnapshotFactory
{
	private const double DodgeTtlSeconds = 2d;
	private const double ChainTtlSeconds = 1.2d;
	private const double QuickAssistTtlSeconds = 2d;
	private const string ChainReadyStateName = "连携技-准备";
	private const string QuickAssistStatePrefix = "快速支援-";

	/// <summary>
	/// battle 面板的状态行条数上限。
	/// </summary>
	internal const int MaxStateRows = 12;

	internal static AutoBattleOverlayStatusSnapshot Create(
		bool isRunning,
		IReadOnlyList<AgentInfo> team,
		IReadOnlyDictionary<string, StateRecorderSnapshot> stateSnapshots,
		float lastCheckDistance,
		DateTimeOffset now,
		AutoBattleExecutionInfo? executionInfo = null,
		string? stateFilter = null)
	{
		if (!isRunning)
		{
			return new AutoBattleOverlayStatusSnapshot(false, null, null, null, null, null, null, null);
		}

		AgentInfo? frontAgent = team.FirstOrDefault();
		AutoBattleExecutionInfo execution = executionInfo ?? AutoBattleExecutionInfo.Idle;
		return new AutoBattleOverlayStatusSnapshot(
			true,
			frontAgent?.Agent?.AgentName,
			frontAgent is null ? null : frontAgent.SpecialReady,
			frontAgent is null ? null : frontAgent.UltimateReady,
			FindLatestRecentState(
				stateSnapshots,
				[
					YoloStateEventEnum.DODGE_YELLOW.GetDescription(),
					YoloStateEventEnum.DODGE_RED.GetDescription(),
					YoloStateEventEnum.DODGE_AUDIO.GetDescription(),
				],
				now,
				DodgeTtlSeconds),
			IsRecent(stateSnapshots, ChainReadyStateName, now, ChainTtlSeconds) ? true : null,
			FindLatestQuickAssistAgent(stateSnapshots, team, now),
			float.IsFinite(lastCheckDistance) && lastCheckDistance >= 0f ? lastCheckDistance : null)
		{
			CurrentTrigger = execution.TriggerDisplay,
			CurrentExpression = execution.ExpressionDisplay,
			CurrentDurationSeconds = execution.DurationSeconds,
			StateRows = AutoBattleExecutionInfoReader.Sample(execution.States, stateFilter, MaxStateRows),
		};
	}

	private static string? FindLatestQuickAssistAgent(
		IReadOnlyDictionary<string, StateRecorderSnapshot> stateSnapshots,
		IReadOnlyList<AgentInfo> team,
		DateTimeOffset now)
	{
		string? latestAgentName = null;
		DateTimeOffset latestAt = DateTimeOffset.MinValue;
		foreach (AgentInfo agentInfo in team)
		{
			string? agentName = agentInfo.Agent?.AgentName;
			if (string.IsNullOrWhiteSpace(agentName) || !stateSnapshots.TryGetValue(QuickAssistStatePrefix + agentName, out StateRecorderSnapshot? state) || !TryGetRecentAt(state, now, QuickAssistTtlSeconds, out DateTimeOffset stateAt))
			{
				continue;
			}

			if (stateAt > latestAt)
			{
				latestAt = stateAt;
				latestAgentName = agentName;
			}
		}

		return latestAgentName;
	}

	private static string? FindLatestRecentState(
		IReadOnlyDictionary<string, StateRecorderSnapshot> stateSnapshots,
		IReadOnlyList<string> candidates,
		DateTimeOffset now,
		double ttlSeconds)
	{
		string? latestName = null;
		DateTimeOffset latestAt = DateTimeOffset.MinValue;
		foreach (string candidate in candidates)
		{
			if (!stateSnapshots.TryGetValue(candidate, out StateRecorderSnapshot? state) || !TryGetRecentAt(state, now, ttlSeconds, out DateTimeOffset stateAt))
			{
				continue;
			}

			if (stateAt > latestAt)
			{
				latestAt = stateAt;
				latestName = candidate;
			}
		}

		return latestName;
	}

	private static bool IsRecent(IReadOnlyDictionary<string, StateRecorderSnapshot> stateSnapshots, string stateName, DateTimeOffset now, double ttlSeconds)
	{
		return stateSnapshots.TryGetValue(stateName, out StateRecorderSnapshot? state) && TryGetRecentAt(state, now, ttlSeconds, out _);
	}

	private static bool TryGetRecentAt(StateRecorderSnapshot state, DateTimeOffset now, double ttlSeconds, out DateTimeOffset stateAt)
	{
		stateAt = default;
		if (state.LastRecordTime <= 0d)
		{
			return false;
		}

		if (state.LastRecordTimestampUtc is DateTimeOffset timestamp)
		{
			stateAt = timestamp;
			return now - timestamp <= TimeSpan.FromSeconds(ttlSeconds);
		}

		double nowSeconds = now.ToUnixTimeMilliseconds() / 1000d;
		if (nowSeconds - state.LastRecordTime > ttlSeconds)
		{
			return false;
		}

		stateAt = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(state.LastRecordTime * 1000d));
		return true;
	}
}
