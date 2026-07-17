using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Operation;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public class AutoBattleStateRecordServiceTests
{
	[Fact]
	public void AbsoluteTimestampsRetainSubSecondPrecisionAtCurrentUnixEpoch()
	{
		double num = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
		StateRecorder stateRecorder = new StateRecorder("自定义-精度");
		stateRecorder.UpdateStateRecord(new StateRecord("自定义-精度", num));
		StateRecorderSnapshot snapshot = stateRecorder.GetSnapshot();
		OperationExecutor operationExecutor = new OperationExecutor(Array.Empty<AtomicOp>(), num);
		Assert.Equal(0.1, num + 0.1 - snapshot.LastRecordTime, 6);
		Assert.Equal(num, operationExecutor.TriggerTime, 6);
		Assert.NotNull(snapshot.LastRecordTimestampUtc);
	}

	[Fact]
	public void Test_StateRecorder_CreationAndValidation()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("按键-闪避");
		Assert.NotNull(stateRecorder);
		Assert.Equal("按键-闪避", stateRecorder.StateName);
		StateRecorder stateRecorder2 = autoBattleStateRecordService.GetStateRecorder("非法状态_123");
		Assert.Null(stateRecorder2);
	}

	[Fact]
	public void Test_StateRecorder_CustomStateRequiresHyphenPrefix()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("自定义-测试状态"));
		Assert.Null(autoBattleStateRecordService.GetStateRecorder("自定义测试状态"));
	}

	[Fact]
	public void Test_StateRecorder_ReturnsSameRecorderInstanceForSameState()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("按键-闪避");
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-闪避", 12.0, 7));
		StateRecorder stateRecorder2 = autoBattleStateRecordService.GetStateRecorder("按键-闪避");
		Assert.Same(stateRecorder, stateRecorder2);
		Assert.Equal(12.0, stateRecorder2.LastRecordTime);
		Assert.Equal(7, stateRecorder2.LastValue);
	}

	[Fact]
	public async Task Test_StateRecorder_ConcurrentUpdate()
	{
		AutoBattleStateRecordService service = new AutoBattleStateRecordService();
		string stateName = "按键-普通攻击";
		Task[] tasks = new Task[100];
		for (int i = 0; i < 100; i++)
		{
			int val = i;
			tasks[i] = Task.Run(delegate
			{
				service.UpdateState(new StateRecord(stateName, val, val));
			});
		}
		await Task.WhenAll(tasks);
		StateRecorder recorder = service.GetStateRecorder(stateName);
		Assert.NotNull(recorder);
		Assert.True(recorder.LastValue >= 0 && recorder.LastValue < 100);
	}

	[Fact]
	public async Task Test_StateRecorder_ConcurrentValueAdd()
	{
		AutoBattleStateRecordService service = new AutoBattleStateRecordService();
		service.UpdateState(new StateRecord("按键-普通攻击", 1.0, 0));
		Task[] tasks = new Task[200];
		for (int i = 0; i < tasks.Length; i++)
		{
			tasks[i] = Task.Run(delegate
			{
				AutoBattleStateRecordService autoBattleStateRecordService = service;
				int? valueToAdd = 1;
				autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 2.0, null, valueToAdd));
			});
		}
		await Task.WhenAll(tasks);
		Assert.Equal(actual: service.GetStateRecorder("按键-普通攻击").LastValue, expected: 200);
	}

	[Fact]
	public void Test_StateRecorder_ValueOverrideThenAdd()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 1.0, 2, 3));
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("按键-普通攻击");
		Assert.Equal(5, stateRecorder.LastValue);
	}

	[Fact]
	public void Test_StateRecorder_ValueAddStartsFromZeroWhenValueIsNull()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		int? valueToAdd = 4;
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 1.0, null, valueToAdd));
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("按键-普通攻击");
		Assert.Equal(4, stateRecorder.LastValue);
	}

	[Fact]
	public void Test_StateRecorder_TriggerTimeAddAdjustsExistingTimestampOnly()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		double? triggerTimeAdd = 2.0;
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 0.0, null, null, triggerTimeAdd));
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("按键-普通攻击");
		Assert.Equal(-1.0, stateRecorder.LastRecordTime);
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 10.0));
		triggerTimeAdd = 2.0;
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 0.0, null, null, triggerTimeAdd));
		Assert.Equal(8.0, stateRecorder.LastRecordTime);
	}

	[Fact]
	public void Test_StateRecorder_ClearWhenNeverTriggeredKeepsInitialTimestamp()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("按键-普通攻击");
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 0.0, null, null, null, isClear: true));
		Assert.Equal(-1.0, stateRecorder.LastRecordTime);
		Assert.Null(stateRecorder.LastValue);
	}

	[Fact]
	public void Test_StateRecorder_MutexClear()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		string stateName = "前台-妮可";
		string stateName2 = "前台-安比";
		autoBattleStateRecordService.UpdateState(new StateRecord(stateName2, 1.0, 1));
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder(stateName2);
		Assert.Equal(1, stateRecorder.LastValue);
		autoBattleStateRecordService.UpdateState(new StateRecord(stateName, 2.0, 1));
		StateRecorder stateRecorder2 = autoBattleStateRecordService.GetStateRecorder(stateName);
		Assert.Equal(1, stateRecorder2.LastValue);
		Assert.Null(stateRecorder.LastValue);
		Assert.Equal(0.0, stateRecorder.LastRecordTime);
	}

	[Fact]
	public void Test_StateRecorder_ClearDoesNotClearMutexStates()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		string stateName = "前台-妮可";
		string stateName2 = "前台-安比";
		autoBattleStateRecordService.UpdateState(new StateRecord(stateName2, 1.0, 1));
		autoBattleStateRecordService.UpdateState(new StateRecord(stateName, 0.0, null, null, null, isClear: true));
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder(stateName2);
		Assert.Equal(1.0, stateRecorder.LastRecordTime);
		Assert.Equal(1, stateRecorder.LastValue);
	}

	[Fact]
	public void Test_StateRecorder_MutexClearLeavesNeverTriggeredRecorderInitial()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("前台-安比");
		autoBattleStateRecordService.UpdateState(new StateRecord("前台-妮可", 2.0, 1));
		Assert.Equal(-1.0, stateRecorder.LastRecordTime);
		Assert.Null(stateRecorder.LastValue);
	}

	[Fact]
	public void Test_StateRecordService_AcceptsAgentTypeBangbooAndTargetStates()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("前台-强攻"));
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("连携技-1-邦布"));
		Assert.NotNull(autoBattleStateRecordService.GetStateRecorder("目标-近距离锁定"));
	}

	[Fact]
	public void Test_StateRecorder_BangbooChainSkillClearsAgentChainSkill()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		autoBattleStateRecordService.UpdateState(new StateRecord("连携技-1-安比", 1.0, 1));
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("连携技-1-安比");
		autoBattleStateRecordService.UpdateState(new StateRecord("连携技-1-邦布", 2.0, 1));
		Assert.Equal(0.0, stateRecorder.LastRecordTime);
		Assert.Null(stateRecorder.LastValue);
	}

	[Fact]
	public void Test_StateRecordService_GetSnapshotReadsStableRecorderValues()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 3.0, 9));
		IReadOnlyDictionary<string, StateRecorderSnapshot> snapshot = autoBattleStateRecordService.GetSnapshot();
		Assert.True(snapshot.ContainsKey("按键-普通攻击"));
		Assert.Equal("按键-普通攻击", snapshot["按键-普通攻击"].StateName);
		Assert.Equal(3.0, snapshot["按键-普通攻击"].LastRecordTime);
		Assert.Equal(9, snapshot["按键-普通攻击"].LastValue);
	}

	[Fact]
	public void Test_StateRecordService_ClearExpiredStatesOnlyClearsTriggeredExpiredStates()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		StateRecorder stateRecorder = autoBattleStateRecordService.GetStateRecorder("按键-普通攻击");
		StateRecorder stateRecorder2 = autoBattleStateRecordService.GetStateRecorder("按键-特殊攻击");
		StateRecorder stateRecorder3 = autoBattleStateRecordService.GetStateRecorder("按键-终结技");
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-普通攻击", 9.0, 1));
		autoBattleStateRecordService.UpdateState(new StateRecord("按键-特殊攻击", 1.0, 1));
		int actual = autoBattleStateRecordService.ClearExpiredStates(10.0, 5.0);
		Assert.Equal(1, actual);
		Assert.Equal(9.0, stateRecorder.LastRecordTime);
		Assert.Equal(1, stateRecorder.LastValue);
		Assert.Equal(0.0, stateRecorder2.LastRecordTime);
		Assert.Null(stateRecorder2.LastValue);
		Assert.Equal(-1.0, stateRecorder3.LastRecordTime);
		Assert.Null(stateRecorder3.LastValue);
	}
}
