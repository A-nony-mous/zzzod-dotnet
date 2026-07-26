using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.HouHouBakery;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class HouHouBakeryAppTests
{
	private sealed class RecordingHouHouBakeryFlow : IHouHouBakeryFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "吼吼饼铺领取完成"));
		}
	}

	private sealed class RecordingHouHouBakeryServices : IHouHouBakeryOperationServices
	{
		public string? CurrentText { get; set; }

		public int TransportCount { get; private set; }

		public int InteractCount { get; private set; }

		public int CenterClickCount { get; private set; }

		public int BlindBoxClickCount { get; private set; }

		public int BackCount { get; private set; }

		public List<string> ClickedTexts { get; } = new List<string>();

		public Dictionary<string, OperationResult> ClickResults { get; } = new Dictionary<string, OperationResult>(StringComparer.Ordinal);

		public OperationResult InteractResult { get; set; } = new OperationResult(IsSuccess: true, "交互");

		public OperationResult CenterClickResult { get; set; } = new OperationResult(IsSuccess: true, "点击盲盒");

		public OperationResult BlindBoxClickResult { get; set; } = new OperationResult(IsSuccess: true, "盲盒");

		public Task<OperationResult> TransportAsync(ZContext context)
		{
			TransportCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "吼吼饼铺"));
		}

		public OperationResult Interact(ZContext context)
		{
			InteractCount++;
			return InteractResult;
		}

		public Task<bool> RecognizeTextAsync(ZContext context, Mat? screen, string targetText)
		{
			return Task.FromResult(CurrentText?.Contains(targetText, StringComparison.Ordinal) ?? false);
		}

		public Task<OperationResult> ClickTextAsync(ZContext context, Mat? screen, string targetText)
		{
			ClickedTexts.Add(targetText);
			OperationResult value;
			return Task.FromResult(ClickResults.TryGetValue(targetText, out value) ? value : new OperationResult(IsSuccess: true, targetText));
		}

		public OperationResult ClickCenter(ZContext context)
		{
			CenterClickCount++;
			return CenterClickResult;
		}

		public Task<OperationResult> ClickBlindBoxAsync(ZContext context)
		{
			BlindBoxClickCount++;
			return Task.FromResult(BlindBoxClickResult);
		}

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
		{
			BackCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}
	}

	private sealed class FixedOcrMatcher(IReadOnlyList<OcrMatchResult> results) : IOcrMatcher
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
	public void Factory_ExposesPythonMetadataAndCreatesHouHouBakeryApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			HouHouBakeryFactory houHouBakeryFactory = zContext.ApplicationFactoryRegistry.CreateHouHouBakeryFactory();
			IApplication application = houHouBakeryFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = houHouBakeryFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = houHouBakeryFactory.GetRunRecord(0);
			Assert.Equal("hou_hou_bakery", houHouBakeryFactory.AppId);
			Assert.Equal("吼吼饼铺", houHouBakeryFactory.AppName);
			Assert.Equal("one_dragon", houHouBakeryFactory.GroupId);
			Assert.True(houHouBakeryFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<HouHouBakeryApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			HouHouBakeryRunRecord houHouBakeryRunRecord = Assert.IsType<HouHouBakeryRunRecord>(runRecord);
			Assert.Equal("hou_hou_bakery", houHouBakeryRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersHouHouBakeryAsNonDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterHouHouBakeryApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("hou_hou_bakery"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("hou_hou_bakery"));
			Assert.DoesNotContain("hou_hou_bakery", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void HouHouBakeryRunRecord_UsesAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		HouHouBakeryRunRecord houHouBakeryRunRecord = new HouHouBakeryRunRecord(4, () => now);
		houHouBakeryRunRecord.UpdateStatus(1);
		Assert.Equal("hou_hou_bakery", houHouBakeryRunRecord.AppId);
		Assert.Equal("20260706", houHouBakeryRunRecord.Dt);
		Assert.True(houHouBakeryRunRecord.IsDone);
	}

	[Fact]
	public async Task HouHouBakeryApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			HouHouBakeryRunRecord runRecord = new HouHouBakeryRunRecord();
			RecordingHouHouBakeryFlow flow = new RecordingHouHouBakeryFlow();
			HouHouBakeryApp app = new HouHouBakeryApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("吼吼饼铺领取完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task HouHouBakeryOperation_UsesInjectedServicesWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingHouHouBakeryServices services = new RecordingHouHouBakeryServices();
			HouHouBakeryOperation operation = new HouHouBakeryOperation(context, services);
			OperationRoundResult transport = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult interact = operation.MoveAndInteract();
			services.CurrentText = "每日可领取一次";
			OperationRoundResult chooseBox = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			services.CurrentText = "查看今天的卡片吧";
			OperationRoundResult openBox = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			services.CurrentText = "确定";
			OperationRoundResult confirm = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			services.CurrentText = "今日已领取同类型奖励";
			OperationRoundResult done = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult back = await operation.BackToWorld().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(transport.IsSuccess);
			Assert.True(interact.IsSuccess);
			Assert.Equal("选择盲盒", chooseBox.Status);
			Assert.Equal("点击盲盒", openBox.Status);
			Assert.Equal("确定", confirm.Status);
			Assert.Equal("领取成功", done.Status);
			Assert.True(back.IsSuccess);
			Assert.True(operation.Claimed);
			Assert.Equal(1, services.TransportCount);
			Assert.Equal(1, services.InteractCount);
			Assert.Equal(1, services.CenterClickCount);
			Assert.Equal(1, services.BlindBoxClickCount);
			Assert.Equal<List<string>>(new List<string>(1) { "确定" }, services.ClickedTexts);
			Assert.Equal(1, services.BackCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task HouHouBakeryOperation_TreatsSameTypeRewardBeforeConfirmText()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingHouHouBakeryServices services = new RecordingHouHouBakeryServices
			{
				CurrentText = "同类型奖励 确认"
			};
			HouHouBakeryOperation operation = new HouHouBakeryOperation(context, services);
			OperationRoundResult result = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("今日已领取", result.Status);
			Assert.Empty(services.ClickedTexts);
			Assert.False(operation.Claimed);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task HouHouBakeryOperation_TriesConfirmAfterOkClickFails()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingHouHouBakeryServices services = new RecordingHouHouBakeryServices
			{
				CurrentText = "确定 确认"
			};
			services.ClickResults["确定"] = new OperationResult(IsSuccess: false, "点击失败 确定");
			services.ClickResults["确认"] = new OperationResult(IsSuccess: true, "确认");
			HouHouBakeryOperation operation = new HouHouBakeryOperation(context, services);
			OperationRoundResult result = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Wait, result.Kind);
			Assert.Equal("确认", result.Status);
			Assert.Equal<List<string>>(new List<string>(2) { "确定", "确认" }, services.ClickedTexts);
			Assert.True(operation.Claimed);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void HouHouBakeryOperation_FailsWhenForegroundInteractionIsUnavailable()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			HouHouBakeryOperation houHouBakeryOperation = new HouHouBakeryOperation(context);
			OperationRoundResult operationRoundResult = houHouBakeryOperation.MoveAndInteract();
			Assert.Equal(OperationRoundResultKind.Fail, operationRoundResult.Kind);
			Assert.Equal("控制器不支持前台键鼠交互", operationRoundResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task HouHouBakeryOperation_RetriesWhenCenterClickFails()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingHouHouBakeryServices services = new RecordingHouHouBakeryServices
			{
				CurrentText = "查看今天的卡片吧",
				CenterClickResult = new OperationResult(IsSuccess: false, "点击失败 盲盒")
			};
			HouHouBakeryOperation operation = new HouHouBakeryOperation(context, services);
			OperationRoundResult result = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Retry, result.Kind);
			Assert.Equal("点击失败 盲盒", result.Status);
			Assert.Equal(1, services.CenterClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task HouHouBakeryOperation_RetriesWhenBlindBoxClickFails()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingHouHouBakeryServices services = new RecordingHouHouBakeryServices
			{
				CurrentText = "每日可领取一次",
				BlindBoxClickResult = new OperationResult(IsSuccess: false, "区域未配置 盲盒")
			};
			HouHouBakeryOperation operation = new HouHouBakeryOperation(context, services);
			OperationRoundResult result = await operation.Collect().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Retry, result.Kind);
			Assert.Equal("盲盒区域点击失败", result.Status);
			Assert.Equal(1, services.BlindBoxClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultHouHouBakeryServices_UseGameTextResolverForRecognitionAndClick()
	{
		OpenCvTestRuntime.RequireAvailable();
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.GameTextResolver = (string text) => (text == "确定") ? "Confirm" : text;
			ReadyController controller = new ReadyController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FixedOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 30, "Confirm") });
			using Mat screen = new Mat(new Size(120, 120), MatType.CV_8UC3, Scalar.Black);
			DefaultHouHouBakeryOperationServices services = new DefaultHouHouBakeryOperationServices();
			Assert.True(await services.RecognizeTextAsync(context, screen, "确定"));
			Stopwatch stopwatch = Stopwatch.StartNew();
			Assert.True((await services.ClickTextAsync(context, screen, "确定")).IsSuccess);
			Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(250L));
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void HouHouBakeryOperation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(HouHouBakeryOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[4] { "传送", "移动交互", "领取奖励", "返回大世界" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["传送"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(20, readOnlyDictionary["领取奖励"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Contains(readOnlyDictionary["移动交互"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "传送");
		Assert.Contains(readOnlyDictionary["领取奖励"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "移动交互");
		Assert.Contains(readOnlyDictionary["返回大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "领取奖励");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
