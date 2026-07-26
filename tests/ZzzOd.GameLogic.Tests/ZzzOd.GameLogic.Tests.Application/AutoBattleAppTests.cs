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
using OpenCvSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class AutoBattleAppTests
{
	private sealed class RecordingAutoBattleFlow : IAutoBattleAppFlow
	{
		public int RunCount { get; private set; }

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "自动战斗已启动"));
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

	private sealed class RecordingVirtualGamepadDependencyChecker(bool available) : IVirtualGamepadDependencyChecker
	{
		public int CallCount { get; private set; }

		public bool IsAvailable()
		{
			CallCount++;
			return available;
		}
	}

	private sealed class RecordingConfigurableGamepadController : IConfigurableButtonController, IButtonController
	{
		public TimeSpan KeyPressTime { get; private set; }

		public TimeSpan ComboPressTime { get; private set; }

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

		public void SetKeyPressTime(TimeSpan keyPressTime)
		{
			KeyPressTime = keyPressTime;
		}

		public void SetComboPressTime(TimeSpan comboPressTime)
		{
			ComboPressTime = comboPressTime;
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

		public List<bool> AutoUltimateValues { get; } = new List<bool>();

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
			AutoUltimateValues.Add(enabled);
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
	public void Factory_ExposesPythonMetadataAndCreatesAutoBattleApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			AutoBattleAppFactory autoBattleAppFactory = zContext.ApplicationFactoryRegistry.CreateAutoBattleFactory();
			IApplication application = autoBattleAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = autoBattleAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = autoBattleAppFactory.GetRunRecord(0);
			Assert.Equal("auto_battle", autoBattleAppFactory.AppId);
			Assert.Equal("自动战斗", autoBattleAppFactory.AppName);
			Assert.Equal("one_dragon", autoBattleAppFactory.GroupId);
			Assert.False(autoBattleAppFactory.NeedNotify);
			Assert.False(condition: false);
			Assert.IsType<AutoBattleApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			Assert.IsType<ZApplicationRunRecord>(runRecord);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersAutoBattleWithoutDefaultGroupOrNotify()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterAutoBattleApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("auto_battle"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("auto_battle"));
			Assert.DoesNotContain("auto_battle", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CheckGamepad_UsesKeyboardWithoutVirtualGamepad()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "keyboard", "测试配置", autoUltimateEnabled: false);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("无需手柄", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.KeyboardCount);
			Assert.Equal(0, recordingAutoBattleAppServices.XboxCount);
			Assert.Equal(0, recordingAutoBattleAppServices.Ds4Count);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CheckGamepad_FailsAndFallsBackToKeyboardWhenVirtualGamepadMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "xbox", "测试配置", autoUltimateEnabled: false);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices
			{
				VirtualGamepadInstalled = false
			};
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsFail);
			Assert.Equal("未安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.KeyboardCount);
			Assert.Equal(0, recordingAutoBattleAppServices.XboxCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CheckGamepad_EnablesDs4WhenVirtualGamepadExists()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "ds4", "测试配置", autoUltimateEnabled: false);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("已安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(0, recordingAutoBattleAppServices.KeyboardCount);
			Assert.Equal(0, recordingAutoBattleAppServices.XboxCount);
			Assert.Equal(1, recordingAutoBattleAppServices.Ds4Count);
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
	public void CheckGamepad_FailsDs4AndFallsBackToKeyboardWhenVirtualGamepadMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "ds4", "测试配置", autoUltimateEnabled: false);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices
			{
				VirtualGamepadInstalled = false
			};
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.CheckGamepad();
			Assert.True(operationRoundResult.IsFail);
			Assert.Equal("未安装虚拟手柄依赖", operationRoundResult.Status);
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
	public void CheckGamepad_KeepsPythonSuccessForUnknownControlMethodWhenVirtualGamepadExists()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "custom", "测试配置", autoUltimateEnabled: false);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.CheckGamepad();
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
	public void Operation_LoadsAutoBattleAndKeepsCheckingScreen()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "xbox", "测试配置", autoUltimateEnabled: true, 0.05);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, recordingAutoBattleAppServices);
			using Mat screen = new Mat(1, 1, MatType.CV_8UC3);
			SetCurrentScreenshot(autoBattleAppOperation, screen);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.CheckGamepad();
			OperationRoundResult operationRoundResult2 = autoBattleAppOperation.LoadOp();
			OperationRoundResult operationRoundResult3 = autoBattleAppOperation.CheckScreen();
			autoBattleAppOperation.PauseAutoBattle();
			autoBattleAppOperation.ResumeAutoBattle();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("已安装虚拟手柄依赖", operationRoundResult.Status);
			Assert.Equal(1, recordingAutoBattleAppServices.XboxCount);
			int num = 1;
			List<float> list = new List<float>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = 0.02f;
			Assert.Equal(list, recordingAutoBattleAppServices.KeyPressTimes);
			Assert.True(operationRoundResult2.IsSuccess);
			Assert.Equal("auto_battle", recordingAutoBattleAppServices.LoadedSubDir);
			Assert.Equal("测试配置", recordingAutoBattleAppServices.LoadedOpName);
			num = 2;
			List<bool> list2 = new List<bool>(num);
			CollectionsMarshal.SetCount(list2, num);
			Span<bool> span = CollectionsMarshal.AsSpan(list2);
			span[0] = true;
			span[1] = true;
			Assert.Equal(list2, recordingAutoBattleAppServices.AutoUltimateValues);
			Assert.Equal(1, recordingAutoBattleAppServices.StartCount);
			Assert.Equal(1, recordingAutoBattleAppServices.ResumeCount);
			Assert.Equal(1, recordingAutoBattleAppServices.StopCount);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult3.Kind);
			// screenshot_interval 节拍走补足制通道（对应 auto_battle_app.py:98 的 wait_round_time），
			// 补足量由框架轮循环按循环顶部锚点计算，业务层只声明目标时长。
			Assert.Null(operationRoundResult3.Delay);
			Assert.Equal(TimeSpan.FromSeconds(zContext.BattleAssistantConfig.ScreenshotInterval), operationRoundResult3.DelayUntilRoundTime);
			Assert.Equal(1, recordingAutoBattleAppServices.CheckScreenCount);
			Assert.False(recordingAutoBattleAppServices.LastBattleStateSync);
			Assert.True(autoBattleAppOperation.ScreenNodeActive);
			Assert.Equal(1, recordingAutoBattleAppServices.DispatchCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_EmptyScreenshotRetriesWithoutPublishingFalseBattleState()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "keyboard", "测试配置", autoUltimateEnabled: false);
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(context, recordingAutoBattleAppServices);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.CheckScreen();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal("未获取截图", operationRoundResult.Status);
			Assert.Equal(0, recordingAutoBattleAppServices.CheckScreenCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { true })]
	[InlineData(new object[] { false })]
	public void DefaultServices_UsesVirtualGamepadDependencyChecker(bool available)
	{
		RecordingVirtualGamepadDependencyChecker recordingVirtualGamepadDependencyChecker = new RecordingVirtualGamepadDependencyChecker(available);
		DefaultAutoBattleAppServices defaultAutoBattleAppServices = new DefaultAutoBattleAppServices(recordingVirtualGamepadDependencyChecker);
		bool actual = defaultAutoBattleAppServices.IsVirtualGamepadInstalled();
		Assert.Equal(available, actual);
		Assert.Equal(1, recordingVirtualGamepadDependencyChecker.CallCount);
	}

	[Fact]
	public void DefaultServices_WritesGamepadKeyPressTimeToActiveController()
	{
		string text = CreateTempRoot();
		try
		{
			RecordingConfigurableGamepadController recordingConfigurableGamepadController = new RecordingConfigurableGamepadController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.AttachController(new ZPcController(new GameConfig(), null, 1920, 1080, null, null, null, null, null, recordingConfigurableGamepadController, skipForegroundActivation: true));
			DefaultAutoBattleAppServices defaultAutoBattleAppServices = new DefaultAutoBattleAppServices(new RecordingVirtualGamepadDependencyChecker(available: true));
			defaultAutoBattleAppServices.SetGamepadKeyPressTime(zContext, 0.075f);
			Assert.Equal(TimeSpan.FromSeconds(0.07500000298023224), recordingConfigurableGamepadController.KeyPressTime);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_RejectsModeChangeWithoutReadyZzzController()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DefaultAutoBattleAppServices defaultAutoBattleAppServices = new DefaultAutoBattleAppServices(new RecordingVirtualGamepadDependencyChecker(available: true));
			Assert.False(defaultAutoBattleAppServices.EnableKeyboard(zContext));
			Assert.False(defaultAutoBattleAppServices.EnableXbox(zContext));
			Assert.False(defaultAutoBattleAppServices.EnableDs4(zContext));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_LoadAndStartUseAutoBattleLifecycle()
	{
		string text = CreateTempRoot();
		try
		{
			WriteBattleAssistantConfig(text, "keyboard", "resume", autoUltimateEnabled: true);
			WriteAutoBattleConfig(text, "resume");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			DefaultAutoBattleAppServices defaultAutoBattleAppServices = new DefaultAutoBattleAppServices(new RecordingVirtualGamepadDependencyChecker(available: true));
			AutoBattleOperator autoBattleOperator = defaultAutoBattleAppServices.LoadAutoOp(zContext, "auto_battle", "resume");
			zContext.AutoBattleContext.AutoUltimateEnabled = false;
			zContext.AutoBattleContext.LastCheckEndResult = "旧结果";
			defaultAutoBattleAppServices.StartAutoBattle(zContext);
			Assert.Same(autoBattleOperator, zContext.AutoBattleContext.AutoOp);
			Assert.True(zContext.AutoBattleContext.AutoUltimateEnabled);
			Assert.Null(zContext.AutoBattleContext.LastCheckEndResult);
			Assert.True(zContext.AutoBattleContext.IsRuntimeRunning);
			Assert.True(autoBattleOperator.IsRunning);
			defaultAutoBattleAppServices.StopAutoBattle(zContext);
			Assert.False(zContext.AutoBattleContext.IsRuntimeRunning);
			Assert.False(autoBattleOperator.IsRunning);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task AutoBattleApp_RunsInjectedFlowAndDelegatesPauseResumeStop()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingAutoBattleFlow flow = new RecordingAutoBattleFlow();
			AutoBattleApp app = new AutoBattleApp(context, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			await app.OnPauseAsync(CancellationToken.None);
			await app.OnResumeAsync(CancellationToken.None);
			await app.OnStopAsync(CancellationToken.None);
			Assert.True(result.IsSuccess);
			Assert.Equal("自动战斗已启动", result.Status);
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
	public void AutoBattleOperation_StopClearsScreenNodeBeforeLateResume()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingAutoBattleAppServices recordingAutoBattleAppServices = new RecordingAutoBattleAppServices();
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, recordingAutoBattleAppServices);
			autoBattleAppOperation.CheckScreen();
			autoBattleAppOperation.StopAutoBattle();
			autoBattleAppOperation.ResumeAutoBattle();
			Assert.False(autoBattleAppOperation.ScreenNodeActive);
			Assert.Equal(1, recordingAutoBattleAppServices.StopCount);
			Assert.Equal(0, recordingAutoBattleAppServices.ResumeCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void AutoBattleOperation_LoadFailureWritesContextLoggerAndNodeStatus()
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
			AutoBattleAppOperation autoBattleAppOperation = new AutoBattleAppOperation(zContext, services);
			OperationRoundResult operationRoundResult = autoBattleAppOperation.LoadOp();
			Assert.False(operationRoundResult.IsSuccess);
			Assert.Equal("加载指令失败", operationRoundResult.Status);
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text == "加载自动战斗指令失败"));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(AutoBattleAppOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[3] { "手柄检测", "加载自动战斗指令", "画面识别" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["手柄检测"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.True(readOnlyDictionary["画面识别"].GetCustomAttribute<OperationNodeAttribute>().Mute);
		Assert.Contains(readOnlyDictionary["加载自动战斗指令"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "手柄检测");
		Assert.Contains(readOnlyDictionary["画面识别"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "加载自动战斗指令");
	}

	private static void WriteBattleAssistantConfig(string rootDirectory, string controlMethod, string autoBattleConfig, bool autoUltimateEnabled, double screenshotInterval = 0.02)
	{
		string text = Path.Combine(rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "battle_assistant.yml"), $"control_method: {controlMethod}\nauto_battle_config: {autoBattleConfig}\nauto_ultimate_enabled: {autoUltimateEnabled.ToString().ToLowerInvariant()}\nscreenshot_interval: {screenshotInterval}");
	}

	private static void SetCurrentScreenshot(ZOperation operation, Mat screen)
	{
		typeof(ZOperation).GetProperty("LastScreenshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, screen);
		typeof(ZOperation).GetProperty("LastScreenshotTimeUtc", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, DateTimeOffset.UtcNow);
	}

	private static void WriteAutoBattleConfig(string rootDirectory, string templateName)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, templateName + ".yml"), "scenes:\n  - triggers: [\"自定义-触发\"]\n    priority: 1\n    interval: 0\n    handlers:\n      - states: \"[自定义-触发, 0, 1]\"\n        operations:\n          - op_name: \"设置状态\"\n            state: \"自定义-已执行\"");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
