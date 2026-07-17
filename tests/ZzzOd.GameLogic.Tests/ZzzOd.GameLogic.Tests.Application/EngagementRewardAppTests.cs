using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.EngagementReward;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class EngagementRewardAppTests
{
	private sealed class RecordingEngagementRewardFlow : IEngagementRewardAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "日常奖励领取成功"));
		}
	}

	private sealed class WindowReadyController : ControllerBase
	{
		public override bool IsGameWindowReady => true;

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
			return null;
		}
	}

	private sealed class RecordingController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(220, 140), MatType.CV_8UC3, Scalar.Black);

		public int ClickCount { get; private set; }

		public override bool IsGameWindowReady => true;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			ClickCount++;
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

		public void Dispose()
		{
			_screenshot.Dispose();
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return _screenshot.Clone();
		}
	}

	private sealed class FakeOcrMatcher(IReadOnlyList<OcrMatchResult> results) : IOcrMatcher
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
			return string.Concat(from result in results
				orderby result.Y, result.X
				select result.Text);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in Ocr(image, threshold.GetValueOrDefault(), mergeLineDistance))
			{
				if (!dictionary.TryGetValue(item.Text, out var value))
				{
					value = new MatchResultList(onlyBest: false);
					dictionary[item.Text] = value;
				}
				value.Append(item, autoMerge: false);
			}
			return dictionary;
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return results.Select((OcrMatchResult result) => new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesEngagementRewardApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			EngagementRewardAppFactory engagementRewardAppFactory = zContext.ApplicationFactoryRegistry.CreateEngagementRewardFactory();
			IApplication application = engagementRewardAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = engagementRewardAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = engagementRewardAppFactory.GetRunRecord(0);
			Assert.Equal("engagement_reward", engagementRewardAppFactory.AppId);
			Assert.Equal("活跃度奖励", engagementRewardAppFactory.AppName);
			Assert.Equal("one_dragon", engagementRewardAppFactory.GroupId);
			Assert.True(engagementRewardAppFactory.NeedNotify);
			Assert.IsType<EngagementRewardApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			EngagementRewardRunRecord engagementRewardRunRecord = Assert.IsType<EngagementRewardRunRecord>(runRecord);
			Assert.Equal("engagement_reward", engagementRewardRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersEngagementRewardAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ApplicationFactoryRegistry.RegisterEngagementRewardApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("engagement_reward"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("engagement_reward"));
			Assert.Contains("engagement_reward", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task EngagementRewardApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new WindowReadyController());
			RecordingEngagementRewardFlow flow = new RecordingEngagementRewardFlow();
			EngagementRewardRunRecord runRecord = new EngagementRewardRunRecord();
			EngagementRewardApp app = new EngagementRewardApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("日常奖励领取成功", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void EngagementRewardRunRecord_UsesAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		EngagementRewardRunRecord engagementRewardRunRecord = new EngagementRewardRunRecord(4, () => now);
		engagementRewardRunRecord.UpdateStatus(1);
		Assert.Equal("engagement_reward", engagementRewardRunRecord.AppId);
		Assert.Equal("20260706", engagementRewardRunRecord.Dt);
		Assert.True(engagementRewardRunRecord.IsDone);
	}

	[Fact]
	public async Task EngagementRewardOperation_UsesInjectedBackToNormalWorld()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			int backCount = 0;
			EngagementRewardOperation operation = new EngagementRewardOperation(context, delegate
			{
				backCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
			});
			OperationRoundResult first = await operation.BackAtFirst().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult afterwards = await operation.BackAfterwards().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(first.IsSuccess);
			Assert.Equal("返回大世界", first.Status);
			Assert.False(afterwards.IsSuccess);
			Assert.Null(afterwards.Status);
			Assert.Equal(2, backCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task EngagementRewardOperation_FailsAfterwardsWhenEngagementCheckFailsEvenIfBackSucceeds()
	{
		OpenCvTestRuntime.RequireAvailable();
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteEngagementRewardScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.ScreenContext.Reload();
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[4]
			{
				new OcrMatchResult(0.99, 2, 2, 20, 8, "日常"),
				new OcrMatchResult(0.99, 12, 2, 40, 16, "今日最大活跃度"),
				new OcrMatchResult(0.99, 14, 4, 60, 16, "奖励预览"),
				new OcrMatchResult(0.99, 22, 12, 20, 14, "关闭")
			});
			using RecordingController controller = new RecordingController();
			context.AttachController(controller);
			int backCount = 0;
			EngagementRewardOperation operation = new EngagementRewardOperation(context, delegate
			{
				backCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
			});
			OperationResult result = await operation.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(8L));
			Assert.False(result.IsSuccess);
			Assert.Null(result.Status);
			Assert.Equal(2, backCount);
			Assert.Equal(2, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void EngagementRewardOperation_CheckRewardPreservesClickSuccessDelay()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			WriteEngagementRewardScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 2, 2, 20, 8, "确认") });
			using RecordingController controller = new RecordingController();
			zContext.AttachController(controller);
			EngagementRewardOperation engagementRewardOperation = new EngagementRewardOperation(zContext);
			CaptureOnce(engagementRewardOperation);
			OperationRoundResult operationRoundResult = engagementRewardOperation.CheckReward();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("日常奖励领取成功", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void EngagementRewardOperation_CheckRewardPreservesPreviewCloseDelay()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			WriteEngagementRewardScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.ScreenContext.Reload();
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 2, 2, 40, 16, "奖励预览"),
				new OcrMatchResult(0.99, 2, 2, 20, 14, "关闭")
			});
			using RecordingController controller = new RecordingController();
			zContext.AttachController(controller);
			EngagementRewardOperation engagementRewardOperation = new EngagementRewardOperation(zContext);
			CaptureOnce(engagementRewardOperation);
			OperationRoundResult operationRoundResult = engagementRewardOperation.CheckReward();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("日常奖励已领取或活跃度未满", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void EngagementReward_FixedPreviewScreenshotFindsCompletedRewardTemplate()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = FindRepoRoot();
		string[] buffer = new string[7];
		buffer[0] = text;
		buffer[1] = "zzzod-dotnet";
		buffer[2] = "tests";
		buffer[3] = "ZzzOd.GameLogic.Tests";
		buffer[4] = "TestData";
		buffer[5] = "EngagementReward";
		buffer[6] = "active-reward-preview.png";
		string fileName = Path.Combine(buffer);
		using ZContext zContext = new ZContext(new OneDragonEnvironment(Path.GetTempPath(), text));
		zContext.ScreenContext.Reload();
		using Mat screen = Cv2.ImRead(fileName);
		FindAreaResultEnum actual = ScreenUtils.FindArea(zContext, screen, "快捷手册", "活跃度奖励-4");
		MatchResult matchResult = ScreenUtils.FindTemplateCoordInArea(zContext, screen, "快捷手册", "活跃度奖励-4");
		Assert.Equal(FindAreaResultEnum.True, actual);
		Assert.NotNull(matchResult);
		Assert.True(matchResult.Confidence >= 0.7);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "assets")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 仓库根目录。");
	}

	private static void WriteEngagementRewardScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: compendium_daily\n  screen_name: 快捷手册-日常\n  area_list:\n    - area_name: 日常标识\n      id_mark: true\n      pc_rect: [0, 0, 40, 20]\n      text: 日常\n      lcs_percent: 0.6\n- screen_id: compendium\n  screen_name: 快捷手册\n  area_list:\n    - area_name: 今日最大活跃度\n      pc_rect: [100, 20, 180, 60]\n      text: 今日最大活跃度\n      lcs_percent: 0.6\n    - area_name: 活跃度奖励-确认\n      pc_rect: [0, 0, 80, 40]\n      text: 确认\n      lcs_percent: 0.6\n    - area_name: 活跃度奖励-奖励预览\n      pc_rect: [0, 40, 100, 80]\n      text: 奖励预览\n      lcs_percent: 0.6\n    - area_name: 活跃度奖励-4\n      pc_rect: [120, 80, 180, 120]\n      text: FULL_MARKER\n      lcs_percent: 0.6\n- screen_id: common_screen\n  screen_name: 画面-通用\n  area_list:\n    - area_name: 关闭\n      pc_rect: [40, 50, 100, 90]\n      text: 关闭\n      lcs_percent: 0.6");
	}

	private static void CaptureOnce(EngagementRewardOperation operation)
	{
		MethodInfo methodInfo = typeof(EngagementRewardOperation).BaseType?.GetMethod("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(methodInfo);
		methodInfo.Invoke(operation, new object[1] { false });
	}
}
