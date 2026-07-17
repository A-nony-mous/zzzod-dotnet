using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class AutoBattleCustomContextTests
{
	[Fact]
	public void SetState_WritesBatchCustomStates()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AutoBattleContext.CustomContext.SetState(new string[2] { "自定义-A", "自定义-B" }, 0.0, 0.0, 7, 2);
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-A");
		StateRecorder stateRecorder2 = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-B");
		Assert.True(stateRecorder.LastRecordTime > 0.0);
		Assert.True(stateRecorder2.LastRecordTime > 0.0);
		Assert.Equal(9, stateRecorder.LastValue);
		Assert.Equal(9, stateRecorder2.LastValue);
	}

	[Fact]
	public void ClearState_ClearsBatchCustomStates()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AutoBattleContext.CustomContext.SetState(new string[2] { "自定义-A", "自定义-B" }, 0.0, 0.0, 1);
		zContext.AutoBattleContext.CustomContext.ClearState(new string[2] { "自定义-A", "自定义-B" });
		StateRecorder stateRecorder = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-A");
		StateRecorder stateRecorder2 = zContext.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-B");
		Assert.Equal(0.0, stateRecorder.LastRecordTime);
		Assert.Equal(0.0, stateRecorder2.LastRecordTime);
		Assert.Null(stateRecorder.LastValue);
		Assert.Null(stateRecorder2.LastValue);
	}
}
