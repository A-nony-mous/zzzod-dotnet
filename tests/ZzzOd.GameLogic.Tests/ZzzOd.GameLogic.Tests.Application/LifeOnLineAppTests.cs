using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.LifeOnLine;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class LifeOnLineAppTests
{
	private sealed class RecordingLifeOnLineFlow : ILifeOnLineAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, LifeOnLineConfig config, LifeOnLineRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "完成指定次数"));
		}
	}

	private sealed class ScopeAssertingLifeOnLineFlow : ILifeOnLineAppFlow
	{
		public bool SawScopedScreen { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, LifeOnLineConfig config, LifeOnLineRunRecord runRecord, CancellationToken cancellationToken)
		{
			SawScopedScreen = context.ScreenContext.ActiveScreenNames?.Contains("真拿命验收") ?? false;
			return Task.FromResult(new OperationResult(IsSuccess: true, "完成指定次数"));
		}
	}

	private sealed class RecordingLifeOnLineServices : ILifeOnLineOperationServices
	{
		public bool HddStreetVisible { get; set; }

		public bool BattleScreenReady { get; set; }

		public bool DialogPersonVisible { get; set; }

		public bool BattleResultCompleteVisible { get; set; }

		public bool WaitWorldOnceSuccess { get; set; }

		public List<int> EnteredTeamIndexes { get; } = new List<int>();

		public int TransportCount { get; private set; }

		public int KeySimCount { get; private set; }

		public int ClickBattleResultCompleteCount { get; private set; }

		public int WaitWorldOnceCount { get; private set; }

		public Task<OperationResult> TransportToHddAsync(ZContext context)
		{
			TransportCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "HDD"));
		}

		public Task<OperationResult> WaitNormalWorldAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}

		public bool IsHddStreetVisible(ZContext context, Mat? screen)
		{
			return HddStreetVisible;
		}

		public void Interact(ZContext context)
		{
		}

		public Task<OperationResult> EnterMissionAsync(ZContext context, int predefinedTeamIndex)
		{
			EnteredTeamIndexes.Add(predefinedTeamIndex);
			return Task.FromResult(new OperationResult(IsSuccess: true, "真·拿命验收"));
		}

		public bool IsBattleScreenReady(ZContext context, Mat? screen)
		{
			return BattleScreenReady;
		}

		public Task<OperationResult> RunKeySimAsync(ZContext context)
		{
			KeySimCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "执行完成"));
		}

		public bool IsDialogPersonVisible(ZContext context, Mat? screen)
		{
			return DialogPersonVisible;
		}

		public bool IsBattleResultCompleteVisible(ZContext context, Mat? screen)
		{
			return BattleResultCompleteVisible;
		}

		public string? ClickFirstDialogOption(ZContext context, Mat? screen)
		{
			return null;
		}

		public OperationResult ClickMenuBack(ZContext context)
		{
			return new OperationResult(IsSuccess: true, "返回");
		}

		public OperationResult ClickBattleResultComplete(ZContext context, Mat? screen)
		{
			ClickBattleResultCompleteCount++;
			return new OperationResult(IsSuccess: false, "未找到 战斗结果-完成");
		}

		public Task<OperationResult> WaitNormalWorldOnceAsync(ZContext context)
		{
			WaitWorldOnceCount++;
			return Task.FromResult(new OperationResult(WaitWorldOnceSuccess, WaitWorldOnceSuccess ? "大世界-普通" : "未到达大世界"));
		}

		public OperationResult ClickHddBlank(ZContext context)
		{
			return new OperationResult(IsSuccess: true, "空白");
		}

		public Task<OperationResult> BackToWorldAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}

		public bool IsExitBattleVisible(ZContext context, Mat? screen)
		{
			return false;
		}

		public OperationResult ClickBattleMenu(ZContext context)
		{
			return new OperationResult(IsSuccess: true, "菜单");
		}

		public OperationResult ClickExitBattle(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: true, "退出战斗");
		}

		public OperationResult ClickExitBattleConfirm(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: true, "退出战斗-确认");
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesLifeOnLineApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			LifeOnLineAppFactory lifeOnLineAppFactory = zContext.ApplicationFactoryRegistry.CreateLifeOnLineFactory();
			IApplication application = lifeOnLineAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = lifeOnLineAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = lifeOnLineAppFactory.GetRunRecord(0);
			Assert.Equal("life_on_line", lifeOnLineAppFactory.AppId);
			Assert.Equal("真·拿命验收", lifeOnLineAppFactory.AppName);
			Assert.Equal("one_dragon", lifeOnLineAppFactory.GroupId);
			Assert.True(lifeOnLineAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<LifeOnLineApp>(application);
			LifeOnLineConfig lifeOnLineConfig = Assert.IsType<LifeOnLineConfig>(config);
			Assert.Equal(20, lifeOnLineConfig.DailyPlanTimes);
			Assert.Equal(-1, lifeOnLineConfig.PredefinedTeamIndex);
			LifeOnLineRunRecord lifeOnLineRunRecord = Assert.IsType<LifeOnLineRunRecord>(runRecord);
			Assert.Equal("life_on_line", lifeOnLineRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersLifeOnLineAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterLifeOnLineApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("life_on_line"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("life_on_line"));
			Assert.Contains("life_on_line", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Config_LoadsPythonFieldsAndSettingMetadata()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "life_on_line.yml"), "daily_plan_times: 12\npredefined_team_idx: 3");
			LifeOnLineConfig lifeOnLineConfig = LifeOnLineConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("life_on_line", lifeOnLineConfig.AppId);
			Assert.Equal(0, lifeOnLineConfig.InstanceIndex);
			Assert.Equal("one_dragon", lifeOnLineConfig.GroupId);
			Assert.Equal(12, lifeOnLineConfig.DailyPlanTimes);
			Assert.Equal(3, lifeOnLineConfig.PredefinedTeamIndex);
			Assert.Equal("FLYOUT", "FLYOUT");
			Assert.Contains((IEnumerable<LifeOnLineSettingField>)LifeOnLineSettings.Fields, (Predicate<LifeOnLineSettingField>)((LifeOnLineSettingField field) => field.Key == "daily_plan_times" && field.DefaultValue.Equals(20)));
			Assert.Contains((IEnumerable<LifeOnLineSettingField>)LifeOnLineSettings.Fields, (Predicate<LifeOnLineSettingField>)((LifeOnLineSettingField field) => field.Key == "predefined_team_idx" && field.DefaultValue.Equals(-1)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_LoadsAndPersistsDailyRunTimes()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "life_on_line.yml"), "dt: \"20260706\"\ndaily_run_times: 4");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			LifeOnLineConfig config = new LifeOnLineConfig
			{
				DailyPlanTimes = 5
			};
			LifeOnLineRunRecord lifeOnLineRunRecord = LifeOnLineRunRecord.Load(environment, 0, config);
			Assert.Equal(4, lifeOnLineRunRecord.DailyRunTimes);
			Assert.False(lifeOnLineRunRecord.IsFinishedByTimes());
			lifeOnLineRunRecord.AddTimes();
			LifeOnLineRunRecord lifeOnLineRunRecord2 = LifeOnLineRunRecord.Load(environment, 0, config);
			Assert.Equal(5, lifeOnLineRunRecord2.DailyRunTimes);
			Assert.True(lifeOnLineRunRecord2.IsFinishedByTimes());
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void LifeOnLineApp_DefaultConstructorLoadsExistingRunRecord()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "life_on_line.yml"), "daily_run_times: 7");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			LifeOnLineApp lifeOnLineApp = new LifeOnLineApp(zContext);
			LifeOnLineRunRecord lifeOnLineRunRecord = Assert.IsType<LifeOnLineRunRecord>(lifeOnLineApp.RunRecord);
			Assert.Equal(7, lifeOnLineRunRecord.DailyRunTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task LifeOnLineApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			LifeOnLineConfig config = new LifeOnLineConfig();
			LifeOnLineRunRecord runRecord = new LifeOnLineRunRecord(config);
			RecordingLifeOnLineFlow flow = new RecordingLifeOnLineFlow();
			LifeOnLineApp app = new LifeOnLineApp(context, config, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("完成指定次数", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task LifeOnLineApp_EntersAndExitsScreenScopeAroundFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			string screenDirectory = Path.Combine(rootDirectory, "screens");
			Directory.CreateDirectory(screenDirectory);
			File.WriteAllText(Path.Combine(screenDirectory, "global.yml"), "screen_id: global_menu\nscreen_name: 菜单\narea_list: []");
			File.WriteAllText(Path.Combine(screenDirectory, "life_on_line.yml"), "screen_id: life_on_line_dialog\nscreen_name: 真拿命验收\napp_id: life_on_line\narea_list: []");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.ScreenContext.LoadExtraScreenDir(screenDirectory);
			context.AttachController(new ReadyController());
			LifeOnLineConfig config = new LifeOnLineConfig();
			LifeOnLineRunRecord runRecord = new LifeOnLineRunRecord(config);
			ScopeAssertingLifeOnLineFlow flow = new ScopeAssertingLifeOnLineFlow();
			LifeOnLineApp app = new LifeOnLineApp(context, config, runRecord, flow);
			Assert.True((await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(flow.SawScopedScreen);
			Assert.Null(context.ScreenContext.ActiveScreenNames);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RunsInjectedMainFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			LifeOnLineConfig config = new LifeOnLineConfig
			{
				DailyPlanTimes = 3,
				PredefinedTeamIndex = 2
			};
			LifeOnLineRunRecord record = new LifeOnLineRunRecord(config);
			RecordingLifeOnLineServices services = new RecordingLifeOnLineServices
			{
				HddStreetVisible = true,
				BattleScreenReady = true,
				DialogPersonVisible = true,
				BattleResultCompleteVisible = true
			};
			LifeOnLineOperation operation = new LifeOnLineOperation(context, config, record, services);
			Assert.True((await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.WaitWorld().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(operation.Interact().IsSuccess);
			Assert.True((await operation.EnterMission().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			// 等待战斗画面加载改为直接查真实的"战斗画面/按键-普通攻击"区域，不再经过注入的 services。
			// 本用例没有配置该画面区域（也没有真实截图），因此识别不到，返回失败而不是成功；
			// _chosenTeam 的写入在识别之前发生，不受这里影响。
			OperationRoundResult waitBattleScreen = operation.WaitBattleScreen();
			Assert.False(waitBattleScreen.IsSuccess);
			Assert.True((await operation.RunKeySim().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(operation.InteractAfterMission().IsSuccess);
			Assert.True(operation.TalkAfterMission().IsSuccess);
			Assert.True((await operation.ClickFinished().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			OperationRoundResult check = operation.CheckTimes();
			Assert.True(check.IsSuccess);
			Assert.Equal("继续", check.Status);
			Assert.True((await operation.EnterMission().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(operation.ChosenTeam);
			Assert.False(operation.IsOverNight);
			Assert.Equal(1, record.DailyRunTimes);
			Assert.Equal(new List<int>(2) { 2, -1 }, services.EnteredTeamIndexes);
			Assert.Equal(1, services.TransportCount);
			Assert.Equal(1, services.KeySimCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_ClickFinishedTracksOvernightAndTimesCompletion()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			LifeOnLineConfig config = new LifeOnLineConfig
			{
				DailyPlanTimes = 2
			};
			LifeOnLineRunRecord record = new LifeOnLineRunRecord(config)
			{
				DailyRunTimes = 1
			};
			RecordingLifeOnLineServices services = new RecordingLifeOnLineServices
			{
				HddStreetVisible = false,
				WaitWorldOnceSuccess = true
			};
			LifeOnLineOperation operation = new LifeOnLineOperation(context, config, record, services);
			OperationRoundResult finished = await operation.ClickFinished().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult check = operation.CheckTimes();
			Assert.True(finished.IsSuccess);
			Assert.True(operation.IsOverNight);
			Assert.Equal(2, record.DailyRunTimes);
			Assert.True(check.IsSuccess);
			Assert.Equal("完成指定次数", check.Status);
			Assert.Equal(1, services.ClickBattleResultCompleteCount);
			Assert.Equal(1, services.WaitWorldOnceCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(LifeOnLineOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[14]
		{
			"传送", "等待加载", "交互", "进入副本", "等待战斗画面加载", "模拟按键", "通关交互", "对话", "完成", "检查运行次数",
			"返回大世界", "交互失败", "点击退出战斗", "点击退出战斗确认"
		}, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["传送"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(60, readOnlyDictionary["等待战斗画面加载"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(10, readOnlyDictionary["通关交互"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(30, readOnlyDictionary["对话"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(60, readOnlyDictionary["完成"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Contains(readOnlyDictionary["交互"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "检查运行次数" && edge.Status == "过夜后继续");
		Assert.Contains(readOnlyDictionary["进入副本"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "检查运行次数" && edge.Status == "继续");
		Assert.Contains(readOnlyDictionary["返回大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "检查运行次数" && edge.Status == "完成指定次数");
		Assert.Contains(readOnlyDictionary["交互失败"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "通关交互" && !edge.Success);
		Assert.Contains(readOnlyDictionary["检查运行次数"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "点击退出战斗确认");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
