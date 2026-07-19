using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Input;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public class AutoBattleContextLifecycleTests
{
	private sealed class RecordingButtonController : IButtonController
	{
		public List<string> Taps { get; } = new List<string>();

		public List<string> Releases { get; } = new List<string>();

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
		}

		public void Release(string key)
		{
			Releases.Add(key);
		}

		public void Reset()
		{
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

	[Fact]
	public void StartContext_DoesNotStartUninitializedOperator()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
		AutoBattleOperator autoBattleOperator = (autoBattleContext.AutoOp = new AutoBattleOperator(autoBattleContext, "test_dir", "test_template"));
		autoBattleContext.StartContextAsync();
		Assert.False(autoBattleOperator.IsRunning);
		autoBattleContext.StopContext();
		Assert.False(autoBattleOperator.IsRunning);
	}

	[Fact]
	public void InitScreenArea_FailsWhenPythonRequiredBattleAreasAreMissing()
	{
		ZContext ctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		try
		{
			InvalidOperationException ex = Assert.Throws<InvalidOperationException>(delegate
			{
				ctx.AutoBattleContext.InitScreenArea();
			});
			Assert.Contains("战斗画面", ex.Message);
		}
		finally
		{
			if (ctx != null)
			{
				((IDisposable)ctx).Dispose();
			}
		}
	}

	[Fact]
	public void StartContext_OnlyStartsPythonDodgeContext()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
		autoBattleContext.StartContextAsync();
		autoBattleContext.StopContext();
		Assert.False(autoBattleContext.IsRuntimeRunning);
	}

	[Fact]
	public void StartContextAsync_IsIdempotent()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
		autoBattleContext.StartContextAsync();
		autoBattleContext.StartContextAsync();
		autoBattleContext.StopContext();
		Assert.False(autoBattleContext.IsRuntimeRunning);
	}

	[Fact]
	public void AfterAppShutdown_StopsContext()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
		autoBattleContext.StartContextAsync();
		autoBattleContext.AfterAppShutdown();
		Assert.False(autoBattleContext.IsRuntimeRunning);
		Assert.True(autoBattleContext.AfterAppShutdownCalled);
	}

	[Fact]
	public void ResumeContextAsync_RestartsContextAndClearsOperatorUsageStates()
	{
		string text = CreateTempRoot();
		try
		{
			WriteResumeConfig(text);
			using ZContext ctx = new ZContext(new OneDragonEnvironment(text));
			AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
			AutoBattleOperator autoBattleOperator = new AutoBattleOperator(autoBattleContext, "auto_battle", "resume", readFromMerged: false);
			Assert.True(autoBattleOperator.InitBeforeRunning().Success);
			autoBattleContext.AutoOp = autoBattleOperator;
			autoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-触发", 7.0, 1));
			autoBattleContext.ResumeContextAsync();
			StateRecorder stateRecorder = autoBattleContext.StateRecordService.GetStateRecorder("自定义-触发");
			Assert.True(autoBattleOperator.IsRunning);
			Assert.True(autoBattleContext.IsRuntimeRunning);
			autoBattleContext.StopContext();
			Assert.Equal(-1.0, stateRecorder.LastRecordTime);
			Assert.Null(stateRecorder.LastValue);
			Assert.False(autoBattleOperator.IsRunning);
			Assert.False(autoBattleContext.IsRuntimeRunning);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void StartAutoBattle_InitializesBattleContextAndClearsUsageStates()
	{
		string text = CreateTempRoot();
		try
		{
			WriteResumeConfig(text);
			using ZContext ctx = new ZContext(new OneDragonEnvironment(text));
			AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
			autoBattleContext.InitAutoOp("resume");
			autoBattleContext.AutoUltimateEnabled = false;
			autoBattleContext.LastCheckEndResult = "旧结果";
			autoBattleContext.TargetContext.UpdateBattleDistance(12f);
			autoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-触发", 7.0, 1));
			autoBattleContext.StartAutoBattle();
			StateRecorder stateRecorder = autoBattleContext.StateRecordService.GetStateRecorder("自定义-触发");
			Assert.True(autoBattleContext.AutoUltimateEnabled);
			Assert.Null(autoBattleContext.LastCheckEndResult);
			Assert.False(autoBattleContext.LastCheckInBattle);
			Assert.Equal(-1f, autoBattleContext.LastCheckDistance);
			Assert.Equal(0, autoBattleContext.WithDistanceTimes);
			Assert.Equal(0, autoBattleContext.WithoutDistanceTimes);
			Assert.Equal(1.0, autoBattleContext.TargetContext.CheckDistanceInterval);
			Assert.Equal(-1.0, stateRecorder.LastRecordTime);
			Assert.Null(stateRecorder.LastValue);
			Assert.True(autoBattleContext.AutoOp.IsRunning);
			Assert.True(autoBattleContext.IsRuntimeRunning);
			autoBattleContext.StopAutoBattle();
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunSessionEmergencyStop_StopsAutoBattleParticipant()
	{
		string text = CreateTempRoot();
		try
		{
			WriteResumeConfig(text);
			using ZContext ctx = new ZContext(new OneDragonEnvironment(text));
			Assert.True(ctx.RunContext.StartRunning());
			AutoBattleContext autoBattleContext = ctx.AutoBattleContext;
			autoBattleContext.InitAutoOp("resume");
			autoBattleContext.StartAutoBattle();
			Assert.True(autoBattleContext.IsRuntimeRunning);
			Assert.True(autoBattleContext.AutoOp!.IsRunning);

			ctx.RunContext.StopRunningAsync().GetAwaiter().GetResult();

			Assert.False(autoBattleContext.IsRuntimeRunning);
			Assert.False(autoBattleContext.AutoOp.IsRunning);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void InitBattleContext_PreservesLastBattleScreenState()
	{
		using ZContext ctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
		PropertyInfo property = typeof(AutoBattleContext).GetProperty("LastCheckInBattle");
		property.SetMethod.Invoke(autoBattleContext, new object[1] { true });
		autoBattleContext.InitBattleContext();
		Assert.True(autoBattleContext.LastCheckInBattle);
	}

	[Fact]
	public void CheckBattleState_EmptyFrameKeepsLastConfirmedBattleState()
	{
		using ZContext ctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
		PropertyInfo property = typeof(AutoBattleContext).GetProperty("LastCheckInBattle");
		property.SetMethod.Invoke(autoBattleContext, new object[1] { true });
		bool condition = autoBattleContext.CheckBattleState(null, 1.0);
		Assert.True(condition);
		Assert.True(autoBattleContext.LastCheckInBattle);
	}

	[Fact]
	public void AfterAppShutdown_KeepsCachedOperatorAndStopsIt()
	{
		using ZContext ctx = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		AutoBattleContext autoBattleContext = new AutoBattleContext(ctx);
		AutoBattleOperator autoBattleOperator = (autoBattleContext.AutoOp = new AutoBattleOperator(autoBattleContext, "test_dir", "test_template"));
		autoBattleOperator.StartRunningAsync();
		autoBattleContext.AfterAppShutdown();
		Assert.Same(autoBattleOperator, autoBattleContext.AutoOp);
		Assert.False(autoBattleOperator.IsRunning);
		Assert.True(autoBattleContext.AfterAppShutdownCalled);
	}

	[Fact]
	public void StopContext_ReleasesZPcControllerBattleKeys_WhenContextWasNotStarted()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		RecordingButtonController recordingButtonController = new RecordingButtonController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(recordingButtonController), null, recordingButtonController, null, null, skipForegroundActivation: true);
		zContext.AttachController(controller);
		AutoBattleContext autoBattleContext = new AutoBattleContext(zContext);
		autoBattleContext.StopContext();
		Assert.Contains("shift", (IEnumerable<string>)recordingButtonController.Releases);
		Assert.Contains("mouse_left", (IEnumerable<string>)recordingButtonController.Releases);
		Assert.Contains("e", (IEnumerable<string>)recordingButtonController.Releases);
		Assert.Contains("w", (IEnumerable<string>)recordingButtonController.Releases);
		Assert.Contains("space", (IEnumerable<string>)recordingButtonController.Releases);
		Assert.Contains("q", (IEnumerable<string>)recordingButtonController.Releases);
		Assert.False(autoBattleContext.IsRuntimeRunning);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteResumeConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "resume.yml"), "scenes:\n  - triggers: [\"自定义-触发\"]\n    priority: 1\n    interval: 0\n    handlers:\n      - states: \"[自定义-触发, 0, 1]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-已执行\"");
	}
}
