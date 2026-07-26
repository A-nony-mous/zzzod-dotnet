using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.GameLogic.Application.BattleAssistant.DodgeAssistant;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class DodgeAssistantAppTests
{
	private sealed class RecordingDodgeAssistantFlow : IDodgeAssistantFlow
	{
		public int RunCount { get; private set; }

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "闪避助手已启动"));
		}

		public void Pause(ZContext context)
		{
			PauseCount++;
		}

		public void Resume(ZContext context)
		{
			ResumeCount++;
		}

		public void Stop(ZContext context)
		{
			StopCount++;
		}
	}

	private sealed class RecordingAutoBattleAppServices : IAutoBattleAppServices
	{
		public bool VirtualGamepadInstalled { get; set; } = true;

		public Exception? LoadException { get; set; }

		public int KeyboardCount { get; private set; }

		public int GamepadCheckCount { get; private set; }

		public int XboxCount { get; private set; }

		public int Ds4Count { get; private set; }

		public int StartCount { get; private set; }

		public int StopCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int DispatchCount { get; private set; }

		public int CheckScreenCount { get; private set; }

		public bool? LastBattleStateSync { get; private set; }

		public string? LoadedSubDir { get; private set; }

		public string? LoadedOpName { get; private set; }

		public List<float> KeyPressTimes { get; } = new List<float>();

		public bool EnableKeyboard(ZContext context)
		{
			KeyboardCount++;
			return true;
		}

		public bool EnableXbox(ZContext context)
		{
			XboxCount++;
			return true;
		}

		public bool EnableDs4(ZContext context)
		{
			Ds4Count++;
			return true;
		}

		public void SetGamepadKeyPressTime(ZContext context, float seconds)
		{
			KeyPressTimes.Add(seconds);
		}

		public bool IsVirtualGamepadInstalled()
		{
			GamepadCheckCount++;
			return VirtualGamepadInstalled;
		}

		public AutoBattleOperator LoadAutoOp(ZContext context, string subDir, string opName)
		{
			if (LoadException != null)
			{
				throw LoadException;
			}
			LoadedSubDir = subDir;
			LoadedOpName = opName;
			return new AutoBattleOperator(context.AutoBattleContext, subDir, opName);
		}

		public void SetAutoUltimateEnabled(ZContext context, bool enabled)
		{
		}

		public void DispatchOpLoaded(ZContext context, AutoBattleOperator autoOp)
		{
			DispatchCount++;
		}

		public void StartAutoBattle(ZContext context)
		{
			StartCount++;
		}

		public void StopAutoBattle(ZContext context)
		{
			StopCount++;
		}

		public void ResumeAutoBattle(ZContext context)
		{
			ResumeCount++;
		}

		public void CheckBattleState(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc, bool sync)
		{
			CheckScreenCount++;
			LastBattleStateSync = sync;
		}
	}

	private sealed class RecordingLogSink : ILogEventSink
	{
		public List<LogEvent> Events { get; } = new List<LogEvent>();

		public void Emit(LogEvent logEvent)
		{
			Events.Add(logEvent);
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesDodgeAssistantApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DodgeAssistantFactory dodgeAssistantFactory = zContext.ApplicationFactoryRegistry.CreateDodgeAssistantFactory();
			IApplication application = dodgeAssistantFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = dodgeAssistantFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = dodgeAssistantFactory.GetRunRecord(0);
			Assert.Equal("dodge_assistant", dodgeAssistantFactory.AppId);
			Assert.Equal("闪避助手", dodgeAssistantFactory.AppName);
			Assert.Equal("one_dragon", dodgeAssistantFactory.GroupId);
			Assert.False(dodgeAssistantFactory.NeedNotify);
			Assert.False(condition: false);
			Assert.IsType<DodgeAssistantApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			Assert.IsType<ZApplicationRunRecord>(runRecord);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersDodgeAssistantWithoutDefaultGroupOrNotify()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterDodgeAssistantApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("dodge_assistant"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("dodge_assistant"));
			Assert.DoesNotContain("dodge_assistant", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_LoadsDodgeConfigAndKeepsCheckingDodge()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "ds4", "闪避-自定义", 0.04);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, recordingAutoBattleAppServices);
			using Mat screen = new Mat(1, 1, MatType.CV_8UC3);
			SetCurrentScreenshot(dodgeAssistantOperation, screen);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.CheckGamepad();
			OperationRoundResult operationRoundResult2 = dodgeAssistantOperation.LoadOp();
			OperationRoundResult operationRoundResult3 = dodgeAssistantOperation.CheckDodge();
			dodgeAssistantOperation.PauseAutoBattle();
			dodgeAssistantOperation.ResumeAutoBattle();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("已安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.Ds4Count);
			int num = 1;
			List<float> list = new List<float>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = 0.02f;
			Assert.Equal(list, recordingAutoBattleAppServices.KeyPressTimes);
			Assert.True(operationRoundResult2.IsSuccess);
			Assert.Equal("dodge", recordingAutoBattleAppServices.LoadedSubDir);
			Assert.Equal("闪避-自定义", recordingAutoBattleAppServices.LoadedOpName);
			Assert.Equal(1, recordingAutoBattleAppServices.StartCount);
			Assert.Equal(1, recordingAutoBattleAppServices.ResumeCount);
			Assert.Equal(1, recordingAutoBattleAppServices.StopCount);
			Assert.Equal(1, recordingAutoBattleAppServices.DispatchCount);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult3.Kind);
			// screenshot_interval 节拍走补足制通道（对应 dodge_assistant_app.py:87 的 wait_round_time）。
			Assert.Null(operationRoundResult3.Delay);
			Assert.Equal(TimeSpan.FromSeconds(zContext.BattleAssistantConfig.ScreenshotInterval), operationRoundResult3.DelayUntilRoundTime);
			Assert.Equal(1, recordingAutoBattleAppServices.CheckScreenCount);
			Assert.False(recordingAutoBattleAppServices.LastBattleStateSync);
			Assert.True(dodgeAssistantOperation.DodgeNodeActive);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_EmptyScreenshotRetriesWithoutSchedulingDodgeCheck()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "keyboard", "闪避-自定义");
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(context, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.CheckDodge();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal("未获取截图", operationRoundResult.Status);
			Assert.Equal(0, recordingAutoBattleAppServices.CheckScreenCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_UsesKeyboardWithoutVirtualGamepad()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "keyboard", "闪避");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("无需手柄", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.KeyboardCount);
			Assert.Equal(0, recordingAutoBattleAppServices.GamepadCheckCount);
			Assert.Equal(0, recordingAutoBattleAppServices.XboxCount);
			Assert.Equal(0, recordingAutoBattleAppServices.Ds4Count);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_EnablesXboxWhenVirtualGamepadExists()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "xbox", "闪避");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("已安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.GamepadCheckCount);
			Assert.Equal(0, recordingAutoBattleAppServices.KeyboardCount);
			Assert.Equal(1, recordingAutoBattleAppServices.XboxCount);
			Assert.Equal(0, recordingAutoBattleAppServices.Ds4Count);
			int num = 1;
			List<float> list = new List<float>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = 0.02f;
			Assert.Equal(list, recordingAutoBattleAppServices.KeyPressTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_FailsWhenVirtualGamepadMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "xbox", "闪避");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices
			{
				VirtualGamepadInstalled = false
			};
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsFail);
			Assert.Equal("未安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.GamepadCheckCount);
			Assert.Equal(1, recordingAutoBattleAppServices.KeyboardCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_FailsDs4AndFallsBackToKeyboardWhenVirtualGamepadMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "ds4", "闪避");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices
			{
				VirtualGamepadInstalled = false
			};
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsFail);
			Assert.Equal("未安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.GamepadCheckCount);
			Assert.Equal(1, recordingAutoBattleAppServices.KeyboardCount);
			Assert.Equal(0, recordingAutoBattleAppServices.Ds4Count);
			Assert.Empty(recordingAutoBattleAppServices.KeyPressTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_KeepsPythonSuccessForUnknownControlMethodWhenVirtualGamepadExists()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "custom", "闪避");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("已安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.GamepadCheckCount);
			Assert.Equal(0, recordingAutoBattleAppServices.KeyboardCount);
			Assert.Equal(0, recordingAutoBattleAppServices.XboxCount);
			Assert.Equal(0, recordingAutoBattleAppServices.Ds4Count);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_LoadsAndInitializesDodgeAutoOp()
	{
		string text = CreateTempRoot();
		try
		{
			WriteDodgeAutoOp(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DefaultAutoBattleAppServices defaultAutoBattleAppServices = new DefaultAutoBattleAppServices();
			AutoBattleOperator autoBattleOperator = defaultAutoBattleAppServices.LoadAutoOp(zContext, "dodge", "闪避");
			Assert.Same(autoBattleOperator, zContext.AutoBattleContext.AutoOp);
			Assert.Contains("闪避识别-红光", autoBattleOperator.UsageStates);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task DodgeAssistantApp_RunsInjectedFlowAndDelegatesPauseResumeStop()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingDodgeAssistantFlow flow = new RecordingDodgeAssistantFlow();
			DodgeAssistantApp app = new DodgeAssistantApp(context, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			await app.OnPauseAsync(CancellationToken.None);
			await app.OnResumeAsync(CancellationToken.None);
			await app.OnStopAsync(CancellationToken.None);
			Assert.True(result.IsSuccess);
			Assert.Equal("闪避助手已启动", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, flow.PauseCount);
			Assert.Equal(1, flow.ResumeCount);
			Assert.Equal(1, flow.StopCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void DodgeAssistantOperation_StopClearsDodgeNodeBeforeLateResume()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, recordingAutoBattleAppServices);
			dodgeAssistantOperation.CheckDodge();
			dodgeAssistantOperation.StopAutoBattle();
			dodgeAssistantOperation.ResumeAutoBattle();
			Assert.False(dodgeAssistantOperation.DodgeNodeActive);
			Assert.Equal(1, recordingAutoBattleAppServices.StopCount);
			Assert.Equal(0, recordingAutoBattleAppServices.ResumeCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DodgeAssistantOperation_LoadFailureWritesContextLoggerAndNodeStatus()
	{
		string text = CreateTempRoot();
		try
		{
			RecordingLogSink recordingLogSink = new RecordingLogSink();
			using Logger logger = new LoggerConfiguration().WriteTo.Sink(recordingLogSink).CreateLogger();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text), logger);
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices services = new RecordingAutoBattleAppServices
			{
				LoadException = new InvalidOperationException("配置损坏")
			};
			DodgeAssistantOperation dodgeAssistantOperation = new DodgeAssistantOperation(zContext, services);
			OperationRoundResult operationRoundResult = dodgeAssistantOperation.LoadOp();
			Assert.False(operationRoundResult.IsSuccess);
			Assert.Equal("加载指令失败: 配置损坏", operationRoundResult.Status);
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text == "加载闪避指令失败"));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(DodgeAssistantOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[3] { "手柄检测", "加载自动战斗指令", "闪避判断" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["手柄检测"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.True(readOnlyDictionary["闪避判断"].GetCustomAttribute<OperationNodeAttribute>().Mute);
		Assert.Contains(readOnlyDictionary["加载自动战斗指令"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "手柄检测");
		Assert.Contains(readOnlyDictionary["闪避判断"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "加载自动战斗指令");
	}

	private static void WriteBattleAssistantConfig(string rootDirectory, string controlMethod, string dodgeAssistantConfig, double screenshotInterval = 0.02)
	{
		string text = Path.Combine(rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "battle_assistant.yml"), $"control_method: {controlMethod}\ndodge_assistant_config: {dodgeAssistantConfig}\nscreenshot_interval: {screenshotInterval}");
	}

	private static void SetCurrentScreenshot(ZOperation operation, Mat screen)
	{
		typeof(ZOperation).GetProperty("LastScreenshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, screen);
		typeof(ZOperation).GetProperty("LastScreenshotTimeUtc", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, DateTimeOffset.UtcNow);
	}

	private static void WriteDodgeAutoOp(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "dodge");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "闪避.sample.yml"), "scenes:\n  - triggers: [\"闪避识别-红光\"]\n    handlers:\n      - states: \"[闪避识别-红光, 0, 1]\"\n        operations:\n          - op_name: \"按键-闪避\"");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
