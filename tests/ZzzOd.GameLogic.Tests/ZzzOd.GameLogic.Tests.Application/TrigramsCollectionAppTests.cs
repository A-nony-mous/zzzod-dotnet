using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.TrigramsCollection;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class TrigramsCollectionAppTests
{
	private sealed class RecordingTrigramsCollectionFlow : ITrigramsCollectionFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "卦象集录完成"));
		}
	}

	private sealed class RecordingTrigramsCollectionServices : ITrigramsCollectionOperationServices
	{
		public TrigramOcrMatch? NextMatch { get; set; }

		public int TransportCount { get; private set; }

		public int InteractCount { get; private set; }

		public int ClickGetTrigramCount { get; private set; }

		public int DragCount { get; private set; }

		public int BackCount { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> ConfirmClicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public OperationResult ClickGetTrigramResult { get; set; } = new OperationResult(IsSuccess: true, "区域-获取卦象");

		public OperationResult ClickConfirmResult { get; set; } = new OperationResult(IsSuccess: true, "确认");

		public Task<OperationResult> TransportAsync(ZContext context)
		{
			TransportCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "阿朔"));
		}

		public OperationResult Interact(ZContext context)
		{
			InteractCount++;
			return new OperationResult(IsSuccess: true);
		}

		public Task<TrigramOcrMatch?> ReadPriorityTextAsync(ZContext context, Mat? screen, IReadOnlyList<string> priorityWords)
		{
			return Task.FromResult(NextMatch);
		}

		public Task<OperationResult> ClickGetTrigramAsync(ZContext context)
		{
			ClickGetTrigramCount++;
			return Task.FromResult(ClickGetTrigramResult);
		}

		public void DragForTrigram(ZContext context)
		{
			DragCount++;
		}

		public Task<OperationResult> ClickConfirmAsync(ZContext context, OneDragon.Core.Abstractions.Geometry.Point? center)
		{
			if (center.HasValue)
			{
				ConfirmClicks.Add(center.Value);
			}
			return Task.FromResult(ClickConfirmResult);
		}

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
		{
			BackCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
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
					dictionary.Add(item.Text, value);
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
	public void Factory_ExposesPythonMetadataAndCreatesTrigramsCollectionApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			TrigramsCollectionFactory trigramsCollectionFactory = zContext.ApplicationFactoryRegistry.CreateTrigramsCollectionFactory();
			IApplication application = trigramsCollectionFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = trigramsCollectionFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = trigramsCollectionFactory.GetRunRecord(0);
			Assert.Equal("trigrams_collection", trigramsCollectionFactory.AppId);
			Assert.Equal("卦象集录", trigramsCollectionFactory.AppName);
			Assert.Equal("one_dragon", trigramsCollectionFactory.GroupId);
			Assert.True(trigramsCollectionFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<TrigramsCollectionApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			TrigramsCollectionRunRecord trigramsCollectionRunRecord = Assert.IsType<TrigramsCollectionRunRecord>(runRecord);
			Assert.Equal("trigrams_collection", trigramsCollectionRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersTrigramsCollectionAsNonDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterTrigramsCollectionApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("trigrams_collection"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("trigrams_collection"));
			Assert.DoesNotContain("trigrams_collection", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void TrigramsCollectionRunRecord_UsesAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		TrigramsCollectionRunRecord trigramsCollectionRunRecord = new TrigramsCollectionRunRecord(4, () => now);
		trigramsCollectionRunRecord.UpdateStatus(1);
		Assert.Equal("trigrams_collection", trigramsCollectionRunRecord.AppId);
		Assert.Equal("20260706", trigramsCollectionRunRecord.Dt);
		Assert.True(trigramsCollectionRunRecord.IsDone);
	}

	[Fact]
	public async Task TrigramsCollectionApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			TrigramsCollectionRunRecord runRecord = new TrigramsCollectionRunRecord();
			RecordingTrigramsCollectionFlow flow = new RecordingTrigramsCollectionFlow();
			TrigramsCollectionApp app = new TrigramsCollectionApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("卦象集录完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_UsesInjectedServicesWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingTrigramsCollectionServices services = new RecordingTrigramsCollectionServices();
			TrigramsCollectionOperation operation = new TrigramsCollectionOperation(context, services);
			Assert.True((await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(operation.MoveAndInteract().IsSuccess);
			services.NextMatch = new TrigramOcrMatch("卦象集录");
			OperationRoundResult open = await operation.GetTrigram().WaitAsync(TimeSpan.FromSeconds(2L));
			services.NextMatch = new TrigramOcrMatch("滑动屏幕以获取卦象");
			OperationRoundResult drag = await operation.GetTrigram().WaitAsync(TimeSpan.FromSeconds(2L));
			services.NextMatch = new TrigramOcrMatch("确认", new OneDragon.Core.Abstractions.Geometry.Point(100, 200));
			OperationRoundResult confirm = await operation.GetTrigram().WaitAsync(TimeSpan.FromSeconds(2L));
			services.NextMatch = new TrigramOcrMatch("卦象集录");
			OperationRoundResult done = await operation.GetTrigram().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal("卦象集录", open.Status);
			Assert.Equal("滑动屏幕以获取卦象", drag.Status);
			Assert.Equal("确认", confirm.Status);
			Assert.Equal("卦象集录", done.Status);
			Assert.True(done.IsSuccess);
			Assert.True(operation.ClaimReward);
			Assert.Equal(1, services.TransportCount);
			Assert.Equal(1, services.InteractCount);
			Assert.Equal(1, services.ClickGetTrigramCount);
			Assert.Equal(1, services.DragCount);
			Assert.Equal(new List<OneDragon.Core.Abstractions.Geometry.Point>(1)
			{
				new OneDragon.Core.Abstractions.Geometry.Point(100, 200)
			}, services.ConfirmClicks);
			Assert.True((await operation.BackAtLast().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal(1, services.BackCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RetriesWhenTextIsUnknown()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			TrigramsCollectionOperation operation = new TrigramsCollectionOperation(context, new RecordingTrigramsCollectionServices());
			OperationRoundResult result = await operation.GetTrigram().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未识别目标文本", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultServices_ResolvePriorityWordsToCurrentGameLanguage()
	{
		OpenCvTestRuntime.RequireAvailable();
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			context.GameTextResolver = (string text) => (text == "卦象集录") ? "Trigram Collection" : text;
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 50, 50, 100, 30, "Trigram Collection") });
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			DefaultTrigramsCollectionOperationServices services = new DefaultTrigramsCollectionOperationServices();
			TrigramOcrMatch match = await services.ReadPriorityTextAsync(context, screen, new string[3] { "卦象集录", "滑动屏幕以获取卦象", "确认" });
			Assert.NotNull(match);
			Assert.Equal("卦象集录", match.Word);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_WaitsAndRecordsClaimWhenPythonClickResultsAreIgnored()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingTrigramsCollectionServices services = new RecordingTrigramsCollectionServices
			{
				ClickGetTrigramResult = new OperationResult(IsSuccess: false, "点击失败 区域-获取卦象"),
				ClickConfirmResult = new OperationResult(IsSuccess: false, "点击失败 确认")
			};
			TrigramsCollectionOperation operation = new TrigramsCollectionOperation(context, services);
			services.NextMatch = new TrigramOcrMatch("卦象集录");
			OperationRoundResult open = await operation.GetTrigram().WaitAsync(TimeSpan.FromSeconds(2L));
			services.NextMatch = new TrigramOcrMatch("确认", new OneDragon.Core.Abstractions.Geometry.Point(100, 200));
			OperationRoundResult confirm = await operation.GetTrigram().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Wait, open.Kind);
			Assert.Equal("卦象集录", open.Status);
			Assert.Equal(OperationRoundResultKind.Wait, confirm.Kind);
			Assert.Equal("确认", confirm.Status);
			Assert.True(operation.ClaimReward);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(TrigramsCollectionOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[4] { "传送", "移动交互", "获取卦象", "结束后返回" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["传送"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(10, readOnlyDictionary["获取卦象"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Contains(readOnlyDictionary["移动交互"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "传送");
		Assert.Contains(readOnlyDictionary["获取卦象"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "移动交互");
		Assert.Contains(readOnlyDictionary["结束后返回"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "获取卦象");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
