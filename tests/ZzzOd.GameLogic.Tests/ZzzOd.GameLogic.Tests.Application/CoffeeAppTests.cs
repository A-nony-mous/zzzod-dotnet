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
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.Coffee;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations.Turning;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class CoffeeAppTests
{
	private sealed class RecordingCoffeeFlow : ICoffeeAppFlow
	{
		public int RunCount { get; private set; }

		public CoffeeConfig? LastConfig { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CoffeeConfig config, ChargePlanConfig chargePlanConfig, CancellationToken cancellationToken)
		{
			RunCount++;
			LastConfig = config;
			return Task.FromResult(new OperationResult(IsSuccess: true, "咖啡完成"));
		}
	}

	private sealed class RecordingClickController : ControllerBase
	{
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

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return null;
		}
	}

	private sealed class CapturingOcrMatcher(IReadOnlyList<OcrMatchResult> results) : IOcrMatcher
	{
		public Mat? LastImage { get; private set; }

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
			LastImage?.Dispose();
			LastImage = image.Clone();
			return results.Select((OcrMatchResult result) => new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesCoffeeApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			CoffeeAppFactory coffeeAppFactory = zContext.ApplicationFactoryRegistry.CreateCoffeeFactory();
			IApplication application = coffeeAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = coffeeAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = coffeeAppFactory.GetRunRecord(0);
			Assert.Equal("coffee", coffeeAppFactory.AppId);
			Assert.Equal("咖啡店", coffeeAppFactory.AppName);
			Assert.Equal("one_dragon", coffeeAppFactory.GroupId);
			Assert.True(coffeeAppFactory.NeedNotify);
			Assert.IsType<CoffeeApp>(application);
			CoffeeConfig coffeeConfig = Assert.IsType<CoffeeConfig>(config);
			Assert.Equal("优先体力计划", coffeeConfig.ChooseWay);
			CoffeeRunRecord coffeeRunRecord = Assert.IsType<CoffeeRunRecord>(runRecord);
			Assert.Equal("coffee", coffeeRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersCoffeeAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ApplicationFactoryRegistry.RegisterCoffeeApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("coffee"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("coffee"));
			Assert.Contains("coffee", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CoffeeConfig_LoadsPythonCompatibleYamlAndDefaults()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "coffee.yml"), "transport_point: \"澄辉坪 - 汀曼咖啡\"\nchoose_way: \"浓缩咖啡\"\nchallenge_way: \"只挑战体力计划\"\ncard_num: \"默认数量\"\nauto_battle: \"安比模板\"\nday_coffee_7: \"沙罗特调\"\npredefined_team_idx: 2\nrun_charge_plan_afterwards: true\nunknown_field: \"ignored\"");
			CoffeeConfig coffeeConfig = CoffeeConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal(CoffeeTransportPoint.FailumeHeights.Value, coffeeConfig.TransportPoint);
			Assert.Equal("浓缩咖啡", coffeeConfig.ChooseWay);
			Assert.Equal("只挑战体力计划", coffeeConfig.ChallengeWay);
			Assert.Equal("默认数量", coffeeConfig.CardNum);
			Assert.Equal("安比模板", coffeeConfig.AutoBattle);
			Assert.Equal("沙罗特调", coffeeConfig.GetCoffeeByDay(7));
			Assert.Equal(2, coffeeConfig.PredefinedTeamIndex);
			Assert.True(coffeeConfig.RunChargePlanAfterwards);
			Assert.Equal("汀曼特调", new CoffeeConfig().GetCoffeeByDay(3));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { "咖啡店", "六分街 - 咖啡店" })]
	[InlineData(new object[] { "汀曼咖啡", "澄辉坪 - 汀曼咖啡" })]
	public void CoffeeTransportPoint_NormalizesLegacyDotNetValues(string legacyValue, string pythonValue)
	{
		Assert.Equal(pythonValue, CoffeeTransportPoint.FromValue(legacyValue).Value);
	}

	[Fact]
	public void CoffeeTransportPoint_RejectsUnknownValueInsteadOfChoosingSixthStreet()
	{
		Assert.False(CoffeeTransportPoint.TryFromValue("不存在的传送点", out CoffeeTransportPoint point));
		Assert.Null(point);
		Assert.Throws<ArgumentOutOfRangeException>(() => CoffeeTransportPoint.FromValue("不存在的传送点"));
	}

	[Theory]
	[InlineData(new object[] { true })]
	[InlineData(new object[] { false })]
	public void CoffeeConfig_MigratesLegacyApplicationPathsToPythonGroupPath(bool dotnetLegacyPath)
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00");
			string text3 = (dotnetLegacyPath ? Path.Combine(text2, "app_config", "one_dragon") : text2);
			Directory.CreateDirectory(text3);
			string path = Path.Combine(text3, "coffee.yml");
			File.WriteAllText(path, "predefined_team_idx: 7\n");
			CoffeeConfig coffeeConfig = CoffeeConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			string path2 = Path.Combine(text2, "one_dragon", "coffee.yml");
			Assert.Equal(7, coffeeConfig.PredefinedTeamIndex);
			Assert.True(File.Exists(path2));
			Assert.Equal(File.ReadAllText(path), File.ReadAllText(path2));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void SelectionService_UsesExtraCoffeePlanPriorityAndConfiguredFallback()
	{
		string text = CreateTempRootWithCoffeeData();
		try
		{
			CompendiumService compendiumService = new CompendiumService(new OneDragonEnvironment(text));
			CoffeeConfig config = new CoffeeConfig
			{
				ChooseWay = "优先体力计划",
				DayCoffee1 = "汀曼特调"
			};
			ChargePlanConfig chargePlanConfig = new ChargePlanConfig();
			int num = 1;
			List<ChargePlanItem> list = new List<ChargePlanItem>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = new ChargePlanItem
			{
				CategoryName = "实战模拟室",
				MissionTypeName = "代理人技能",
				MissionName = "升温测试",
				RunTimes = 0,
				PlanTimes = 1
			};
			chargePlanConfig.PlanList = list;
			ChargePlanConfig chargePlanConfig2 = chargePlanConfig;
			IReadOnlyList<string> coffeeToChoose = new CoffeeSelectionService().GetCoffeeToChoose(config, chargePlanConfig2, compendiumService, 1, new string[] { "沙罗特调" });
			Assert.Equal(new string[2] { "麦草拿提", "汀曼特调" }, coffeeToChoose);
			Assert.True(CoffeeSelectionService.IsCoffeeForPlan(compendiumService.NameToCoffee["麦草拿提"], chargePlanConfig2.PlanList[0]));
			Assert.False(CoffeeSelectionService.IsCoffeeForPlan(compendiumService.NameToCoffee["汀曼特调"], chargePlanConfig2.PlanList[0]));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void SelectionService_PreservesPythonFirstScheduledCoffeeRuleForPlans()
	{
		string text = CreateTempRootWithCoffeeData();
		try
		{
			string path = Path.Combine(text, "assets", "game_data", "coffee_data.yml");
			File.WriteAllText(path, File.ReadAllText(path).Replace("coffee_list: [\"麦草拿提\", \"汀曼特调\"]", "coffee_list: [\"汀曼特调\", \"麦草拿提\"]", StringComparison.Ordinal));
			CompendiumService compendiumService = new CompendiumService(new OneDragonEnvironment(text));
			CoffeeConfig config = new CoffeeConfig
			{
				ChooseWay = "优先体力计划",
				DayCoffee1 = "汀曼特调"
			};
			ChargePlanConfig chargePlanConfig = new ChargePlanConfig();
			int num = 1;
			List<ChargePlanItem> list = new List<ChargePlanItem>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = new ChargePlanItem
			{
				CategoryName = "实战模拟室",
				MissionTypeName = "代理人技能",
				MissionName = "升温测试",
				RunTimes = 0,
				PlanTimes = 1
			};
			chargePlanConfig.PlanList = list;
			ChargePlanConfig chargePlanConfig2 = chargePlanConfig;
			IReadOnlyList<string> coffeeToChoose = new CoffeeSelectionService().GetCoffeeToChoose(config, chargePlanConfig2, compendiumService, 1, Array.Empty<string>());
			Assert.Equal(new string[2] { "沙罗特调", "汀曼特调" }, coffeeToChoose);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task CoffeeApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingCoffeeFlow flow = new RecordingCoffeeFlow();
			CoffeeRunRecord runRecord = new CoffeeRunRecord();
			CoffeeApp app = new CoffeeApp(context, new CoffeeConfig(), new ChargePlanConfig(), runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("咖啡完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal("优先体力计划", flow.LastConfig?.ChooseWay);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void CoffeeRunRecord_UsesCoffeeAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		CoffeeRunRecord coffeeRunRecord = new CoffeeRunRecord(4, () => now);
		coffeeRunRecord.UpdateStatus(1);
		Assert.Equal("coffee", coffeeRunRecord.AppId);
		Assert.Equal("20260706", coffeeRunRecord.Dt);
		Assert.True(coffeeRunRecord.IsDone);
	}

	[Fact]
	public async Task CoffeeOperation_UsesInjectedTransportWaitBackAndAfterwardsFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			int transportCount = 0;
			int waitCount = 0;
			int moveCount = 0;
			int backCount = 0;
			int afterwardsCount = 0;
			CoffeeConfig config = new CoffeeConfig
			{
				RunChargePlanAfterwards = true
			};
			CoffeeOperation operation = new CoffeeOperation(context, config, new ChargePlanConfig(), null, null, delegate
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
			}, null, null, null, delegate
			{
				afterwardsCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "体力计划完成"));
			}, delegate
			{
				moveCount++;
				return new OperationRoundResult(OperationRoundResultKind.Success);
			});
			OperationRoundResult transport = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult wait = await operation.WaitWorld().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult move = operation.MoveAndInteract();
			OperationRoundResult back = await operation.BackToWorld().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult afterwards = await operation.ChargePlanAfterwards().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(transport.IsSuccess);
			Assert.Equal("传送完成", transport.Status);
			Assert.True(wait.IsSuccess);
			Assert.Equal("大世界", wait.Status);
			Assert.True(move.IsSuccess);
			Assert.True(back.IsSuccess);
			Assert.Equal("返回大世界", back.Status);
			Assert.True(afterwards.IsSuccess);
			Assert.Equal("体力计划完成", afterwards.Status);
			Assert.Equal(1, transportCount);
			Assert.Equal(1, waitCount);
			Assert.Equal(1, moveCount);
			Assert.Equal(1, backCount);
			Assert.Equal(1, afterwardsCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task CoffeeOperation_TransportDoesNotConsumeRetryUntilCoffeeShopLoadFailure()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			int transportCount = 0;
			AngleTurnCompensator compensator = new AngleTurnCompensator(delegate
			{
			});
			compensator.TurnFromAngle(90.0, 20.0);
			CoffeeOperation operation = new CoffeeOperation(context, new CoffeeConfig(), new ChargePlanConfig(), null, null, delegate
			{
				transportCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, $"传送完成{transportCount}"));
			}, null, null, null, null, null, null, null, compensator);
			OperationRoundResult first = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult second = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(first.IsSuccess);
			Assert.True(second.IsSuccess);
			Assert.Equal("传送完成1", first.Status);
			Assert.Equal("传送完成2", second.Status);
			Assert.Equal(2, transportCount);
			Assert.Null(compensator.LastSourceAngle);
			Assert.Null(compensator.LastEffectiveAngleDiff);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void CoffeeOperation_ClickCoffeeByOcrAppliesPythonWhiteMaskAndOffset()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRootWithCoffeeData();
		try
		{
			WriteCoffeeScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			RecordingClickController recordingClickController = new RecordingClickController();
			zContext.AttachController(recordingClickController);
			CapturingOcrMatcher capturingOcrMatcher = new CapturingOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 70, 60, 20, "汀曼特调") });
			zContext.OcrService.Matcher = capturingOcrMatcher;
			CoffeeOperation coffeeOperation = new CoffeeOperation(zContext, new CoffeeConfig(), new ChargePlanConfig());
			using Mat mat = new Mat(new Size(120, 120), MatType.CV_8UC3, Scalar.Black);
			Cv2.Rectangle(mat, new OpenCvSharp.Rect(10, 20, 80, 80), new Scalar(20.0, 20.0, 20.0), -1);
			mat.Set(30, 30, new Vec3b(byte.MaxValue, byte.MaxValue, byte.MaxValue));
			mat.Set(40, 40, new Vec3b(0, 0, byte.MaxValue));
			OperationRoundResult operationRoundResult = coffeeOperation.ClickCoffeeByOcrForTesting(mat, new string[] { "汀曼特调" });
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("汀曼特调", operationRoundResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(60, 50), recordingClickController.LastClickPoint);
			Assert.NotNull(capturingOcrMatcher.LastImage);
			Vec3b vec3b = capturingOcrMatcher.LastImage.At<Vec3b>(10, 20);
			Vec3b vec3b2 = capturingOcrMatcher.LastImage.At<Vec3b>(20, 30);
			Assert.Equal(255, vec3b.Item0);
			Assert.Equal(255, vec3b.Item1);
			Assert.Equal(255, vec3b.Item2);
			Assert.Equal(0, vec3b2.Item0);
			Assert.Equal(0, vec3b2.Item1);
			Assert.Equal(0, vec3b2.Item2);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CoffeeOperation_ClickCoffeeByOcrUsesGameTextResolver()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRootWithCoffeeData();
		try
		{
			WriteCoffeeScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			zContext.GameTextResolver = (string text2) => (text2 == "汀曼特调") ? "Tinman Special" : text2;
			RecordingClickController recordingClickController = new RecordingClickController();
			zContext.AttachController(recordingClickController);
			zContext.OcrService.Matcher = new CapturingOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 70, 60, 20, "Tinman Special") });
			CoffeeOperation coffeeOperation = new CoffeeOperation(zContext, new CoffeeConfig(), new ChargePlanConfig());
			using Mat screen = new Mat(new Size(120, 120), MatType.CV_8UC3, Scalar.Black);
			OperationRoundResult operationRoundResult = coffeeOperation.ClickCoffeeByOcrForTesting(screen, new string[] { "汀曼特调" });
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("汀曼特调", operationRoundResult.Status);
			Assert.NotNull(recordingClickController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CoffeeOperation_ResetsPlansBeforePlanPrioritySelection()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			ChargePlanConfig chargePlanConfig = new ChargePlanConfig();
			int num = 1;
			List<ChargePlanItem> list = new List<ChargePlanItem>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = new ChargePlanItem
			{
				CategoryName = "实战模拟室",
				MissionTypeName = "代理人技能",
				PlanTimes = 1,
				RunTimes = 1
			};
			chargePlanConfig.PlanList = list;
			ChargePlanConfig chargePlanConfig2 = chargePlanConfig;
			CoffeeOperation coffeeOperation = new CoffeeOperation(context, new CoffeeConfig
			{
				ChooseWay = "优先体力计划"
			}, chargePlanConfig2);
			OperationRoundResult operationRoundResult = coffeeOperation.ChooseCoffee();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal(0, chargePlanConfig2.PlanList[0].RunTimes);
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

	private static string CreateTempRootWithCoffeeData()
	{
		string text = CreateTempRoot();
		string text2 = Path.Combine(text, "assets", "game_data");
		Directory.CreateDirectory(text2);
		File.WriteAllText(Path.Combine(text2, "compendium_data.yml"), "- tab_name: \"训练\"\n  category_list:\n    - category_name: \"实战模拟室\"\n      mission_type_list:\n        - mission_type_name: \"代理人技能\"\n          mission_list:\n            - mission_name: \"升温测试\"\n              mission_name_display: \"火属性\"");
		File.WriteAllText(Path.Combine(text2, "coffee_data.yml"), "coffee_list:\n  - coffee_name: \"汀曼特调\"\n  - coffee_name: \"麦草拿提\"\n    tab_name: \"训练\"\n    category_name: \"实战模拟室\"\n    mission_type_name: \"代理人技能\"\n    mission_name: \"升温测试\"\n  - coffee_name: \"沙罗特调\"\n    extra: true\nschedule:\n  - days: [1]\n    coffee_list: [\"麦草拿提\", \"汀曼特调\"]");
		return text;
	}

	private static void WriteCoffeeScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: coffee_shop\n  screen_name: \"咖啡店\"\n  area_list:\n    - area_name: \"咖啡列表\"\n      pc_rect: [10, 20, 90, 100]");
	}
}
