using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Input;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class MouseSensitivityCheckerAppTests
{
	private sealed class RecordingMouseSensitivityFlow : IMouseSensitivityCheckerFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "鼠标灵敏度检测完成"));
		}
	}

	private sealed class RecordingMouseSensitivityServices : IMouseSensitivityCheckerOperationServices
	{
		private int _angleIndex;

		public List<double> Angles { get; set; } = new List<double>();

		public bool GamepadMode { get; set; }

		public List<int> MouseTurns { get; } = new List<int>();

		public List<double> GamepadTurns { get; } = new List<double>();

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}

		public Task<OperationResult> TransportToVideoStoreAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "录像店"));
		}

		public bool IsGamepadMode(ZContext context)
		{
			return GamepadMode;
		}

		public double? ReadViewAngle(ZContext context)
		{
			if (_angleIndex >= Angles.Count)
			{
				return null;
			}
			return Angles[_angleIndex++];
		}

		public void TurnByDistance(ZContext context, int distance)
		{
			MouseTurns.Add(distance);
		}

		public void TurnGamepad(ZContext context, double durationSeconds)
		{
			GamepadTurns.Add(durationSeconds);
		}

		public void UpdateTurnDx(ZContext context, double turnDx)
		{
		}

		public void UpdateGamepadTurnSpeed(ZContext context, double speed)
		{
		}
	}

	private sealed class RecordingAnalogGamepad : IButtonController, IAnalogGamepadController
	{
		public List<(float X, float Y, TimeSpan Duration)> RightStickMoves { get; } = new List<(float, float, TimeSpan)>();

		public void MoveRightStick(float x, float y, TimeSpan duration)
		{
			RightStickMoves.Add((x, y, duration));
		}

		public void Tap(string key)
		{
		}

		public void TapCombo(IReadOnlyList<string> keys)
		{
		}

		public void Press(string key, TimeSpan? pressTime = null)
		{
		}

		public void Release(string key)
		{
		}

		public void Reset()
		{
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesMouseSensitivityCheckerApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			MouseSensitivityCheckerFactory mouseSensitivityCheckerFactory = zContext.ApplicationFactoryRegistry.CreateMouseSensitivityCheckerFactory();
			IApplication application = mouseSensitivityCheckerFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = mouseSensitivityCheckerFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = mouseSensitivityCheckerFactory.GetRunRecord(0);
			Assert.Equal("mouse_sensitivity_checker", mouseSensitivityCheckerFactory.AppId);
			Assert.Equal("鼠标灵敏度检测", mouseSensitivityCheckerFactory.AppName);
			Assert.Equal("one_dragon", mouseSensitivityCheckerFactory.GroupId);
			Assert.False(mouseSensitivityCheckerFactory.NeedNotify);
			Assert.False(condition: false);
			Assert.IsType<MouseSensitivityCheckerApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			ZApplicationRunRecord zApplicationRunRecord = Assert.IsType<ZApplicationRunRecord>(runRecord);
			Assert.Equal("mouse_sensitivity_checker", zApplicationRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersMouseSensitivityCheckerWithoutDefaultGroupOrNotify()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterMouseSensitivityCheckerApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("mouse_sensitivity_checker"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("mouse_sensitivity_checker"));
			Assert.DoesNotContain("mouse_sensitivity_checker", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task App_RunsInjectedCheckerFlowAndUpdatesRunRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ZApplicationRunRecord runRecord = new ZApplicationRunRecord("mouse_sensitivity_checker");
			RecordingMouseSensitivityFlow flow = new RecordingMouseSensitivityFlow();
			MouseSensitivityCheckerApp app = new MouseSensitivityCheckerApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("鼠标灵敏度检测完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void NormalizeAngleDiff_HandlesWrapAround()
	{
		Assert.Equal(-350.0, MouseSensitivityCheckerOperation.NormalizeAngleDiff(-350.0));
		Assert.Equal(-10.0, MouseSensitivityCheckerOperation.NormalizeAngleDiff(350.0));
		Assert.Equal(-357.0, MouseSensitivityCheckerOperation.NormalizeAngleDiff(-357.0));
		Assert.Equal(45.0, MouseSensitivityCheckerOperation.NormalizeAngleDiff(45.0));
	}

	[Fact]
	public void Operation_RejectsGamepadCalibrationWithoutMouseTurnDx()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(new ReadyController());
		zContext.GameConfig.TurnDx = 0f;
		MouseSensitivityCheckerOperation mouseSensitivityCheckerOperation = new MouseSensitivityCheckerOperation(zContext, new RecordingMouseSensitivityServices
		{
			GamepadMode = true
		});
		OperationRoundResult operationRoundResult = mouseSensitivityCheckerOperation.Check();
		Assert.False(operationRoundResult.IsSuccess);
		Assert.Equal("手柄灵敏度检测需先完成鼠标灵敏度检测 (turn_dx)", operationRoundResult.Status);
	}

	[Fact]
	public async Task Operation_CalculatesMouseTurnDxWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingMouseSensitivityServices services = new RecordingMouseSensitivityServices
			{
				Angles = new List<double>(10) { 0.0, 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0 }
			};
			MouseSensitivityCheckerOperation operation = new MouseSensitivityCheckerOperation(context, services);
			await operation.BackAtFirst().WaitAsync(TimeSpan.FromSeconds(2L));
			await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			for (int i = 0; i < 10; i++)
			{
				operation.Check();
			}
			OperationRoundResult result = operation.Calculate();
			Assert.True(result.IsSuccess);
			Assert.Equal("完成检测", result.Status);
			Assert.Equal(50f, context.GameConfig.TurnDx, 3);
			Assert.Equal(9, services.MouseTurns.Count);
			Assert.All(services.MouseTurns, delegate(int distance)
			{
				Assert.Equal(500, distance);
			});
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Operation_CalculatesGamepadTurnSpeedWithoutGameWindow()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.GameConfig.TurnDx = -5.5f;
			RecordingMouseSensitivityServices obj = new RecordingMouseSensitivityServices
			{
				GamepadMode = true
			};
			int num = 10;
			List<double> list = new List<double>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<double> span = CollectionsMarshal.AsSpan(list);
			span[0] = 0.0;
			span[1] = 12.0;
			span[2] = 24.0;
			span[3] = 36.0;
			span[4] = 48.0;
			span[5] = 60.0;
			span[6] = 72.0;
			span[7] = 84.0;
			span[8] = 96.0;
			span[9] = 108.0;
			obj.Angles = list;
			RecordingMouseSensitivityServices recordingMouseSensitivityServices = obj;
			MouseSensitivityCheckerOperation mouseSensitivityCheckerOperation = new MouseSensitivityCheckerOperation(zContext, recordingMouseSensitivityServices);
			for (int i = 0; i < 10; i++)
			{
				mouseSensitivityCheckerOperation.Check();
			}
			OperationRoundResult operationRoundResult = mouseSensitivityCheckerOperation.Calculate();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal(220f, zContext.GameConfig.GamepadTurnSpeed, 3);
			Assert.Equal(9, recordingMouseSensitivityServices.GamepadTurns.Count);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_TurnGamepadSendsPythonRightStickGesture()
	{
		RecordingAnalogGamepad recordingAnalogGamepad = new RecordingAnalogGamepad();
		GameConfig gameConfig = new GameConfig();
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, null, null, null, null, recordingAnalogGamepad, skipForegroundActivation: true);
		zPcController.EnableBackgroundMode();
		zContext.AttachController(zPcController);
		DefaultMouseSensitivityCheckerOperationServices defaultMouseSensitivityCheckerOperationServices = new DefaultMouseSensitivityCheckerOperationServices();
		defaultMouseSensitivityCheckerOperationServices.TurnGamepad(zContext, 0.3);
		int num = 1;
		List<(float, float, TimeSpan)> list = new List<(float, float, TimeSpan)>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = (1f, 0f, TimeSpan.FromSeconds(0.3));
		Assert.Equal<List<(float, float, TimeSpan)>>(list, recordingAnalogGamepad.RightStickMoves);
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(MouseSensitivityCheckerOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[4] { "返回大世界", "传送", "转向检测", "结果统计" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["返回大世界"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["传送"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "返回大世界");
		Assert.Contains(readOnlyDictionary["转向检测"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "传送");
		Assert.Contains(readOnlyDictionary["结果统计"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "转向检测");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
