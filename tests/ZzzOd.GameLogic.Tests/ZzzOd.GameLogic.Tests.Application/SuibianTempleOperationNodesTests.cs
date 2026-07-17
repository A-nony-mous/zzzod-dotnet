using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.SuibianTemple;
using ZzzOd.GameLogic.Application.SuibianTemple.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class SuibianTempleOperationNodesTests
{
	private sealed class RecordingController(Mat screenshot) : ControllerBase
	{
		public int ClickCount { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPoint { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastDragStart { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastDragEnd { get; private set; }

		public TimeSpan? LastDragDuration { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastMouseMovePoint { get; private set; }

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			ClickCount++;
			LastClickPoint = position;
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
			LastDragEnd = end;
			LastDragStart = start;
			LastDragDuration = duration;
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
			LastMouseMovePoint = position;
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return screenshot.Clone();
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
	public void AdventureDispatch_DeclaresPythonNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = GetNodes<SuibianTempleAdventureDispatch>();
		Assert.Equal(new string[9] { "检查画面", "选择游历时间", "游历时间弹窗确认", "点击自动选择邦布", "点击派遣", "点击派遣弹窗确认", "已派遣", "无法派遣", "派遣成功" }, nodes.Keys);
		Assert.True(nodes["检查画面"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(1, nodes["游历时间弹窗确认"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Contains(nodes["无法派遣"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "点击派遣" && edge.Status == "邦布电量不足");
		Assert.Contains(nodes["派遣成功"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "点击派遣弹窗确认" && !edge.Success);
	}

	[Fact]
	public void AdventureSquad_DeclaresPythonNodesAndConfigBranches()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = GetNodes<SuibianTempleAdventureSquad>();
		using ZContext context = CreateContext();
		SuibianTempleAdventureSquad suibianTempleAdventureSquad = new SuibianTempleAdventureSquad(context, new SuibianTempleConfig(), claim: false, dispatch: false);
		OperationRoundResult operationRoundResult = suibianTempleAdventureSquad.ClickSquadTeam();
		OperationRoundResult operationRoundResult2 = suibianTempleAdventureSquad.PrepareToChooseMission();
		Assert.Equal(new string[11]
		{
			"前往游历", "点击游历小队", "点击游历完成", "点击可收获", "点击确认", "收获后重新派遣", "准备选择副本", "选择副本", "选择子副本", "选择新派遣",
			"返回随便观"
		}, nodes.Keys);
		Assert.True(nodes["前往游历"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal("跳过收获", operationRoundResult.Status);
		Assert.Equal("跳过派遣", operationRoundResult2.Status);
		Assert.Contains(nodes["返回随便观"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "准备选择副本" && edge.Status == "已完成所有副本选择");
	}

	[Fact]
	public void AutoManageCraftAndCraftDispatch_DeclarePythonNodes()
	{
		Assert.Equal(new string[2] { "检查并停止托管", "返回随便观" }, GetNodes<SuibianTempleAutoManage>().Keys);
		Assert.Equal(new string[4] { "前往制造", "点击开工", "制造派驻", "返回随便观" }, GetNodes<SuibianTempleCraft>().Keys);
		Assert.Equal(new string[7] { "检查邦布", "打开选择邦布", "选择邦布", "点击派驻", "选择商品", "点击开始制造", "完成后返回" }, GetNodes<SuibianTempleCraftDispatch>().Keys);
		Assert.Equal(1, GetNodes<SuibianTempleCraftDispatch>()["检查邦布"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
	}

	[Fact]
	public void AutoManage_KeepsWaitingWhenStopHostingStaysVisible()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("ZZZOD_ACTION_DEBUG_DIR");
		Environment.SetEnvironmentVariable("ZZZOD_ACTION_DEBUG_DIR", null);
		try
		{
			using ZContext zContext = CreateContext();
			using Mat screenshot = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.88, 1157, 879, 129, 41, "停止托管") });
			SuibianTempleAutoManage suibianTempleAutoManage = new SuibianTempleAutoManage(zContext, new SuibianTempleConfig());
			SetLastScreenshot(suibianTempleAutoManage, screenshot);
			OperationRoundResult operationRoundResult = suibianTempleAutoManage.CheckAndStopHosting();
			OperationRoundResult operationRoundResult2 = suibianTempleAutoManage.CheckAndStopHosting();
			OperationRoundResult operationRoundResult3 = suibianTempleAutoManage.CheckAndStopHosting();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal("点击停止", operationRoundResult.Status);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult2.Kind);
			Assert.Equal("点击停止", operationRoundResult2.Status);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult3.Kind);
			Assert.Equal("点击停止", operationRoundResult3.Status);
			Assert.Equal(3, recordingController.ClickCount);
		}
		finally
		{
			Environment.SetEnvironmentVariable("ZZZOD_ACTION_DEBUG_DIR", environmentVariable);
		}
	}

	[Fact]
	public void AdventureDispatch_IgnoresNonTargetDurationAndDispatchHeader()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		RecordingController recordingController = new RecordingController(screenshot);
		zContext.AttachController(recordingController);
		SuibianTempleAdventureDispatch suibianTempleAdventureDispatch = new SuibianTempleAdventureDispatch(zContext, new SuibianTempleConfig(), SuibianTempleAdventureDispatchDuration.Hour20.Name);
		SetLastScreenshot(suibianTempleAdventureDispatch, screenshot);
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 10, 10, 60, 20, "3分钟"),
			new OcrMatchResult(0.99, 100, 10, 60, 20, "确认")
		});
		OperationRoundResult operationRoundResult = suibianTempleAdventureDispatch.ChoosePeriod();
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("确认", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(130, 20), recordingController.LastClickPoint);
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 200, 20, 100, 20, "可派遣小队"),
			new OcrMatchResult(0.99, 360, 20, 60, 20, "派遣")
		});
		OperationRoundResult operationRoundResult2 = suibianTempleAdventureDispatch.ClickDispatch();
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult2.Kind);
		Assert.Equal("派遣", operationRoundResult2.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(390, 30), recordingController.LastClickPoint);
	}

	[Fact]
	public void AdventureSquad_DragsHorizontallyWhenMissionIsMissing()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		RecordingController recordingController = new RecordingController(screenshot);
		zContext.AttachController(recordingController);
		zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
		SuibianTempleAdventureSquad suibianTempleAdventureSquad = new SuibianTempleAdventureSquad(zContext, new SuibianTempleConfig(), claim: false);
		SetLastScreenshot(suibianTempleAdventureSquad, screenshot);
		OperationRoundResult operationRoundResult = suibianTempleAdventureSquad.PrepareToChooseMission();
		OperationRoundResult operationRoundResult2 = suibianTempleAdventureSquad.ChooseMission();
		Assert.True(operationRoundResult.IsSuccess);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
		Assert.Equal("未识别到副本", operationRoundResult2.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 540), recordingController.LastDragStart);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(160, 540), recordingController.LastDragEnd);
	}

	[Fact]
	public void AdventureSquad_AlternatesHorizontalDragByNodeRetryTimesLikePython()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		RecordingController recordingController = new RecordingController(screenshot);
		zContext.AttachController(recordingController);
		zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
		SuibianTempleAdventureSquad suibianTempleAdventureSquad = new SuibianTempleAdventureSquad(zContext, new SuibianTempleConfig(), claim: false);
		SetLastScreenshot(suibianTempleAdventureSquad, screenshot);
		FieldInfo field = typeof(Operation).GetField("_nodeRetryTimes", BindingFlags.Instance | BindingFlags.NonPublic);
		suibianTempleAdventureSquad.PrepareToChooseMission();
		field.SetValue(suibianTempleAdventureSquad, 0);
		suibianTempleAdventureSquad.ChooseMission();
		OneDragon.Core.Abstractions.Geometry.Point? lastDragEnd = recordingController.LastDragEnd;
		field.SetValue(suibianTempleAdventureSquad, 1);
		suibianTempleAdventureSquad.ChooseMission();
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(160, 540), lastDragEnd);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1760, 540), recordingController.LastDragEnd);
	}

	[Fact]
	public void AdventureSquad_WaitsUntilAdventureButtonDisappearsAfterClick()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			string text2 = Path.Combine(text, "assets", "game_data", "screen_info");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "_od_merged.yml"), "- screen_id: suibian_temple_entry\n  screen_name: \"随便观-入口\"\n  area_list:\n    - area_name: \"按钮-游历\"\n      pc_rect: [0, 0, 200, 100]\n      text: \"游历\"");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			SuibianTempleAdventureSquad suibianTempleAdventureSquad = new SuibianTempleAdventureSquad(zContext, new SuibianTempleConfig());
			SetLastScreenshot(suibianTempleAdventureSquad, screenshot);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 20, 40, 20, "游历") });
			OperationRoundResult operationRoundResult = suibianTempleAdventureSquad.GoToAdventure();
			OperationRoundResult operationRoundResult2 = suibianTempleAdventureSquad.GoToAdventure();
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			OperationRoundResult operationRoundResult3 = suibianTempleAdventureSquad.GoToAdventure();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult2.Kind);
			Assert.Equal(OperationRoundResultKind.Success, operationRoundResult3.Kind);
			Assert.Equal(2, recordingController.ClickCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CraftDispatch_SkipsWorkingBangbooAndSelectsFirstAvailableSlot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WriteCraftScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 30, "制造中") });
			SuibianTempleCraftDispatch suibianTempleCraftDispatch = new SuibianTempleCraftDispatch(zContext, new SuibianTempleConfig(), fromCraft: true, new List<string>());
			SetLastScreenshot(suibianTempleCraftDispatch, screenshot);
			OperationRoundResult operationRoundResult = suibianTempleCraftDispatch.ChooseBangboo();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("已选择邦布", operationRoundResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(150, 50), recordingController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CraftDispatch_ChoosesCraftableItemAtPythonRightBottomOffset()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WriteCraftScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[3]
			{
				new OcrMatchResult(0.99, 20, 600, 120, 20, "所需材料不足"),
				new OcrMatchResult(0.99, 100, 100, 100, 20, "某商品1"),
				new OcrMatchResult(0.99, 300, 105, 80, 20, "可制造")
			});
			SuibianTempleCraftDispatch suibianTempleCraftDispatch = new SuibianTempleCraftDispatch(zContext, new SuibianTempleConfig(), fromCraft: true, new List<string>());
			SetLastScreenshot(suibianTempleCraftDispatch, screenshot);
			OperationRoundResult operationRoundResult = suibianTempleCraftDispatch.ChooseItem();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal("选择下一个商品", operationRoundResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(250, 120), recordingController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void SalesStall_IgnoresHeaderWhenChoosingShelfToSell()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		RecordingController recordingController = new RecordingController(screenshot);
		zContext.AttachController(recordingController);
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 20, 20, 70, 20, "售卖铺"),
			new OcrMatchResult(0.99, 150, 20, 80, 20, "开始售卖")
		});
		SuibianTempleSalesStall suibianTempleSalesStall = new SuibianTempleSalesStall(zContext, new SuibianTempleConfig());
		SetLastScreenshot(suibianTempleSalesStall, screenshot);
		OperationRoundResult operationRoundResult = suibianTempleSalesStall.ClickChooseShelfSell();
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("开始售卖", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(190, 30), recordingController.LastClickPoint);
	}

	[Fact]
	public void GoodGoods_DragsExchangeSliderBeforeConfirming()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		RecordingController recordingController = new RecordingController(screenshot);
		zContext.AttachController(recordingController);
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 400, 300, 80, 30, "兑换确认"),
			new OcrMatchResult(0.99, 960, 700, 80, 30, "确认")
		});
		SuibianTempleGoodGoods suibianTempleGoodGoods = new SuibianTempleGoodGoods(zContext, new SuibianTempleConfig());
		SetLastScreenshot(suibianTempleGoodGoods, screenshot);
		OperationRoundResult operationRoundResult = suibianTempleGoodGoods.ProcessGoodGoods();
		Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
		Assert.Equal("已确认兑换", operationRoundResult.Status);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(755, 672), recordingController.LastDragStart);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1300, 672), recordingController.LastDragEnd);
		Assert.Equal(TimeSpan.FromSeconds(2L), recordingController.LastDragDuration);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1000, 715), recordingController.LastClickPoint);
	}

	[Fact]
	public void GoodGoods_ChoosesLeftmostBottomPluginOnlyWhenItsPriceIsPresent()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		RecordingController recordingController = new RecordingController(screenshot);
		zContext.AttachController(recordingController);
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[3]
		{
			new OcrMatchResult(0.99, 320, 400, 100, 20, "邦布能源插件"),
			new OcrMatchResult(0.99, 100, 560, 100, 20, "邦布能源插件"),
			new OcrMatchResult(0.99, 20, 20, 40, 20, "5OO")
		});
		SuibianTempleGoodGoods suibianTempleGoodGoods = new SuibianTempleGoodGoods(zContext, new SuibianTempleConfig());
		SetLastScreenshot(suibianTempleGoodGoods, screenshot);
		OperationRoundResult operationRoundResult = suibianTempleGoodGoods.ProcessGoodGoods();
		Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
		Assert.Equal("已点击邦布能源插件", operationRoundResult.Status);
		Assert.Equal(TimeSpan.FromMilliseconds(1500L), operationRoundResult.Delay);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(150, 570), recordingController.LastClickPoint);
	}

	[Fact]
	public void GoodGoods_SkipsSelectedPluginWhenItsCardShowsSoldOut()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		RecordingController recordingController = new RecordingController(screenshot);
		zContext.AttachController(recordingController);
		zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
		{
			new OcrMatchResult(0.99, 100, 560, 100, 20, "邦布能源插件"),
			new OcrMatchResult(0.99, 20, 10, 60, 20, "已售罄")
		});
		SuibianTempleGoodGoods suibianTempleGoodGoods = new SuibianTempleGoodGoods(zContext, new SuibianTempleConfig());
		SetLastScreenshot(suibianTempleGoodGoods, screenshot);
		OperationRoundResult operationRoundResult = suibianTempleGoodGoods.ProcessGoodGoods();
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Equal("跳过购买-已售罄", operationRoundResult.Status);
		Assert.Equal(0, recordingController.ClickCount);
	}

	[Fact]
	public void BooBox_SkipsRejectedCardAndSelectsNextUnseenPriceCard()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WriteBooBoxScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[4]
			{
				new OcrMatchResult(0.99, 0, 0, 50, 20, "聘用"),
				new OcrMatchResult(0.99, 0, 0, 50, 20, "售卖"),
				new OcrMatchResult(0.99, 300, 220, 80, 30, "40000"),
				new OcrMatchResult(0.99, 600, 220, 80, 30, "40000")
			});
			SuibianTempleBooBox suibianTempleBooBox = new SuibianTempleBooBox(zContext, new SuibianTempleConfig
			{
				BooBoxSellPrice = "NONE"
			});
			SetLastScreenshot(suibianTempleBooBox, screenshot);
			OperationRoundResult operationRoundResult = suibianTempleBooBox.CheckBangboo();
			OperationRoundResult operationRoundResult2 = suibianTempleBooBox.CheckBangbooType();
			OperationRoundResult operationRoundResult3 = suibianTempleBooBox.CheckBangboo();
			Assert.Equal("点击S级邦布", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromMilliseconds(1500L), operationRoundResult.Delay);
			Assert.Equal("不购买该类型邦布", operationRoundResult2.Status);
			Assert.Equal("点击S级邦布", operationRoundResult3.Status);
			Assert.Equal(2, recordingController.ClickCount);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(640, 85), recordingController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void BooBox_DragsSkipFromLeftTopThenClicksAtCurrentPointer()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WriteBooBoxScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			SuibianTempleBooBox suibianTempleBooBox = new SuibianTempleBooBox(zContext, new SuibianTempleConfig());
			SetLastScreenshot(suibianTempleBooBox, screenshot);
			OperationRoundResult operationRoundResult = suibianTempleBooBox.HandlePurchaseAnimation();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal("点击跳过", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromMilliseconds(500L), operationRoundResult.Delay);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(500, 500), recordingController.LastDragStart);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(600, 550), recordingController.LastDragEnd);
			Assert.Equal(TimeSpan.FromMilliseconds(200L), recordingController.LastDragDuration);
			Assert.Null(recordingController.LastClickPoint);
			Assert.Equal(1, recordingController.ClickCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void BooBox_WaitsAfterCancellingAtHireLimit()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WriteBooBoxScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 0, 0, 100, 20, "无法聘用"),
				new OcrMatchResult(0.99, 0, 0, 100, 20, "取消")
			});
			SuibianTempleBooBox suibianTempleBooBox = new SuibianTempleBooBox(zContext, new SuibianTempleConfig());
			SetLastScreenshot(suibianTempleBooBox, screenshot);
			OperationRoundResult operationRoundResult = suibianTempleBooBox.HandlePurchaseAnimation();
			Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
			Assert.Equal("持有上限", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
			Assert.Equal(1, recordingController.ClickCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void SalesStall_UsesPythonRetryDelaysForCancelAndStart()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		zContext.AttachController(new RecordingController(screenshot));
		zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
		SuibianTempleSalesStall suibianTempleSalesStall = new SuibianTempleSalesStall(zContext, new SuibianTempleConfig());
		SetLastScreenshot(suibianTempleSalesStall, screenshot);
		OperationRoundResult operationRoundResult = suibianTempleSalesStall.CancelSelling();
		OperationRoundResult operationRoundResult2 = suibianTempleSalesStall.ClickStartSelling();
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal(TimeSpan.FromMilliseconds(500L), operationRoundResult.Delay);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
		Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult2.Delay);
	}

	[Fact]
	public void YumChaSin_UsesPythonBrightnessThresholdForProcurementButtons()
	{
		MethodInfo method = typeof(SuibianTempleYumChaSin).GetMethod("IsButtonAvailable", BindingFlags.Static | BindingFlags.NonPublic, new Type[1] { typeof(Mat) });
		using Mat mat = new Mat(10, 10, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(0, 0, 9, 9), Scalar.White, -1);
		using Mat mat2 = new Mat(10, 10, MatType.CV_8UC3, new Scalar(229.0, 229.0, 229.0));
		Assert.True((bool)method.Invoke(null, new object[1] { mat }));
		Assert.False((bool)method.Invoke(null, new object[1] { mat2 }));
	}

	[Fact]
	public void YumChaSin_UsesDifflibForTaskAndMaterialDeduplication()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WriteYumChaSinScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			SuibianTempleYumChaSin suibianTempleYumChaSin = new SuibianTempleYumChaSin(zContext, new SuibianTempleConfig());
			SetLastScreenshot(suibianTempleYumChaSin, screenshot);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 20, 100, 20, "委托测试甲") });
			zContext.OcrService.ClearCache();
			Assert.True(suibianTempleYumChaSin.CheckRegularProcurement().IsSuccess);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 20, 100, 20, "委托测试乙") });
			zContext.OcrService.ClearCache();
			OperationRoundResult operationRoundResult = suibianTempleYumChaSin.CheckRegularProcurement();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(200, 150), recordingController.LastDragStart);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(200, -250), recordingController.LastDragEnd);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 10, 20, 20, "绿茶叶") });
			zContext.OcrService.ClearCache();
			Assert.False(suibianTempleYumChaSin.GoToCraft().IsSuccess);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 10, 20, 20, "绿茶叶材") });
			zContext.OcrService.ClearCache();
			OperationRoundResult operationRoundResult2 = suibianTempleYumChaSin.GoToCraft();
			Assert.Equal(OperationRoundResultKind.Success, operationRoundResult2.Kind);
			Assert.Equal("材料已处理过", operationRoundResult2.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void YumChaSin_ClearsMaterialPositionsWhenSwitchingProcurement()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WriteYumChaSinScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			SuibianTempleYumChaSin suibianTempleYumChaSin = new SuibianTempleYumChaSin(zContext, new SuibianTempleConfig());
			SetLastScreenshot(suibianTempleYumChaSin, screenshot);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 20, 100, 20, "葡萄订单") });
			zContext.OcrService.ClearCache();
			Assert.True(suibianTempleYumChaSin.CheckRegularProcurement().IsSuccess);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 10, 20, 20, "1") });
			zContext.OcrService.ClearCache();
			Assert.True(suibianTempleYumChaSin.CheckLackOfMaterial().IsSuccess);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 20, 100, 20, "香蕉委托") });
			zContext.OcrService.ClearCache();
			Assert.True(suibianTempleYumChaSin.CheckRegularProcurement().IsSuccess);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 10, 20, 20, "1") });
			zContext.OcrService.ClearCache();
			Assert.True(suibianTempleYumChaSin.CheckLackOfMaterial().IsSuccess);
			Assert.Equal(4, recordingController.ClickCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void YumChaSin_ResetsCraftFlagBeforeEachCraftAttempt()
	{
		using ZContext zContext = CreateContext();
		using Mat screenshot = CreateScreen();
		zContext.AttachController(new RecordingController(screenshot));
		zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
		SuibianTempleYumChaSin suibianTempleYumChaSin = new SuibianTempleYumChaSin(zContext, new SuibianTempleConfig());
		SetLastScreenshot(suibianTempleYumChaSin, screenshot);
		FieldInfo field = typeof(SuibianTempleYumChaSin).GetField("_doneCraft", BindingFlags.Instance | BindingFlags.NonPublic);
		field.SetValue(suibianTempleYumChaSin, true);
		suibianTempleYumChaSin.GoToCraft();
		Assert.False((bool)field.GetValue(suibianTempleYumChaSin));
	}

	[Fact]
	public void OptionalShopOperations_DeclarePythonNodes()
	{
		Assert.Equal(new string[5] { "前往邻里街坊", "已在邻里街坊-进入好物铺", "已在好物铺-购买", "好物铺-返回邻里", "返回随便观" }, GetNodes<SuibianTempleGoodGoods>().Keys);
		Assert.Equal(new string[7] { "前往邦巢", "检查邦布", "检查邦布类型", "点击聘用", "处理购买动画", "返回界面", "返回随便观" }, GetNodes<SuibianTempleBooBox>().Keys);
		Assert.Equal(new string[9] { "前往售卖铺", "更换邦布", "选择库存不足货架", "点击取消售卖", "取消售卖后返回售卖铺", "选择货架开始售卖", "选择商品", "点击开始售卖", "返回随便观" }, GetNodes<SuibianTempleSalesStall>().Keys);
		Assert.Equal(5, GetNodes<SuibianTempleGoodGoods>()["前往邻里街坊"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(5, GetNodes<SuibianTempleBooBox>()["前往邦巢"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(2, GetNodes<SuibianTempleSalesStall>()["选择库存不足货架"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
	}

	[Fact]
	public void Pawnshop_DeclaresPythonNodesAndHonorsDisabledConfig()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = GetNodes<SuibianTemplePawnshop>();
		using ZContext context = CreateContext();
		SuibianTemplePawnshop suibianTemplePawnshop = new SuibianTemplePawnshop(context, new SuibianTempleConfig
		{
			PawnshopOmnicoinEnabled = false,
			PawnshopCrestEnabled = false
		});
		OperationRoundResult operationRoundResult = suibianTemplePawnshop.GoToOmnicoin();
		OperationRoundResult operationRoundResult2 = suibianTemplePawnshop.GoToCrest();
		Assert.Equal(new string[10] { "前往德丰大押", "切换到百通宝-周期", "选择百通宝商品", "购买百通宝商品", "购买百通宝商品后处理", "切换到云纹徽-周期", "选择云纹徽商品", "购买云纹徽商品", "购买云纹徽商品后处理", "返回随便观" }, nodes.Keys);
		Assert.True(nodes["前往德丰大押"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal("未开启", operationRoundResult.Status);
		Assert.Equal("未开启", operationRoundResult2.Status);
		Assert.Contains(nodes["返回随便观"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "购买云纹徽商品后处理" && !edge.Success);
	}

	[Fact]
	public void Pawnshop_ChoosesOnlyAvailableGoodsByPythonCardAssociation()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WritePawnshopScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[3]
			{
				new OcrMatchResult(0.99, 20, 20, 120, 20, "高保真母盘"),
				new OcrMatchResult(0.99, 20, 60, 60, 20, "已售罄"),
				new OcrMatchResult(0.99, 20, 180, 160, 20, "资深调查员记录")
			});
			SuibianTempleConfig suibianTempleConfig = new SuibianTempleConfig();
			int num = 2;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<string> span = CollectionsMarshal.AsSpan(list);
			span[0] = "HIFI_MASTER_COPY";
			span[1] = "SENIOR_INVESTIGATOR_LOG";
			suibianTempleConfig.PawnshopOmnicoinPriority = list;
			SuibianTemplePawnshop suibianTemplePawnshop = new SuibianTemplePawnshop(zContext, suibianTempleConfig);
			SetLastScreenshot(suibianTemplePawnshop, screenshot);
			OperationRoundResult operationRoundResult = suibianTemplePawnshop.ChooseOmnicoinGoods();
			Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
			Assert.Equal("资深调查员记录", operationRoundResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1151, 411), recordingController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Pawnshop_UsesPythonUnlimitedSelectionRules()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WritePawnshopScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 20, 20, 160, 20, "邦布系统控件"),
				new OcrMatchResult(0.99, 20, 60, 60, 20, "不限购")
			});
			SuibianTempleConfig suibianTempleConfig = new SuibianTempleConfig();
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "BANGBOO_SYSTEM_WIDGET";
			suibianTempleConfig.PawnshopCrestPriority = list;
			suibianTempleConfig.PawnshopCrestUnlimitedDennyEnabled = true;
			SuibianTemplePawnshop suibianTemplePawnshop = new SuibianTemplePawnshop(zContext, suibianTempleConfig);
			SetLastScreenshot(suibianTemplePawnshop, screenshot);
			OperationRoundResult operationRoundResult = suibianTemplePawnshop.ChooseCrestGoods();
			Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
			Assert.Equal("邦布系统控件", operationRoundResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1151, 251), recordingController.LastClickPoint);
			zContext.OcrService.ClearCache();
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 20, 20, 60, 20, "不限购"),
				new OcrMatchResult(0.99, 20, 60, 40, 20, "丁尼")
			});
			SuibianTempleConfig suibianTempleConfig2 = new SuibianTempleConfig();
			num = 1;
			List<string> list2 = new List<string>(num);
			CollectionsMarshal.SetCount(list2, num);
			CollectionsMarshal.AsSpan(list2)[0] = "HIFI_MASTER_COPY";
			suibianTempleConfig2.PawnshopOmnicoinPriority = list2;
			SuibianTemplePawnshop suibianTemplePawnshop2 = new SuibianTemplePawnshop(zContext, suibianTempleConfig2);
			SetLastScreenshot(suibianTemplePawnshop2, screenshot);
			OperationRoundResult operationRoundResult2 = suibianTemplePawnshop2.ChooseOmnicoinGoods();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
			Assert.Equal(1, recordingController.ClickCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Pawnshop_UsesPythonBuyDragAndClosesCurrencyShortage()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		try
		{
			WritePawnshopScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.ScreenContext.Reload();
			using Mat screenshot = CreateScreen();
			RecordingController recordingController = new RecordingController(screenshot);
			zContext.AttachController(recordingController);
			SuibianTemplePawnshop suibianTemplePawnshop = new SuibianTemplePawnshop(zContext, new SuibianTempleConfig());
			SetLastScreenshot(suibianTemplePawnshop, screenshot);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			suibianTemplePawnshop.BuyOmnicoinGoods();
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(756, 673), recordingController.LastDragStart);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1209, 673), recordingController.LastDragEnd);
			Assert.Equal(TimeSpan.FromSeconds(2L), recordingController.LastDragDuration);
			zContext.OcrService.ClearCache();
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 20, 20, 20, "1") });
			OperationRoundResult operationRoundResult = suibianTemplePawnshop.AfterBuyOmnicoinGoods();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1306, 313), recordingController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void YumChaSin_DeclaresPythonNodesAndSubmitOnlyBranch()
	{
		IReadOnlyDictionary<string, MethodInfo> nodes = GetNodes<SuibianTempleYumChaSin>();
		using ZContext context = CreateContext();
		SuibianTempleYumChaSin suibianTempleYumChaSin = new SuibianTempleYumChaSin(context, new SuibianTempleConfig(), submitOnly: true);
		OperationRoundResult operationRoundResult = suibianTempleYumChaSin.CheckRegularProcurement();
		Assert.Equal(new string[12]
		{
			"前往饮茶仙", "前往定期采办", "定期采办提交", "检查定期采办委托", "检查缺少的素材", "前往制作", "制造派驻", "前往游历", "派遣游历小队", "从游历返回材料菜单",
			"返回定期采办", "返回随便观"
		}, nodes.Keys);
		Assert.True(nodes["前往饮茶仙"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(2, nodes["定期采办提交"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(2, nodes["检查定期采办委托"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal("跳过缺失材料判断", operationRoundResult.Status);
		Assert.Contains(nodes["返回随便观"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "检查定期采办委托" && edge.Status == "跳过缺失材料判断");
	}

	private static IReadOnlyDictionary<string, MethodInfo> GetNodes<T>()
	{
		return (from method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
	}

	private static ZContext CreateContext()
	{
		string workDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		return new ZContext(new OneDragonEnvironment(workDirectory));
	}

	private static Mat CreateScreen()
	{
		return new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
	}

	private static void WriteCraftScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: suibian_temple_craft\n  screen_name: \"随便观-制造坊\"\n  area_list:\n    - area_name: \"区域-邦布-1\"\n      pc_rect: [0, 0, 100, 100]\n    - area_name: \"区域-邦布-2\"\n      pc_rect: [100, 0, 200, 100]\n    - area_name: \"区域-商品列表\"\n      pc_rect: [0, 0, 1000, 500]");
	}

	private static void WriteBooBoxScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: suibian_temple_boo_box\n  screen_name: \"随便观-邦巢\"\n  area_list:\n    - area_name: \"按钮-聘用\"\n      pc_rect: [0, 0, 200, 50]\n      text: \"聘用\"\n    - area_name: \"区域-邦布列表\"\n      pc_rect: [0, 100, 1000, 900]\n    - area_name: \"标题-邦布名称\"\n      pc_rect: [0, 50, 200, 100]\n      text: \"售卖\"\n    - area_name: \"按钮-刷新\"\n      pc_rect: [1200, 800, 1400, 900]\n    - area_name: \"标题-无法聘用\"\n      pc_rect: [0, 900, 200, 950]\n      text: \"无法聘用\"\n    - area_name: \"取消\"\n      pc_rect: [200, 0, 400, 50]\n      text: \"取消\"\n    - area_name: \"按钮-跳过\"\n      pc_rect: [500, 500, 700, 600]");
	}

	private static void WritePawnshopScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: suibian_temple_pawnshop\n  screen_name: \"随便观-德丰大押\"\n  area_list:\n    - area_name: \"区域-商品列表\"\n      pc_rect: [1051, 221, 1816, 937]\n    - area_name: \"按钮-购买件数-最小\"\n      pc_rect: [741, 659, 771, 688]\n    - area_name: \"按钮-购买件数-最大\"\n      pc_rect: [1150, 659, 1168, 687]\n    - area_name: \"按钮-确认\"\n      pc_rect: [995, 712, 1254, 801]\n      text: \"确认\"\n    - area_name: \"按钮-兑换关闭\"\n      pc_rect: [1279, 289, 1333, 337]\n    - area_name: \"区域-购买货币\"\n      pc_rect: [683, 712, 985, 807]");
	}

	private static void WriteYumChaSinScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: suibian_temple_yum_cha_sin\n  screen_name: \"随便观-饮茶仙\"\n  area_list:\n    - area_name: \"区域-任务列表\"\n      pc_rect: [0, 0, 400, 300]\n    - area_name: \"区域-材料名称\"\n      pc_rect: [0, 80, 400, 120]\n    - area_name: \"区域-材料数量\"\n      pc_rect: [0, 80, 400, 120]\n    - area_name: \"区域-材料-1\"\n      pc_rect: [0, 80, 200, 120]\n    - area_name: \"区域-材料-2\"\n      pc_rect: [0, 200, 200, 240]\n    - area_name: \"区域-材料-3\"\n      pc_rect: [0, 320, 200, 360]");
	}

	private static void SetLastScreenshot(ZOperation operation, Mat screenshot)
	{
		typeof(ZOperation).GetProperty("LastScreenshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, screenshot);
	}
}
