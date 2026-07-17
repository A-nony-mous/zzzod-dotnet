using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.RiduWeekly;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class RiduWeeklyAppTests
{
	private sealed class RecordingRiduWeeklyFlow : IRiduWeeklyAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "丽都周纪领取完成"));
		}
	}

	private sealed class ScoreCandidateController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.Black);

		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPoint { get; private set; }

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			LastClickPoint = position;
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

	private sealed class ScoreRowOcrMatcher(IReadOnlyList<string> candidates) : IOcrMatcher
	{
		private int _candidateIndex;

		public List<int> CroppedHeights { get; } = new List<int>();

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
			return string.Concat(from result in Ocr(image, threshold.GetValueOrDefault())
				select result.Text);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			IReadOnlyList<OcrMatchResult> source = Ocr(image, threshold.GetValueOrDefault(), mergeLineDistance);
			return source.ToDictionary<OcrMatchResult, string, MatchResultList>((OcrMatchResult result) => result.Text, delegate(OcrMatchResult result)
			{
				MatchResultList matchResultList = new MatchResultList(onlyBest: false);
				matchResultList.Append(result, autoMerge: false);
				return matchResultList;
			}, StringComparer.Ordinal);
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			CroppedHeights.Add(image.Height);
			if (_candidateIndex >= candidates.Count)
			{
				return Array.Empty<OcrMatchResult>();
			}
			string text = candidates[_candidateIndex++];
			return new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 20, 10, text) };
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesRiduWeeklyApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RiduWeeklyAppFactory riduWeeklyAppFactory = zContext.ApplicationFactoryRegistry.CreateRiduWeeklyFactory();
			IApplication application = riduWeeklyAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = riduWeeklyAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = riduWeeklyAppFactory.GetRunRecord(0);
			Assert.Equal("ridu_weekly", riduWeeklyAppFactory.AppId);
			Assert.Equal("丽都周纪 (领奖励)", riduWeeklyAppFactory.AppName);
			Assert.Equal("one_dragon", riduWeeklyAppFactory.GroupId);
			Assert.True(riduWeeklyAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<RiduWeeklyApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			RiduWeeklyRunRecord riduWeeklyRunRecord = Assert.IsType<RiduWeeklyRunRecord>(runRecord);
			Assert.Equal("ridu_weekly", riduWeeklyRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersRiduWeeklyAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterRiduWeeklyApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("ridu_weekly"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("ridu_weekly"));
			Assert.Contains("ridu_weekly", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task RiduWeeklyApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingRiduWeeklyFlow flow = new RecordingRiduWeeklyFlow();
			RiduWeeklyRunRecord runRecord = new RiduWeeklyRunRecord();
			RiduWeeklyApp app = new RiduWeeklyApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("丽都周纪领取完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task OperationRiduWeeklyAppFlow_UsesInjectedOperationExecutor()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		context.AttachController(new ReadyController());
		ZContext receivedContext = null;
		CancellationToken receivedToken = default(CancellationToken);
		using CancellationTokenSource cts = new CancellationTokenSource();
		OperationRiduWeeklyAppFlow flow = new OperationRiduWeeklyAppFlow(delegate(ZContext ctx, CancellationToken token)
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
	public void RiduWeeklyRunRecord_UsesAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		RiduWeeklyRunRecord riduWeeklyRunRecord = new RiduWeeklyRunRecord(4, () => now);
		riduWeeklyRunRecord.UpdateStatus(1);
		Assert.Equal("ridu_weekly", riduWeeklyRunRecord.AppId);
		Assert.Equal("20260706", riduWeeklyRunRecord.Dt);
		Assert.True(riduWeeklyRunRecord.IsDone);
	}

	[Fact]
	public async Task RiduWeeklyOperation_UsesInjectedBackToNormalWorld()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			int backCount = 0;
			RiduWeeklyOperation operation = new RiduWeeklyOperation(context, delegate
			{
				backCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
			});
			OperationRoundResult first = await operation.BackAtFirst().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult finish = await operation.Finish().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(first.IsSuccess);
			Assert.Equal("返回大世界", first.Status);
			Assert.True(finish.IsSuccess);
			Assert.Equal("返回大世界", finish.Status);
			Assert.Equal(2, backCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void RiduWeeklyOperation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(RiduWeeklyOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[6] { "返回大世界", "日常", "丽都周纪", "领取积分", "领取奖励", "完成后返回" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["返回大世界"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["领取奖励"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "领取积分" && !edge.Success);
		Assert.Contains(readOnlyDictionary["完成后返回"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "领取奖励");
	}

	[Fact]
	public void RiduWeeklyOperation_ClaimScoreUsesFirstExact100AcrossThreeRows()
	{
		OpenCvTestRuntime.RequireAvailable();
		string resourceDirectory = FindRepoRoot();
		using ZContext zContext = new ZContext(new OneDragonEnvironment(Path.GetTempPath(), resourceDirectory));
		zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
		ScoreRowOcrMatcher scoreRowOcrMatcher = new ScoreRowOcrMatcher(new string[3] { "10O", "100", "100" });
		zContext.OcrService.Matcher = scoreRowOcrMatcher;
		using ScoreCandidateController scoreCandidateController = new ScoreCandidateController();
		zContext.AttachController(scoreCandidateController);
		RiduWeeklyOperation riduWeeklyOperation = new RiduWeeklyOperation(zContext);
		CaptureOnce(riduWeeklyOperation);
		OperationRoundResult operationRoundResult = riduWeeklyOperation.ClaimScore();
		Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
		Assert.Equal("100", operationRoundResult.Status);
		Assert.Equal(TimeSpan.FromMilliseconds(500L), operationRoundResult.Delay);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(140, 655), scoreCandidateController.LastClickPoint);
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = 60;
		span[1] = 50;
		Assert.Equal(list, scoreRowOcrMatcher.CroppedHeights);
	}

	[Fact]
	public void RiduWeeklyOperation_ClaimScoreRejectsNear100AcrossAllThreeRows()
	{
		OpenCvTestRuntime.RequireAvailable();
		string resourceDirectory = FindRepoRoot();
		using ZContext zContext = new ZContext(new OneDragonEnvironment(Path.GetTempPath(), resourceDirectory));
		zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
		ScoreRowOcrMatcher scoreRowOcrMatcher = new ScoreRowOcrMatcher(new string[3] { "10O", "10O", "10O" });
		zContext.OcrService.Matcher = scoreRowOcrMatcher;
		using ScoreCandidateController scoreCandidateController = new ScoreCandidateController();
		zContext.AttachController(scoreCandidateController);
		RiduWeeklyOperation riduWeeklyOperation = new RiduWeeklyOperation(zContext);
		CaptureOnce(riduWeeklyOperation);
		OperationRoundResult operationRoundResult = riduWeeklyOperation.ClaimScore();
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("找不到 100", operationRoundResult.Status);
		Assert.Equal(TimeSpan.FromMilliseconds(500L), operationRoundResult.Delay);
		Assert.Null(scoreCandidateController.LastClickPoint);
		int num = 3;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = 60;
		span[1] = 50;
		span[2] = 55;
		Assert.Equal(list, scoreRowOcrMatcher.CroppedHeights);
	}

	[Fact]
	public async Task RiduWeeklyOperation_ClaimRewardThenPropagatesBackToNormalWorldFailure()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(resourceDirectory: FindRepoRoot(), workDirectory: Path.GetTempPath()));
		context.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
		using ScoreCandidateController controller = new ScoreCandidateController();
		context.AttachController(controller);
		int backCount = 0;
		RiduWeeklyOperation operation = new RiduWeeklyOperation(context, delegate
		{
			backCount++;
			return Task.FromResult(new OperationResult(IsSuccess: false, "返回失败"));
		});
		OperationRoundResult reward = operation.ClaimReward();
		OperationRoundResult finish = await operation.Finish().WaitAsync(TimeSpan.FromSeconds(2L));
		Assert.Equal(OperationRoundResultKind.Success, reward.Kind);
		Assert.Equal(TimeSpan.FromSeconds(1L), reward.Delay);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1558, 314), controller.LastClickPoint);
		Assert.Equal(OperationRoundResultKind.Fail, finish.Kind);
		Assert.Equal("返回失败", finish.Status);
		Assert.Equal(1, backCount);
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
			string[] buffer = new string[6];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "zzzod-dotnet";
			buffer[2] = "assets";
			buffer[3] = "game_data";
			buffer[4] = "screen_info";
			buffer[5] = "ridu_weekly.yml";
			if (File.Exists(Path.Combine(buffer)))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到含丽都周纪区域配置的仓库根目录。");
	}

	private static void CaptureOnce(RiduWeeklyOperation operation)
	{
		MethodInfo methodInfo = typeof(RiduWeeklyOperation).BaseType?.GetMethod("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(methodInfo);
		methodInfo.Invoke(operation, new object[1] { false });
	}
}
