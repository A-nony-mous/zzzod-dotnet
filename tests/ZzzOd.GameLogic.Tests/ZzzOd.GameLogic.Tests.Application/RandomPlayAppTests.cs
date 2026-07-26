using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.RandomPlay;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class RandomPlayAppTests
{
	private sealed class RecordingRandomPlayFlow : IRandomPlayAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, RandomPlayConfig config, RandomPlayRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "录像店营业完成"));
		}
	}

	private sealed class RecordingRandomPlayServices : IRandomPlayOperationServices
	{
		public Dictionary<string, bool> VisibleAreas { get; } = new Dictionary<string, bool>(StringComparer.Ordinal);

		public RandomPlayTransportPoint? TransportedPoint { get; private set; }

		public bool MovedAndInteracted { get; private set; }

		public int ClearPendingTurnSampleCount { get; private set; }

		public List<string> ClickedAreas { get; } = new List<string>();

		public List<TimeSpan?> ClickPreDelayLog { get; } = new List<TimeSpan?>();

		public Dictionary<string, TimeSpan?> ClickPreDelays { get; } = new Dictionary<string, TimeSpan?>(StringComparer.Ordinal);

		public List<string> ClickedThemes { get; } = new List<string>();

		public IReadOnlyList<string> VideoThemes { get; init; } = new string[2] { "动作", "爱情" };

		public OperationResult ThemeClickResult { get; init; } = new OperationResult(IsSuccess: true, "主题已点击");

		public int ScrollThemeListCount { get; private set; }

		public int ScrollPromoterListCount { get; private set; }

		public int BackToWorldCount { get; private set; }

		public Task<OperationResult> TransportAsync(ZContext context, RandomPlayTransportPoint point)
		{
			TransportedPoint = point;
			return Task.FromResult(new OperationResult(IsSuccess: true, point.AreaName + "-" + point.TransportPointName));
		}

		public void ClearPendingTurnSample()
		{
			ClearPendingTurnSampleCount++;
		}

		public Task<OperationRoundResult> MoveAndInteractAsync(ZContext context, RandomPlayConfig config, Mat? screen)
		{
			MovedAndInteracted = true;
			return Task.FromResult(new OperationRoundResult(OperationRoundResultKind.Success, "移动交互"));
		}

		public bool IsAreaVisible(ZContext context, Mat? screen, string screenName, string areaName)
		{
			bool value;
			return VisibleAreas.TryGetValue(screenName + "/" + areaName, out value) && value;
		}

		public OperationResult FindAndClickArea(ZContext context, Mat? screen, string screenName, string areaName)
		{
			string item = screenName + "/" + areaName;
			bool flag = !(areaName == "推荐上架");
			if (flag)
			{
				ClickedAreas.Add(item);
			}
			return new OperationResult(flag, flag ? areaName : ("未找到 " + areaName));
		}

		public OperationResult ClickArea(ZContext context, string screenName, string areaName, TimeSpan? preDelay = null)
		{
			string text = screenName + "/" + areaName;
			ClickedAreas.Add(text);
			ClickPreDelayLog.Add(preDelay);
			ClickPreDelays[text] = preDelay;
			return new OperationResult(IsSuccess: true, areaName);
		}

		public OperationResult ClickText(ZContext context, Mat? screen, string targetText, string screenName, string areaName)
		{
			return new OperationResult(IsSuccess: false, "找不到 " + targetText);
		}

		public bool TrySelectAgent(ZContext context, Mat? screen, string agentName)
		{
			return false;
		}

		public void ScrollPromoterList(ZContext context)
		{
			ScrollPromoterListCount++;
		}

		public IReadOnlyList<string> ReadVideoThemes(ZContext context, Mat? screen)
		{
			return VideoThemes;
		}

		public OperationResult ClickTheme(ZContext context, Mat? screen, string theme)
		{
			ClickedThemes.Add(theme);
			return ThemeClickResult.IsSuccess ? new OperationResult(IsSuccess: true, theme) : ThemeClickResult;
		}

		public void ScrollThemeList(ZContext context)
		{
			ScrollThemeListCount++;
		}

		public Task<OperationResult> BackToWorldAsync(ZContext context)
		{
			BackToWorldCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}
	}

	private sealed class SingleTextOcrMatcher(string text) : IOcrMatcher
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
			return text;
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			MatchResultList matchResultList = new MatchResultList(onlyBest: false);
			matchResultList.Append(new OcrMatchResult(0.99, 10, 10, 100, 20, text), autoMerge: false);
			return new Dictionary<string, MatchResultList>(StringComparer.Ordinal) { [text] = matchResultList };
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 100, 20, text) };
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesRandomPlayApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RandomPlayAppFactory randomPlayAppFactory = zContext.ApplicationFactoryRegistry.CreateRandomPlayFactory();
			IApplication application = randomPlayAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = randomPlayAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = randomPlayAppFactory.GetRunRecord(0);
			Assert.Equal("random_play", randomPlayAppFactory.AppId);
			Assert.Equal("录像店营业", randomPlayAppFactory.AppName);
			Assert.Equal("one_dragon", randomPlayAppFactory.GroupId);
			Assert.True(randomPlayAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<RandomPlayApp>(application);
			RandomPlayConfig randomPlayConfig = Assert.IsType<RandomPlayConfig>(config);
			Assert.Equal(RandomPlayTransportPoint.VideoStoreCounter.Value, randomPlayConfig.TransportPoint);
			Assert.Equal("随机", randomPlayConfig.AgentName1);
			Assert.Equal("随机", randomPlayConfig.AgentName2);
			RandomPlayRunRecord randomPlayRunRecord = Assert.IsType<RandomPlayRunRecord>(runRecord);
			Assert.Equal("random_play", randomPlayRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersRandomPlayAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterRandomPlayApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("random_play"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("random_play"));
			Assert.Contains("random_play", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
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
			File.WriteAllText(Path.Combine(text2, "random_play.yml"), "transport_point: \"录像店营业点\"\nagent_name_1: \"安比\"\nagent_name_2: \"妮可\"");
			RandomPlayConfig randomPlayConfig = RandomPlayConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("random_play", randomPlayConfig.AppId);
			Assert.Equal(0, randomPlayConfig.InstanceIndex);
			Assert.Equal("one_dragon", randomPlayConfig.GroupId);
			Assert.Equal(RandomPlayTransportPoint.FailumeHeightsBusinessPoint.Value, randomPlayConfig.TransportPoint);
			Assert.Equal("安比", randomPlayConfig.AgentName1);
			Assert.Equal("妮可", randomPlayConfig.AgentName2);
			Assert.Equal("FLYOUT", "FLYOUT");
			Assert.Contains((IEnumerable<RandomPlaySettingField>)RandomPlaySettings.Fields, (Predicate<RandomPlaySettingField>)((RandomPlaySettingField field) => field.Key == "transport_point" && field.DefaultValue.Equals(RandomPlayTransportPoint.VideoStoreCounter.Value)));
			Assert.Contains((IEnumerable<ConfigItem>)RandomPlayTransportPoint.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, RandomPlayTransportPoint.FailumeHeightsBusinessPoint.Value)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void TransportPoint_RejectsUnknownValueInsteadOfSelectingVideoStoreCounter()
	{
		Assert.False(RandomPlayTransportPoint.TryFromValue("不存在的传送点", out RandomPlayTransportPoint point));
		Assert.Null(point);
		Assert.Throws<ArgumentOutOfRangeException>(() => RandomPlayTransportPoint.FromValue("不存在的传送点"));
	}

	[Fact]
	public void RunRecord_UsesAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		RandomPlayRunRecord randomPlayRunRecord = new RandomPlayRunRecord(4, () => now);
		randomPlayRunRecord.UpdateStatus(1);
		Assert.Equal("random_play", randomPlayRunRecord.AppId);
		Assert.Equal("20260706", randomPlayRunRecord.Dt);
		Assert.True(randomPlayRunRecord.IsDone);
	}

	[Fact]
	public async Task RandomPlayApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RandomPlayConfig config = new RandomPlayConfig();
			RandomPlayRunRecord runRecord = new RandomPlayRunRecord();
			RecordingRandomPlayFlow flow = new RecordingRandomPlayFlow();
			RandomPlayApp app = new RandomPlayApp(context, config, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("录像店营业完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_RunsInjectedBusinessFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RandomPlayConfig config = new RandomPlayConfig();
			RandomPlayRunRecord record = new RandomPlayRunRecord
			{
				Dt = "20260706"
			};
			RecordingRandomPlayServices recordingRandomPlayServices = new RecordingRandomPlayServices();
			recordingRandomPlayServices.VisibleAreas["影像店营业/经营状况"] = true;
			recordingRandomPlayServices.VisibleAreas["影像店营业/选择宣传员"] = true;
			recordingRandomPlayServices.VisibleAreas["影像店营业/上架筛选"] = true;
			recordingRandomPlayServices.VisibleAreas["影像店营业/上架"] = true;
			RecordingRandomPlayServices services = recordingRandomPlayServices;
			RandomPlayOperation operation = new RandomPlayOperation(context, config, record, services, () => new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero));
			Assert.True((await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True((await operation.MoveAndInteract().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(operation.WaitRun().IsSuccess);
			Assert.True(operation.CheckRunning().IsSuccess);
			Assert.True(operation.ClickPromoterEntry().IsSuccess);
			Assert.True(operation.ChoosePromoter().IsSuccess);
			Assert.True(operation.CheckVideoTheme().IsSuccess);
			Assert.Equal(new string[3] { "动作", "爱情", "纪实" }, operation.NeedVideoThemes);
			Assert.True(operation.ClickVideoEntry().IsSuccess);
			Assert.Equal("上架筛选", operation.CheckRecommended().Status);
			Assert.True(operation.ClickFilter().IsSuccess);
			Assert.True(operation.ChooseTheme().IsSuccess);
			Assert.Equal(OperationRoundResultKind.Wait, operation.ChooseOnShelf().Kind);
			operation.ClickFilter();
			operation.ChooseTheme();
			operation.ClickFilter();
			operation.ChooseTheme();
			OperationRoundResult allChosen = operation.ClickFilter();
			Assert.Equal("已选择全部录像带", allChosen.Status);
			Assert.True(operation.Back().IsSuccess);
			Assert.True(operation.Start().IsSuccess);
			Assert.True(operation.ConfirmBusiness().IsSuccess);
			Assert.True(operation.Confirm().IsSuccess);
			Assert.True((await operation.BackToWorld().WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.Equal<RandomPlayTransportPoint>(RandomPlayTransportPoint.VideoStoreCounter, services.TransportedPoint);
			Assert.Equal(1, services.ClearPendingTurnSampleCount);
			Assert.True(services.MovedAndInteracted);
			Assert.Contains("影像店营业/宣传员-1", (IEnumerable<string>)services.ClickedAreas);
			Assert.Equal<List<string>>(new List<string>(3) { "动作", "爱情", "纪实" }, services.ClickedThemes);
			Assert.Equal(1, services.BackToWorldCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Operation_ClosesPageWhenAlreadyRunning()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RandomPlayConfig config = new RandomPlayConfig();
			RandomPlayRunRecord record = new RandomPlayRunRecord();
			RecordingRandomPlayServices recordingRandomPlayServices = new RecordingRandomPlayServices();
			recordingRandomPlayServices.VisibleAreas["影像店营业/正在营业"] = true;
			RecordingRandomPlayServices services = recordingRandomPlayServices;
			RandomPlayOperation operation = new RandomPlayOperation(context, config, record, services);
			OperationRoundResult running = operation.CheckRunning();
			OperationRoundResult close = operation.CloseBusinessPage();
			OperationRoundResult back = await operation.BackToWorld().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(running.IsSuccess);
			Assert.Equal("正在营业", running.Status);
			Assert.True(close.IsSuccess);
			Assert.True(back.IsSuccess);
			Assert.Contains("影像店营业/返回", (IEnumerable<string>)services.ClickedAreas);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(RandomPlayOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[18]
		{
			"传送", "移动交互", "等待经营画面加载", "识别营业状态", "关闭经营页面", "点击宣传员入口", "选择宣传员", "识别录像带主题", "点击录像带入口", "识别推荐上架",
			"上架筛选", "选择主题", "上架", "返回", "开始营业", "确认营业", "营业后确认", "返回大世界"
		}, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["传送"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(10, readOnlyDictionary["移动交互"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(10, readOnlyDictionary["等待经营画面加载"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Contains(readOnlyDictionary["传送"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "等待经营画面加载" && !edge.Success);
		Assert.Contains(readOnlyDictionary["关闭经营页面"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "识别营业状态" && edge.Status == "正在营业");
		Assert.Contains(readOnlyDictionary["上架筛选"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "识别推荐上架" && edge.Status == "上架筛选");
		Assert.Contains(readOnlyDictionary["返回"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "上架筛选" && edge.Status == "已选择全部录像带");
		Assert.Contains(readOnlyDictionary["返回大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "关闭经营页面");
	}

	[Theory]
	[InlineData(new object[] { "20260706", 1 })]
	[InlineData(new object[] { "20260707", 2 })]
	[InlineData(new object[] { "", 1 })]
	public void GetPromoterSlotIndex_MatchesPythonDateParity(string dt, int expected)
	{
		Assert.Equal(expected, RandomPlayOperation.GetPromoterSlotIndex(dt));
	}

	[Fact]
	public void FindBestTheme_UsesBaselineCandidateSortingAndCutoff()
	{
		Assert.Equal("ac", RandomPlayOperation.FindBestTheme("a", new string[2] { "ab", "ac" }));
		Assert.Equal("动作", RandomPlayOperation.FindBestTheme("动作片", new string[3] { "爱情", "动作", "喜剧" }));
		Assert.Null(RandomPlayOperation.FindBestTheme("x", new string[2] { "动作", "爱情" }));
	}

	[Fact]
	public void Operation_PreservesDuplicateVideoThemesFromPython()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RandomPlayConfig config = new RandomPlayConfig();
			RandomPlayRunRecord runRecord = new RandomPlayRunRecord();
			RecordingRandomPlayServices recordingRandomPlayServices = new RecordingRandomPlayServices
			{
				VideoThemes = new string[3] { "动作", "动作", "爱情" }
			};
			recordingRandomPlayServices.VisibleAreas["影像店营业/经营状况"] = true;
			RecordingRandomPlayServices services = recordingRandomPlayServices;
			RandomPlayOperation randomPlayOperation = new RandomPlayOperation(zContext, config, runRecord, services);
			OperationRoundResult operationRoundResult = randomPlayOperation.CheckVideoTheme();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal(new string[3] { "动作", "动作", "爱情" }, randomPlayOperation.NeedVideoThemes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_RandomPromoterUsesCurrentGameRefreshDayAfterRecordCrossesDay()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RandomPlayConfig config = new RandomPlayConfig
			{
				AgentName1 = "随机",
				AgentName2 = "安比"
			};
			RandomPlayRunRecord runRecord = new RandomPlayRunRecord(4)
			{
				Dt = "20260706"
			};
			RecordingRandomPlayServices recordingRandomPlayServices = new RecordingRandomPlayServices();
			recordingRandomPlayServices.VisibleAreas["影像店营业/选择宣传员"] = true;
			RecordingRandomPlayServices recordingRandomPlayServices2 = recordingRandomPlayServices;
			RandomPlayOperation randomPlayOperation = new RandomPlayOperation(zContext, config, runRecord, recordingRandomPlayServices2, () => new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero));
			randomPlayOperation.ChoosePromoter();
			Assert.Contains("影像店营业/宣传员-2", (IEnumerable<string>)recordingRandomPlayServices2.ClickedAreas);
			Assert.Equal(TimeSpan.FromSeconds(1L), recordingRandomPlayServices2.ClickPreDelays["影像店营业/宣传员-2"]);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_PromoterFallbackUsesPythonNodeRetryTimes()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingRandomPlayServices recordingRandomPlayServices = new RecordingRandomPlayServices();
			recordingRandomPlayServices.VisibleAreas["影像店营业/选择宣传员"] = true;
			RecordingRandomPlayServices recordingRandomPlayServices2 = recordingRandomPlayServices;
			RandomPlayOperation randomPlayOperation = new RandomPlayOperation(zContext, new RandomPlayConfig
			{
				AgentName1 = "安比",
				AgentName2 = "妮可"
			}, new RandomPlayRunRecord(), recordingRandomPlayServices2);
			FieldInfo fieldInfo = typeof(RandomPlayOperation).BaseType?.BaseType?.GetField("_nodeRetryTimes", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(fieldInfo);
			fieldInfo.SetValue(randomPlayOperation, 2);
			OperationRoundResult operationRoundResult = randomPlayOperation.ChoosePromoter();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Contains("影像店营业/宣传员-1", (IEnumerable<string>)recordingRandomPlayServices2.ClickedAreas);
			Assert.Equal(0, recordingRandomPlayServices2.ScrollPromoterListCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_ChooseOnShelfUsesPythonSecondClickPreDelay()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RandomPlayConfig config = new RandomPlayConfig();
			RandomPlayRunRecord runRecord = new RandomPlayRunRecord();
			RecordingRandomPlayServices recordingRandomPlayServices = new RecordingRandomPlayServices();
			recordingRandomPlayServices.VisibleAreas["影像店营业/上架"] = true;
			RecordingRandomPlayServices recordingRandomPlayServices2 = recordingRandomPlayServices;
			RandomPlayOperation randomPlayOperation = new RandomPlayOperation(zContext, config, runRecord, recordingRandomPlayServices2);
			OperationRoundResult operationRoundResult = randomPlayOperation.ChooseOnShelf();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			int num = 2;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<string> span = CollectionsMarshal.AsSpan(list);
			span[0] = "影像店营业/上架";
			span[1] = "影像店营业/上架";
			Assert.Equal<List<string>>(list, recordingRandomPlayServices2.ClickedAreas);
			Assert.Equal(TimeSpan.FromMilliseconds(500L), recordingRandomPlayServices2.ClickPreDelayLog[1]);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_ChooseThemeDoesNotScrollAfterPythonThemeClickFailure()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingRandomPlayServices obj = new RecordingRandomPlayServices
			{
				VideoThemes = new string[] { "动作" },
				ThemeClickResult = new OperationResult(IsSuccess: false, "点击失败 动作")
			};
			obj.VisibleAreas["影像店营业/经营状况"] = true;
			RecordingRandomPlayServices recordingRandomPlayServices = obj;
			RandomPlayOperation randomPlayOperation = new RandomPlayOperation(zContext, new RandomPlayConfig(), new RandomPlayRunRecord(), recordingRandomPlayServices);
			Assert.True(randomPlayOperation.CheckVideoTheme().IsSuccess);
			Assert.True(randomPlayOperation.ClickFilter().IsSuccess);
			OperationRoundResult operationRoundResult = randomPlayOperation.ChooseTheme();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal("点击失败 动作", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromMilliseconds(500L), operationRoundResult.Delay);
			Assert.Equal(0, recordingRandomPlayServices.ScrollThemeListCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_UsesPythonSuccessAndRetryWaitsForPageAndBusinessClicks()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingRandomPlayServices services = new RecordingRandomPlayServices();
			RandomPlayOperation randomPlayOperation = new RandomPlayOperation(zContext, new RandomPlayConfig(), new RandomPlayRunRecord(), services);
			OperationRoundResult[] collection = new OperationRoundResult[7]
			{
				randomPlayOperation.CloseBusinessPage(),
				randomPlayOperation.ClickPromoterEntry(),
				randomPlayOperation.ClickVideoEntry(),
				randomPlayOperation.Back(),
				randomPlayOperation.Start(),
				randomPlayOperation.ConfirmBusiness(),
				randomPlayOperation.Confirm()
			};
			Assert.All(collection, delegate(OperationRoundResult result)
			{
				Assert.True(result.IsSuccess);
				Assert.Null(result.Delay);
			});
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_UsePython300MillisecondPreDelayForTemplateAndOcrClicks()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.OcrService.Matcher = new SingleTextOcrMatcher("查看经营状况");
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			List<TimeSpan> list = new List<TimeSpan>();
			DefaultRandomPlayOperationServices defaultRandomPlayOperationServices = new DefaultRandomPlayOperationServices(list.Add);
			defaultRandomPlayOperationServices.FindAndClickArea(zContext, screen, "影像店营业", "经营状况");
			defaultRandomPlayOperationServices.ClickText(zContext, screen, "查看经营状况", "影像店营业", "右侧选项区域");
			int num = 2;
			List<TimeSpan> list2 = new List<TimeSpan>(num);
			CollectionsMarshal.SetCount(list2, num);
			Span<TimeSpan> span = CollectionsMarshal.AsSpan(list2);
			span[0] = TimeSpan.FromMilliseconds(300L);
			span[1] = TimeSpan.FromMilliseconds(300L);
			Assert.Equal(list2, list);
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
