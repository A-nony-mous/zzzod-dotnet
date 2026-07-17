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
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.CityFund;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class CityFundAppTests
{
	private sealed class RecordingCityFundFlow : ICityFundAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "丽都城募领取完成"));
		}
	}

	private sealed class ScopeAssertingCityFundFlow : ICityFundAppFlow
	{
		public bool SawScopedScreen { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			SawScopedScreen = context.ScreenContext.ActiveScreenNames?.Contains("丽都城募") ?? false;
			return Task.FromResult(new OperationResult(IsSuccess: true));
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesCityFundApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			CityFundAppFactory cityFundAppFactory = zContext.ApplicationFactoryRegistry.CreateCityFundFactory();
			IApplication application = cityFundAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = cityFundAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = cityFundAppFactory.GetRunRecord(0);
			Assert.Equal("city_fund", cityFundAppFactory.AppId);
			Assert.Equal("丽都城募", cityFundAppFactory.AppName);
			Assert.Equal("one_dragon", cityFundAppFactory.GroupId);
			Assert.True(cityFundAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<CityFundApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			CityFundRunRecord cityFundRunRecord = Assert.IsType<CityFundRunRecord>(runRecord);
			Assert.Equal("city_fund", cityFundRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersCityFundAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterCityFundApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("city_fund"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("city_fund"));
			Assert.Contains("city_fund", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task CityFundApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingCityFundFlow flow = new RecordingCityFundFlow();
			CityFundRunRecord runRecord = new CityFundRunRecord();
			CityFundApp app = new CityFundApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("丽都城募领取完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task CityFundApp_EntersAndExitsScreenScopeAroundFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			string screenDirectory = Path.Combine(rootDirectory, "screens");
			Directory.CreateDirectory(screenDirectory);
			File.WriteAllText(Path.Combine(screenDirectory, "global.yml"), "screen_id: menu\nscreen_name: 菜单\narea_list: []\n");
			File.WriteAllText(Path.Combine(screenDirectory, "city_fund.yml"), "screen_id: city_fund\nscreen_name: 丽都城募\napp_id: city_fund\narea_list: []\n");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.ScreenContext.LoadExtraScreenDir(screenDirectory);
			context.AttachController(new ReadyController());
			ScopeAssertingCityFundFlow flow = new ScopeAssertingCityFundFlow();
			CityFundApp app = new CityFundApp(context, new CityFundRunRecord(), flow);
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
	public async Task OperationCityFundAppFlow_UsesInjectedOperationExecutor()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.AttachController(new ReadyController());
		ZContext receivedContext = null;
		CancellationToken receivedToken = default(CancellationToken);
		using CancellationTokenSource cts = new CancellationTokenSource();
		OperationCityFundAppFlow flow = new OperationCityFundAppFlow(delegate(ZContext ctx, CancellationToken token)
		{
			receivedContext = ctx;
			receivedToken = token;
			return Task.FromResult(new OperationResult(IsSuccess: true, "operation-ok"));
		});
		OperationResult result = await flow.RunAsync(context, cts.Token);
		Assert.True(result.IsSuccess);
		Assert.Equal("operation-ok", result.Status);
		Assert.Same(context, receivedContext);
		Assert.Equal(cts.Token, receivedToken);
	}

	[Fact]
	public void CityFundRunRecord_UsesCityFundAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		CityFundRunRecord cityFundRunRecord = new CityFundRunRecord(4, () => now);
		cityFundRunRecord.UpdateStatus(1);
		Assert.Equal("city_fund", cityFundRunRecord.AppId);
		Assert.Equal("20260706", cityFundRunRecord.Dt);
		Assert.True(cityFundRunRecord.IsDone);
	}

	[Fact]
	public void CityFundOperation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(CityFundOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[7] { "打开菜单", "点击丽都城募", "点击成长任务", "任务全部领取", "点击等级回馈", "等级全部领取", "返回大世界" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["打开菜单"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["点击成长任务"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "点击成长任务" && edge.Status == "按钮-确认");
		Assert.Contains(readOnlyDictionary["返回大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "等级全部领取" && !edge.Success);
		Assert.Contains(readOnlyDictionary["等级全部领取"].GetCustomAttributes<OperationNodeNotifyAttribute>(), (OperationNodeNotifyAttribute annotation) => annotation.Timing == OperationNodeNotifyTiming.CurrentSuccess);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
