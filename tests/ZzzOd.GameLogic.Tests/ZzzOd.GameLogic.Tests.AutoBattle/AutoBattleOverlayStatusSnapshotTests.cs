using System;
using System.Collections.Generic;
using System.Linq;
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

	/// <summary>
	/// battle 面板取样：只保留已触发过的状态，按最近触发降序，并截断到上限。
	/// </summary>
	[Fact]
	public void Sample_KeepsTriggeredStatesInDescendingOrderWithinLimit()
	{
		List<AutoBattleStateRow> rows = [];
		for (int index = 0; index < 20; index++)
		{
			rows.Add(new AutoBattleStateRow($"状态{index:00}", index + 1, index, null, index));
		}

		rows.Add(new AutoBattleStateRow("从未触发", 0d, 999d, null, 0));

		IReadOnlyList<AutoBattleStateRow> sampled = AutoBattleExecutionInfoReader.Sample(rows, null, 12);

		Assert.Equal(12, sampled.Count);
		Assert.Equal("状态19", sampled[0].StateName);
		Assert.Equal("状态08", sampled[11].StateName);
		Assert.DoesNotContain(sampled, row => row.StateName == "从未触发");
	}

	/// <summary>
	/// 过滤关键词按空白拆分，状态名命中任一关键词即保留。
	/// </summary>
	[Fact]
	public void Sample_KeepsOnlyStatesMatchingAnyFilterKeyword()
	{
		List<AutoBattleStateRow> rows =
		[
			new("前台-安比", 3d, 1d, null, 1),
			new("后台-妮可", 2d, 2d, null, 2),
			new("连携技-准备", 1d, 3d, null, 3),
		];

		IReadOnlyList<AutoBattleStateRow> sampled = AutoBattleExecutionInfoReader.Sample(rows, " 妮可  连携 ", 12);

		Assert.Equal(2, sampled.Count);
		Assert.Equal("后台-妮可", sampled[0].StateName);
		Assert.Equal("连携技-准备", sampled[1].StateName);
	}

	/// <summary>
	/// 判定现场应带出触发器、条件集与持续时间，并过滤从未触发的前台/后台状态。
	/// </summary>
	[Fact]
	public void Read_MapsExecutionInfoAndDropsNeverTriggeredPrefixedStates()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		AutoBattleOperatorRuntimeSnapshot runtime = new(
			IsRunning: true,
			"闪避识别-黄光",
			"[前台-安比] and not [后台-妮可]",
			now.AddSeconds(-2.5d),
			["前台-安比", "后台-妮可", "连携技-准备"]);
		Dictionary<string, StateRecorderSnapshot> states = new(StringComparer.Ordinal)
		{
			["前台-安比"] = CreateState("前台-安比", now.AddSeconds(-1d)),
			["后台-妮可"] = new StateRecorderSnapshot("后台-妮可", 0d, null, Array.Empty<string>()),
			["连携技-准备"] = CreateState("连携技-准备", now.AddMilliseconds(-500d)),
		};

		AutoBattleExecutionInfo info = AutoBattleExecutionInfoReader.Read(runtime, states, now);

		Assert.True(info.IsRunning);
		Assert.Equal("闪避识别-黄光", info.TriggerDisplay);
		Assert.Equal("[前台-安比] and not [后台-妮可]", info.ExpressionDisplay);
		Assert.Equal(2.5d, info.DurationSeconds!.Value, 1);
		Assert.Equal(["前台-安比", "连携技-准备"], info.States.Select(row => row.StateName));
	}

	private static StateRecorderSnapshot CreateState(string name, DateTimeOffset timestamp)
	{
		return new StateRecorderSnapshot(name, timestamp.ToUnixTimeMilliseconds() / 1000d, null, Array.Empty<string>())
		{
			LastRecordTimestampUtc = timestamp,
		};
	}
}
