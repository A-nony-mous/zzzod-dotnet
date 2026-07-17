using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Input;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.Devtools.OperationDebug;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class OperationDebugTests
{
	private sealed class RecordingAtomicOpFactory : IOperationDebugAtomicOpFactory
	{
		public List<RecordingAtomicOp> Created { get; } = new List<RecordingAtomicOp>();

		public AtomicOp Create(OperationDef operationDef)
		{
			RecordingAtomicOp recordingAtomicOp = new RecordingAtomicOp(operationDef.OpName ?? string.Empty);
			Created.Add(recordingAtomicOp);
			return recordingAtomicOp;
		}
	}

	private sealed class RecordingAtomicOp : AtomicOp
	{
		public int ExecuteCount { get; private set; }

		public int StopCount { get; private set; }

		public int DisposeCount { get; private set; }

		public RecordingAtomicOp(string opName)
			: base(opName)
		{
		}

		public override void Execute()
		{
			ExecuteCount++;
		}

		public override void Stop()
		{
			StopCount++;
		}

		public override void Dispose()
		{
			DisposeCount++;
		}
	}

	private sealed class BlockingAtomicOpFactory : IOperationDebugAtomicOpFactory
	{
		public BlockingAtomicOp Operation { get; } = new BlockingAtomicOp();

		public AtomicOp Create(OperationDef operationDef)
		{
			return Operation;
		}
	}

	private sealed class BlockingAtomicOp : AtomicOp
	{
		private readonly ManualResetEventSlim _stopGate = new ManualResetEventSlim(initialState: false);

		public TaskCompletionSource Started { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public int StopCount { get; private set; }

		public int DisposeCount { get; private set; }

		public override void Execute()
		{
			Started.TrySetResult();
			_stopGate.Wait(TimeSpan.FromSeconds(5L));
		}

		public override void Stop()
		{
			StopCount++;
			_stopGate.Set();
		}

		public override void Dispose()
		{
			DisposeCount++;
		}
	}

	private sealed class StaticControllerModeSwitcher(OperationDebugControllerModeResult result) : IOperationDebugControllerModeSwitcher
	{
		public OperationDebugControllerModeResult CheckAndApply()
		{
			return result;
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

	[Fact]
	public void Config_LoadsPythonCompatibleDefaultsAndSnakeCaseYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "operation_debug.yml"), "operation_template: \"测试模板\"\nrepeat_enabled: false");
			OperationDebugConfig operationDebugConfig = OperationDebugConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("operation_debug", operationDebugConfig.AppId);
			Assert.Equal("测试模板", operationDebugConfig.OperationTemplate);
			Assert.False(operationDebugConfig.RepeatEnabled);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void TemplateLoader_LoadsAndExpandsOperationTemplates()
	{
		string text = CreateTempRoot();
		try
		{
			WriteOperationTemplate(text);
			OperationDebugTemplateLoader operationDebugTemplateLoader = new OperationDebugTemplateLoader(new OneDragonEnvironment(text));
			IReadOnlyList<OperationDef> readOnlyList = operationDebugTemplateLoader.LoadOperations("主模板");
			Assert.Equal<string>((IEnumerable<string>?)new string[3] { "按键-普通攻击", "等待秒数", "设置状态" }, readOnlyList.Select((OperationDef operation) => operation.OpName));
			Assert.Equal("调试-完成", readOnlyList[2].State);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RunsLoadedAtomicOpsOnceWhenRepeatDisabled()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteOperationTemplate(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingAtomicOpFactory factory = new RecordingAtomicOpFactory();
			OperationDebugService service = new OperationDebugService(controllerModeSwitcher: new StaticControllerModeSwitcher(new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄")), config: new OperationDebugConfig
			{
				OperationTemplate = "主模板",
				RepeatEnabled = false
			}, templateLoader: new OperationDebugTemplateLoader(context.Environment), atomicOpFactory: factory);
			OperationDebugOperation operation = new OperationDebugOperation(context, service);
			Assert.True((await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal(new string[3] { "按键-普通攻击", "等待秒数", "设置状态" }, factory.Created.Select((RecordingAtomicOp recordingAtomicOp) => recordingAtomicOp.OpName));
			Assert.All(factory.Created, delegate(RecordingAtomicOp recordingAtomicOp)
			{
				Assert.Equal(1, recordingAtomicOp.ExecuteCount);
			});
			Assert.Equal(3, service.OperationIndex);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_FailsWhenControllerModeCheckFails()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			OperationDebugService service = new OperationDebugService(new OperationDebugConfig(), new OperationDebugTemplateLoader(context.Environment), new RecordingAtomicOpFactory(), new StaticControllerModeSwitcher(new OperationDebugControllerModeResult(IsSuccess: false, "未安装虚拟手柄依赖")));
			OperationDebugOperation operation = new OperationDebugOperation(context, service);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未安装虚拟手柄依赖", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Service_RepeatsFromFirstOperationWhenEnabled()
	{
		string text = CreateTempRoot();
		try
		{
			WriteOperationTemplate(text);
			RecordingAtomicOpFactory recordingAtomicOpFactory = new RecordingAtomicOpFactory();
			OperationDebugService operationDebugService = new OperationDebugService(new OperationDebugConfig
			{
				OperationTemplate = "主模板",
				RepeatEnabled = true
			}, new OperationDebugTemplateLoader(new OneDragonEnvironment(text)), recordingAtomicOpFactory, new StaticControllerModeSwitcher(new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄")));
			Assert.True(operationDebugService.LoadOperations().IsSuccess);
			operationDebugService.RunNextOperation();
			operationDebugService.RunNextOperation();
			OperationDebugStepResult operationDebugStepResult = operationDebugService.RunNextOperation();
			Assert.True(operationDebugStepResult.IsSuccess);
			Assert.False(operationDebugStepResult.Completed);
			Assert.Equal(0, operationDebugService.OperationIndex);
			OperationDebugStepResult operationDebugStepResult2 = operationDebugService.RunNextOperation();
			Assert.True(operationDebugStepResult2.IsSuccess);
			Assert.Equal(1, operationDebugService.OperationIndex);
			Assert.Equal(2, recordingAtomicOpFactory.Created[0].ExecuteCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_WaitsWithoutAnExtraDelayBetweenPythonAtomicOperations()
	{
		string text = CreateTempRoot();
		try
		{
			WriteOperationTemplate(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			OperationDebugService operationDebugService = new OperationDebugService(new OperationDebugConfig
			{
				OperationTemplate = "主模板"
			}, new OperationDebugTemplateLoader(zContext.Environment), new RecordingAtomicOpFactory(), new StaticControllerModeSwitcher(new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄")));
			OperationDebugOperation operationDebugOperation = new OperationDebugOperation(zContext, operationDebugService);
			Assert.True(operationDebugService.LoadOperations().IsSuccess);
			OperationRoundResult operationRoundResult = operationDebugOperation.RunOperations();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Null(operationRoundResult.Delay);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ControllerModeSwitcher_UsesInjectedVirtualGamepadDependencyCheck()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.BattleAssistantConfig.ControlMethod = "xbox";
			int checkCount = 0;
			ZContextOperationDebugControllerModeSwitcher zContextOperationDebugControllerModeSwitcher = new ZContextOperationDebugControllerModeSwitcher(zContext, delegate
			{
				checkCount++;
				return false;
			});
			OperationDebugControllerModeResult operationDebugControllerModeResult = zContextOperationDebugControllerModeSwitcher.CheckAndApply();
			Assert.False(operationDebugControllerModeResult.IsSuccess);
			Assert.Equal("未安装虚拟手柄依赖", operationDebugControllerModeResult.Status);
			Assert.Equal(1, checkCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ControllerModeSwitcher_AppliesPythonGamepadKeyPressTime()
	{
		string text = CreateTempRoot();
		try
		{
			RecordingConfigurableGamepadController recordingConfigurableGamepadController = new RecordingConfigurableGamepadController();
			string text2 = Path.Combine(text, "config", "00");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "game.yml"), "ds4_key_press_time: 0.075\n");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.AttachController(new ZPcController(new GameConfig(), null, 1920, 1080, null, null, null, null, null, recordingConfigurableGamepadController, skipForegroundActivation: true));
			zContext.BattleAssistantConfig.ControlMethod = "ds4";
			ZContextOperationDebugControllerModeSwitcher zContextOperationDebugControllerModeSwitcher = new ZContextOperationDebugControllerModeSwitcher(zContext, () => true);
			OperationDebugControllerModeResult operationDebugControllerModeResult = zContextOperationDebugControllerModeSwitcher.CheckAndApply();
			Assert.True(operationDebugControllerModeResult.IsSuccess);
			Assert.Equal(TimeSpan.FromSeconds(0.07500000298023224), recordingConfigurableGamepadController.KeyPressTime);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			OperationDebugAppFactory operationDebugAppFactory = zContext.ApplicationFactoryRegistry.CreateOperationDebugFactory();
			IApplication application = operationDebugAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = operationDebugAppFactory.GetConfig(0, "one_dragon");
			Assert.Equal("operation_debug", operationDebugAppFactory.AppId);
			Assert.Equal("指令调试", operationDebugAppFactory.AppName);
			Assert.Equal("one_dragon", operationDebugAppFactory.GroupId);
			Assert.False(operationDebugAppFactory.NeedNotify);
			Assert.IsType<OperationDebugApp>(application);
			Assert.IsType<OperationDebugConfig>(config);
			Assert.IsType<ZApplicationRunRecord>(operationDebugAppFactory.GetRunRecord(0));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersOperationDebugAsNonDefaultDevtool()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterOperationDebugApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("operation_debug"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("operation_debug"));
			Assert.DoesNotContain("operation_debug", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task OperationDebugApp_RunsInjectedServiceAndUpdatesRunRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteOperationTemplate(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			OperationDebugConfig config = new OperationDebugConfig
			{
				OperationTemplate = "主模板",
				RepeatEnabled = false
			};
			RecordingAtomicOpFactory factory = new RecordingAtomicOpFactory();
			OperationDebugService service = new OperationDebugService(config, new OperationDebugTemplateLoader(context.Environment), factory, new StaticControllerModeSwitcher(new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄")));
			ZApplicationRunRecord runRecord = new ZApplicationRunRecord("operation_debug");
			OperationDebugApp app = new OperationDebugApp(context, config, runRecord, service);
			Assert.True((await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal(1, runRecord.RunStatus);
			Assert.Equal(new string[3] { "按键-普通攻击", "等待秒数", "设置状态" }, factory.Created.Select((RecordingAtomicOp operation) => operation.OpName));
			Assert.All(factory.Created, delegate(RecordingAtomicOp operation)
			{
				Assert.Equal(1, operation.ExecuteCount);
			});
			Assert.All(factory.Created, delegate(RecordingAtomicOp operation)
			{
				Assert.Equal(1, operation.StopCount);
			});
			Assert.All(factory.Created, delegate(RecordingAtomicOp operation)
			{
				Assert.Equal(1, operation.DisposeCount);
			});
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task OperationDebugApp_CancellationStopsAndDisposesRunningAtomicOp()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteSingleOperationTemplate(rootDirectory, "阻塞指令");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			BlockingAtomicOpFactory factory = new BlockingAtomicOpFactory();
			OperationDebugService service = new OperationDebugService(new OperationDebugConfig
			{
				OperationTemplate = "单条模板"
			}, new OperationDebugTemplateLoader(context.Environment), factory, new StaticControllerModeSwitcher(new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄")));
			OperationDebugApp app = new OperationDebugApp(context, new OperationDebugConfig
			{
				OperationTemplate = "单条模板"
			}, null, service);
			CancellationTokenSource cancellation = new CancellationTokenSource();
			try
			{
				Task<OperationResult> execution = Task.Run(() => app.ExecuteAsync(cancellation.Token));
				await factory.Operation.Started.Task.WaitAsync(TimeSpan.FromSeconds(2L));
				cancellation.Cancel();
				await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
				Assert.Equal(1, factory.Operation.StopCount);
				Assert.Equal(1, factory.Operation.DisposeCount);
				Assert.Empty(service.Operations);
			}
			finally
			{
				if (cancellation != null)
				{
					((IDisposable)cancellation).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task OperationDebugApp_OnStopDisposesLoadedAtomicOps()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteOperationTemplate(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingAtomicOpFactory factory = new RecordingAtomicOpFactory();
			OperationDebugService service = new OperationDebugService(new OperationDebugConfig
			{
				OperationTemplate = "主模板"
			}, new OperationDebugTemplateLoader(context.Environment), factory, new StaticControllerModeSwitcher(new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄")));
			OperationDebugApp app = new OperationDebugApp(context, new OperationDebugConfig
			{
				OperationTemplate = "主模板"
			}, null, service);
			Assert.True(service.LoadOperations().IsSuccess);
			await app.OnStopAsync(CancellationToken.None);
			Assert.All(factory.Created, delegate(RecordingAtomicOp operation)
			{
				Assert.Equal(1, operation.StopCount);
			});
			Assert.All(factory.Created, delegate(RecordingAtomicOp operation)
			{
				Assert.Equal(1, operation.DisposeCount);
			});
			Assert.Empty(service.Operations);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteOperationTemplate(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle_operation");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "主模板.yml"), "operations:\n  - op_name: \"按键-普通攻击\"\n  - operation_template: \"子模板\"");
		File.WriteAllText(Path.Combine(text, "子模板.yml"), "operations:\n  - op_name: \"等待秒数\"\n    seconds: 0\n  - op_name: \"设置状态\"\n    state: \"调试-完成\"");
	}

	private static void WriteSingleOperationTemplate(string rootDirectory, string operationName)
	{
		string text = Path.Combine(rootDirectory, "config", "auto_battle_operation");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "单条模板.yml"), "operations:\n  - op_name: \"" + operationName + "\"");
	}
}
