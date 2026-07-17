using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class ZOneDragonAppTests
{
	private sealed class TestFactory : IApplicationFactory
	{
		private readonly ZContext _context;

		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		private readonly ZApplicationRunRecord _runRecord;

		public string AppId { get; }

		public string AppName { get; }

		public bool NeedNotify => false;

		public TestFactory(ZContext context, string appId, string appName, Func<CancellationToken, Task<OperationResult>> executeAsync)
		{
			_context = context;
			AppId = appId;
			AppName = appName;
			_executeAsync = executeAsync;
			_runRecord = new ZApplicationRunRecord(appId);
		}

		public IApplication CreateApplication(int instanceIndex, string groupId)
		{
			return new TestApplication(_context, AppId, _runRecord, _executeAsync);
		}

		public IApplicationConfig GetConfig(int instanceIndex, string groupId)
		{
			return new EmptyConfig();
		}

		public IApplicationRunRecord GetRunRecord(int instanceIndex)
		{
			return _runRecord;
		}
	}

	private sealed class TestApplication : ZApplication
	{
		private readonly Func<CancellationToken, Task<OperationResult>> _executeAsync;

		public TestApplication(ZContext context, string appId, ZApplicationRunRecord runRecord, Func<CancellationToken, Task<OperationResult>> executeAsync)
			: base(context, appId, runRecord)
		{
			_executeAsync = executeAsync;
		}

		protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
		{
			return _executeAsync(cancellationToken);
		}
	}

	private sealed class EmptyConfig : IApplicationConfig
	{
	}

	[Fact]
	public void GameAccountConfig_DifferentGamePathRequiresTwoNonEmptyDistinctPaths()
	{
		string text = CreateTempRoot();
		try
		{
			Directory.CreateDirectory(Path.Combine(text, "config", "00"));
			Directory.CreateDirectory(Path.Combine(text, "config", "01"));
			File.WriteAllText(Path.Combine(text, "config", "00", "game_account.yml"), "game_path: D:/Games/A.exe\n");
			File.WriteAllText(Path.Combine(text, "config", "01", "game_account.yml"), "game_path: D:/Games/B.exe\n");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			Assert.True(GameAccountConfig.IsDifferentGamePath(environment, 0, 1));
			File.WriteAllText(Path.Combine(text, "config", "01", "game_account.yml"), "game_path: D:/Games/A.exe\n");
			Assert.False(GameAccountConfig.IsDifferentGamePath(environment, 0, 1));
			File.WriteAllText(Path.Combine(text, "config", "01", "game_account.yml"), "game_path: \n");
			Assert.False(GameAccountConfig.IsDifferentGamePath(environment, 0, 1));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DirectoryCatalog_MatchesPythonApplicationTopLevelDirectories()
	{
		string[] expected = new string[25]
		{
			"battle_assistant", "charge_plan", "city_fund", "coffee", "commission_assistant", "devtools", "drive_disc_dismantle", "email_app", "engagement_reward", "game_config_checker",
			"hollow_zero", "hou_hou_bakery", "intel_board", "life_on_line", "notify", "notorious_hunt", "one_dragon_app", "random_play", "redemption_code", "ridu_weekly",
			"scratch_card", "shiyu_defense", "suibian_temple", "trigrams_collection", "world_patrol"
		};
		IReadOnlyList<ZApplicationDirectoryMetadata> builtInDirectories = ZApplicationDirectoryCatalog.BuiltInDirectories;
		Assert.Equal(25, builtInDirectories.Count);
		Assert.Equal(expected, builtInDirectories.Select((ZApplicationDirectoryMetadata directory) => directory.DirectoryName));
		Assert.All(builtInDirectories, delegate(ZApplicationDirectoryMetadata directory)
		{
			Assert.NotEmpty(directory.AppIds);
		});
		Assert.Contains((IEnumerable<ZApplicationDirectoryMetadata>)builtInDirectories, (Predicate<ZApplicationDirectoryMetadata>)((ZApplicationDirectoryMetadata directory) => directory.DirectoryName == "one_dragon_app" && directory.AppIds.SequenceEqual(new string[] { "one_dragon" }) && !directory.DefaultGroup && !directory.NeedNotify));
	}

	[Fact]
	public void Factory_ExposesPythonConstMetadataAndCreatesOneDragonApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			ZOneDragonAppFactory zOneDragonAppFactory = zContext.ApplicationFactoryRegistry.CreateOneDragonFactory();
			IApplication application = zOneDragonAppFactory.CreateApplication(0, "one_dragon");
			Assert.Equal("one_dragon", zOneDragonAppFactory.AppId);
			Assert.Equal("一条龙", zOneDragonAppFactory.AppName);
			Assert.Equal("one_dragon", zOneDragonAppFactory.GroupId);
			Assert.False(zOneDragonAppFactory.NeedNotify);
			Assert.IsType<ZOneDragonApp>(application);
			Assert.Equal("one_dragon", application.AppId);
			Assert.IsType<ZApplicationRunRecord>(zOneDragonAppFactory.GetRunRecord(0));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_ExposesDirectoryCatalogAndRegistersOneDragonFactory()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterOneDragonApplication();
			Assert.Equal(25, zContext.ApplicationFactoryRegistry.BuiltInApplicationDirectories.Count);
			Assert.True(zContext.RunContext.IsAppRegistered("one_dragon"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("one_dragon"));
			Assert.DoesNotContain("one_dragon", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersAllBuiltInApplicationFactoriesWithConfigAndRunRecord()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			ApplicationFactoryRegistry applicationFactoryRegistry = zContext.ApplicationFactoryRegistry;
			IReadOnlyList<ZApplicationDirectoryMetadata> builtInApplicationDirectories = applicationFactoryRegistry.BuiltInApplicationDirectories;
			string[] source = builtInApplicationDirectories.SelectMany((ZApplicationDirectoryMetadata directory) => directory.AppIds).ToArray();
			RegisterAllBuiltInApplications(applicationFactoryRegistry);
			Assert.Equal(25, builtInApplicationDirectories.Count);
			Assert.Equal(ZzzApplicationIds.All.OrderBy((string id) => id), source.OrderBy((string id) => id));
			foreach (string item in ZzzApplicationIds.All)
			{
				Assert.True(zContext.RunContext.IsAppRegistered(item), item);
				Assert.Equal(item, zContext.RunContext.GetApplication(item, 0, "default").AppId);
				Assert.NotNull(zContext.RunContext.GetConfig(item, 0, "default"));
				Assert.NotNull(zContext.RunContext.GetRunRecord(item, 0));
			}
			string[] source2 = builtInApplicationDirectories.Where((ZApplicationDirectoryMetadata directory) => directory.DefaultGroup).SelectMany((ZApplicationDirectoryMetadata directory) => directory.AppIds).ToArray();
			string[] source3 = builtInApplicationDirectories.Where((ZApplicationDirectoryMetadata directory) => directory.NeedNotify).SelectMany((ZApplicationDirectoryMetadata directory) => directory.AppIds).ToArray();
			Assert.Equal(source2.OrderBy((string id) => id), zContext.RunContext.DefaultGroupApps.OrderBy((string id) => id));
			Assert.Equal(source3.OrderBy((string id) => id), zContext.RunContext.NotifyAppMap.Keys.OrderBy((string id) => id));
			Assert.False(zContext.RunContext.IsAppNeedNotify("notify"));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task OneDragonApp_RunsConfiguredGroupApplicationsInOrder()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			List<string> executionOrder = new List<string>();
			TestFactory first = new TestFactory(context, "daily-first", "每日一", delegate
			{
				executionOrder.Add("daily-first");
				return Task.FromResult(new OperationResult(IsSuccess: true, "first-ok"));
			});
			TestFactory second = new TestFactory(context, "daily-second", "每日二", delegate
			{
				executionOrder.Add("daily-second");
				return Task.FromResult(new OperationResult(IsSuccess: true, "second-ok"));
			});
			context.ApplicationFactoryRegistry.RegisterOneDragonApplication();
			context.ApplicationFactoryRegistry.RegisterApplications(new IApplicationFactory[2] { first, second }, defaultGroup: true);
			WriteOneDragonConfig(rootDirectory, "仅运行当前", (3, true, true));
			WriteGroupConfig(rootDirectory, 3, ("daily-first", true), ("daily-second", true));
			ZOneDragonApp app = (ZOneDragonApp)context.RunContext.GetApplication("one_dragon", 3, "one_dragon");
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部结束", result.Status);
			Assert.Equal<List<string>>(new List<string>(2) { "daily-first", "daily-second" }, executionOrder);
			ZOneDragonRunSummary summary = Assert.IsType<ZOneDragonRunSummary>(result.Data);
			Assert.Equal(3, summary.InstanceIndex);
			Assert.Equal("one_dragon", summary.GroupId);
			Assert.Equal(new string[2] { "daily-first", "daily-second" }, summary.Results.Select((ZOneDragonApplicationResult item) => item.AppId));
			Assert.All(summary.Results, delegate(ZOneDragonApplicationResult item)
			{
				Assert.True(item.IsSuccess);
			});
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task OneDragonApp_ContinuesConfiguredGroupWhenApplicationFails()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			List<string> executionOrder = new List<string>();
			TestFactory failed = new TestFactory(context, "daily-failed", "失败应用", delegate
			{
				executionOrder.Add("daily-failed");
				return Task.FromResult(new OperationResult(IsSuccess: false, "failed-status"));
			});
			TestFactory skipped = new TestFactory(context, "daily-skipped", "跳过应用", delegate
			{
				executionOrder.Add("daily-skipped");
				return Task.FromResult(new OperationResult(IsSuccess: true, "skipped"));
			});
			context.ApplicationFactoryRegistry.RegisterApplications(new IApplicationFactory[2] { failed, skipped }, defaultGroup: true);
			WriteOneDragonConfig(rootDirectory, "仅运行当前", (0, true, true));
			WriteGroupConfig(rootDirectory, 0, ("daily-failed", true), ("daily-skipped", true));
			ZOneDragonApp app = new ZOneDragonApp(context, 0);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部结束", result.Status);
			Assert.Equal<List<string>>(new List<string>(2) { "daily-failed", "daily-skipped" }, executionOrder);
			ZOneDragonRunSummary summary = Assert.IsType<ZOneDragonRunSummary>(result.Data);
			Assert.Equal(2, summary.Results.Count);
			Assert.False(summary.Results[0].IsSuccess);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task OneDragonApp_SelectsConfiguredActiveInstanceAndUsesItsGroupConfig()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			try
			{
				context.AttachController(new ReadyController());
				List<int> executionInstances = new List<int>();
				TestFactory application = new TestFactory(context, "instance-one", "实例一", delegate
				{
					executionInstances.Add(context.InstanceIndex);
					return Task.FromResult(new OperationResult(IsSuccess: true, "done"));
				});
				context.ApplicationFactoryRegistry.RegisterApplications(new IApplicationFactory[] { application }, defaultGroup: true);
				WriteOneDragonConfig(rootDirectory, "仅运行当前", (0, false, true), (1, true, true));
				WriteGroupConfig(rootDirectory, 1, ("instance-one", true));
				Assert.True((await new ZOneDragonApp(context, 0).ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
				Assert.Equal(new List<int>(1) { 1 }, executionInstances);
				Assert.Equal(1, context.InstanceIndex);
				string oneDragonYaml = File.ReadAllText(Path.Combine(rootDirectory, "config", "one_dragon.yml"));
				Assert.Contains("idx: 1", oneDragonYaml, StringComparison.Ordinal);
				Assert.Contains("active: true", oneDragonYaml, StringComparison.Ordinal);
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

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteOneDragonConfig(string rootDirectory, string instanceRun, params (int Index, bool Active, bool ActiveInOneDragon)[] instances)
	{
		string text = Path.Combine(rootDirectory, "config");
		Directory.CreateDirectory(text);
		string value = string.Join("\n", instances.Select(((int Index, bool Active, bool ActiveInOneDragon) instance) => $"- idx: {instance.Index}\n  name: '{instance.Index:00}'\n  active: {instance.Active.ToString().ToLowerInvariant()}\n  active_in_od: {instance.ActiveInOneDragon.ToString().ToLowerInvariant()}"));
		File.WriteAllText(Path.Combine(text, "one_dragon.yml"), $"instance_list:\n{value}\ninstance_run: {instanceRun}\n");
	}

	private static void WriteGroupConfig(string rootDirectory, int instanceIndex, params (string AppId, bool Enabled)[] apps)
	{
		string text = Path.Combine(rootDirectory, "config", instanceIndex.ToString("00"), "one_dragon");
		Directory.CreateDirectory(text);
		string text2 = string.Join("\n", apps.Select(((string AppId, bool Enabled) app) => "- app_id: " + app.AppId + "\n  enabled: " + app.Enabled.ToString().ToLowerInvariant()));
		File.WriteAllText(Path.Combine(text, "_group.yml"), "app_list:\n" + text2 + "\n");
	}

	private static void RegisterAllBuiltInApplications(ApplicationFactoryRegistry registry)
	{
		registry.RegisterOneDragonApplication();
		registry.RegisterAutoBattleApplication();
		registry.RegisterDodgeAssistantApplication();
		registry.RegisterScreenshotHelperApplication();
		registry.RegisterOperationDebugApplication();
		registry.RegisterEmailApplication();
		registry.RegisterChargePlanApplication();
		registry.RegisterCoffeeApplication();
		registry.RegisterCommissionAssistantApplication();
		registry.RegisterCityFundApplication();
		registry.RegisterScratchCardApplication();
		registry.RegisterEngagementRewardApplication();
		registry.RegisterRedemptionCodeApplication();
		registry.RegisterNotoriousHuntApplication();
		registry.RegisterShiyuDefenseApplication();
		registry.RegisterRiduWeeklyApplication();
		registry.RegisterIntelBoardApplication();
		registry.RegisterDriveDiscDismantleApplication();
		registry.RegisterPredefinedTeamCheckerApplication();
		registry.RegisterMouseSensitivityCheckerApplication();
		registry.RegisterHouHouBakeryApplication();
		registry.RegisterLifeOnLineApplication();
		registry.RegisterNotifyApplication();
		registry.RegisterRandomPlayApplication();
		registry.RegisterTrigramsCollectionApplication();
		registry.RegisterSuibianTempleApplication();
		registry.RegisterWorldPatrolApplication();
		registry.RegisterWitheredDomainApplication();
		registry.RegisterLostVoidApplication();
	}
}
