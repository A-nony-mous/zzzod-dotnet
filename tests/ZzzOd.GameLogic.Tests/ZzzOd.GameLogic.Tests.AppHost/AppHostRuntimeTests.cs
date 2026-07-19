using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Context;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// AppHost 运行时测试。
/// </summary>
public sealed class AppHostRuntimeTests
{
	private sealed class BackendHarness : IDisposable
	{
		public string RunRoot { get; }

		public ZzzRuntimeManager Runtime { get; }

		public ZzzBattleAssistantRuntimeSource BattleAssistantRuntimeSource { get; }

		public ZzzAppBackend Backend { get; }

		public TestApplication Application { get; }

		private BackendHarness(string runRoot, ZzzRuntimeManager runtime, ZzzBattleAssistantRuntimeSource battleAssistantRuntimeSource, ZzzAppBackend backend, TestApplication application)
		{
			RunRoot = runRoot;
			Runtime = runtime;
			BattleAssistantRuntimeSource = battleAssistantRuntimeSource;
			Backend = backend;
			Application = application;
		}

		public static BackendHarness Create(bool failOnCreate = false, bool ignoreCancellation = false)
		{
			string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-apphost-tests", Guid.NewGuid().ToString("N"));
			CreateRequiredAssets(runRoot);
			TestApplication application = new TestApplication(ignoreCancellation);
			ZzzRuntimeManager runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance, (int instanceIndex) => CreateContext(runRoot, instanceIndex, new TestApplicationFactory(application, failOnCreate)));
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			ZzzBattleAssistantRuntimeSource battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			ZzzAppBackend backend = new ZzzAppBackend(runtime, eventBus, battleAssistantRuntimeSource, new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(runRoot), eventBus), new ZzzHostModeOptions(ZzzHostMode.ApiOnly), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
			return new BackendHarness(runRoot, runtime, battleAssistantRuntimeSource, backend, application);
		}

		public void Dispose()
		{
			Runtime.Dispose();
			BattleAssistantRuntimeSource.Dispose();
		}
	}

	private sealed class TestApplication : IApplication
	{
		private readonly bool _ignoreCancellation;

		private readonly TaskCompletionSource _allowExit = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public TestApplication(bool ignoreCancellation = false)
		{
			_ignoreCancellation = ignoreCancellation;
		}

		public TaskCompletionSource Started { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public string AppId => "test-app";

		public async Task<OperationResult> ExecuteAsync(CancellationToken cancellationToken)
		{
			Started.TrySetResult();
			if (_ignoreCancellation)
			{
				await _allowExit.Task.ConfigureAwait(false);
				return new OperationResult(IsSuccess: false, "迟到退出");
			}

			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
			return new OperationResult(IsSuccess: true, "完成");
		}

		public void AllowExit() => _allowExit.TrySetResult();

		public Task OnPauseAsync(CancellationToken cancellationToken)
		{
			PauseCount++;
			return Task.CompletedTask;
		}

		public Task OnResumeAsync(CancellationToken cancellationToken)
		{
			ResumeCount++;
			return Task.CompletedTask;
		}

		public Task OnStopAsync(CancellationToken cancellationToken)
		{
			StopCount++;
			return Task.CompletedTask;
		}
	}

	private sealed class TestApplicationFactory : IApplicationFactory
	{
		private readonly TestApplication _application;

		private readonly bool _failOnCreate;

		public string AppId => "test-app";

		public string AppName => "测试应用";

		public bool NeedNotify => false;

		public TestApplicationFactory(TestApplication application, bool failOnCreate = false)
		{
			_application = application;
			_failOnCreate = failOnCreate;
		}

		public IApplication CreateApplication(int instanceIndex, string groupId)
		{
			if (!_failOnCreate)
			{
				return _application;
			}
			throw new InvalidOperationException("测试应用工厂异常");
		}

		public IApplicationConfig GetConfig(int instanceIndex, string groupId)
		{
			return new TestApplicationConfig();
		}

		public IApplicationRunRecord GetRunRecord(int instanceIndex)
		{
			return new TestApplicationRunRecord();
		}
	}

	private sealed class TestApplicationConfig : IApplicationConfig
	{
	}

	private sealed class TestApplicationRunRecord : IApplicationRunRecord
	{
		public void CheckAndUpdateStatus()
		{
		}
	}

	/// <summary>
	/// 宿主日志使用无 BOM UTF-8 保存中文。
	/// </summary>
	[Fact]
	public void LogFanOutProviderWritesChineseAsUtf8WithoutBom()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-apphost-tests", Guid.NewGuid().ToString("N"));
		ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
		using ZzzLogFanOutLoggerProvider zzzLogFanOutLoggerProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(text), eventBus);
		ILogger logger = zzzLogFanOutLoggerProvider.CreateLogger("encoding-test");
		logger.LogInformation("迷失之地战斗结束");
		string path = Path.Combine(text, ".log", "zzz-app-host.log");
		byte[] array = File.ReadAllBytes(path);
		Assert.False(((ReadOnlySpan<byte>)array.AsSpan()).StartsWith(Encoding.UTF8.Preamble));
		Assert.Contains("迷失之地战斗结束", Encoding.UTF8.GetString(array), StringComparison.Ordinal);
	}

	/// <summary>
	/// 显式参数优先于环境变量和应用目录。
	/// </summary>
	[Fact]
	public void RunRootResolverPrefersCommandLineArgument()
	{
		string applicationBaseDirectory = Path.Combine(Path.GetTempPath(), "zzzod-run-root-app");
		string text = Path.Combine(Path.GetTempPath(), "zzzod-run-root-command");
		string environmentRunRoot = Path.Combine(Path.GetTempPath(), "zzzod-run-root-environment");
		ZzzRunRootResolution zzzRunRootResolution = ZzzRunRootResolver.Resolve(new string[4] { "--urls", "http://127.0.0.1:0", "--run-root", text }, environmentRunRoot, applicationBaseDirectory);
		Assert.Equal(Path.GetFullPath(text), zzzRunRootResolution.RunRoot.Path);
		Assert.Equal(ZzzRunRootSource.CommandLine, zzzRunRootResolution.Source);
	}

	/// <summary>
	/// 等号形式的显式参数使用应用目录解析相对路径。
	/// </summary>
	[Fact]
	public void RunRootResolverSupportsEqualsArgumentWithoutUsingCurrentDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-run-root-app");
		ZzzRunRootResolution zzzRunRootResolution = ZzzRunRootResolver.Resolve(new string[] { "--run-root=workspace" }, null, text);
		Assert.Equal(Path.GetFullPath("workspace", text), zzzRunRootResolution.RunRoot.Path);
		Assert.Equal(ZzzRunRootSource.CommandLine, zzzRunRootResolution.Source);
	}

	/// <summary>
	/// 未提供显式参数时使用外部环境变量。
	/// </summary>
	[Fact]
	public void RunRootResolverUsesEnvironmentValue()
	{
		string applicationBaseDirectory = Path.Combine(Path.GetTempPath(), "zzzod-run-root-app");
		string text = Path.Combine(Path.GetTempPath(), "zzzod-run-root-environment");
		ZzzRunRootResolution zzzRunRootResolution = ZzzRunRootResolver.Resolve(Array.Empty<string>(), text, applicationBaseDirectory);
		Assert.Equal(Path.GetFullPath(text), zzzRunRootResolution.RunRoot.Path);
		Assert.Equal(ZzzRunRootSource.Environment, zzzRunRootResolution.Source);
	}

	/// <summary>
	/// 无外部配置时使用应用目录，不读取当前工作目录。
	/// </summary>
	[Fact]
	public void RunRootResolverFallsBackToApplicationDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-run-root-app");
		ZzzRunRootResolution zzzRunRootResolution = ZzzRunRootResolver.Resolve(Array.Empty<string>(), null, text);
		Assert.Equal(Path.GetFullPath(text), zzzRunRootResolution.RunRoot.Path);
		Assert.Equal(ZzzRunRootSource.ApplicationBaseDirectory, zzzRunRootResolution.Source);
	}

	/// <summary>
	/// 缺少路径值的显式参数应立即报错。
	/// </summary>
	[Fact]
	public void RunRootResolverRejectsMissingArgumentValue()
	{
		Assert.Throws<ArgumentException>(() => ZzzRunRootResolver.Resolve(new string[] { "--run-root" }, null, AppContext.BaseDirectory));
	}

	/// <summary>
	/// token 校验应拒绝长度不同的输入。
	/// </summary>
	[Fact]
	public void ApiTokenValidationRejectsDifferentLengthToken()
	{
		ZzzApiOptions zzzApiOptions = new ZzzApiOptions
		{
			Token = "abcdef"
		};
		Assert.False(zzzApiOptions.IsTokenValid("abc"));
		Assert.False(zzzApiOptions.IsTokenValid("abcdef0"));
		Assert.True(zzzApiOptions.IsTokenValid("abcdef"));
	}

	/// <summary>
	/// token 可以重置并保存到配置文件。
	/// </summary>
	[Fact]
	public void ApiTokenCanBeResetAndPersisted()
	{
		string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-apphost-tests", Guid.NewGuid().ToString("N"));
		ZzzApiOptions zzzApiOptions = new ZzzApiOptions
		{
			Token = "abcdef"
		};
		string text = zzzApiOptions.ResetToken(runRoot);
		ZzzApiOptions zzzApiOptions2 = ZzzApiOptions.LoadOrCreate(runRoot);
		Assert.NotEqual("abcdef", text);
		Assert.Equal(text, zzzApiOptions2.Token);
	}

	/// <summary>
	/// 同一运行根目录只能有一个运行锁持有者。
	/// </summary>
	[Fact]
	public void RuntimeLockAllowsOnlyOneOwnerPerRunRoot()
	{
		string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-apphost-tests", Guid.NewGuid().ToString("N"));
		using ZzzRuntimeLock zzzRuntimeLock = ZzzRuntimeLock.TryAcquire(runRoot);
		using ZzzRuntimeLock zzzRuntimeLock2 = ZzzRuntimeLock.TryAcquire(runRoot);
		Assert.NotNull(zzzRuntimeLock);
		Assert.Null(zzzRuntimeLock2);
	}

	/// <summary>
	/// 事件总线完成后应结束订阅。
	/// </summary>
	[Fact]
	public async Task EventBusCompletesSubscribers()
	{
		ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
		ChannelReader<ZzzBackendEvent> reader = eventBus.Subscribe();
		eventBus.Publish("test", new
		{
			Ok = true
		});
		Assert.True(await reader.WaitToReadAsync());
		Assert.True(reader.TryRead(out ZzzBackendEvent item));
		Assert.Equal("test", item.Type);
		eventBus.Complete();
		Assert.False(await reader.WaitToReadAsync());
	}

	/// <summary>
	/// 运行状态映射应优先反映运行和暂停状态。
	/// </summary>
	[Fact]
	public void RunStateMapperUsesContextStateWhenActive()
	{
		Assert.Equal(ZzzRunState.Running, ZzzRunStateMapper.Map(ApplicationRunContextState.Running, ZzzRunState.Succeeded));
		Assert.Equal(ZzzRunState.Paused, ZzzRunStateMapper.Map(ApplicationRunContextState.Pause, ZzzRunState.Failed));
	}

	/// <summary>
	/// 运行状态映射应在空闲或停止时保留终态。
	/// </summary>
	[Fact]
	public void RunStateMapperKeepsTerminalStateWhenContextIsInactive()
	{
		Assert.Equal(ZzzRunState.Idle, ZzzRunStateMapper.Map(null, ZzzRunState.Idle));
		Assert.Equal(ZzzRunState.Stopping, ZzzRunStateMapper.Map(ApplicationRunContextState.Stop, ZzzRunState.Stopping));
		Assert.Equal(ZzzRunState.Cancelled, ZzzRunStateMapper.Map(ApplicationRunContextState.Stop, ZzzRunState.Cancelled));
	}

	/// <summary>
	/// GUI 和 API 解析的业务门面必须是同一个 AppHost 单例。
	/// </summary>
	[Fact]
	public void AppHostDependencyInjectionUsesOneSharedBackendInstance()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-apphost-di-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		try
		{
			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddZzzAppHost(text, ZzzHostMode.Gui);
			using ServiceProvider provider = services.BuildServiceProvider();
			IZzzAppBackend requiredService = provider.GetRequiredService<IZzzAppBackend>();
			IZzzAppBackend requiredService2 = provider.GetRequiredService<IZzzAppBackend>();
			Assert.IsType<ZzzAppBackend>(requiredService);
			Assert.Same(requiredService, requiredService2);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// 一条龙运行意图启动失败时，运行面显示真实 AppHost 错误。
	/// </summary>
	[Fact]
	public void RunPanelShowsRealBackendStartFailureFromRunIntent()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-run-panel-backend-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		try
		{
			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddZzzAppHost(text, ZzzHostMode.Gui);
			using ServiceProvider provider = services.BuildServiceProvider();
			IZzzAppBackend backend = provider.GetRequiredService<IZzzAppBackend>();
			ZzzGuiRunIntentService runIntent = new ZzzGuiRunIntentService();
			runIntent.RequestStartOneDragon();
			GuiParityAndFacadeTests.RunOnUiThread(delegate
			{
				ZzzRunPanel zzzRunPanel = new ZzzRunPanel(backend, "one_dragon", null, runIntent, null, "default");
				zzzRunPanel.OnPageShown();
				FAInfoBar infoBar = zzzRunPanel.FindControl<FAInfoBar>("RunErrorBar");
				Assert.True(infoBar.IsOpen);
				Assert.False(string.IsNullOrWhiteSpace(infoBar.Message));
				Assert.NotEqual("运行状态读取失败。", infoBar.Message);
				zzzRunPanel.DisposePage();
			});
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// 运行状态和战斗助手状态不能用本地空值掩盖尚未创建的运行上下文。
	/// </summary>
	[Fact]
	public void BackendReportsNotReadyBeforeContextExists()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<ZzzRunStatusDto> currentRun = backendHarness.Backend.GetCurrentRun();
		ZzzBackendResult<ZzzBattleAssistantRuntimeDto> battleAssistantRuntime = backendHarness.Backend.GetBattleAssistantRuntime();
		Assert.False(currentRun.Success);
		Assert.Equal(ZzzBackendErrorCode.NotReady, currentRun.ErrorCode);
		Assert.False(battleAssistantRuntime.Success);
		Assert.Equal(ZzzBackendErrorCode.NotReady, battleAssistantRuntime.ErrorCode);
	}

	/// <summary>
	/// 应用工厂异常会进入同一运行终态和错误事件流，不能让启动请求永久停留在启动中。
	/// </summary>
	[Fact]
	public async Task BackendPublishesFailureWhenApplicationFactoryThrows()
	{
		using BackendHarness harness = BackendHarness.Create(failOnCreate: true);
		ChannelReader<ZzzBackendEvent> events = harness.Backend.SubscribeEvents();
		ZzzBackendResult<ZzzRunStatusDto> started = await harness.Backend.StartRunAsync(new ZzzStartRunRequest("test-app"));
		ZzzBackendEvent error = await ReadEventAsync(events, "error.raised");
		ZzzBackendResult<ZzzRunStatusDto> current = harness.Backend.GetCurrentRun();
		Assert.True(started.Success);
		Assert.Equal("error.raised", error.Type);
		Assert.True(current.Success);
		Assert.Equal(ZzzRunState.Failed, current.Value.State);
		Assert.Equal("执行异常", current.Value.LastStatus);
		Assert.Contains("测试应用工厂异常", current.Value.Error, StringComparison.Ordinal);
		harness.Backend.UnsubscribeEvents(events);
	}

	/// <summary>
	/// 后端服务应能暂停、恢复并停止当前运行。
	/// </summary>
	[Fact]
	public async Task BackendCanPauseResumeAndStopCurrentRun()
	{
		using BackendHarness harness = BackendHarness.Create();
		ZzzBackendResult<ZzzRunStatusDto> started = await harness.Backend.StartRunAsync(new ZzzStartRunRequest("test-app"));
		await harness.Application.Started.Task.WaitAsync(TimeSpan.FromSeconds(3L));
		ZzzBackendResult<ZzzRunStatusDto> paused = harness.Backend.PauseRun();
		ZzzBackendResult<ZzzRunStatusDto> resumed = harness.Backend.ResumeRun();
		ZzzBackendResult<ZzzRunStatusDto> stopped = await harness.Backend.StopRunAsync();
		Assert.True(started.Success);
		Assert.True(paused.Success);
		Assert.Equal(ZzzRunState.Paused, paused.Value.State);
		Assert.Equal(1, harness.Application.PauseCount);
		Assert.True(resumed.Success);
		Assert.Equal(ZzzRunState.Running, resumed.Value.State);
		Assert.Equal(1, harness.Application.ResumeCount);
		Assert.True(stopped.Success);
		Assert.Equal(ZzzRunState.Cancelled, stopped.Value.State);
		Assert.Equal(0, harness.Application.StopCount);
	}

	/// <summary>
	/// 停止已返回 Cancelled 后，仍在退出的旧 application 不得改写终态。
	/// </summary>
	[Fact]
	public async Task BackendKeepsCancelledWhenOldApplicationExitsLate()
	{
		using BackendHarness harness = BackendHarness.Create(ignoreCancellation: true);
		ZzzBackendResult<ZzzRunStatusDto> started = await harness.Backend.StartRunAsync(new ZzzStartRunRequest("test-app"));
		await harness.Application.Started.Task.WaitAsync(TimeSpan.FromSeconds(3L));

		ZzzBackendResult<ZzzRunStatusDto> stopped = await harness.Backend.StopRunAsync();
		Assert.True(started.Success);
		Assert.Equal(ZzzRunState.Cancelled, stopped.Value.State);

		harness.Application.AllowExit();
		await Task.Delay(50);

		ZzzBackendResult<ZzzRunStatusDto> current = harness.Backend.GetCurrentRun();
		Assert.True(current.Success);
		Assert.Equal(ZzzRunState.Cancelled, current.Value.State);
	}

	/// <summary>
	/// 后端服务应拒绝运行中实例切换，并允许空闲实例切换。
	/// </summary>
	[Fact]
	public async Task BackendInstanceSwitchAllowsIdleAndRejectsRunning()
	{
		using BackendHarness harness = BackendHarness.Create();
		ChannelReader<ZzzBackendEvent> events = harness.Backend.SubscribeEvents();
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> idleSwitch = harness.Backend.ActivateInstance(1);
		ZzzBackendEvent instanceEvent = await ReadEventAsync(events, "instance.activeChanged");
		ZzzBackendResult<ZzzRunStatusDto> started = await harness.Backend.StartRunAsync(new ZzzStartRunRequest("test-app"));
		await harness.Application.Started.Task.WaitAsync(TimeSpan.FromSeconds(3L));
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> runningSwitch = harness.Backend.ActivateInstance(0);
		await harness.Backend.StopRunAsync();
		Assert.True(idleSwitch.Success);
		Assert.Contains((IEnumerable<ZzzInstanceDto>)idleSwitch.Value, (Predicate<ZzzInstanceDto>)((ZzzInstanceDto instance) => instance.Index == 1 && instance.Active));
		Assert.Equal("instance.activeChanged", instanceEvent.Type);
		Assert.True(started.Success);
		Assert.False(runningSwitch.Success);
		Assert.Equal(ZzzBackendErrorCode.Conflict, runningSwitch.ErrorCode);
		harness.Backend.UnsubscribeEvents(events);
	}

	/// <summary>
	/// 实例列表只读取 one_dragon.yml，不从孤立配置目录生成默认账户。
	/// </summary>
	[Fact]
	public void BackendInstanceListIgnoresOrphanConfigDirectories()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		Directory.CreateDirectory(Path.Combine(backendHarness.RunRoot, "config", "09"));
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> instances = backendHarness.Backend.GetInstances();
		Assert.True(instances.Success);
		IReadOnlyList<ZzzInstanceDto> readOnlyList = Assert.IsAssignableFrom<IReadOnlyList<ZzzInstanceDto>>(instances.Value);
		Assert.Equal(new int[2] { 0, 1 }, readOnlyList.Select((ZzzInstanceDto instance) => instance.Index));
		Assert.DoesNotContain((IEnumerable<ZzzInstanceDto>)readOnlyList, (Predicate<ZzzInstanceDto>)((ZzzInstanceDto instance) => instance.Index == 9));
	}

	/// <summary>
	/// 运行期间实例新增、编辑、登录和删除都由后端统一拒绝。
	/// </summary>
	[Fact]
	public async Task BackendInstanceMutationsRejectRunningState()
	{
		using BackendHarness harness = BackendHarness.Create();
		ZzzBackendResult<ZzzRunStatusDto> started = await harness.Backend.StartRunAsync(new ZzzStartRunRequest("test-app"));
		await harness.Application.Started.Task.WaitAsync(TimeSpan.FromSeconds(3L));
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> created = harness.Backend.CreateInstance();
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> updated = harness.Backend.UpdateInstance(new ZzzUpdateInstanceRequest(1, "运行中修改"));
		ZzzBackendResult<ZzzRunStatusDto> login = harness.Backend.LoginInstance(1);
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> deleted = harness.Backend.DeleteInstance(1);
		await harness.Backend.StopRunAsync();
		Assert.True(started.Success);
		Assert.All(new ZzzBackendErrorCode?[4] { created.ErrorCode, updated.ErrorCode, login.ErrorCode, deleted.ErrorCode }, delegate(ZzzBackendErrorCode? code)
		{
			Assert.Equal(ZzzBackendErrorCode.Conflict, code);
		});
	}

	/// <summary>
	/// 账户窗口标题和区服变化后应按 BaselineParity 语义重建当前实例 controller。
	/// </summary>
	[Fact]
	public void BackendAccountWindowSettingsReinitializeCurrentController()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZContext zContext = backendHarness.Runtime.EnsureContext();
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("instance", new Dictionary<string, object> { ["game_region"] = "cn_b" }, 0));
		object controller = zContext.Controller;
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("instance", new Dictionary<string, object>
		{
			["use_custom_win_title"] = true,
			["custom_win_title"] = "绝区零测试窗口"
		}, 0));
		Assert.True(zzzBackendResult.Success);
		Assert.NotNull(controller);
		Assert.True(zzzBackendResult2.Success);
		Assert.NotNull(zContext.Controller);
		Assert.NotSame(controller, zContext.Controller);
	}

	/// <summary>
	/// 保存其他实例的账号配置不能重建当前实例 controller。
	/// </summary>
	[Fact]
	public void BackendOtherInstanceAccountSaveDoesNotReinitializeCurrentController()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZContext zContext = backendHarness.Runtime.EnsureContext();
		zContext.InitController();
		object controller = zContext.Controller;
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("instance", new Dictionary<string, object> { ["game_region"] = "asia" }, 1));
		Assert.True(zzzBackendResult.Success);
		Assert.Same(controller, zContext.Controller);
	}

	/// <summary>
	/// OCR GPU 配置保存后应立即更新当前 matcher，匹配 BaselineParity init_ocr 的运行期效果。
	/// </summary>
	[Fact]
	public void BackendOcrGpuSaveReconfiguresCurrentMatcher()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZContext zContext = backendHarness.Runtime.EnsureContext();
		Assert.False(zContext.OcrService.Matcher.IsUseGpu());
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("model", new Dictionary<string, object> { ["ocr_use_gpu"] = true }));
		Assert.True(zzzBackendResult.Success);
		Assert.True(zContext.ModelConfig.OcrUseGpu);
		Assert.True(zContext.OcrService.Matcher.IsUseGpu());
	}

	/// <summary>
	/// 删除当前实例应同时删除 YAML 元数据和实例目录，保持 BaselineParity 允许删除当前实例的行为。
	/// </summary>
	[Fact]
	public void BackendDeleteCurrentInstanceRemovesYamlEntryAndDirectory()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		string text = Path.Combine(backendHarness.RunRoot, "config", "00");
		File.WriteAllText(Path.Combine(text, "game_account.yml"), "game_region: cn\n");
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> zzzBackendResult = backendHarness.Backend.DeleteInstance(0);
		string actualString = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "one_dragon.yml"));
		Assert.True(zzzBackendResult.Success);
		Assert.DoesNotContain((IEnumerable<ZzzInstanceDto>)zzzBackendResult.Value, (Predicate<ZzzInstanceDto>)((ZzzInstanceDto instance) => instance.Index == 0));
		Assert.DoesNotContain("idx: 0", actualString, StringComparison.Ordinal);
		Assert.False(Directory.Exists(text));
	}

	/// <summary>
	/// 仅剩一个实例时应拒绝删除。
	/// </summary>
	[Fact]
	public void BackendRejectsDeletingLastInstance()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		Assert.True(backendHarness.Backend.DeleteInstance(1).Success);
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> zzzBackendResult = backendHarness.Backend.DeleteInstance(0);
		Assert.False(zzzBackendResult.Success);
		Assert.Equal(ZzzBackendErrorCode.Conflict, zzzBackendResult.ErrorCode);
		Assert.True(Directory.Exists(Path.Combine(backendHarness.RunRoot, "config", "00")));
	}

	/// <summary>
	/// 同值实例更新不应触发实例变更事件。
	/// </summary>
	[Fact]
	public void BackendSameInstanceUpdateDoesNotPublishEvents()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ChannelReader<ZzzBackendEvent> events = backendHarness.Backend.SubscribeEvents();
		ZzzInstanceDto current = backendHarness.Backend.GetCurrentInstance().Value!;
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = backendHarness.Backend.UpdateInstance(
			new ZzzUpdateInstanceRequest(current.Index, current.Name, current.ActiveInOneDragon));

		Assert.True(result.Success);
		Assert.False(events.TryRead(out _));
		backendHarness.Backend.UnsubscribeEvents(events);
	}

	/// <summary>
	/// BaselineParity 当前没有配置登录操作，空闲调用应返回同义状态。
	/// </summary>
	[Fact]
	public void BackendIdleLoginReportsUnconfiguredOperation()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<ZzzRunStatusDto> zzzBackendResult = backendHarness.Backend.LoginInstance(1);
		Assert.False(zzzBackendResult.Success);
		Assert.Equal(ZzzBackendErrorCode.NotReady, zzzBackendResult.ErrorCode);
		Assert.Equal("当前未配置登录操作。", zzzBackendResult.Error);
	}

	private static ZContext CreateContext(string runRoot, int instanceIndex, IApplicationFactory factory)
	{
		ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot), null, instanceIndex);
		zContext.RunContext.RegisterApplication(factory, defaultGroup: true);
		return zContext;
	}

	private static void CreateRequiredAssets(string runRoot)
	{
		Directory.CreateDirectory(Path.Combine(runRoot, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(runRoot, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(runRoot, "assets", "game_data", "screen_info"));
		Directory.CreateDirectory(Path.Combine(runRoot, "config", "00"));
		Directory.CreateDirectory(Path.Combine(runRoot, "config", "01"));
		File.WriteAllText(Path.Combine(runRoot, "config", "one_dragon.yml"), "instance_list:\n- idx: 0\n  name: '00'\n  active: true\n  active_in_od: true\n- idx: 1\n  name: '01'\n  active: false\n  active_in_od: true");
	}

	private static async Task<ZzzBackendEvent> ReadEventAsync(ChannelReader<ZzzBackendEvent> reader, string type)
	{
		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3L));
		await foreach (ZzzBackendEvent item in reader.ReadAllAsync(timeout.Token))
		{
			if (item.Type == type)
			{
				return item;
			}
		}
		throw new InvalidOperationException("未收到事件 " + type + "。");
	}
}
