using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.EnterGame;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class EnterGameOperationTests : IDisposable
{
	private sealed class RecordingHdrStore : IAutoHdrPreferenceStore
	{
		public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public List<(string GamePath, string Value)> Writes { get; } = new List<(string, string)>();

		public List<string> Deletes { get; } = new List<string>();

		public string? ReadValue(string gamePath)
		{
			return Values.GetValueOrDefault(gamePath);
		}

		public void WriteValue(string gamePath, string value)
		{
			Values[gamePath] = value;
			Writes.Add((gamePath, value));
		}

		public void DeleteValue(string gamePath)
		{
			Values.Remove(gamePath);
			Deletes.Add(gamePath);
		}
	}

	private sealed class StageController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot;

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public StageController()
			: this(new Size(160, 80))
		{
		}

		public StageController(Size screenshotSize)
		{
			_screenshot = new Mat(screenshotSize, MatType.CV_8UC3, Scalar.Black);
		}

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			if (position.HasValue)
			{
				Clicks.Add(position.Value);
			}
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
			ScreenshotStage++;
			return _screenshot.Clone();
		}
	}

	private sealed class RefreshableWindowController : ControllerBase
	{
		private bool _isGameWindowReady;

		public int InitializeCount { get; private set; }

		public override bool IsGameWindowReady => _isGameWindowReady;

		public override bool InitBeforeContextRun()
		{
			InitializeCount++;
			_isGameWindowReady = true;
			return true;
		}

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

	private sealed class StageOcrMatcher(StageController controller, Func<int, IReadOnlyList<string>> stageWords) : IOcrMatcher
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
			return string.Concat(stageWords(controller.ScreenshotStage));
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
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => new OcrMatchResult(0.99, 10 + index * 30, 5, 20, 10, word)).ToArray();
		}
	}

	private sealed class EnterClickStatusMatcher : IOcrMatcher
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
			return string.Empty;
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in Ocr(image, threshold.GetValueOrDefault(), mergeLineDistance))
			{
				MatchResultList matchResultList = new MatchResultList(onlyBest: false);
				matchResultList.Append(item, autoMerge: false);
				dictionary[item.Text] = matchResultList;
			}
			return dictionary;
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			IReadOnlyList<OcrMatchResult> result;
			if (image.Width < 1900 || image.Height < 1000)
			{
				IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 100, 50, 130, 32, "点击进入游戏") };
				result = readOnlyList;
			}
			else
			{
				IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 900, 880, 130, 32, "点击进入游戏") };
				result = readOnlyList;
			}
			return result;
		}
	}

	private readonly string _rootDirectory;

	public EnterGameOperationTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-enter-game-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_rootDirectory);
	}

	[Fact]
	public void OpenGameCommandBuilder_BuildsPythonCompatibleCommandWithLaunchArguments()
	{
		string actual = OpenGameCommandBuilder.Build("D:\\Games\\Zenless Zone Zero\\ZenlessZoneZero.exe", launchArgument: true, "2560x1440", "1", popupWindow: true, "2", "-force-d3d11");
		Assert.Equal("cmd /c \"start \"\" /d \"D:\\Games\\Zenless Zone Zero\" \"ZenlessZoneZero.exe\" -force-d3d11 -screen-width 2560 -screen-height 1440 -screen-fullscreen 1 -popupwindow -monitor 2 & exit\"", actual);
	}

	[Fact]
	public void OpenGameProcessLauncher_DoesNotUseBreakawayCreationFlag()
	{
		// CREATE_BREAKAWAY_FROM_JOB (0x1000000) 在宿主进程位于未启用
		// JOB_OBJECT_LIMIT_BREAKAWAY_OK 的 Job 中时会使 CreateProcessW 返回 ERROR_ACCESS_DENIED，
		// 且本进程不创建任何 Job，因此启动游戏不得携带该标志。
		Assert.Equal(0u, OpenGameProcessLauncher.CreationFlags);
	}

	[Fact]
	public async Task OpenGame_ExecuteAsync_StartsBuiltCommand()
	{
		WriteInstanceConfig("game_path: 'D:\\Games\\Zenless Zone Zero\\ZenlessZoneZero.exe'", "launch_argument: false");
		using ZContext context = CreateContext();
		List<string> commands = new List<string>();
		OpenGame operation = new OpenGame(context, delegate(string command)
		{
			commands.Add(command);
			return true;
		}, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("打开游戏", result.Status);
		Assert.Single(commands);
		Assert.Equal("cmd /c \"start \"\" /d \"D:\\Games\\Zenless Zone Zero\" \"ZenlessZoneZero.exe\" & exit\"", commands[0]);
	}

	[Fact]
	public async Task OpenGame_ExecuteAsync_FailsWhenGamePathMissing()
	{
		WriteInstanceConfig("game_path: ''", "");
		using ZContext context = CreateContext();
		OpenGame operation = new OpenGame(context, delegate
		{
			throw new InvalidOperationException("should not start");
		}, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("未配置游戏路径，请前往 [ 账户管理 ] -> [ 游戏路径 ] 手动设置", result.Status);
	}

	[Fact]
	public void AutoHdrManager_DisablesAutoHdrAndReturnsOriginalValue()
	{
		RecordingHdrStore recordingHdrStore = new RecordingHdrStore();
		recordingHdrStore.Values["D:\\Games\\ZenlessZoneZero.exe"] = "AutoHDREnable=2097;";
		AutoHdrChangeResult autoHdrChangeResult = AutoHdrManager.Disable("D:\\Games\\ZenlessZoneZero.exe", recordingHdrStore);
		Assert.True(autoHdrChangeResult.IsSuccess);
		Assert.Equal("已禁用HDR", autoHdrChangeResult.Status);
		Assert.Equal("AutoHDREnable=2097;", autoHdrChangeResult.OriginalValue);
		Assert.Equal("AutoHDREnable=2096;", recordingHdrStore.Values["D:\\Games\\ZenlessZoneZero.exe"]);
	}

	[Fact]
	public void AutoHdrManager_RestoresOriginalValueOrDeletesSavedValue()
	{
		RecordingHdrStore recordingHdrStore = new RecordingHdrStore();
		AutoHdrChangeResult autoHdrChangeResult = AutoHdrManager.Enable("D:\\Games\\ZenlessZoneZero.exe", "AutoHDREnable=2097;", recordingHdrStore);
		AutoHdrChangeResult autoHdrChangeResult2 = AutoHdrManager.Enable("D:\\Games\\ZenlessZoneZero.exe", null, recordingHdrStore);
		Assert.True(autoHdrChangeResult.IsSuccess);
		Assert.Equal("AutoHDREnable=2097;", recordingHdrStore.Writes[0].Value);
		Assert.True(autoHdrChangeResult2.IsSuccess);
		Assert.Contains("D:\\Games\\ZenlessZoneZero.exe", (IEnumerable<string>)recordingHdrStore.Deletes);
	}

	[Fact]
	public void EnterGame_MatchesEnterClickStatusByPythonPriority()
	{
		Assert.Equal("加载配置数据中", EnterGame.MatchEnterClickStatusText(new string[2] { "登录游戏服务器中", "加载配置数据中" }));
		Assert.Equal("资源下载中", EnterGame.MatchEnterClickStatusText(new string[] { "资源下载中" }));
		Assert.Null(EnterGame.MatchEnterClickStatusText(new string[] { "点击进入游戏" }));
		Assert.Equal("点击进入游戏", EnterGame.MatchEnterClickStatusText(new string[] { "点击进入游戏" }, includeEnterClick: true));
	}

	[Fact]
	public void EnterGame_WaitResourceDownloadFailsAfterTimeout()
	{
		using ZContext context = CreateContext();
		DateTimeOffset now = new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero);
		EnterGame enterGame = new EnterGame(context, switchAccount: false, () => now, TimeSpan.FromSeconds(10L));
		OperationRoundResult operationRoundResult = enterGame.WaitResourceDownload();
		now = now.AddSeconds(11.0);
		OperationRoundResult operationRoundResult2 = enterGame.WaitResourceDownload();
		Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
		Assert.Equal("资源下载中", operationRoundResult.Status);
		Assert.Equal(OperationRoundResultKind.Fail, operationRoundResult2.Kind);
		Assert.Equal("资源下载超时", operationRoundResult2.Status);
	}

	[Fact]
	public void EnterGame_IsGrayLoadingScreenDetectsLowSaturationFrames()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using Mat screen = new Mat(new Size(30, 30), MatType.CV_8UC3, new Scalar(128.0, 128.0, 128.0));
		using Mat screen2 = new Mat(new Size(30, 30), MatType.CV_8UC3, new Scalar(255.0, 0.0, 0.0));
		Assert.True(EnterGame.IsGrayLoadingScreen(screen));
		Assert.False(EnterGame.IsGrayLoadingScreen(screen2));
	}

	[Fact]
	public void EnterGame_CheckEnterClickStatusClicksConfiguredAreaCenter()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteEnterClickScreenYaml();
		using StageController stageController = new StageController(new Size(1920, 1080));
		using ZContext zContext = CreateContext();
		zContext.AttachController(stageController);
		zContext.OcrService.Matcher = new EnterClickStatusMatcher();
		zContext.ScreenContext.Reload();
		TimeSpan? retryDelay = TimeSpan.Zero;
		TimeSpan? waitDelay = TimeSpan.Zero;
		EnterGame obj = new EnterGame(zContext, switchAccount: false, null, null, retryDelay, waitDelay);
		typeof(ZOperation).GetMethod("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, new object[1] { false });
		MethodInfo method = typeof(EnterGame).GetMethod("CheckEnterClickStatus", BindingFlags.Instance | BindingFlags.NonPublic);
		OperationRoundResult operationRoundResult = (OperationRoundResult)method.Invoke(obj, Array.Empty<object>());
		Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
		Assert.Equal("点击进入游戏", operationRoundResult.Status);
		// 还原为纯区域点击后，点击落在配置区域中心，不再是 OCR 命中中心。
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(959, 939), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)stageController.Clicks);
		Assert.DoesNotContain(new OneDragon.Core.Abstractions.Geometry.Point(965, 896), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)stageController.Clicks);
	}

	[Fact]
	public async Task OpenAndEnterGame_ExecuteAsync_DisablesHdrOpensWindowEnablesHdrThenEntersGame()
	{
		List<string> events;
		using (ZContext context = CreateContext())
		{
			events = new List<string>();
			int checks = 0;
			OpenAndEnterGame operation = new OpenAndEnterGame(context, () => Record("disable_hdr", success: true), () => Record("open_game", success: true), delegate
			{
				events.Add("check_window");
				checks++;
				return checks >= 2;
			}, delegate
			{
				events.Add("activate_window");
			}, () => Record("enable_hdr", success: true), () => Record("enter_game", success: true, "大世界"), TimeSpan.Zero);
			OperationResult result = await operation.ExecuteAsync();
			Assert.True(result.IsSuccess);
			Assert.Equal("大世界", result.Status);
			Assert.Equal<List<string>>(new List<string>(7) { "disable_hdr", "open_game", "check_window", "check_window", "activate_window", "enable_hdr", "enter_game" }, events);
		}
		Task<OperationResult> Record(string name, bool success, string? status = null)
		{
			events.Add(name);
			return Task.FromResult(new OperationResult(success, status ?? name));
		}
	}

	[Fact]
	public async Task OpenAndEnterGame_DefaultWindowCheckRefreshesBeforeReadingReadiness()
	{
		using ZContext context = CreateContext();
		RefreshableWindowController controller = new RefreshableWindowController();
		context.AttachController(controller);
		OpenAndEnterGame operation = new OpenAndEnterGame(context, () => Task.FromResult(new OperationResult(IsSuccess: true, "已禁用HDR")), () => Task.FromResult(new OperationResult(IsSuccess: true, "打开游戏")), null, delegate
		{
		}, () => Task.FromResult(new OperationResult(IsSuccess: true, "已恢复HDR")), () => Task.FromResult(new OperationResult(IsSuccess: true, "大世界")), TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界", result.Status);
		Assert.Equal(1, controller.InitializeCount);
		Assert.True(controller.IsGameWindowReady);
	}

	[Fact(Skip = "Requires a configured game path, live ZZZ window, real input device and account state.")]
	[Trait("Category", "E2E")]
	public async Task OpenAndEnterGame_E2E_UsesRealWindowAndRegistryPath()
	{
		using ZContext context = CreateContext();
		context.InitController();
		OpenAndEnterGame operation = new OpenAndEnterGame(context);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
	}

	[Fact]
	public async Task SwitchAccount_ExecuteAsync_ClicksLogoutAndRunsEnterGameWithSwitchFlag()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext();
		context.AttachController(controller);
		context.OcrService.Matcher = new StageOcrMatcher(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[2] { "信息", "菜单" }, 
				2 => new string[] { "返回" }, 
				3 => new string[] { "更多" }, 
				4 => new string[] { "登出" }, 
				5 => new string[] { "确认" }, 
				6 => new string[] { "点击进入游戏" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		context.ScreenContext.Reload();
		bool? switchFlag = null;
		SwitchAccount operation = new SwitchAccount(context, delegate(ZContext _, bool switchAccount)
		{
			switchFlag = switchAccount;
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
		}, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界", result.Status);
		Assert.True(switchFlag);
		Assert.Equal(3, controller.Clicks.Count((OneDragon.Core.Abstractions.Geometry.Point point) => point == new OneDragon.Core.Abstractions.Geometry.Point(20, 10)));
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	private ZContext CreateContext()
	{
		return new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
	}

	private void WriteInstanceConfig(string gameAccountYaml, string gameYaml)
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "game_account.yml"), gameAccountYaml);
		File.WriteAllText(Path.Combine(text, "game.yml"), gameYaml);
	}

	private void WriteScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: normal_world\n  screen_name: 大世界-普通\n  area_list:\n    - area_name: 信息\n      id_mark: true\n      pc_rect: [0, 0, 30, 20]\n      text: 信息\n      lcs_percent: 0.8\n    - area_name: 打开菜单\n      pc_rect: [40, 0, 80, 20]\n      text: 菜单\n      lcs_percent: 0.8\n      goto_list: [菜单]\n- screen_id: menu\n  screen_name: 菜单\n  area_list:\n    - area_name: 返回\n      id_mark: true\n      pc_rect: [0, 20, 30, 40]\n      text: 返回\n      lcs_percent: 0.8\n    - area_name: 底部列表\n      pc_rect: [0, 0, 120, 40]\n    - area_name: 更多功能\n      pc_rect: [0, 0, 120, 40]\n    - area_name: 更多登出确认\n      pc_rect: [0, 0, 120, 40]\n      text: 确认\n      lcs_percent: 0.8\n- screen_id: open_game\n  screen_name: 打开游戏\n  area_list:\n    - area_name: 点击进入游戏\n      pc_rect: [0, 0, 140, 40]\n      text: 点击进入游戏\n      lcs_percent: 0.8\n    - area_name: B服新-登录记录\n      pc_rect: [0, 0, 140, 40]\n      text: 登录记录\n      lcs_percent: 0.8");
	}

	private void WriteEnterClickScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: enter_game\n  screen_name: 打开游戏\n  area_list:\n    - area_name: 点击进入游戏\n      pc_rect: [782, 846, 1136, 1032]\n      text: 点击进入游戏\n      lcs_percent: 0.6\n    - area_name: 进入游戏点击后状态\n      pc_rect: [700, 820, 1220, 1032]");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
