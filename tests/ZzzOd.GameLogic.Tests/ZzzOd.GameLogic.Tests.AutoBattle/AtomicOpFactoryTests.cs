using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Input;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.AutoBattle.AtomicOp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class AtomicOpFactoryTests
{
	private sealed class RecordingButtonController : IButtonController
	{
		public List<string> Taps { get; } = new List<string>();

		public List<(string Key, TimeSpan? PressTime)> Presses { get; } = new List<(string, TimeSpan?)>();

		public List<string> Releases { get; } = new List<string>();

		public int ResetCount { get; private set; }

		public void Tap(string key)
		{
			Taps.Add(key);
		}

		public void TapCombo(IReadOnlyList<string> keys)
		{
			Taps.Add(string.Join("+", keys));
		}

		public void Press(string key, TimeSpan? pressTime = null)
		{
			Presses.Add((key, pressTime));
		}

		public void Release(string key)
		{
			Releases.Add(key);
		}

		public void Reset()
		{
			ResetCount++;
		}
	}

	private sealed class RecordingInputController(IButtonController buttonController) : IInputController
	{
		public IButtonController ButtonController { get; } = buttonController;

		public bool Click(Point? position = null, TimeSpan? pressTime = null, bool primary = true)
		{
			return true;
		}

		public void DragTo(Point end, Point? start = null, TimeSpan? duration = null)
		{
		}

		public void Scroll(int clicks, Point? position = null)
		{
		}

		public void InputText(string text)
		{
		}

		public void MouseMove(Point position)
		{
		}
	}

	private readonly AtomicOpFactory _factory = new AtomicOpFactory();

	[Fact]
	public void GetAtomicOp_MapsCommonButton()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = BattleStateEnum.BtnSwitchNormalAttack.GetDescription(),
			Way = "点按",
			Repeat = 2,
			PreDelay = 0.1,
			PostDelay = 0.2
		});
		AtomicBtnCommon atomicBtnCommon = Assert.IsType<AtomicBtnCommon>(atomicOp);
		Assert.Equal("普通攻击", atomicBtnCommon.BtnName);
		Assert.Equal(BtnWayEnum.Tap, atomicBtnCommon.BtnWay);
		Assert.Equal(2, atomicBtnCommon.RepeatTimes);
		Assert.Equal(0.1, atomicBtnCommon.PreDelay);
		Assert.Equal(0.2, atomicBtnCommon.PostDelay);
		Assert.Equal(BattleStateEnum.BtnSwitchNormalAttack.GetDescription(), atomicBtnCommon.OpName);
	}

	[Fact]
	public void GetAtomicOp_MapsPressSuffixAndUsesPressField()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = BattleStateEnum.BtnDodge.GetDescription() + "-按下",
			Press = 0.25,
			Data = new string[] { "0.75" }
		});
		AtomicBtnDodge atomicBtnDodge = Assert.IsType<AtomicBtnDodge>(atomicOp);
		Assert.True(atomicBtnDodge.Press);
		Assert.False(atomicBtnDodge.Release);
		Assert.Equal(0.25, atomicBtnDodge.PressTimeSeconds);
		Assert.False(atomicBtnDodge.AsyncOp);
		Assert.Equal(BattleStateEnum.BtnDodge.GetDescription() + "按下", atomicBtnDodge.OpName);
	}

	[Fact]
	public void GetAtomicOp_MapsPressSuffixAndFallsBackToData()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = BattleStateEnum.BtnMoveW.GetDescription() + "-按下",
			Data = new string[] { "0.4" }
		});
		AtomicBtnMoveW atomicBtnMoveW = Assert.IsType<AtomicBtnMoveW>(atomicOp);
		Assert.True(atomicBtnMoveW.Press);
		Assert.Equal(0.4, atomicBtnMoveW.PressTimeSeconds);
		Assert.Equal(TimeSpan.FromSeconds(0.4), atomicBtnMoveW.PressTime);
	}

	[Fact]
	public void GetAtomicOp_MapsReleaseSuffix()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = BattleStateEnum.BtnMoveS.GetDescription() + "-松开",
			Press = 0.5
		});
		AtomicBtnMoveS atomicBtnMoveS = Assert.IsType<AtomicBtnMoveS>(atomicOp);
		Assert.False(atomicBtnMoveS.Press);
		Assert.True(atomicBtnMoveS.Release);
		Assert.Null(atomicBtnMoveS.PressTimeSeconds);
		Assert.Equal(BattleStateEnum.BtnMoveS.GetDescription() + "松开", atomicBtnMoveS.OpName);
	}

	[Theory]
	[InlineData(new object[] { "切换角色" })]
	[InlineData(new object[] { "按键-切换角色" })]
	public void GetAtomicOp_MapsSwitchAgentAndCompatibilityAlias(string opName)
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = opName,
			AgentName = "安比"
		});
		AtomicBtnSwitchAgent atomicBtnSwitchAgent = Assert.IsType<AtomicBtnSwitchAgent>(atomicOp);
		Assert.Equal("安比", atomicBtnSwitchAgent.AgentName);
		Assert.Equal("按键-切换角色 安比", atomicBtnSwitchAgent.OpName);
	}

	[Fact]
	public void GetAtomicOp_MapsQuickAssist()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-快速支援"
		});
		Assert.IsType<AtomicBtnQuickAssist>(atomicOp);
		Assert.Equal("按键-快速支援", atomicOp.OpName);
	}

	[Fact]
	public void GetAtomicOp_SpecialButtonsKeepPythonDelays()
	{
		AtomicBtnSwitchAgent atomicBtnSwitchAgent = Assert.IsType<AtomicBtnSwitchAgent>(_factory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-切换角色",
			AgentName = "安比",
			PreDelay = 0.1,
			PostDelay = 0.2
		}));
		AtomicBtnQuickAssist atomicBtnQuickAssist = Assert.IsType<AtomicBtnQuickAssist>(_factory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-快速支援",
			PreDelay = 0.3,
			PostDelay = 0.4
		}));
		Assert.Equal(0.1, atomicBtnSwitchAgent.PreDelay);
		Assert.Equal(0.2, atomicBtnSwitchAgent.PostDelay);
		Assert.Equal(0.3, atomicBtnQuickAssist.PreDelay);
		Assert.Equal(0.4, atomicBtnQuickAssist.PostDelay);
	}

	[Fact]
	public void GetAtomicOp_MapsWaitAndDataOverridesSeconds()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = "等待秒数",
			Seconds = 1.0,
			Data = new string[] { "2.5" }
		});
		AtomicWait atomicWait = Assert.IsType<AtomicWait>(atomicOp);
		Assert.Equal(2.5, atomicWait.WaitSeconds);
		Assert.Equal("等待秒数 2.50", atomicWait.OpName);
	}

	[Fact]
	public void GetAtomicOp_MapsSetStateAndDataOverridesLegacyFields()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = "设置状态",
			State = "自定义-原状态",
			Seconds = 1.0,
			Value = 3,
			Add = 2,
			Data = new string[3] { "自定义-新状态", "3.5", "7" }
		});
		AtomicSetState atomicSetState = Assert.IsType<AtomicSetState>(atomicOp);
		Assert.Equal("自定义-新状态", atomicSetState.StateName);
		Assert.Equal(3.5, atomicSetState.DiffTime);
		Assert.Equal(7, atomicSetState.Value);
		Assert.Equal(2, atomicSetState.ValueAdd);
		Assert.Equal("设置状态 自定义-新状态", atomicSetState.OpName);
	}

	[Fact]
	public void GetAtomicOp_MapsClearState()
	{
		AtomicOp atomicOp = _factory.GetAtomicOp(new OperationDef
		{
			OpName = "清除状态",
			State = "自定义-原状态",
			StateList = new string[2] { "自定义-A", "自定义-B" },
			Data = new string[] { "自定义-新状态" }
		});
		AtomicClearState atomicClearState = Assert.IsType<AtomicClearState>(atomicOp);
		Assert.Equal("自定义-新状态", atomicClearState.StateName);
		Assert.Equal(new string[2] { "自定义-A", "自定义-B" }, atomicClearState.StateNameList);
		Assert.Equal("清除状态", atomicClearState.OpName);
	}

	[Fact]
	public void GetAtomicOp_RejectsUnknownCommand()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>(() => _factory.GetAtomicOp(new OperationDef
		{
			OpName = "未知指令"
		}));
		Assert.Contains("非法的指令 未知指令", ex.Message);
	}

	[Fact]
	public void GetAtomicOp_RejectsUnknownCommonButton()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>(() => _factory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-不存在"
		}));
		Assert.Contains("非法按键 不存在", ex.Message);
	}

	[Fact]
	public void AtomicBtnCommon_ExecuteRunsControllerAndWritesState()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = BattleStateEnum.BtnSwitchNormalAttack.GetDescription(),
			Way = "点按",
			Repeat = 2
		});
		atomicOp.Execute();
		Assert.Equal(new string[2] { "mouse_left", "mouse_left" }, buttons.Taps);
		Assert.True(zContext.AutoBattleContext.StateRecordService.GetStateRecorder(BattleStateEnum.BtnSwitchNormalAttack.GetDescription()).LastRecordTime > 0.0);
	}

	[Fact]
	public void AtomicSpecificButton_StopReleasesPressedButton()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = BattleStateEnum.BtnMoveW.GetDescription() + "-按下"
		});
		atomicOp.Execute();
		atomicOp.Stop();
		Assert.Contains<(string, TimeSpan?)>(buttons.Presses, ((string Key, TimeSpan? PressTime) press) => press.Key == "w" && !press.PressTime.HasValue);
		Assert.Contains("w", (IEnumerable<string>)buttons.Releases);
		Assert.True(zContext.AutoBattleContext.StateRecordService.GetStateRecorder(BattleStateEnum.BtnMoveW.GetDescription() + "-按下").LastRecordTime > 0.0);
		Assert.True(zContext.AutoBattleContext.StateRecordService.GetStateRecorder(BattleStateEnum.BtnMoveW.GetDescription() + "-松开").LastRecordTime > 0.0);
	}

	[Fact]
	public void AtomicSwitchNext_ReleasesConflictKeysAndUpdatesAgentState()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = BattleStateEnum.BtnSwitchNext.GetDescription()
		});
		atomicOp.Execute();
		Assert.Contains("shift", (IEnumerable<string>)buttons.Releases);
		Assert.Contains("mouse_left", (IEnumerable<string>)buttons.Releases);
		Assert.Contains("space", (IEnumerable<string>)buttons.Taps);
		Assert.Equal("妮可", zContext.AutoBattleContext.AgentContext.Team.Agents[0].Agent.AgentName);
		Assert.True(zContext.AutoBattleContext.StateRecordService.GetStateRecorder("前台-妮可").LastRecordTime > 0.0);
		Assert.True(zContext.AutoBattleContext.StateRecordService.GetStateRecorder(BattleStateEnum.BtnSwitchNext.GetDescription()).LastRecordTime > 0.0);
	}

	[Fact]
	public void AtomicQuickAssist_UsesLatestAssistState()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("快速支援-比利", 2.0));
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("快速支援-妮可", 5.0));
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-快速支援"
		});
		atomicOp.Execute();
		Assert.Contains("space", (IEnumerable<string>)buttons.Taps);
		Assert.Equal("妮可", zContext.AutoBattleContext.AgentContext.Team.Agents[0].Agent.AgentName);
		Assert.True(zContext.AutoBattleContext.StateRecordService.GetStateRecorder(BattleStateEnum.BtnSwitchNext.GetDescription()).LastRecordTime > 0.0);
	}

	[Fact]
	public void AtomicSwitchAgent_UsesAgentName()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-切换角色",
			AgentName = "比利"
		});
		atomicOp.Execute();
		Assert.Contains("c", (IEnumerable<string>)buttons.Taps);
		Assert.Equal("比利", zContext.AutoBattleContext.AgentContext.Team.Agents[0].Agent.AgentName);
		Assert.True(zContext.AutoBattleContext.StateRecordService.GetStateRecorder("前台-比利").LastRecordTime > 0.0);
	}

	[Fact]
	public async Task AtomicSwitchAgent_StopDuringPreDelaySkipsInputAndResets()
	{
		RecordingButtonController buttons;
		using ZContext ctx = CreateContext(out buttons);
		ctx.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		ctx.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		ctx.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.BILLY.Value));
		AtomicOp op = ctx.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-切换角色",
			AgentName = "比利",
			PreDelay = 0.2
		});
		Task execution = Task.Run((Action)op.Execute);
		await Task.Delay(50);
		op.Stop();
		await execution.WaitAsync(TimeSpan.FromSeconds(1L));
		Assert.DoesNotContain("c", (IEnumerable<string>)buttons.Taps);
		op.Execute();
		Assert.Contains("c", (IEnumerable<string>)buttons.Taps);
	}

	[Fact]
	public async Task AtomicQuickAssist_ConcurrentExecuteRunsOnce()
	{
		RecordingButtonController buttons;
		using ZContext ctx = CreateContext(out buttons);
		ctx.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		ctx.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		ctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("快速支援-妮可", 5.0));
		AtomicOp op = ctx.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-快速支援",
			PreDelay = 0.2
		});
		Task firstExecution = Task.Run((Action)op.Execute);
		await Task.Delay(50);
		op.Execute();
		await firstExecution.WaitAsync(TimeSpan.FromSeconds(1L));
		Assert.Equal(1, buttons.Taps.Count((string key) => key == "space"));
	}

	[Fact]
	public async Task AtomicQuickAssist_StopDuringPreDelaySkipsInputAndResets()
	{
		RecordingButtonController buttons;
		using ZContext ctx = CreateContext(out buttons);
		ctx.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		ctx.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		ctx.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("快速支援-妮可", 5.0));
		AtomicOp op = ctx.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "按键-快速支援",
			PreDelay = 0.2
		});
		Task execution = Task.Run((Action)op.Execute);
		await Task.Delay(50);
		op.Stop();
		await execution.WaitAsync(TimeSpan.FromSeconds(1L));
		Assert.DoesNotContain("space", (IEnumerable<string>)buttons.Taps);
		op.Execute();
		Assert.Contains("space", (IEnumerable<string>)buttons.Taps);
	}

	[Fact]
	public async Task AtomicWait_StopInterruptsWait()
	{
		AtomicWait op = (AtomicWait)_factory.GetAtomicOp(new OperationDef
		{
			OpName = "等待秒数",
			Seconds = 5.0
		});
		Stopwatch stopwatch = Stopwatch.StartNew();
		Task waitTask = Task.Run((Action)op.Execute);
		await Task.Delay(50);
		op.Stop();
		await waitTask.WaitAsync(TimeSpan.FromSeconds(1L));
		Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1L));
	}

	[Fact]
	public void AtomicSetState_ExecuteWritesStateRecords()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "设置状态",
			StateList = new string[2] { "自定义-A", "自定义-B" },
			Seconds = 0.25,
			Value = 7,
			Add = 2
		});
		atomicOp.Execute();
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-A");
		StateRecorder stateRecorder2 = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-B");
		Assert.True(stateRecorder.LastRecordTime > 0.0);
		Assert.True(stateRecorder2.LastRecordTime > 0.0);
		Assert.Equal(9, stateRecorder.LastValue);
		Assert.Equal(9, stateRecorder2.LastValue);
	}

	[Fact]
	public void AtomicSetState_SecondsAddAdjustsExistingStateTime()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-A", 10.0, 1));
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "设置状态",
			State = "自定义-A",
			SecondsAdd = 2.0,
			Add = 3
		});
		atomicOp.Execute();
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-A");
		Assert.Equal(8.0, stateRecorder.LastRecordTime);
		Assert.Equal(4, stateRecorder.LastValue);
	}

	[Fact]
	public void AtomicSetState_EmptyStateListNoOps()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "设置状态",
			StateList = Array.Empty<string>()
		});
		atomicOp.Execute();
		Assert.Empty(zContext.AutoBattleContext.StateRecordService.GetSnapshot());
	}

	[Fact]
	public void AtomicClearState_ExecuteClearsStateRecords()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		zContext.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-A", 10.0, 1));
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "清除状态",
			State = "自定义-A"
		});
		atomicOp.Execute();
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-A");
		Assert.Equal(0.0, stateRecorder.LastRecordTime);
		Assert.Null(stateRecorder.LastValue);
	}

	[Fact]
	public void AtomicClearState_EmptyStateListNoOps()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		AtomicOp atomicOp = zContext.AutoBattleContext.AtomicOpFactory.GetAtomicOp(new OperationDef
		{
			OpName = "清除状态",
			StateList = Array.Empty<string>()
		});
		atomicOp.Execute();
		Assert.Empty(zContext.AutoBattleContext.StateRecordService.GetSnapshot());
	}

	[Fact]
	public void AtomicTurn_ExecuteTurnsControllerByDistance()
	{
		List<(float Dx, float Dy)> moves = new List<(float, float)>();
		RecordingButtonController recordingButtonController = new RecordingButtonController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(recordingButtonController), null, recordingButtonController, null, null, skipForegroundActivation: true, delegate(float dx, float dy)
		{
			moves.Add((dx, dy));
		});
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(controller);
		AtomicTurn atomicTurn = new AtomicTurn(zContext.AutoBattleContext, 123f);
		atomicTurn.Execute();
		Assert.Equal(new(float, float)[1] { (123f, 0f) }, moves);
	}

	[Fact]
	public void OperationDef_ParsesPythonCompatibleDictionary()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary["op_name"] = BattleStateEnum.BtnDodge.GetDescription() + "-按下";
		dictionary["data"] = new List<object> { "0.3" };
		dictionary["pre_delay"] = "0.1";
		dictionary["post_delay"] = 0.2;
		dictionary["way"] = "按下";
		dictionary["press"] = "0.4";
		dictionary["repeat"] = "2";
		dictionary["seconds"] = "1.5";
		dictionary["state"] = "自定义-状态";
		dictionary["state_list"] = new string[2] { "自定义-A", "自定义-B" };
		dictionary["seconds_add"] = "0.6";
		dictionary["value"] = "7";
		dictionary["add"] = "8";
		dictionary["agent_name"] = "安比";
		OperationDef operationDef = new OperationDef(dictionary);
		Assert.Equal(BattleStateEnum.BtnDodge.GetDescription() + "-按下", operationDef.OpName);
		Assert.Equal(new string[1] { "0.3" }, operationDef.Data);
		Assert.Equal(0.1, operationDef.PreDelay);
		Assert.Equal(0.2, operationDef.PostDelay);
		Assert.Equal("按下", operationDef.BtnWay);
		Assert.Equal(0.4, operationDef.BtnPress);
		Assert.Equal(2, operationDef.BtnRepeatTimes);
		Assert.Equal(1.5, operationDef.WaitSeconds);
		Assert.Equal("自定义-状态", operationDef.StateName);
		Assert.Equal(new string[2] { "自定义-A", "自定义-B" }, operationDef.StateNameList);
		Assert.Equal(0.6, operationDef.StateSecondsAdd);
		Assert.Equal(7, operationDef.StateValue);
		Assert.Equal(8, operationDef.StateValueAdd);
		Assert.Equal("安比", operationDef.AgentName);
	}

	[Fact]
	public void OperationDef_RejectsUnknownField()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>(() => new OperationDef(new Dictionary<string, object>
		{
			["op_name"] = "等待秒数",
			["unknown"] = true
		}));
		Assert.Contains("未知字段 unknown", ex.Message);
	}

	private static ZContext CreateContext(out RecordingButtonController buttons)
	{
		buttons = new RecordingButtonController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(buttons), null, buttons, null, null, skipForegroundActivation: true);
		ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(controller);
		return zContext;
	}
}
