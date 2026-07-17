using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.Notify;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class NotifyAppTests
{
	private sealed class TestFactory : IApplicationFactory
	{
		private readonly IApplicationRunRecord _runRecord;

		public string AppId { get; }

		public string AppName { get; }

		public bool NeedNotify { get; }

		public TestFactory(string appId, string appName, IApplicationRunRecord runRecord, bool needNotify)
		{
			AppId = appId;
			AppName = appName;
			NeedNotify = needNotify;
			_runRecord = runRecord;
		}

		public IApplication CreateApplication(int instanceIndex, string groupId)
		{
			throw new NotSupportedException();
		}

		public IApplicationConfig GetConfig(int instanceIndex, string groupId)
		{
			return new ZApplicationConfig();
		}

		public IApplicationRunRecord GetRunRecord(int instanceIndex)
		{
			return _runRecord;
		}
	}

	private sealed class RecordingPushNotificationService : IPushNotificationService
	{
		public OperationResult Result { get; set; } = new OperationResult(IsSuccess: true, "ok");

		public int PushCount { get; private set; }

		public string? Title { get; private set; }

		public string? Content { get; private set; }

		public Mat? Image { get; private set; }

		public Task<OperationResult> PushAsync(ZContext context, string title, string content, Mat? image, CancellationToken cancellationToken)
		{
			PushCount++;
			Title = title;
			Content = content;
			Image = image;
			return Task.FromResult(Result);
		}
	}

	private sealed class NotifyScreenshotController : ControllerBase, IDisposable
	{
		public Mat Screen { get; } = new Mat(16, 16, MatType.CV_8UC3, Scalar.Black);

		public bool LastScreenshotWasIndependent { get; private set; }

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			LastScreenshotWasIndependent = independent;
			return Screen;
		}

		public void Dispose()
		{
			Screen.Dispose();
			CleanupAfterAppShutdown();
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesNotifyApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			NotifyAppFactory notifyAppFactory = zContext.ApplicationFactoryRegistry.CreateNotifyFactory();
			IApplication application = notifyAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = notifyAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = notifyAppFactory.GetRunRecord(0);
			Assert.Equal("notify", notifyAppFactory.AppId);
			Assert.Equal("通知", notifyAppFactory.AppName);
			Assert.Equal("one_dragon", notifyAppFactory.GroupId);
			Assert.False(notifyAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<NotifyApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			NotifyRunRecord notifyRunRecord = Assert.IsType<NotifyRunRecord>(runRecord);
			Assert.Equal("notify", notifyRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersNotifyAsDefaultApplicationWithoutNotify()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterNotifyApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("notify"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("notify"));
			Assert.Contains("notify", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void NotifyRunRecord_ResetsEveryRunLikePython()
	{
		NotifyRunRecord notifyRunRecord = new NotifyRunRecord();
		notifyRunRecord.UpdateStatus(1);
		notifyRunRecord.CheckAndUpdateStatus();
		Assert.Equal(0, notifyRunRecord.RunStatus);
	}

	[Fact]
	public void Formatter_BuildsSuccessFailureAndChargePowerMessage()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
			ZApplicationRunRecord runRecord = new ZApplicationRunRecord("success", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 11:30",
				RunStatus = 1
			};
			ZApplicationRunRecord runRecord2 = new ZApplicationRunRecord("failure", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 10:30",
				RunStatus = 2
			};
			ZApplicationRunRecord runRecord3 = new ZApplicationRunRecord("old", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 08:00",
				RunStatus = 1
			};
			ChargePlanRunRecord obj = new ChargePlanRunRecord(0, () => now)
			{
				RunTime = "07-06 11:50",
				RunStatus = 1
			};
			int num = 2;
			List<int> list = new List<int>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<int> span = CollectionsMarshal.AsSpan(list);
			span[0] = 100;
			span[1] = (int)now.AddMinutes(-36.0).ToUnixTimeSeconds();
			obj.ChargePowerSnapshot = list;
			ChargePlanRunRecord runRecord4 = obj;
			zContext.RunContext.RegisterApplication(new TestFactory("success", "成功应用", runRecord, needNotify: true), defaultGroup: true);
			zContext.RunContext.RegisterApplication(new TestFactory("failure", "失败应用", runRecord2, needNotify: true), defaultGroup: true);
			zContext.RunContext.RegisterApplication(new TestFactory("old", "过期应用", runRecord3, needNotify: true), defaultGroup: true);
			zContext.RunContext.RegisterApplication(new TestFactory("charge_plan", "体力刷本", runRecord4, needNotify: true), defaultGroup: true);
			WriteOneDragonGroup(text, new string[4] { "success", "failure", "old", "charge_plan" });
			NotifyMessage notifyMessage = new NotifyMessageFormatter().Format(zContext, now);
			Assert.True(notifyMessage.HasFailure);
			Assert.Contains("一条龙运行完成", notifyMessage.Content, StringComparison.Ordinal);
			Assert.Contains("当前体力", notifyMessage.Content, StringComparison.Ordinal);
			Assert.Contains("失败指令", notifyMessage.Content, StringComparison.Ordinal);
			Assert.Contains("体力刷本", notifyMessage.Content, StringComparison.Ordinal);
			Assert.DoesNotContain("过期应用", notifyMessage.Content, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Formatter_ReturnsAllSuccessWhenNoFailure()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
			ZApplicationRunRecord runRecord = new ZApplicationRunRecord("success", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 11:30",
				RunStatus = 1
			};
			zContext.RunContext.RegisterApplication(new TestFactory("success", "成功应用", runRecord, needNotify: true), defaultGroup: true);
			WriteOneDragonGroup(text, new string[] { "success" });
			NotifyMessage notifyMessage = new NotifyMessageFormatter().Format(zContext, now);
			Assert.False(notifyMessage.HasFailure);
			Assert.Contains("全部成功", notifyMessage.Content, StringComparison.Ordinal);
			Assert.Contains("成功指令", notifyMessage.Content, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void IsWithinTime_MatchesPythonThreeHourWindow()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero);
		Assert.True(NotifyMessageFormatter.IsWithinTime("12-31 23:30", now));
		Assert.False(NotifyMessageFormatter.IsWithinTime("12-31 21:30", now));
		Assert.False(NotifyMessageFormatter.IsWithinTime("bad", now));
	}

	[Fact]
	public async Task NotifyApp_PublishesFormattedMessageAndFailsWhenAnyAppFailed()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
			ZApplicationRunRecord failure = new ZApplicationRunRecord("failure", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 11:30",
				RunStatus = 2
			};
			context.RunContext.RegisterApplication(new TestFactory("failure", "失败应用", failure, needNotify: true), defaultGroup: true);
			WriteOneDragonGroup(rootDirectory, new string[] { "failure" });
			WriteNotifyConfig(rootDirectory, "Python 通知标题");
			RecordingPushNotificationService pushService = new RecordingPushNotificationService();
			NotifyApp app = new NotifyApp(flow: new DefaultNotifyAppFlow(null, pushService, () => now, null, (TimeSpan _, CancellationToken _) => Task.CompletedTask), context: context, runRecord: new NotifyRunRecord(0, () => now));
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("存在失败指令", result.Status);
			Assert.Equal("Python 通知标题", pushService.Title);
			Assert.Contains("失败指令", pushService.Content, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NotifyApp_UsesPushServiceWithoutInitializingTelemetry()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
			ZApplicationRunRecord success = new ZApplicationRunRecord("success", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 11:30",
				RunStatus = 1
			};
			context.RunContext.RegisterApplication(new TestFactory("success", "成功应用", success, needNotify: true), defaultGroup: true);
			WriteOneDragonGroup(rootDirectory, new string[] { "success" });
			RecordingPushNotificationService pushService = new RecordingPushNotificationService();
			NotifyApp app = new NotifyApp(flow: new DefaultNotifyAppFlow(null, pushService, () => now, null, (TimeSpan _, CancellationToken _) => Task.CompletedTask), context: context, runRecord: new NotifyRunRecord(0, () => now));
			Assert.True((await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal(1, pushService.PushCount);
			Assert.Equal("一条龙运行通知", pushService.Title);
			Assert.Contains("成功指令", pushService.Content, StringComparison.Ordinal);
			Assert.False(context.Telemetry.IsInitialized);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Formatter_IncludesRecentDisabledApplicationLikePythonGroupIteration()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
			ZApplicationRunRecord runRecord = new ZApplicationRunRecord("disabled", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 11:30",
				RunStatus = 1
			};
			zContext.RunContext.RegisterApplication(new TestFactory("disabled", "已禁用应用", runRecord, needNotify: true), defaultGroup: true);
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "_group.yml"), "app_list:\n  - app_id: disabled\n    enabled: false\n");
			NotifyMessage notifyMessage = new NotifyMessageFormatter().Format(zContext, now);
			Assert.Contains("已禁用应用", notifyMessage.Content, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task NotifyApp_IgnoresPushFailureLikePythonWhenNoAppFailed()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
			ZApplicationRunRecord success = new ZApplicationRunRecord("success", 0, ZApplicationRunRecordPeriod.Daily, () => now)
			{
				RunTime = "07-06 11:30",
				RunStatus = 1
			};
			context.RunContext.RegisterApplication(new TestFactory("success", "成功应用", success, needNotify: true), defaultGroup: true);
			WriteOneDragonGroup(rootDirectory, new string[] { "success" });
			RecordingPushNotificationService pushService = new RecordingPushNotificationService
			{
				Result = new OperationResult(IsSuccess: false, "未配置第三方推送通道")
			};
			NotifyApp app = new NotifyApp(flow: new DefaultNotifyAppFlow(null, pushService, () => now, (ZContext _) => "自定义标题", (TimeSpan _, CancellationToken _) => Task.CompletedTask), context: context, runRecord: new NotifyRunRecord(0, () => now));
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("通知已发送", result.Status);
			Assert.Equal("自定义标题", pushService.Title);
			Assert.Equal(1, pushService.PushCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NotifyApp_PassesItsCurrentScreenshotAndWaitsFiveSeconds()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteOneDragonGroup(rootDirectory, Array.Empty<string>());
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using NotifyScreenshotController controller = new NotifyScreenshotController();
			context.AttachController(controller);
			RecordingPushNotificationService pushService = new RecordingPushNotificationService();
			TimeSpan? delayed = null;
			NotifyApp app = new NotifyApp(flow: new DefaultNotifyAppFlow(null, pushService, null, null, delegate(TimeSpan delay, CancellationToken _)
			{
				delayed = delay;
				return Task.CompletedTask;
			}), context: context, runRecord: new NotifyRunRecord(), needCheckGameWindow: false);
			Assert.True((await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Same(controller.Screen, pushService.Image);
			Assert.False(controller.LastScreenshotWasIndependent);
			Assert.Equal(TimeSpan.FromSeconds(5L), delayed);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task NotifyFlow_CancelsDuringPythonFiveSecondWait()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteOneDragonGroup(rootDirectory, Array.Empty<string>());
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DefaultNotifyAppFlow flow = new DefaultNotifyAppFlow(null, new RecordingPushNotificationService(), null, null, (TimeSpan _, CancellationToken cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			Task<OperationResult> task = flow.RunAsync(context, null, cancellation.Token);
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static void WriteOneDragonGroup(string rootDirectory, IReadOnlyList<string> enabledAppIds)
	{
		string text = Path.Combine(rootDirectory, "config", "00", "one_dragon");
		Directory.CreateDirectory(text);
		string text2 = string.Join(Environment.NewLine, enabledAppIds.Select((string appId) => "  - app_id: " + appId + Environment.NewLine + "    enabled: true"));
		string contents = ((enabledAppIds.Count == 0) ? ("app_list: []" + Environment.NewLine) : ("app_list:" + Environment.NewLine + text2 + Environment.NewLine));
		File.WriteAllText(Path.Combine(text, "_group.yml"), contents);
	}

	private static void WriteNotifyConfig(string rootDirectory, string title)
	{
		string text = Path.Combine(rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "notify.yml"), "title: " + title + Environment.NewLine);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
