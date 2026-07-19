using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Abstractions.Runtime;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class ZApplicationRunContextTests
{
	private sealed class TestZApplicationFactory : IApplicationFactory
	{
		private readonly ZContext _context;

		private readonly RecordingRunRecord _runRecord;

		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		private readonly Action<TestZApplication>? _onCreated;

		public string AppId { get; }

		public string AppName { get; }

		public bool NeedNotify { get; }

		public TestZApplicationFactory(ZContext context, string appId, string appName, bool needNotify, RecordingRunRecord runRecord, Func<CancellationToken, Task<OperationResult>> executeAsync, Action<TestZApplication>? onCreated = null)
		{
			_context = context;
			AppId = appId;
			AppName = appName;
			NeedNotify = needNotify;
			_runRecord = runRecord;
			_executeAsync = executeAsync;
			_onCreated = onCreated;
		}

		public IApplication CreateApplication(int instanceIndex, string groupId)
		{
			TestZApplication testZApplication = new TestZApplication(_context, AppId, _runRecord, _executeAsync);
			_onCreated?.Invoke(testZApplication);
			return testZApplication;
		}

		public IApplicationConfig GetConfig(int instanceIndex, string groupId)
		{
			return new EmptyApplicationConfig();
		}

		public IApplicationRunRecord GetRunRecord(int instanceIndex)
		{
			return _runRecord;
		}
	}

	private sealed class TestZApplication : ZApplication
	{
		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public TaskCompletionSource PauseCalled { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public TaskCompletionSource ResumeCalled { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public TaskCompletionSource StopCalled { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public TestZApplication(ZContext context, string appId, ZApplicationRunRecord runRecord, Func<CancellationToken, Task<OperationResult>> executeAsync)
			: base(context, appId, runRecord)
		{
			_executeAsync = executeAsync;
		}

		protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
		{
			return _executeAsync(cancellationToken);
		}

		public override Task OnPauseAsync(CancellationToken cancellationToken)
		{
			PauseCount++;
			PauseCalled.TrySetResult();
			return Task.CompletedTask;
		}

		public override Task OnResumeAsync(CancellationToken cancellationToken)
		{
			ResumeCount++;
			ResumeCalled.TrySetResult();
			return Task.CompletedTask;
		}

		public override Task OnStopAsync(CancellationToken cancellationToken)
		{
			StopCount++;
			StopCalled.TrySetResult();
			return Task.CompletedTask;
		}
	}

	private sealed class ConfigurableZApplication : ZApplication
	{
		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		public ConfigurableZApplication(ZContext context, string appId, ZApplicationRunRecord runRecord, Func<CancellationToken, Task<OperationResult>> executeAsync, Func<CancellationToken, Task<OperationResult>>? enterGameAsync = null, Action<OperationResult>? operationCallback = null, bool needCheckGameWindow = true)
			: base(context, appId, runRecord, null, 1, null, needCheckGameWindow, enterGameAsync, null, operationCallback)
		{
			_executeAsync = executeAsync;
		}

		protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
		{
			return _executeAsync(cancellationToken);
		}
	}

	private sealed class MetadataTestFactory : ZApplicationFactory
	{
		public MetadataTestFactory(ZContext context)
			: base(context, new ZApplicationFactoryMetadata("metadata-app", "元数据应用", "daily", NeedNotify: true))
		{
		}

		public override IApplication CreateApplication(int instanceIndex, string groupId)
		{
			return new ConfigurableZApplication(base.Context, base.AppId, new ZApplicationRunRecord(base.AppId), (CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: true)));
		}
	}

	private sealed class SampleApplicationConfig : ZApplicationConfig
	{
		[YamlMember(Alias = "mission_name", ApplyNamingConventions = false)]
		public string MissionName { get; set; } = string.Empty;

		[YamlMember(Alias = "plan_times", ApplyNamingConventions = false)]
		public int PlanTimes { get; set; }
	}

	private sealed class RecordingRunRecord(string appId) : ZApplicationRunRecord(appId)
	{
		public List<int> StatusHistory { get; } = new List<int>();

		public int CheckCount { get; private set; }

		public override void CheckAndUpdateStatus()
		{
			CheckCount++;
			base.CheckAndUpdateStatus();
		}

		public override void UpdateStatus(int newStatus, bool onlyStatus = false)
		{
			StatusHistory.Add(newStatus);
			base.UpdateStatus(newStatus, onlyStatus);
		}
	}

	private sealed class EmptyApplicationConfig : IApplicationConfig
	{
	}

	private sealed class FakeOcrLauncher : ZApplicationLauncher
	{
		public FakeOcrLauncher(Func<ZContext> contextFactory)
			: base(contextFactory, initializeContext: true, initializeOcrProfile: true, validateAssets: false)
		{
		}

		protected override void InitializeOcrProfile(ZContext context)
		{
			context.OcrService.Matcher = new FakeOcrMatcher();
		}
	}

	private sealed class MissingControllerLauncher : ZApplicationLauncher
	{
		public MissingControllerLauncher(Func<ZContext> contextFactory)
			: base(contextFactory, initializeContext: true, initializeOcrProfile: false, validateAssets: false)
		{
		}

		protected override void InitializeController(ZContext context)
		{
		}
	}

	private sealed class MissingApplicationsLauncher : ZApplicationLauncher
	{
		public MissingApplicationsLauncher(Func<ZContext> contextFactory)
			: base(contextFactory, initializeContext: true, initializeOcrProfile: false, validateAssets: false)
		{
		}

		protected override void RegisterBuiltInApplications(ZContext context)
		{
		}
	}

	private sealed class FailingOcrLauncher : ZApplicationLauncher
	{
		public FailingOcrLauncher(Func<ZContext> contextFactory)
			: base(contextFactory, initializeContext: true, initializeOcrProfile: true, validateAssets: false)
		{
		}

		protected override void InitializeOcrProfile(ZContext context)
		{
			throw new InvalidOperationException("fake ocr failure");
		}
	}

	private sealed class RecordingOcrProfileLauncher : ZApplicationLauncher
	{
		public RecordingOcrProfileLauncher()
			: base(initializeContext: false, initializeOcrProfile: false, validateAssets: false)
		{
		}

		public bool? RequestedGpu { get; private set; }

		public string? RequestedProfileId { get; private set; }

		public void InvokeInitializeOcrProfile(ZContext context)
		{
			InitializeOcrProfile(context);
		}

		protected override bool UseOcrProfile(ZContext context, string profileId, bool useGpu, double? detLimitSideLen)
		{
			RequestedProfileId = profileId;
			RequestedGpu = useGpu;
			return true;
		}
	}

	private sealed class OrderingLauncher(Func<ZContext> contextFactory, List<string> order) : ZApplicationLauncher(contextFactory, initializeContext: true, initializeOcrProfile: true, validateAssets: false)
	{
		protected override void RegisterBuiltInApplications(ZContext context)
		{
			order.Add("register");
			base.RegisterBuiltInApplications(context);
		}

		protected override void InitializeOcrProfile(ZContext context)
		{
			order.Add("ocr");
			context.OcrService.Matcher = new FakeOcrMatcher();
		}

		protected override void InitializeController(ZContext context)
		{
			order.Add("controller");
			base.InitializeController(context);
		}

		protected override void InitializeForApplication(ZContext context)
		{
			order.Add("application");
			base.InitializeForApplication(context);
		}
	}

	private sealed class RecordingShutdownParticipant : IShutdownParticipant
	{
		public bool ShutdownCalled { get; private set; }

		public Task ShutdownAsync(CancellationToken cancellationToken)
		{
			ShutdownCalled = true;
			return Task.CompletedTask;
		}
	}

	private sealed class FakeOcrMatcher : IOcrMatcher
	{
		public void UpdateUseGpu(bool useGpu)
		{
		}

		public bool IsUseGpu()
		{
			return false;
		}

		public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
		{
			return true;
		}

		public string RunOcrSingleLine(Mat image, double? threshold = null, bool strictOneLine = true)
		{
			return string.Empty;
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			return new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return Array.Empty<OcrMatchResult>();
		}
	}

	[Fact]
	public void Launcher_CreateContextInitializesRunnableContext()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			ZApplicationLauncher zApplicationLauncher = new ZApplicationLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)), initializeContext: true, initializeOcrProfile: false, validateAssets: false);
			ZContext context = zApplicationLauncher.CreateContext();
			try
			{
				Assert.Same(context, zApplicationLauncher.Context);
				Assert.True(context.ReadyForApplication);
				Assert.NotNull(context.Controller);
				Assert.All(ZzzApplicationIds.All, delegate(string appId)
				{
					Assert.True(context.RunContext.IsAppRegistered(appId), appId);
				});
				Assert.NotEmpty(context.RunContext.DefaultGroupApps);
				Assert.Contains("coffee", (IEnumerable<string>)context.RunContext.DefaultGroupApps);
				Assert.Contains("shiyu_defense", (IEnumerable<string>)context.RunContext.DefaultGroupApps);
				Assert.Contains("notify", (IEnumerable<string>)context.RunContext.DefaultGroupApps);
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_InitializeOcrProfileUsesPersistedGpuSetting()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			Directory.CreateDirectory(Path.Combine(rootDirectory, "config"));
			File.WriteAllText(Path.Combine(rootDirectory, "config", "model.yml"), "ocr: ppocrv6\nocr_use_gpu: true\n");
			using ZContext context = new(new OneDragonEnvironment(rootDirectory));
			RecordingOcrProfileLauncher launcher = new();

			launcher.InvokeInitializeOcrProfile(context);

			Assert.Equal("v6-small", launcher.RequestedProfileId);
			Assert.True(launcher.RequestedGpu);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_CreateContextReplacesNullOcrMatcherBeforeApplicationsRun()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			FakeOcrLauncher fakeOcrLauncher = new FakeOcrLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)));
			using ZContext zContext = fakeOcrLauncher.CreateContext();
			Assert.IsType<FakeOcrMatcher>(zContext.OcrService.Matcher);
			Assert.True(zContext.ReadyForApplication);
			Assert.NotEmpty(zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_CreateContextFailsWithClearAssetsError()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			ZApplicationLauncher launcher = new ZApplicationLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)), initializeContext: true, initializeOcrProfile: false);
			InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => launcher.CreateContext());
			Assert.Contains("assets 初始化失败", ex.Message);
			Assert.Contains("assets", ex.Message);
			Assert.Contains("models", ex.Message);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_CreateContextFailsWhenControllerIsNotBound()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			MissingControllerLauncher launcher = new MissingControllerLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)));
			InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => launcher.CreateContext());
			Assert.Contains("controller 初始化失败", ex.Message);
			Assert.Contains("未绑定 Controller", ex.Message);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_CreateContextFailsWhenApplicationRegistrationIsIncomplete()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			MissingApplicationsLauncher launcher = new MissingApplicationsLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)));
			InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => launcher.CreateContext());
			Assert.Contains("应用注册 初始化失败", ex.Message);
			Assert.Contains("内置应用注册不完整", ex.Message);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_CreateContextFailsWhenOcrProfileInitializationFails()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			FailingOcrLauncher launcher = new FailingOcrLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)));
			InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => launcher.CreateContext());
			Assert.Contains("OCR profile 初始化失败", ex.Message);
			Assert.Contains("fake ocr failure", ex.Message);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_CreateContextRunsInitializationHooksInOrder()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			List<string> list = new List<string>();
			OrderingLauncher orderingLauncher = new OrderingLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)), list);
			ZContext context = orderingLauncher.CreateContext();
			try
			{
				int num = 4;
				List<string> list2 = new List<string>(num);
				CollectionsMarshal.SetCount(list2, num);
				Span<string> span = CollectionsMarshal.AsSpan(list2);
				span[0] = "register";
				span[1] = "ocr";
				span[2] = "controller";
				span[3] = "application";
				Assert.Equal(list2, list);
				Assert.IsType<FakeOcrMatcher>(context.OcrService.Matcher);
				Assert.NotNull(context.Controller);
				Assert.All(ZzzApplicationIds.All, delegate(string appId)
				{
					Assert.True(context.RunContext.IsAppRegistered(appId), appId);
				});
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Launcher_ContextShutdownRunsRegisteredCleanupParticipants()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			ZApplicationLauncher zApplicationLauncher = new ZApplicationLauncher(() => new ZContext(new OneDragonEnvironment(rootDirectory)), initializeContext: true, initializeOcrProfile: false, validateAssets: false);
			ZContext zContext = zApplicationLauncher.CreateContext();
			RecordingShutdownParticipant recordingShutdownParticipant = new RecordingShutdownParticipant();
			zContext.RegisterShutdownParticipant(recordingShutdownParticipant);
			zContext.Dispose();
			Assert.True(recordingShutdownParticipant.ShutdownCalled);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersFactoriesIntoRunContext()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingRunRecord recordingRunRecord = new RecordingRunRecord("notify-app");
			TestZApplicationFactory factory = new TestZApplicationFactory(zContext, "notify-app", "通知测试", needNotify: true, recordingRunRecord, (CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: true, "ok")));
			zContext.ApplicationFactoryRegistry.RegisterApplication(factory, defaultGroup: true);
			Assert.True(zContext.RunContext.IsAppRegistered("notify-app"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("notify-app"));
			Assert.Equal("通知测试", zContext.RunContext.NotifyAppMap["notify-app"]);
			Assert.Equal(new string[] { "notify-app" }, zContext.RunContext.DefaultGroupApps);
			Assert.Same(recordingRunRecord, zContext.RunContext.GetRunRecord("notify-app", 0));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegisterBuiltInApplicationsUsesCatalogDefaultGroup()
	{
		string text = CreateTempRoot();
		try
		{
			ZContext context = new ZContext(new OneDragonEnvironment(text));
			try
			{
				context.AttachController(new ReadyController());
				context.ApplicationFactoryRegistry.RegisterBuiltInApplications();
				string[] expected = context.ApplicationFactoryRegistry.BuiltInApplicationDirectories.Where((ZApplicationDirectoryMetadata directory) => directory.DefaultGroup).SelectMany((ZApplicationDirectoryMetadata directory) => directory.AppIds).ToArray();
				Assert.Equal(expected, context.RunContext.DefaultGroupApps);
				Assert.All(ZzzApplicationIds.All, delegate(string appId)
				{
					Assert.True(context.RunContext.IsAppRegistered(appId), appId);
				});
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task Launcher_RunDefaultGroupFailsWhenDefaultGroupIsEmpty()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			try
			{
				context.AttachController(new ReadyController());
				ZApplicationLauncher launcher = new ZApplicationLauncher(() => context, initializeContext: false);
				Assert.Equal("默认应用组未注册。", (await Assert.ThrowsAsync<InvalidOperationException>(async delegate
				{
					await launcher.RunDefaultGroupAsync(0);
				})).Message);
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Launcher_RunsDefaultGroupSequentiallyAndExposesNotifyMap()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			try
			{
				context.AttachController(new ReadyController());
				ZApplicationLauncher launcher = new ZApplicationLauncher(() => context, initializeContext: false);
				List<string> executionOrder = new List<string>();
				RecordingRunRecord firstRecord = new RecordingRunRecord("launcher-1");
				RecordingRunRecord secondRecord = new RecordingRunRecord("launcher-2");
				context.ApplicationFactoryRegistry.RegisterApplications(new IApplicationFactory[2]
				{
					new TestZApplicationFactory(context, "launcher-1", "启动器一", needNotify: true, firstRecord, delegate
					{
						executionOrder.Add("launcher-1");
						return Task.FromResult(new OperationResult(IsSuccess: true, "one"));
					}),
					new TestZApplicationFactory(context, "launcher-2", "启动器二", needNotify: false, secondRecord, delegate
					{
						executionOrder.Add("launcher-2");
						return Task.FromResult(new OperationResult(IsSuccess: true, "two"));
					})
				}, defaultGroup: true);
				IReadOnlyList<OperationResult> results = await launcher.RunDefaultGroupAsync(0);
				Assert.Equal<List<string>>(new List<string>(2) { "launcher-1", "launcher-2" }, executionOrder);
				Assert.All(results, delegate(OperationResult result)
				{
					Assert.True(result.IsSuccess);
				});
				Assert.Equal("启动器一", launcher.NotifyAppMap["launcher-1"]);
				Assert.False(launcher.NotifyAppMap.ContainsKey("launcher-2"));
				Assert.Equal(new List<int>(2) { 3, 1 }, firstRecord.StatusHistory);
				Assert.Equal(new List<int>(2) { 3, 1 }, secondRecord.StatusHistory);
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Launcher_ForwardsPauseResumeAndStop()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			try
			{
				context.AttachController(new ReadyController());
				ZApplicationLauncher launcher = new ZApplicationLauncher(() => context, initializeContext: false);
				RecordingRunRecord runRecord = new RecordingRunRecord("launcher-long");
				TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				TestZApplication createdApp = null;
				context.ApplicationFactoryRegistry.RegisterApplication(new TestZApplicationFactory(context, "launcher-long", "启动器长运行", needNotify: false, runRecord, async delegate(CancellationToken ct)
				{
					started.TrySetResult();
					await Task.Delay(Timeout.InfiniteTimeSpan, ct);
					return new OperationResult(IsSuccess: true);
				}, delegate(TestZApplication app)
				{
					createdApp = app;
				}));
				Task<IReadOnlyList<OperationResult>> runningTask = launcher.RunApplicationsAsync(new string[] { "launcher-long" }, 0);
				await started.Task.WaitAsync(TimeSpan.FromSeconds(2L));
				Assert.NotNull(createdApp);
				launcher.PauseOrResume();
				await createdApp.PauseCalled.Task.WaitAsync(TimeSpan.FromSeconds(2L));
				launcher.PauseOrResume();
				await createdApp.ResumeCalled.Task.WaitAsync(TimeSpan.FromSeconds(2L));
				await launcher.StopAsync(TimeSpan.FromSeconds(1L));
				await Assert.ThrowsAnyAsync<OperationCanceledException>(async delegate
				{
					await runningTask;
				});
				Assert.Equal(1, createdApp.PauseCount);
				Assert.Equal(1, createdApp.ResumeCount);
				Assert.Equal(0, createdApp.StopCount);
			}
			finally
			{
				if (context != null)
				{
					((IDisposable)context).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunContext_RunsZApplicationsSequentiallyAndUpdatesSuccessRecords()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			List<string> executionOrder = new List<string>();
			int runningCount = 0;
			int maxRunningCount = 0;
			RecordingRunRecord firstRecord = new RecordingRunRecord("app-1");
			RecordingRunRecord secondRecord = new RecordingRunRecord("app-2");
			context.ApplicationFactoryRegistry.RegisterApplications(new IApplicationFactory[2]
			{
				new TestZApplicationFactory(context, "app-1", "应用一", needNotify: false, firstRecord, async delegate(CancellationToken ct)
				{
					executionOrder.Add("app-1-start");
					maxRunningCount = Math.Max(val2: Interlocked.Increment(ref runningCount), val1: maxRunningCount);
					await Task.Delay(80, ct);
					Interlocked.Decrement(ref runningCount);
					executionOrder.Add("app-1-end");
					return new OperationResult(IsSuccess: true, "done-1");
				}),
				new TestZApplicationFactory(context, "app-2", "应用二", needNotify: false, secondRecord, async delegate(CancellationToken ct)
				{
					executionOrder.Add("app-2-start");
					maxRunningCount = Math.Max(val2: Interlocked.Increment(ref runningCount), val1: maxRunningCount);
					await Task.Delay(10, ct);
					Interlocked.Decrement(ref runningCount);
					executionOrder.Add("app-2-end");
					return new OperationResult(IsSuccess: true, "done-2");
				})
			});
			Task<OperationResult> first = context.RunContext.RunApplicationAsync("app-1", 0, "default");
			Task<OperationResult> second = context.RunContext.RunApplicationAsync("app-2", 0, "default");
			Task<OperationResult>[] buffer = new Task<OperationResult>[2];
			buffer[0] = first;
			buffer[1] = second;
			Assert.All(await Task.WhenAll<OperationResult>(buffer).WaitAsync(TimeSpan.FromSeconds(3L)), delegate(OperationResult result)
			{
				Assert.True(result.IsSuccess);
			});
			Assert.Equal<List<string>>(new List<string>(4) { "app-1-start", "app-1-end", "app-2-start", "app-2-end" }, executionOrder);
			Assert.Equal(1, maxRunningCount);
			Assert.Equal(new List<int>(2) { 3, 1 }, firstRecord.StatusHistory);
			Assert.Equal(new List<int>(2) { 3, 1 }, secondRecord.StatusHistory);
			Assert.True(firstRecord.IsDone);
			Assert.True(secondRecord.IsDone);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ZApplication_WritesFailRecordWhenCoreReturnsFailure()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingRunRecord runRecord = new RecordingRunRecord("failed-app");
			context.ApplicationFactoryRegistry.RegisterApplication(new TestZApplicationFactory(context, "failed-app", "失败应用", needNotify: false, runRecord, (CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: false, "failed"))));
			Assert.False((await context.RunContext.RunApplicationAsync("failed-app", 0, "default").WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal(new List<int>(2) { 3, 2 }, runRecord.StatusHistory);
			Assert.False(runRecord.IsDone);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunContext_PauseResumeAndStopReachZApplicationAndMarkFail()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingRunRecord runRecord = new RecordingRunRecord("long-app");
			TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			TestZApplication createdApp = null;
			TestZApplicationFactory factory = new TestZApplicationFactory(context, "long-app", "长运行应用", needNotify: false, runRecord, async delegate(CancellationToken ct)
			{
				started.TrySetResult();
				await Task.Delay(Timeout.InfiniteTimeSpan, ct);
				return new OperationResult(IsSuccess: true, "should-not-complete");
			}, delegate(TestZApplication app)
			{
				createdApp = app;
			});
			context.ApplicationFactoryRegistry.RegisterApplication(factory);
			Task<OperationResult> runningTask = context.RunContext.RunApplicationAsync("long-app", 0, "default");
			await started.Task.WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.NotNull(createdApp);
			context.RunContext.SwitchContextPauseAndRun();
			await createdApp.PauseCalled.Task.WaitAsync(TimeSpan.FromSeconds(2L));
			context.RunContext.SwitchContextPauseAndRun();
			await createdApp.ResumeCalled.Task.WaitAsync(TimeSpan.FromSeconds(2L));
			await context.RunContext.StopRunningAsync(TimeSpan.FromSeconds(1L));
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async delegate
			{
				await runningTask;
			});
			Assert.Equal(1, createdApp.PauseCount);
			Assert.Equal(1, createdApp.ResumeCount);
			Assert.Equal(0, createdApp.StopCount);
			Assert.Equal(new List<int>(2) { 3, 2 }, runRecord.StatusHistory);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunContext_ExternalCancellationDoesNotLeaveRunRecordRunning()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingRunRecord runRecord = new RecordingRunRecord("cancel-app");
			TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			using CancellationTokenSource cts = new CancellationTokenSource();
			context.ApplicationFactoryRegistry.RegisterApplication(new TestZApplicationFactory(context, "cancel-app", "取消应用", needNotify: false, runRecord, async delegate(CancellationToken ct)
			{
				started.TrySetResult();
				await Task.Delay(Timeout.InfiniteTimeSpan, ct);
				return new OperationResult(IsSuccess: true, "should-not-complete");
			}));
			Task<OperationResult> runningTask = context.RunContext.RunApplicationAsync("cancel-app", 0, "default", cts.Token);
			await started.Task.WaitAsync(TimeSpan.FromSeconds(2L));
			cts.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async delegate
			{
				await runningTask;
			});
			Assert.Equal(new List<int>(2) { 3, 2 }, runRecord.StatusHistory);
			Assert.Equal(2, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void RunContext_CheckAndUpdateAllRunRecordUsesZApplicationFactories()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingRunRecord recordingRunRecord = new RecordingRunRecord("app-1");
			RecordingRunRecord recordingRunRecord2 = new RecordingRunRecord("app-2");
			zContext.ApplicationFactoryRegistry.RegisterApplications(new IApplicationFactory[2]
			{
				new TestZApplicationFactory(zContext, "app-1", "应用一", needNotify: false, recordingRunRecord, (CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: true))),
				new TestZApplicationFactory(zContext, "app-2", "应用二", needNotify: false, recordingRunRecord2, (CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: true)))
			});
			zContext.RunContext.CheckAndUpdateAllRunRecord(0);
			Assert.Equal(1, recordingRunRecord.CheckCount);
			Assert.Equal(1, recordingRunRecord2.CheckCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task ZApplication_RunsEnterGameBeforeCoreAndInvokesCallback()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		RecordingRunRecord runRecord = new RecordingRunRecord("base-app");
		List<string> steps = new List<string>();
		OperationResult callbackResult = null;
		ConfigurableZApplication app = new ConfigurableZApplication(context, "base-app", runRecord, delegate
		{
			steps.Add("core");
			return Task.FromResult(new OperationResult(IsSuccess: true, "core-ok"));
		}, delegate
		{
			steps.Add("enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "entered"));
		}, delegate(OperationResult result)
		{
			callbackResult = result;
		});
		Assert.True((await app.ExecuteAsync(CancellationToken.None)).IsSuccess);
		Assert.Equal<List<string>>(new List<string>(2) { "enter", "core" }, steps);
		Assert.Equal("core-ok", callbackResult?.Status);
		Assert.Equal(new List<int>(2) { 3, 1 }, runRecord.StatusHistory);
		Assert.True(app.NeedCheckGameWindow);
		Assert.Equal("base-app", app.AppName);
	}

	[Fact]
	public async Task ZApplication_ReturnsFailureWhenEnterGameFails()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		RecordingRunRecord runRecord = new RecordingRunRecord("enter-fail");
		bool coreCalled = false;
		OperationResult callbackResult = null;
		ConfigurableZApplication app = new ConfigurableZApplication(context, "enter-fail", runRecord, delegate
		{
			coreCalled = true;
			return Task.FromResult(new OperationResult(IsSuccess: true));
		}, (CancellationToken _) => Task.FromResult(new OperationResult(IsSuccess: false, "enter-failed")), delegate(OperationResult result)
		{
			callbackResult = result;
		});
		Assert.False((await app.ExecuteAsync(CancellationToken.None)).IsSuccess);
		Assert.False(coreCalled);
		Assert.Equal("enter-failed", callbackResult?.Status);
		Assert.Equal(new List<int>(2) { 3, 2 }, runRecord.StatusHistory);
	}

	[Fact]
	public void ZApplicationConfig_LoadsFromGroupedInstanceYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "daily");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "sample_app.yml"), "mission_name: \"旧都列车-内部\"\r\nplan_times: 3");
			SampleApplicationConfig sampleApplicationConfig = ZApplicationConfig.Load<SampleApplicationConfig>(new OneDragonEnvironment(text), "sample_app", 0, "daily");
			Assert.Equal("sample_app", sampleApplicationConfig.AppId);
			Assert.Equal(0, sampleApplicationConfig.InstanceIndex);
			Assert.Equal("daily", sampleApplicationConfig.GroupId);
			Assert.Equal("旧都列车-内部", sampleApplicationConfig.MissionName);
			Assert.Equal(3, sampleApplicationConfig.PlanTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ZApplicationRunRecord_LoadsPythonCompatibleYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "record_app.yml"), "dt: \"20260706\"\r\nrun_time: \"07-06 10:30\"\r\nrun_time_float: 1783305000\r\nrun_status: 1");
			ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(new OneDragonEnvironment(text), "record_app", 0, 0, ZApplicationRunRecordPeriod.Daily, () => new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
			Assert.Equal("record_app", zApplicationRunRecord.AppId);
			Assert.Equal("20260706", zApplicationRunRecord.Dt);
			Assert.Equal("07-06 10:30", zApplicationRunRecord.RunTime);
			Assert.Equal(1783305000.0, zApplicationRunRecord.RunTimeFloat);
			Assert.Equal(1, zApplicationRunRecord.RunStatus);
			Assert.True(zApplicationRunRecord.IsDone);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ZApplicationRunRecord_CheckAndUpdateStatus_ResetsWhenDailyDtExpired()
	{
		ZApplicationRunRecord zApplicationRunRecord = new ZApplicationRunRecord("daily-record", 0, ZApplicationRunRecordPeriod.Daily, () => new DateTimeOffset(2026, 7, 7, 3, 0, 0, TimeSpan.Zero))
		{
			Dt = "20260706",
			RunStatus = 1
		};
		zApplicationRunRecord.CheckAndUpdateStatus();
		Assert.Equal(0, zApplicationRunRecord.RunStatus);
		Assert.Equal("20260706", zApplicationRunRecord.Dt);
	}

	[Fact]
	public void ZApplicationRunRecord_Load_MissingDtUsesConfiguredRefreshOffset()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-run-record-missing-dt", Guid.NewGuid().ToString("N"));
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "record_app.yml"), "run_status: 1\n");
			ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(new OneDragonEnvironment(text), "record_app", 0, 4, ZApplicationRunRecordPeriod.Daily, () => new DateTimeOffset(2026, 7, 13, 2, 0, 0, TimeSpan.Zero));
			Assert.Equal("20260713", zApplicationRunRecord.Dt);
			Assert.Equal(1, zApplicationRunRecord.RunStatusUnderNow);
		}
		finally
		{
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
		}
	}

	[Fact]
	public void ZApplicationRunRecord_CheckAndUpdateStatus_KeepsStatusWithinSameWeeklyPeriod()
	{
		ZApplicationRunRecord zApplicationRunRecord = new ZApplicationRunRecord("weekly-record", 0, ZApplicationRunRecordPeriod.Weekly, () => new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero))
		{
			Dt = "20260706",
			RunStatus = 1
		};
		zApplicationRunRecord.CheckAndUpdateStatus();
		Assert.Equal(1, zApplicationRunRecord.RunStatus);
		Assert.Equal(1, zApplicationRunRecord.RunStatusUnderNow);
	}

	[Fact]
	public void ZApplicationRunRecord_GameRefreshHourOffset_UsesShiftedDate()
	{
		ZApplicationRunRecord zApplicationRunRecord = new ZApplicationRunRecord("offset-record", -4, ZApplicationRunRecordPeriod.Daily, () => new DateTimeOffset(2026, 7, 7, 2, 0, 0, TimeSpan.Zero));
		zApplicationRunRecord.UpdateStatus(1);
		Assert.Equal("20260706", zApplicationRunRecord.Dt);
		Assert.Equal("07-07 02:00", zApplicationRunRecord.RunTime);
	}

	[Fact]
	public void ZApplicationRunRecord_UpdateStatusOnlyStatusKeepsRunTimeFields()
	{
		ZApplicationRunRecord zApplicationRunRecord = new ZApplicationRunRecord("status-only-record", 0, ZApplicationRunRecordPeriod.Daily, () => new DateTimeOffset(2026, 7, 7, 2, 0, 0, TimeSpan.Zero))
		{
			Dt = "20260706",
			RunTime = "07-06 10:30",
			RunTimeFloat = 1783305000.0
		};
		zApplicationRunRecord.UpdateStatus(2, onlyStatus: true);
		Assert.Equal(2, zApplicationRunRecord.RunStatus);
		Assert.Equal("20260706", zApplicationRunRecord.Dt);
		Assert.Equal("07-06 10:30", zApplicationRunRecord.RunTime);
		Assert.Equal(1783305000.0, zApplicationRunRecord.RunTimeFloat);
	}

	[Fact]
	public void ZApplicationRunRecord_UpdateStatus_PersistsAndReloadsYaml()
	{
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "persisted_record", 0, 0, ZApplicationRunRecordPeriod.Daily, () => new DateTimeOffset(2026, 7, 6, 10, 30, 0, TimeSpan.Zero));
			zApplicationRunRecord.UpdateStatus(1);
			ZApplicationRunRecord zApplicationRunRecord2 = ZApplicationRunRecord.Load(environment, "persisted_record", 0, 0, ZApplicationRunRecordPeriod.Daily, () => new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
			Assert.Equal("persisted_record", zApplicationRunRecord2.AppId);
			Assert.Equal("20260706", zApplicationRunRecord2.Dt);
			Assert.Equal("07-06 10:30", zApplicationRunRecord2.RunTime);
			Assert.Equal(1783333800.0, zApplicationRunRecord2.RunTimeFloat);
			Assert.Equal(1, zApplicationRunRecord2.RunStatus);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ZApplicationFactory_ExposesMetadataAndCreatesRuntimeObjects()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			MetadataTestFactory metadataTestFactory = new MetadataTestFactory(zContext);
			IApplication application = metadataTestFactory.CreateApplication(0, metadataTestFactory.GroupId);
			IApplicationConfig config = metadataTestFactory.GetConfig(0, metadataTestFactory.GroupId);
			IApplicationRunRecord runRecord = metadataTestFactory.GetRunRecord(0);
			Assert.Equal("metadata-app", metadataTestFactory.AppId);
			Assert.Equal("元数据应用", metadataTestFactory.AppName);
			Assert.Equal("daily", metadataTestFactory.GroupId);
			Assert.True(metadataTestFactory.NeedNotify);
			Assert.Equal("metadata-app", application.AppId);
			Assert.IsType<ZApplicationConfig>(config);
			Assert.IsType<ZApplicationRunRecord>(runRecord);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
