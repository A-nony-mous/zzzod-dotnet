using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Input;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class AutoBattleUtilsTests
{
	private sealed class FakeMergeBuilder : IAutoBattleMergeBuilder
	{
		public AutoBattleMergeBuildRequest Request { get; }

		public bool LoadCalled { get; private set; }

		public bool SaveCalled { get; private set; }

		public FakeMergeBuilder(AutoBattleMergeBuildRequest request)
		{
			Request = request;
		}

		public void Load()
		{
			LoadCalled = true;
		}

		public void SaveAsOneFile()
		{
			SaveCalled = true;
		}
	}

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

	[Theory]
	[InlineData(new object[] { "anby", 0 })]
	[InlineData(new object[] { "nicole", 1 })]
	[InlineData(new object[] { "ben", 4 })]
	[InlineData(new object[] { "astra_yao", 5 })]
	public void GetAgentPriority_MatchesPythonMovePriority(string agentId, int expectedPriority)
	{
		Agent value = AgentEnum.Values.Single((AgentEnum agentEnum) => agentEnum.Value.AgentId == agentId).Value;
		Assert.Equal(expectedPriority, AutoBattleUtils.GetAgentPriority(value));
	}

	[Fact]
	public void GetBestAgentForMoving_ReturnsLowestPriorityAgent()
	{
		TeamInfo teamInfo = new TeamInfo();
		teamInfo.Agents.Add(new AgentInfo(AgentEnum.ASTRA_YAO.Value));
		teamInfo.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		teamInfo.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		AgentInfo bestAgentForMoving = AutoBattleUtils.GetBestAgentForMoving(teamInfo);
		Assert.Equal("安比", bestAgentForMoving.Agent.AgentName);
	}

	[Fact]
	public void SwitchToBestAgentForMoving_SwitchesByAgentNameWhenBestIsBackstage()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ASTRA_YAO.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		bool condition = AutoBattleUtils.SwitchToBestAgentForMoving(zContext);
		Assert.True(condition);
		Assert.Equal("安比", zContext.AutoBattleContext.AgentContext.Team.Agents[0].Agent.AgentName);
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = "space";
		Assert.Equal<List<string>>(list, buttons.Taps);
	}

	[Fact]
	public void SwitchToBestAgentForMoving_DoesNotSwitchWhenBestIsFront()
	{
		RecordingButtonController buttons;
		using ZContext zContext = CreateContext(out buttons);
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
		zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
		bool condition = AutoBattleUtils.SwitchToBestAgentForMoving(zContext);
		Assert.True(condition);
		Assert.Equal("安比", zContext.AutoBattleContext.AgentContext.Team.Agents[0].Agent.AgentName);
		Assert.Empty(buttons.Taps);
	}

	[Fact]
	public void CheckBattleEncounterFromState_ReturnsTrueForLifeDeductionOrDodgeAtFrameTime()
	{
		AutoBattleStateRecordService autoBattleStateRecordService = new AutoBattleStateRecordService();
		Assert.False(AutoBattleUtils.CheckBattleEncounterFromState(autoBattleStateRecordService, 10.0));
		autoBattleStateRecordService.UpdateState(new StateRecord("前台-血量扣减", 10.0, 1));
		Assert.True(AutoBattleUtils.CheckBattleEncounterFromState(autoBattleStateRecordService, 10.0));
		autoBattleStateRecordService.UpdateState(new StateRecord("前台-血量扣减", 0.0, null, null, null, isClear: true));
		autoBattleStateRecordService.UpdateState(new StateRecord("闪避识别-红光", 11.0));
		Assert.True(AutoBattleUtils.CheckBattleEncounterFromState(autoBattleStateRecordService, 11.0));
		Assert.False(AutoBattleUtils.CheckBattleEncounterFromState(autoBattleStateRecordService, 12.0));
	}

	[Fact]
	public void CreateMergeBuildRequests_UsesPythonAutoBattleDefaults()
	{
		IReadOnlyList<AutoBattleMergeBuildRequest> readOnlyList = AutoBattleBuildUtils.CreateMergeBuildRequests(new string[3] { "alpha", "", "beta" });
		Assert.Equal(2, readOnlyList.Count);
		Assert.All(readOnlyList, delegate(AutoBattleMergeBuildRequest request)
		{
			Assert.Equal("auto_battle", request.SubDir);
		});
		Assert.All(readOnlyList, delegate(AutoBattleMergeBuildRequest request)
		{
			Assert.False(request.ReadFromMerged);
		});
		Assert.Equal("alpha", readOnlyList[0].TemplateName);
		Assert.Equal("beta", readOnlyList[1].TemplateName);
	}

	[Fact]
	public void BuildAllMerge_LoadsAndSavesEachRequest()
	{
		List<FakeMergeBuilder> builders = new List<FakeMergeBuilder>();
		int actual = AutoBattleBuildUtils.BuildAllMerge(new string[2] { "alpha", "beta" }, delegate(AutoBattleMergeBuildRequest request)
		{
			FakeMergeBuilder fakeMergeBuilder = new FakeMergeBuilder(request);
			builders.Add(fakeMergeBuilder);
			return fakeMergeBuilder;
		});
		Assert.Equal(2, actual);
		Assert.All(builders, delegate(FakeMergeBuilder builder)
		{
			Assert.True(builder.LoadCalled);
		});
		Assert.All(builders, delegate(FakeMergeBuilder builder)
		{
			Assert.True(builder.SaveCalled);
		});
		Assert.Equal("alpha", builders[0].Request.TemplateName);
		Assert.Equal("beta", builders[1].Request.TemplateName);
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
