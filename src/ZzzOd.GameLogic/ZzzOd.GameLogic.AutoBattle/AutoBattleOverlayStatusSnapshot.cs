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
	double? DistanceMeters);

internal static class AutoBattleOverlayStatusSnapshotFactory
{
	private const double DodgeTtlSeconds = 2d;
	private const double ChainTtlSeconds = 1.2d;
	private const double QuickAssistTtlSeconds = 2d;
	private const string ChainReadyStateName = "连携技-准备";
	private const string QuickAssistStatePrefix = "快速支援-";

	internal static AutoBattleOverlayStatusSnapshot Create(
		bool isRunning,
		IReadOnlyList<AgentInfo> team,
		IReadOnlyDictionary<string, StateRecorderSnapshot> stateSnapshots,
		float lastCheckDistance,
		DateTimeOffset now)
	{
		if (!isRunning)
		{
			return new AutoBattleOverlayStatusSnapshot(false, null, null, null, null, null, null, null);
		}

		AgentInfo? frontAgent = team.FirstOrDefault();
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
			float.IsFinite(lastCheckDistance) && lastCheckDistance >= 0f ? lastCheckDistance : null);
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
