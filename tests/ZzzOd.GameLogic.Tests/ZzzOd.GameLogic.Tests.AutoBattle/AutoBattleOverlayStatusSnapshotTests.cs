using System;
using System.Collections.Generic;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class AutoBattleOverlayStatusSnapshotTests
{
	[Fact]
	public void Create_MapsPythonAutoBattleFieldsFromRuntimeState()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		IReadOnlyList<AgentInfo> team =
		[
			new AgentInfo(AgentEnum.ANBY.Value, specialReady: true, ultimateReady: false),
			new AgentInfo(AgentEnum.NICOLE.Value),
		];
		IReadOnlyDictionary<string, StateRecorderSnapshot> states = new Dictionary<string, StateRecorderSnapshot>
		{
			[YoloStateEventEnum.DODGE_RED.GetDescription()] = CreateState(YoloStateEventEnum.DODGE_RED.GetDescription(), now.AddSeconds(-1d)),
			[YoloStateEventEnum.DODGE_YELLOW.GetDescription()] = CreateState(YoloStateEventEnum.DODGE_YELLOW.GetDescription(), now.AddMilliseconds(-200d)),
			["连携技-准备"] = CreateState("连携技-准备", now.AddMilliseconds(-400d)),
			["快速支援-妮可"] = CreateState("快速支援-妮可", now.AddMilliseconds(-100d)),
		};

		AutoBattleOverlayStatusSnapshot snapshot = AutoBattleOverlayStatusSnapshotFactory.Create(true, team, states, 12.5f, now);

		Assert.True(snapshot.IsRunning);
		Assert.Equal("安比", snapshot.FrontAgentName);
		Assert.True(snapshot.FrontSpecialReady);
		Assert.False(snapshot.FrontUltimateReady);
		Assert.Equal(YoloStateEventEnum.DODGE_YELLOW.GetDescription(), snapshot.LatestDodgeState);
		Assert.True(snapshot.ChainReady);
		Assert.Equal("妮可", snapshot.LatestQuickAssistAgent);
		Assert.Equal(12.5d, snapshot.DistanceMeters);
	}

	[Fact]
	public void Create_LeavesDetailsEmptyWhenAutoBattleIsStopped()
	{
		AutoBattleOverlayStatusSnapshot snapshot = AutoBattleOverlayStatusSnapshotFactory.Create(
			false,
			Array.Empty<AgentInfo>(),
			new Dictionary<string, StateRecorderSnapshot>(),
			0f,
			DateTimeOffset.UtcNow);

		Assert.False(snapshot.IsRunning);
		Assert.Null(snapshot.FrontAgentName);
		Assert.Null(snapshot.FrontSpecialReady);
		Assert.Null(snapshot.FrontUltimateReady);
		Assert.Null(snapshot.LatestDodgeState);
		Assert.Null(snapshot.ChainReady);
		Assert.Null(snapshot.LatestQuickAssistAgent);
		Assert.Null(snapshot.DistanceMeters);
	}

	[Fact]
	public void TryGetAutoBattleOverlayStatus_DoesNotInitializeLazyContext()
	{
		using ZContext context = new(new OneDragonEnvironment("test_project", "test_user_id"));

		Assert.Null(context.TryGetAutoBattleOverlayStatus());

		_ = context.AutoBattleContext;

		Assert.NotNull(context.TryGetAutoBattleOverlayStatus());
	}

	private static StateRecorderSnapshot CreateState(string name, DateTimeOffset timestamp)
	{
		return new StateRecorderSnapshot(name, timestamp.ToUnixTimeMilliseconds() / 1000d, null, Array.Empty<string>())
		{
			LastRecordTimestampUtc = timestamp,
		};
	}
}
