using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.ScratchCard;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class ScratchCardAppTests
{
	private sealed class RecordingScratchCardFlow : IScratchCardAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "刮刮卡完成"));
		}
	}

	private sealed class ScopeAssertingScratchCardFlow : IScratchCardAppFlow
	{
		public bool SawScopedScreen { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			SawScopedScreen = context.ScreenContext.ActiveScreenNames?.Contains("报刊亭") ?? false;
			return Task.FromResult(new OperationResult(IsSuccess: true));
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesScratchCardApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			ScratchCardFactory scratchCardFactory = zContext.ApplicationFactoryRegistry.CreateScratchCardFactory();
			IApplication application = scratchCardFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = scratchCardFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = scratchCardFactory.GetRunRecord(0);
			Assert.Equal("scratch_card", scratchCardFactory.AppId);
			Assert.Equal("刮刮卡", scratchCardFactory.AppName);
			Assert.Equal("one_dragon", scratchCardFactory.GroupId);
			Assert.True(scratchCardFactory.NeedNotify);
			Assert.IsType<ScratchCardApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			ScratchCardRunRecord scratchCardRunRecord = Assert.IsType<ScratchCardRunRecord>(runRecord);
			Assert.Equal("scratch_card", scratchCardRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersScratchCardAsNonDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterScratchCardApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("scratch_card"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("scratch_card"));
			Assert.DoesNotContain("scratch_card", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task ScratchCardApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingScratchCardFlow flow = new RecordingScratchCardFlow();
			ScratchCardRunRecord runRecord = new ScratchCardRunRecord();
			ScratchCardApp app = new ScratchCardApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("刮刮卡完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScratchCardApp_EntersAndExitsScreenScopeAroundFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			string screenDirectory = Path.Combine(rootDirectory, "screens");
			Directory.CreateDirectory(screenDirectory);
			File.WriteAllText(Path.Combine(screenDirectory, "global.yml"), "screen_id: menu\nscreen_name: 菜单\narea_list: []\n");
			File.WriteAllText(Path.Combine(screenDirectory, "scratch_card.yml"), "screen_id: news_stand\nscreen_name: 报刊亭\napp_id: scratch_card\narea_list: []\n");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.ScreenContext.LoadExtraScreenDir(screenDirectory);
			context.AttachController(new ReadyController());
			ScopeAssertingScratchCardFlow flow = new ScopeAssertingScratchCardFlow();
			ScratchCardApp app = new ScratchCardApp(context, new ScratchCardRunRecord(), flow);
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
	public void ScratchCardRunRecord_UsesScratchCardAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		ScratchCardRunRecord scratchCardRunRecord = new ScratchCardRunRecord(4, () => now);
		scratchCardRunRecord.UpdateStatus(1);
		Assert.Equal("scratch_card", scratchCardRunRecord.AppId);
		Assert.Equal("20260706", scratchCardRunRecord.Dt);
		Assert.True(scratchCardRunRecord.IsDone);
	}

	[Fact]
	public async Task ScratchCardOperation_UsesInjectedTransportWaitBackAndMoveActions()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			int transportCount = 0;
			int waitCount = 0;
			int backCount = 0;
			int moveCount = 0;
			ScratchCardOperation operation = new ScratchCardOperation(context, delegate
			{
				transportCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "传送完成"));
			}, delegate
			{
				waitCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
			}, delegate
			{
				backCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
			}, delegate
			{
				moveCount++;
				return new OperationResult(IsSuccess: true);
			});
			OperationRoundResult transport = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult wait = await operation.WaitWorld().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult move = operation.MoveAndInteract();
			OperationRoundResult back = await operation.BackToWorld().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(transport.IsSuccess);
			Assert.Equal("传送完成", transport.Status);
			Assert.True(wait.IsSuccess);
			Assert.Equal("大世界", wait.Status);
			Assert.True(move.IsSuccess);
			Assert.True(back.IsSuccess);
			Assert.Equal("返回大世界", back.Status);
			Assert.Equal(1, transportCount);
			Assert.Equal(1, waitCount);
			Assert.Equal(1, moveCount);
			Assert.Equal(1, backCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void ScratchCardOperation_MoveFailureDoesNotReturnSuccess()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			ScratchCardOperation scratchCardOperation = new ScratchCardOperation(zContext, null, null, null, (ZContext _) => new OperationResult(IsSuccess: false, "控制器不支持前台键鼠移动交互"));
			OperationRoundResult operationRoundResult = scratchCardOperation.MoveAndInteract();
			Assert.False(operationRoundResult.IsSuccess);
			Assert.Equal("控制器不支持前台键鼠移动交互", operationRoundResult.Status);
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
