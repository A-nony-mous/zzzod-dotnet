using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application.DailySignIn;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class DailySignInAppTests
{
	private sealed class RecordingDailySignInFlow : IDailySignInFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, DailySignInConfig config, int instanceIndex, string groupId, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "每日签到完成"));
		}
	}

	private sealed class EmptySubAppConfig : IApplicationConfig
	{
	}

	private sealed class StubSubApplication : IApplication
	{
		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		public string AppId { get; }

		public StubSubApplication(string appId, Func<CancellationToken, Task<OperationResult>> executeAsync)
		{
			AppId = appId;
			_executeAsync = executeAsync;
		}

		public Task<OperationResult> ExecuteAsync(CancellationToken cancellationToken) => _executeAsync(cancellationToken);
	}

	private sealed class StubSubAppFactory : IApplicationFactory
	{
		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		public string AppId { get; }

		public string AppName { get; }

		public bool NeedNotify => true;

		public int CreateApplicationCallCount { get; private set; }

		public int? LastInstanceIndex { get; private set; }

		public string? LastGroupId { get; private set; }

		public StubSubAppFactory(string appId, string appName, Func<CancellationToken, Task<OperationResult>> executeAsync)
		{
			AppId = appId;
			AppName = appName;
			_executeAsync = executeAsync;
		}

		public IApplication CreateApplication(int instanceIndex, string groupId)
		{
			CreateApplicationCallCount++;
			LastInstanceIndex = instanceIndex;
			LastGroupId = groupId;
			return new StubSubApplication(AppId, _executeAsync);
		}

		public IApplicationConfig GetConfig(int instanceIndex, string groupId) => new EmptySubAppConfig();

		public IApplicationRunRecord GetRunRecord(int instanceIndex) => new ZzzOd.GameLogic.Application.ZApplicationRunRecord(AppId);
	}

	[Fact]
	public void Factory_ExposesBaselineMetadataAndCreatesDailySignInApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DailySignInFactory dailySignInFactory = zContext.ApplicationFactoryRegistry.CreateDailySignInFactory();
			IApplication application = dailySignInFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = dailySignInFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = dailySignInFactory.GetRunRecord(0);
			Assert.Equal("daily_signin", dailySignInFactory.AppId);
			Assert.Equal("每日签到", dailySignInFactory.AppName);
			Assert.Equal("one_dragon", dailySignInFactory.GroupId);
			Assert.True(dailySignInFactory.NeedNotify);
			Assert.IsType<DailySignInApp>(application);
			DailySignInConfig dailySignInConfig = Assert.IsType<DailySignInConfig>(config);
			Assert.Equal("hou_hou_bakery", dailySignInConfig.SelectedSign);
			DailySignInRunRecord dailySignInRunRecord = Assert.IsType<DailySignInRunRecord>(runRecord);
			Assert.Equal("daily_signin", dailySignInRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersDailySignInAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterDailySignInApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("daily_signin"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("daily_signin"));
			Assert.Contains("daily_signin", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DailySignInRunRecord_UsesAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		DailySignInRunRecord dailySignInRunRecord = new DailySignInRunRecord(4, () => now);
		dailySignInRunRecord.UpdateStatus(1);
		Assert.Equal("daily_signin", dailySignInRunRecord.AppId);
		Assert.Equal("20260706", dailySignInRunRecord.Dt);
		Assert.True(dailySignInRunRecord.IsDone);
	}

	[Fact]
	public async Task DailySignInApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DailySignInConfig config = new DailySignInConfig();
			DailySignInRunRecord runRecord = new DailySignInRunRecord();
			RecordingDailySignInFlow flow = new RecordingDailySignInFlow();
			DailySignInApp app = new DailySignInApp(context, 0, "one_dragon", config, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("每日签到完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RunSubApp_InvokesConfiguredSubAppWithMatchingInstanceAndGroup()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			StubSubAppFactory subAppFactory = new StubSubAppFactory("hou_hou_bakery", "吼吼饼铺", (CancellationToken _) =>
				Task.FromResult(new OperationResult(IsSuccess: true, "签到成功")));
			context.ApplicationFactoryRegistry.RegisterApplication(subAppFactory);
			DailySignInConfig config = new DailySignInConfig { SelectedSign = "hou_hou_bakery" };
			DailySignInOperation operation = new DailySignInOperation(context, config, 3, "daily");
			OperationRoundResult result = await operation.RunSubApp().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("签到成功", result.Status);
			Assert.Equal(1, subAppFactory.CreateApplicationCallCount);
			Assert.Equal(3, subAppFactory.LastInstanceIndex);
			Assert.Equal("daily", subAppFactory.LastGroupId);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RunSubApp_ForwardsSubAppFailureStatus()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			StubSubAppFactory subAppFactory = new StubSubAppFactory("scratch_card", "刮刮卡", (CancellationToken _) =>
				Task.FromResult(new OperationResult(IsSuccess: false, "今日已刮")));
			context.ApplicationFactoryRegistry.RegisterApplication(subAppFactory);
			DailySignInConfig config = new DailySignInConfig { SelectedSign = "scratch_card" };
			DailySignInOperation operation = new DailySignInOperation(context, config, 0, "one_dragon");
			OperationRoundResult result = await operation.RunSubApp().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("今日已刮", result.Status);
			Assert.Equal(1, subAppFactory.CreateApplicationCallCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RunSubApp_FailsWhenNoSignSelected()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DailySignInConfig config = new DailySignInConfig { SelectedSign = string.Empty };
			DailySignInOperation operation = new DailySignInOperation(context, config, 0, "one_dragon");
			OperationRoundResult result = await operation.RunSubApp().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未选择子应用", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RunSubApp_FailsWhenSubAppNotRegistered()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DailySignInConfig config = new DailySignInConfig { SelectedSign = "hou_hou_bakery" };
			DailySignInOperation operation = new DailySignInOperation(context, config, 0, "one_dragon");
			OperationRoundResult result = await operation.RunSubApp().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未找到应用 hou_hou_bakery", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresSingleRunSubAppStartNode()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = (from method in typeof(DailySignInOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[1] { "运行子应用" }, nodes.Keys);
		Assert.True(nodes["运行子应用"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
