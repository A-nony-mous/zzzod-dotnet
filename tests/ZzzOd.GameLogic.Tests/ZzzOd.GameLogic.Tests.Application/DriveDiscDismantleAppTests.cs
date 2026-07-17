using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.DriveDiscDismantle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class DriveDiscDismantleAppTests
{
	private sealed class RecordingDriveDiscDismantleFlow : IDriveDiscDismantleAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, DriveDiscDismantleConfig config, DriveDiscDismantleRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "驱动盘拆解完成"));
		}
	}

	private sealed class RecordingDriveDiscDismantleServices : IDriveDiscDismantleOperationServices
	{
		public int BackCount { get; private set; }

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
		{
			BackCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}
	}

	private sealed class TestScreenshotController(Action? onClick = null) : ControllerBase, IDisposable
	{
		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPoint { get; private set; }

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			LastClickPoint = position;
			onClick?.Invoke();
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
			return new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
		}

		public void Dispose()
		{
			CleanupAfterAppShutdown();
		}
	}

	private sealed class MutableOcrMatcher(IReadOnlyList<OcrMatchResult> results) : IOcrMatcher
	{
		public IReadOnlyList<OcrMatchResult> Results { get; set; } = results;

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
			return string.Concat(from result in Results
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
			return Results.Select((OcrMatchResult result) => new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesDriveDiscDismantleApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DriveDiscDismantleAppFactory driveDiscDismantleAppFactory = zContext.ApplicationFactoryRegistry.CreateDriveDiscDismantleFactory();
			IApplication application = driveDiscDismantleAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = driveDiscDismantleAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = driveDiscDismantleAppFactory.GetRunRecord(0);
			Assert.Equal("drive_disc_dismantle", driveDiscDismantleAppFactory.AppId);
			Assert.Equal("驱动盘拆解", driveDiscDismantleAppFactory.AppName);
			Assert.Equal("one_dragon", driveDiscDismantleAppFactory.GroupId);
			Assert.True(driveDiscDismantleAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<DriveDiscDismantleApp>(application);
			Assert.IsType<DriveDiscDismantleConfig>(config);
			Assert.IsType<DriveDiscDismantleRunRecord>(runRecord);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersDriveDiscDismantleAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterDriveDiscDismantleApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("drive_disc_dismantle"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("drive_disc_dismantle"));
			Assert.Contains("drive_disc_dismantle", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DriveDiscDismantleConfig_LoadsPythonFieldsAndSettingsMetadata()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "drive_disc_dismantle.yml"), "dismantle_level: \"S及以下\"\ndismantle_abandon: true");
			DriveDiscDismantleConfig driveDiscDismantleConfig = DriveDiscDismantleConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("drive_disc_dismantle", driveDiscDismantleConfig.AppId);
			Assert.Equal("S及以下", driveDiscDismantleConfig.DismantleLevel);
			Assert.True(driveDiscDismantleConfig.DismantleAbandon);
			Assert.Contains((IEnumerable<ConfigItem>)DismantleLevel.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, "A及以下")));
			Assert.Equal("FLYOUT", "FLYOUT");
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DriveDiscDismantleRunRecord_UsesAppId()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		DriveDiscDismantleRunRecord driveDiscDismantleRunRecord = new DriveDiscDismantleRunRecord(0, () => now);
		driveDiscDismantleRunRecord.UpdateStatus(1);
		Assert.Equal("drive_disc_dismantle", driveDiscDismantleRunRecord.AppId);
		Assert.Equal("20260706", driveDiscDismantleRunRecord.Dt);
		Assert.True(driveDiscDismantleRunRecord.IsDone);
	}

	[Fact]
	public async Task DriveDiscDismantleApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DriveDiscDismantleConfig config = new DriveDiscDismantleConfig();
			DriveDiscDismantleRunRecord runRecord = new DriveDiscDismantleRunRecord();
			RecordingDriveDiscDismantleFlow flow = new RecordingDriveDiscDismantleFlow();
			DriveDiscDismantleApp app = new DriveDiscDismantleApp(context, config, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("驱动盘拆解完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DriveDiscDismantleOperation_BackNodesUseInjectedServicesWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DriveDiscDismantleConfig config = new DriveDiscDismantleConfig
			{
				DismantleLevel = "S及以下",
				DismantleAbandon = true
			};
			RecordingDriveDiscDismantleServices services = new RecordingDriveDiscDismantleServices();
			DriveDiscDismantleOperation operation = new DriveDiscDismantleOperation(context, config, services);
			await operation.BackAtFirst().WaitAsync(TimeSpan.FromSeconds(2L));
			await operation.BackAtLast().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(2, services.BackCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void DriveDiscDismantleOperation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(DriveDiscDismantleOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[9] { "开始前返回", "前往分解画面", "快速选择", "选择等级", "选择弃置", "快速选择确认", "点击拆解", "点击拆解确认", "完成后返回" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["开始前返回"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["快速选择确认"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "选择等级" && !edge.Success);
		Assert.Contains(readOnlyDictionary["完成后返回"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "点击拆解确认" && !edge.Success);
	}

	[Fact]
	public void DriveDiscDismantleOperation_GotoSalvage_UsesRoundByGotoScreenWaitAndRetryDelay()
	{
		string text = CreateTempRoot();
		try
		{
			WriteDriveDiscScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			MutableOcrMatcher matcher = new MutableOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 10, 10, 80, 20, "驱动仓库"),
				new OcrMatchResult(0.99, 20, 20, 60, 20, "拆解")
			});
			using TestScreenshotController testScreenshotController = new TestScreenshotController(delegate
			{
				matcher.Results = new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 120, 20, "驱动盘拆解") };
			});
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = matcher;
			DriveDiscDismantleOperation driveDiscDismantleOperation = new DriveDiscDismantleOperation(zContext, new DriveDiscDismantleConfig());
			OperationRoundResult operationRoundResult = driveDiscDismantleOperation.GotoSalvage();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal("按钮-拆解", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(300, 930), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DriveDiscDismantleOperation_GotoSalvage_RetriesUnknownScreenWithPythonDelay()
	{
		string text = CreateTempRoot();
		try
		{
			WriteDriveDiscScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new MutableOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "未知") });
			DriveDiscDismantleOperation driveDiscDismantleOperation = new DriveDiscDismantleOperation(zContext, new DriveDiscDismantleConfig());
			OperationRoundResult operationRoundResult = driveDiscDismantleOperation.GotoSalvage();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal("未能识别当前画面", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DriveDiscDismantleOperation_ClickArea_ClicksConfiguredSalvageAreaWithPythonWaits()
	{
		string text = CreateTempRoot();
		try
		{
			WriteDriveDiscScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new MutableOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 10, 10, 120, 20, "驱动盘拆解"),
				new OcrMatchResult(0.99, 20, 20, 80, 20, "快速选择")
			});
			DriveDiscDismantleOperation driveDiscDismantleOperation = new DriveDiscDismantleOperation(zContext, new DriveDiscDismantleConfig());
			OperationRoundResult operationRoundResult = driveDiscDismantleOperation.ClickFilter();
			Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
			Assert.Equal("按钮-快速选择", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(226, 1030), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DriveDiscDismantleOperation_ClickArea_RetriesMissingAreaTextWithPythonDelay()
	{
		string text = CreateTempRoot();
		try
		{
			WriteDriveDiscScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new MutableOcrMatcher(Array.Empty<OcrMatchResult>());
			DriveDiscDismantleOperation driveDiscDismantleOperation = new DriveDiscDismantleOperation(zContext, new DriveDiscDismantleConfig());
			OperationRoundResult operationRoundResult = driveDiscDismantleOperation.ClickFilter();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal("未找到 按钮-快速选择", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
			Assert.Null(testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DriveDiscDismantleOperation_AllClickNodesUsePythonSuccessWait()
	{
		string text = CreateTempRoot();
		try
		{
			WriteDriveDiscScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new MutableOcrMatcher(new OcrMatchResult[6]
			{
				new OcrMatchResult(0.99, 10, 10, 120, 20, "驱动盘拆解"),
				new OcrMatchResult(0.99, 20, 20, 80, 20, "快速选择"),
				new OcrMatchResult(0.99, 20, 20, 80, 20, "S及以下"),
				new OcrMatchResult(0.99, 20, 20, 100, 20, "全选已弃置"),
				new OcrMatchResult(0.99, 20, 20, 60, 20, "确认"),
				new OcrMatchResult(0.99, 20, 20, 80, 20, "拆解")
			});
			DriveDiscDismantleConfig config = new DriveDiscDismantleConfig
			{
				DismantleLevel = "S及以下",
				DismantleAbandon = true
			};
			DriveDiscDismantleOperation driveDiscDismantleOperation = new DriveDiscDismantleOperation(zContext, config);
			AssertClickSuccess(driveDiscDismantleOperation.ClickFilter(), "按钮-快速选择");
			AssertClickSuccess(driveDiscDismantleOperation.ChooseLevel(), "按钮-S及以下");
			AssertClickSuccess(driveDiscDismantleOperation.ChooseAbandon(), "按钮-全选已弃置");
			AssertClickSuccess(driveDiscDismantleOperation.ClickFilterConfirm(), "按钮-快速选择-确认");
			AssertClickSuccess(driveDiscDismantleOperation.ClickSalvage(), "按钮-拆解");
			AssertClickSuccess(driveDiscDismantleOperation.ClickSalvageConfirm(), "按钮-拆解-确认");
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
		static void AssertClickSuccess(OperationRoundResult result, string status)
		{
			Assert.Equal(OperationRoundResultKind.Success, result.Kind);
			Assert.Equal(status, result.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), result.Delay);
		}
	}

	[Fact]
	public void DriveDiscDismantleOperation_AllClickNodesUsePythonRetryWait()
	{
		string text = CreateTempRoot();
		try
		{
			WriteDriveDiscScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new MutableOcrMatcher(Array.Empty<OcrMatchResult>());
			DriveDiscDismantleConfig config = new DriveDiscDismantleConfig
			{
				DismantleLevel = "S及以下",
				DismantleAbandon = true
			};
			DriveDiscDismantleOperation driveDiscDismantleOperation = new DriveDiscDismantleOperation(zContext, config);
			AssertClickRetry(driveDiscDismantleOperation.ClickFilter(), "未找到 按钮-快速选择");
			AssertClickRetry(driveDiscDismantleOperation.ChooseLevel(), "未找到 按钮-S及以下");
			AssertClickRetry(driveDiscDismantleOperation.ChooseAbandon(), "未找到 按钮-全选已弃置");
			AssertClickRetry(driveDiscDismantleOperation.ClickFilterConfirm(), "未找到 按钮-快速选择-确认");
			AssertClickRetry(driveDiscDismantleOperation.ClickSalvage(), "未找到 按钮-拆解");
			AssertClickRetry(driveDiscDismantleOperation.ClickSalvageConfirm(), "未找到 按钮-拆解-确认");
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
		static void AssertClickRetry(OperationRoundResult result, string status)
		{
			Assert.Equal(OperationRoundResultKind.Retry, result.Kind);
			Assert.Equal(status, result.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), result.Delay);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteDriveDiscScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "storage_drive_disc.yml"), "screen_id: storage_drive_disc\nscreen_name: 仓库-驱动仓库\narea_list:\n- area_name: 标题-驱动仓库\n  id_mark: true\n  pc_rect:\n  - 70\n  - 116\n  - 164\n  - 164\n  text: 驱动仓库\n  lcs_percent: 0.5\n- area_name: 按钮-拆解\n  pc_rect:\n  - 250\n  - 900\n  - 438\n  - 960\n  text: 拆解\n  lcs_percent: 0.5\n  goto_list:\n  - 仓库-驱动仓库-驱动盘拆解");
		File.WriteAllText(Path.Combine(text, "drive_disc_dismantle.yml"), "screen_id: drive_disc_dismantle\nscreen_name: 仓库-驱动仓库-驱动盘拆解\narea_list:\n- area_name: 标题-驱动盘拆解\n  id_mark: true\n  pc_rect:\n  - 184\n  - 4\n  - 326\n  - 102\n  text: 驱动盘拆解\n  lcs_percent: 0.5\n- area_name: 按钮-快速选择\n  pc_rect:\n  - 166\n  - 1000\n  - 342\n  - 1054\n  text: 快速选择\n  lcs_percent: 0.5\n- area_name: 按钮-S及以下\n  pc_rect:\n  - 350\n  - 300\n  - 520\n  - 360\n  text: S及以下\n  lcs_percent: 0.5\n- area_name: 按钮-全选已弃置\n  pc_rect:\n  - 350\n  - 380\n  - 560\n  - 440\n  text: 全选已弃置\n  lcs_percent: 0.5\n- area_name: 按钮-快速选择-确认\n  pc_rect:\n  - 980\n  - 690\n  - 1180\n  - 750\n  text: 确认\n  lcs_percent: 0.5\n- area_name: 按钮-拆解\n  pc_rect:\n  - 1626\n  - 876\n  - 1808\n  - 926\n  text: 拆解\n  lcs_percent: 0.5\n- area_name: 按钮-拆解-确认\n  pc_rect:\n  - 1030\n  - 622\n  - 1220\n  - 672\n  text: 确认\n  lcs_percent: 0.5");
	}
}
