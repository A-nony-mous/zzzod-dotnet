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
using OneDragon.Core.Configuration;
using OneDragon.Core.Controller;
using OneDragon.Core.Input;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OneDragon.Core.Windows.Controller;
using OneDragon.Core.Windows.Screening;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.CommissionAssistant;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.HollowMap;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

[Collection("Screenshot helper global input source")]
public sealed class CommissionAssistantAppTests
{
	private sealed class RecordingCommissionAssistantFlow : ICommissionAssistantAppFlow
	{
		public int RunCount { get; private set; }

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "委托助手已启动"));
		}

		public void Pause(ZContext context, CommissionAssistantRuntimeState state)
		{
			PauseCount++;
		}

		public void Resume(ZContext context, CommissionAssistantRuntimeState state)
		{
			ResumeCount++;
		}

		public void Stop(ZContext context, CommissionAssistantRuntimeState state)
		{
			StopCount++;
		}
	}

	private sealed class BlockingCommissionAssistantFlow : ICommissionAssistantAppFlow
	{
		private readonly TaskCompletionSource<OperationResult> _completion = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

		public TaskCompletionSource Started { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public Task<OperationResult> RunAsync(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, CancellationToken cancellationToken)
		{
			Started.TrySetResult();
			return _completion.Task;
		}

		public void Pause(ZContext context, CommissionAssistantRuntimeState state)
		{
		}

		public void Resume(ZContext context, CommissionAssistantRuntimeState state)
		{
		}

		public void Stop(ZContext context, CommissionAssistantRuntimeState state)
		{
		}

		public void Complete()
		{
			_completion.TrySetResult(new OperationResult(IsSuccess: true, "委托助手已停止"));
		}
	}

	private sealed class TestScreenshotController : ControllerBase, IDisposable
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

		public void Dispose()
		{
			CleanupAfterAppShutdown();
		}
	}

	private sealed class FreshFrameController : ControllerBase, IDisposable
	{
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
			return new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
		}

		public void Dispose()
		{
			CleanupAfterAppShutdown();
		}
	}

	private sealed class TestZzzController : ControllerBase, IZzzControllerActions, IDisposable
	{
		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPoint { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastMouseMovePoint { get; private set; }

		public int InteractCount { get; private set; }

		public TimeSpan? LastInteractPressTime { get; private set; }

		public int MoveACount { get; private set; }

		public int MoveDCount { get; private set; }

		public string? LastInputText { get; private set; }

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
			LastInputText = text;
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
			LastMouseMovePoint = position;
		}

		public void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			MoveACount++;
		}

		public void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			MoveDCount++;
		}

		public void Interact(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			InteractCount++;
			LastInteractPressTime = pressTime;
		}

		public void TurnByDistance(float distance)
		{
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return null;
		}

		public void Dispose()
		{
			CleanupAfterAppShutdown();
		}
	}

	private sealed class FishingWindowsController : WindowsGameController, IZzzControllerActions, IDisposable
	{
		private readonly FishingButtonController _buttons;

		public int MoveACount { get; private set; }

		public int MoveDCount { get; private set; }

		public IReadOnlyList<(string Key, TimeSpan? PressTime)> ButtonPresses => _buttons.Presses;

		public FishingWindowsController()
			: this(new FishingButtonController())
		{
		}

		private FishingWindowsController(FishingButtonController buttons)
			: base(null, null, 1920, 1080, new WindowsGameWindow(), null, new FishingInputController(buttons), null, buttons, null, null, null, null, skipForegroundActivation: true)
		{
			_buttons = buttons;
		}

		public void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			MoveACount++;
		}

		public void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			MoveDCount++;
		}

		public void Interact(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void TurnByDistance(float distance)
		{
		}

		public void Dispose()
		{
			CleanupAfterAppShutdown();
		}
	}

	private sealed class FishingButtonController : IButtonController
	{
		public List<(string Key, TimeSpan? PressTime)> Presses { get; } = new List<(string, TimeSpan?)>();

		public void Tap(string key)
		{
		}

		public void TapCombo(IReadOnlyList<string> keys)
		{
		}

		public void Press(string key, TimeSpan? pressTime = null)
		{
			Presses.Add((key, pressTime));
		}

		public void Release(string key)
		{
		}

		public void Reset()
		{
		}
	}

	private sealed class FishingInputController(IButtonController buttonController) : IInputController
	{
		public IButtonController ButtonController { get; } = buttonController;

		public bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool primary = true)
		{
			return true;
		}

		public void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
		}

		public void Scroll(int clicks, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public void InputText(string text)
		{
		}

		public void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
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

	private sealed class AreaAwareFakeOcrMatcher(Func<Mat, IReadOnlyList<OcrMatchResult>> resolveResults) : IOcrMatcher
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
			return string.Concat(from result in resolveResults(image)
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
			return (from result in resolveResults(image)
				select new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	private sealed class RecordingCommissionAssistantServices : ICommissionAssistantOperationServices
	{
		public List<string> LoadedSubDirs { get; } = new List<string>();

		public List<string> LoadedOpNames { get; } = new List<string>();

		public int StartAutoBattleCount { get; private set; }

		public int StopAutoBattleCount { get; private set; }

		public int ResumeAutoBattleCount { get; private set; }

		public int DispatchCount { get; private set; }

		public int CheckBattleStateCount { get; private set; }

		public bool FishingDetected { get; set; }

		public OperationResult HollowResult { get; set; } = new OperationResult(IsSuccess: false, "未在空洞中");

		public Mat? LastDialogConfirmScreen { get; private set; }

		public OperationResult FishingResult { get; set; } = new OperationResult(IsSuccess: false, "未识别到指令");

		public bool NeedPauseInBackground(ZContext context, CommissionAssistantConfig config)
		{
			return false;
		}

		public OperationResult ClickDialogConfirm(ZContext context, Mat? screen)
		{
			LastDialogConfirmScreen = screen;
			return new OperationResult(IsSuccess: false, "未找到 对话框确认");
		}

		public bool IsInteractVisible(ZContext context, Mat? screen)
		{
			return false;
		}

		public string? CheckCurrentWorldScreen(ZContext context, Mat? screen)
		{
			return null;
		}

		public bool IsSecondaryMenuVisible(ZContext context, Mat? screen)
		{
			return false;
		}

		public OperationResult HandleHollow(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			return HollowResult;
		}

		public OperationResult ClickHollowFinished(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: false, "未找到 通关-完成");
		}

		public AutoBattleOperator LoadAutoOp(ZContext context, string subDir, string opName)
		{
			LoadedSubDirs.Add(subDir);
			LoadedOpNames.Add(opName);
			return new AutoBattleOperator(context.AutoBattleContext, subDir, opName);
		}

		public void DispatchOpLoaded(ZContext context, AutoBattleOperator autoOp)
		{
			DispatchCount++;
		}

		public void StartAutoBattle(ZContext context)
		{
			StartAutoBattleCount++;
		}

		public void ResumeAutoBattle(ZContext context)
		{
			ResumeAutoBattleCount++;
		}

		public void StopAutoBattle(ZContext context)
		{
			StopAutoBattleCount++;
		}

		public void CheckBattleState(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			CheckBattleStateCount++;
		}

		public OperationResult HandleStoryMode(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, Mat? screen)
		{
			return new OperationResult(IsSuccess: false, "未匹配剧情按钮");
		}

		public OperationResult HandleSkipStoryConfirm(ZContext context, CommissionAssistantRuntimeState state, Mat? screen)
		{
			return new OperationResult(IsSuccess: false, "未匹配剧情按钮");
		}

		public OperationResult WaitSecondaryMenu(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: false, "未处于二级界面");
		}

		public OperationResult CheckGameTutorial(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: false, "未处于玩法引导");
		}

		public OperationResult HandleKnockKnock(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: false, "未处于短信");
		}

		public OperationResult CheckFishing(ZContext context, Mat? screen, CommissionAssistantRuntimeState state)
		{
			return FishingDetected ? new OperationResult(IsSuccess: true, "钓鱼") : new OperationResult(IsSuccess: false, "未处于钓鱼");
		}

		public OperationResult DoDialogClick(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, Mat? screen, bool checkCenterWords)
		{
			return new OperationResult(IsSuccess: false, "未知画面");
		}

		public OperationResult HandleFishing(ZContext context, Mat? screen, CommissionAssistantRuntimeState state)
		{
			return FishingResult;
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesCommissionAssistantApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			CommissionAssistantFactory commissionAssistantFactory = zContext.ApplicationFactoryRegistry.CreateCommissionAssistantFactory();
			IApplication application = commissionAssistantFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = commissionAssistantFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = commissionAssistantFactory.GetRunRecord(0);
			Assert.Equal("commission_assistant", commissionAssistantFactory.AppId);
			Assert.Equal("委托助手", commissionAssistantFactory.AppName);
			Assert.Equal("one_dragon", commissionAssistantFactory.GroupId);
			Assert.False(commissionAssistantFactory.NeedNotify);
			Assert.False(condition: false);
			Assert.IsType<CommissionAssistantApp>(application);
			CommissionAssistantConfig commissionAssistantConfig = Assert.IsType<CommissionAssistantConfig>(config);
			Assert.Equal("闪避", commissionAssistantConfig.DodgeConfig);
			Assert.Equal("全配队通用", commissionAssistantConfig.AutoBattle);
			ZApplicationRunRecord zApplicationRunRecord = Assert.IsType<ZApplicationRunRecord>(runRecord);
			Assert.Equal("commission_assistant", zApplicationRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersCommissionAssistantWithoutDefaultGroupOrNotify()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterCommissionAssistantApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("commission_assistant"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("commission_assistant"));
			Assert.DoesNotContain("commission_assistant", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
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
			File.WriteAllText(Path.Combine(text2, "screenshot_helper.yml"), "pause_in_background: false\ndialog_click_interval: 0.25\nstory_mode: 跳过剧情\ndialog_option: 第一个\ndodge_config: 闪避-自定义\ndodge_switch: F5\nauto_battle: 自动战斗-自定义\nauto_battle_switch: F6\nsleep_after_empty_screen: 0.75");
			CommissionAssistantConfig commissionAssistantConfig = CommissionAssistantConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("commission_assistant", commissionAssistantConfig.AppId);
			Assert.False(commissionAssistantConfig.PauseInBackground);
			Assert.Equal(0.25, commissionAssistantConfig.DialogClickInterval);
			Assert.Equal(CommissionAssistantStoryMode.Skip.Value, commissionAssistantConfig.StoryMode);
			Assert.Equal(CommissionAssistantDialogOption.First.Value, commissionAssistantConfig.DialogOption);
			Assert.Equal("闪避-自定义", commissionAssistantConfig.DodgeConfig);
			Assert.Equal("F5", commissionAssistantConfig.DodgeSwitch);
			Assert.Equal("自动战斗-自定义", commissionAssistantConfig.AutoBattle);
			Assert.Equal("F6", commissionAssistantConfig.AutoBattleSwitch);
			Assert.Equal(0.75, commissionAssistantConfig.SleepAfterEmptyScreen);
			Assert.Equal("FLYOUT", "FLYOUT");
			Assert.Contains((IEnumerable<CommissionAssistantSettingField>)CommissionAssistantSettings.Fields, (Predicate<CommissionAssistantSettingField>)((CommissionAssistantSettingField field) => field.Key == "story_mode" && field.DefaultValue.Equals(CommissionAssistantStoryMode.Click.Value)));
			Assert.Contains((IEnumerable<ConfigItem>)CommissionAssistantStoryMode.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, CommissionAssistantStoryMode.Skip.Value)));
			Assert.Contains((IEnumerable<ConfigItem>)CommissionAssistantDialogOption.Options, (Predicate<ConfigItem>)((ConfigItem option) => object.Equals(option.Value, CommissionAssistantDialogOption.Last.Value)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Config_CopiesLegacyScreenshotHelperFileIntoOneDragonGroup()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00");
			Directory.CreateDirectory(text2);
			string path = Path.Combine(text2, "screenshot_helper.yml");
			File.WriteAllText(path, "dialog_click_interval: 0.35\ndodge_switch: f5\n");
			CommissionAssistantConfig commissionAssistantConfig = CommissionAssistantConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			string[] buffer = new string[5];
			buffer[0] = text;
			buffer[1] = "config";
			buffer[2] = "00";
			buffer[3] = "one_dragon";
			buffer[4] = "screenshot_helper.yml";
			string path2 = Path.Combine(buffer);
			Assert.True(File.Exists(path2));
			Assert.Equal(0.35, commissionAssistantConfig.DialogClickInterval);
			Assert.Equal("f5", commissionAssistantConfig.DodgeSwitch);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RuntimeState_TogglesDodgeAndAutoBattleModesByHotkey()
	{
		CommissionAssistantConfig config = new CommissionAssistantConfig
		{
			DodgeSwitch = "5",
			AutoBattleSwitch = "6"
		};
		CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState();
		Assert.Equal(1, commissionAssistantRuntimeState.HandleKeyPress("5", config));
		Assert.Equal(0, commissionAssistantRuntimeState.HandleKeyPress("5", config));
		Assert.Equal(2, commissionAssistantRuntimeState.HandleKeyPress("6", config));
		Assert.Equal(0, commissionAssistantRuntimeState.HandleKeyPress("6", config));
		Assert.Equal(0, commissionAssistantRuntimeState.HandleKeyPress("unknown", config));
	}

	[Fact]
	public async Task CommissionAssistantApp_RunsInjectedFlowAndDelegatesLifecycle()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			CommissionAssistantConfig config = new CommissionAssistantConfig();
			CommissionAssistantRuntimeState state = new CommissionAssistantRuntimeState();
			RecordingCommissionAssistantFlow flow = new RecordingCommissionAssistantFlow();
			CommissionAssistantApp app = new CommissionAssistantApp(context, config, state, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			app.HandleKeyPress(config.DodgeSwitch);
			await app.OnPauseAsync(CancellationToken.None);
			await app.OnResumeAsync(CancellationToken.None);
			await app.OnStopAsync(CancellationToken.None);
			Assert.True(result.IsSuccess);
			Assert.Equal("委托助手已启动", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, flow.PauseCount);
			Assert.Equal(1, flow.ResumeCount);
			Assert.Equal(1, flow.StopCount);
			Assert.Equal(1, state.RunMode);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task CommissionAssistantApp_ConsumesGlobalHotkeysOnlyWhileRunningAndSubscribed()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			Assert.True(context.RunContext.StartRunning());
			CommissionAssistantConfig config = new CommissionAssistantConfig
			{
				DodgeSwitch = "F5",
				AutoBattleSwitch = "F6"
			};
			CommissionAssistantRuntimeState state = new CommissionAssistantRuntimeState();
			BlockingCommissionAssistantFlow flow = new BlockingCommissionAssistantFlow();
			CommissionAssistantApp app = new CommissionAssistantApp(context, config, state, flow);
			Task<OperationResult> runTask = app.ExecuteAsync(CancellationToken.None);
			await flow.Started.Task.WaitAsync(TimeSpan.FromSeconds(2L));
			ScreenshotHelperGlobalInputSource.Publish(config.DodgeSwitch);
			Assert.Equal(1, state.RunMode);
			await app.OnPauseAsync(CancellationToken.None);
			ScreenshotHelperGlobalInputSource.Publish(config.AutoBattleSwitch);
			Assert.Equal(1, state.RunMode);
			await app.OnResumeAsync(CancellationToken.None);
			ScreenshotHelperGlobalInputSource.Publish(config.AutoBattleSwitch);
			Assert.Equal(0, state.RunMode);
			await context.RunContext.StopRunningAsync();
			ScreenshotHelperGlobalInputSource.Publish(config.DodgeSwitch);
			Assert.Equal(0, state.RunMode);
			Assert.True(context.RunContext.StartRunning());
			await app.OnStopAsync(CancellationToken.None);
			ScreenshotHelperGlobalInputSource.Publish(config.DodgeSwitch);
			Assert.Equal(0, state.RunMode);
			flow.Complete();
			Assert.True((await runTask.WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			await context.RunContext.StopRunningAsync();
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void OperationServices_PauseWhenConfiguredGameWindowIsNotForeground()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new WindowsGameController($"CommissionAssistantTests-{Guid.NewGuid():N}"));
			CommissionAssistantConfig config = new CommissionAssistantConfig
			{
				PauseInBackground = true
			};
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			Assert.True(defaultCommissionAssistantOperationServices.NeedPauseInBackground(zContext, config));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_LoadsDodgeAndAutoBattleModes()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			CommissionAssistantConfig commissionAssistantConfig = new CommissionAssistantConfig
			{
				DodgeConfig = "闪避-自定义",
				AutoBattle = "自动战斗-自定义"
			};
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState();
			RecordingCommissionAssistantServices recordingCommissionAssistantServices = new RecordingCommissionAssistantServices();
			CommissionAssistantOperation commissionAssistantOperation = new CommissionAssistantOperation(zContext, commissionAssistantConfig, commissionAssistantRuntimeState, recordingCommissionAssistantServices);
			commissionAssistantRuntimeState.HandleKeyPress(commissionAssistantConfig.DodgeSwitch, commissionAssistantConfig);
			OperationRoundResult operationRoundResult = commissionAssistantOperation.DialogMode();
			OperationRoundResult operationRoundResult2 = commissionAssistantOperation.AutoMode();
			commissionAssistantRuntimeState.HandleKeyPress(commissionAssistantConfig.DodgeSwitch, commissionAssistantConfig);
			OperationRoundResult operationRoundResult3 = commissionAssistantOperation.AutoMode();
			commissionAssistantRuntimeState.HandleKeyPress(commissionAssistantConfig.AutoBattleSwitch, commissionAssistantConfig);
			OperationRoundResult operationRoundResult4 = commissionAssistantOperation.DialogMode();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("自动战斗模式", operationRoundResult.Status);
			Assert.Equal("dodge", recordingCommissionAssistantServices.LoadedSubDirs[0]);
			Assert.Equal("闪避-自定义", recordingCommissionAssistantServices.LoadedOpNames[0]);
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult2.Kind);
			Assert.Equal(0, recordingCommissionAssistantServices.CheckBattleStateCount);
			Assert.True(operationRoundResult3.IsSuccess);
			Assert.Equal(1, recordingCommissionAssistantServices.StopAutoBattleCount);
			Assert.True(operationRoundResult4.IsSuccess);
			Assert.Equal("auto_battle", recordingCommissionAssistantServices.LoadedSubDirs[1]);
			Assert.Equal("自动战斗-自定义", recordingCommissionAssistantServices.LoadedOpNames[1]);
			Assert.Equal(2, recordingCommissionAssistantServices.StartAutoBattleCount);
			Assert.Equal(2, recordingCommissionAssistantServices.DispatchCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_HandlesStoryFishingAndUnknownScreenBranches()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			CommissionAssistantConfig config = new CommissionAssistantConfig
			{
				SleepAfterEmptyScreen = 0.75
			};
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState
			{
				DialogClicked = true
			};
			RecordingCommissionAssistantServices services = new RecordingCommissionAssistantServices
			{
				FishingDetected = true,
				FishingResult = new OperationResult(IsSuccess: true, "钓鱼结束")
			};
			CommissionAssistantOperation commissionAssistantOperation = new CommissionAssistantOperation(zContext, config, commissionAssistantRuntimeState, services);
			OperationRoundResult operationRoundResult = commissionAssistantOperation.DialogMode();
			OperationRoundResult operationRoundResult2 = commissionAssistantOperation.StoryMode();
			OperationRoundResult operationRoundResult3 = commissionAssistantOperation.OnFishing();
			OperationRoundResult operationRoundResult4 = commissionAssistantOperation.SleepAfterEmptyScreen();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("检测剧情模式", operationRoundResult.Status);
			Assert.True(operationRoundResult2.IsSuccess);
			Assert.Equal("钓鱼", operationRoundResult2.Status);
			Assert.True(operationRoundResult3.IsSuccess);
			Assert.Equal("钓鱼结束", operationRoundResult3.Status);
			Assert.True(operationRoundResult4.IsSuccess);
			Assert.Equal("等待重新检测", operationRoundResult4.Status);
			Assert.Equal(TimeSpan.FromSeconds(0.75), operationRoundResult4.Delay);
			Assert.False(commissionAssistantRuntimeState.DialogClicked);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_TreatsHollowProcessingResultAsPythonWait()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RecordingCommissionAssistantServices services = new RecordingCommissionAssistantServices
			{
				HollowResult = new OperationResult(IsSuccess: false, "空洞地图识别失败 模型不可用")
			};
			CommissionAssistantOperation commissionAssistantOperation = new CommissionAssistantOperation(zContext, new CommissionAssistantConfig(), new CommissionAssistantRuntimeState(), services);
			OperationRoundResult operationRoundResult = commissionAssistantOperation.DialogMode();
			Assert.False(operationRoundResult.IsSuccess);
			Assert.Equal("空洞地图识别失败 模型不可用", operationRoundResult.Status);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal(TimeSpan.FromMilliseconds(500L), operationRoundResult.Delay);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_RefreshesScreenshotBeforeDialogConfirm()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			using FreshFrameController controller = new FreshFrameController();
			zContext.AttachController(controller);
			RecordingCommissionAssistantServices recordingCommissionAssistantServices = new RecordingCommissionAssistantServices();
			CommissionAssistantOperation commissionAssistantOperation = new CommissionAssistantOperation(zContext, new CommissionAssistantConfig(), new CommissionAssistantRuntimeState(), recordingCommissionAssistantServices);
			OperationRoundResult operationRoundResult = commissionAssistantOperation.DialogMode();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.NotNull(recordingCommissionAssistantServices.LastDialogConfirmScreen);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_KeepsFishingNodeWaitingUntilFishingDone()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			CommissionAssistantOperation commissionAssistantOperation = new CommissionAssistantOperation(zContext, new CommissionAssistantConfig(), new CommissionAssistantRuntimeState(), new RecordingCommissionAssistantServices
			{
				FishingResult = new OperationResult(IsSuccess: true, "连点")
			});
			OperationRoundResult operationRoundResult = commissionAssistantOperation.OnFishing();
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal("连点", operationRoundResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_ClickDialogConfirm_ClicksMatchedConfirmText()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 30, 40, 20, "确认") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.ClickDialogConfirm(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("对话框确认", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1062, 584), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_ClickDialogConfirm_DoesNotClickWhenConfirmTextMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 30, 40, 20, "取消") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.ClickDialogConfirm(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("未找到 对话框确认", operationResult.Status);
			Assert.Null(testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_ClickDialogConfirm_ReturnsScreenshotFailureWithoutScreen()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment("test_project", "test_user_id"));
		zContext.AttachController(new ReadyController());
		DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
		OperationResult operationResult = defaultCommissionAssistantOperationServices.ClickDialogConfirm(zContext, null);
		Assert.False(operationResult.IsSuccess);
		Assert.Equal("未获取截图", operationResult.Status);
	}

	[Fact]
	public void DefaultServices_IsInteractVisible_UsesBattleInteractArea()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantDetectionScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 20, 10, "交互") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			Assert.True(defaultCommissionAssistantOperationServices.IsInteractVisible(zContext, screen));
			Assert.False(defaultCommissionAssistantOperationServices.IsInteractVisible(zContext, null));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_CheckCurrentWorldScreen_UsesNormalWorldScreenRecognition()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantDetectionScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 40, 10, "街区") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			Assert.Equal("大世界-普通", defaultCommissionAssistantOperationServices.CheckCurrentWorldScreen(zContext, screen));
			Assert.Null(defaultCommissionAssistantOperationServices.CheckCurrentWorldScreen(zContext, null));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_IsSecondaryMenuVisible_UsesCommissionAssistantBackArea()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantDetectionScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 20, 10, "返回") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			Assert.True(defaultCommissionAssistantOperationServices.IsSecondaryMenuVisible(zContext, screen));
			Assert.False(defaultCommissionAssistantOperationServices.IsSecondaryMenuVisible(zContext, null));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleHollow_DoesNotTreatBackpackAsHollowCompletion()
	{
		string text = CreateTempRoot();
		try
		{
			WriteHollowZeroEventScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 8, 5, 40, 20, "背包") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleHollow(zContext, screen, DateTimeOffset.UtcNow);
			Assert.False(operationResult.IsSuccess);
			Assert.False(defaultCommissionAssistantOperationServices.HandleHollow(zContext, null, DateTimeOffset.UtcNow).IsSuccess);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void HollowYoloMapService_ConstructsCurrentMapWithFrameCaptureTime()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "assets", "game_data", "hollow_zero");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "entry_list.yml"), "- entry_name: \"9000-当前\"\n  need_step: 0\n- entry_name: \"9999-未知\"\n  is_benefit: false");
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			YoloDetectFrameResult frame = new YoloDetectFrameResult(new YoloDetectObjectResult[] { new YoloDetectObjectResult(new OpenCvSharp.Rect(100, 200, 40, 40), 0.99, new YoloDetectClass(78, "9000-当前")) }, 1234.5);
			HollowZeroMap hollowZeroMap = HollowYoloMapService.ConstructMap(context, frame);
			Assert.NotNull(hollowZeroMap);
			Assert.Equal(0, hollowZeroMap.CurrentIdx);
			Assert.Equal(1234.5f, hollowZeroMap.CheckTime);
			Assert.True(hollowZeroMap.ContainsEntry("当前"));
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Rect(100, 200, 140, 253), hollowZeroMap.Nodes[0].Pos);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleHollow_ReturnsFalseWhenBackpackIsMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteHollowZeroEventScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 8, 5, 40, 20, "地图") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleHollow(zContext, screen, DateTimeOffset.UtcNow);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("未在空洞中", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_ClickHollowFinished_ClicksMatchedCompleteText()
	{
		string text = CreateTempRoot();
		try
		{
			WriteHollowZeroEventScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 24, 8, 60, 24, "完成") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.ClickHollowFinished(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("通关-完成", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1618, 1024), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_ClickHollowFinished_DoesNotClickWhenCompleteTextMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteHollowZeroEventScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 24, 8, 60, 24, "返回") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.ClickHollowFinished(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("未找到 通关-完成", operationResult.Status);
			Assert.Null(testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleStoryMode_ClicksSkipButtonAndRequestsFreshConfirmationFrame()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 20, 40, 20, "跳过") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantConfig config = new CommissionAssistantConfig
			{
				StoryMode = CommissionAssistantStoryMode.Skip.Value
			};
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleStoryMode(zContext, config, commissionAssistantRuntimeState, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("需要重截图确认", operationResult.Status);
			Assert.Null(operationResult.Data);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1710, 50), testScreenshotController.LastClickPoint);
			Assert.Equal(default(DateTimeOffset), commissionAssistantRuntimeState.MainStoryClickTime);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleStoryMode_ResolvesGameLanguageSkipText()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.GameTextResolver = (string text2) => (text2 == "跳过") ? "Skip" : text2;
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 20, 40, 20, "Skip") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantConfig config = new CommissionAssistantConfig
			{
				StoryMode = CommissionAssistantStoryMode.Skip.Value
			};
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleStoryMode(zContext, config, new CommissionAssistantRuntimeState(), screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("需要重截图确认", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1710, 50), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleSkipStoryConfirm_ConfirmsSkipOnFreshFrame()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 20, 40, 20, "确认") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantConfig commissionAssistantConfig = new CommissionAssistantConfig
			{
				StoryMode = CommissionAssistantStoryMode.Skip.Value
			};
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState
			{
				ChosenOptionLastTime = DateTimeOffset.UtcNow,
				MainStoryClickTime = DateTimeOffset.UtcNow
			};
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleSkipStoryConfirm(zContext, commissionAssistantRuntimeState, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("跳过剧情", operationResult.Status);
			Assert.Equal(default(DateTimeOffset), commissionAssistantRuntimeState.ChosenOptionLastTime);
			Assert.Equal(default(DateTimeOffset), commissionAssistantRuntimeState.MainStoryClickTime);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1052, 574), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleStoryMode_AutoClicksMiddleOptionBeforeMenu()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantConfig config = new CommissionAssistantConfig
			{
				StoryMode = CommissionAssistantStoryMode.Auto.Value
			};
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleStoryMode(zContext, config, new CommissionAssistantRuntimeState(), screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("点击中间选项", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 540), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_DoDialogClick_ChoosesLastRightOption()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 10, 10, 120, 20, "第一个"),
				new OcrMatchResult(0.99, 10, 100, 120, 20, "最后一个")
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState();
			CommissionAssistantConfig config = new CommissionAssistantConfig
			{
				DialogOption = CommissionAssistantDialogOption.Last.Value
			};
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.DoDialogClick(zContext, config, commissionAssistantRuntimeState, screen, checkCenterWords: true);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("点击右方选项", operationResult.Status);
			Assert.Equal("最后一个", commissionAssistantRuntimeState.ChosenOption);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1570, 390), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_DoDialogClick_HoldsChosenOptionDuringProtectionWindow()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 120, 20, "第一个") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantRuntimeState state = new CommissionAssistantRuntimeState
			{
				ChosenOption = "第一个",
				ChosenOptionLastTime = DateTimeOffset.UtcNow
			};
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.DoDialogClick(zContext, new CommissionAssistantConfig(), state, screen, checkCenterWords: true);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("点击右方选项", operationResult.Status);
			Assert.Null(testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_DoDialogClick_ClicksDialogContentWhenChineseTextVisible()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new AreaAwareFakeOcrMatcher(delegate(Mat image)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (image.Width != 760 || image.Height != 220)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 80, 20, 160, 20, "正在对话") };
					result = readOnlyList;
				}
				return result;
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Gray);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.DoDialogClick(zContext, new CommissionAssistantConfig(), commissionAssistantRuntimeState, screen, checkCenterWords: true);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("对话中点击", operationResult.Status);
			Assert.True(commissionAssistantRuntimeState.DialogClicked);
			Assert.Null(testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_DoDialogClick_RejectsDialogWhenGrayMaskCoverageIsBelowPythonThreshold()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new AreaAwareFakeOcrMatcher(delegate(Mat image)
			{
				IReadOnlyList<OcrMatchResult> result;
				if (image.Width != 760 || image.Height != 220)
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = Array.Empty<OcrMatchResult>();
					result = readOnlyList;
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList = new OcrMatchResult[] { new OcrMatchResult(0.99, 80, 20, 160, 20, "正在对话") };
					result = readOnlyList;
				}
				return result;
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState();
			using Mat mat = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Gray);
			mat[new OpenCvSharp.Rect(580, 790, 114, 220)].SetTo(new Scalar(0.0, 100.0, 0.0));
			OperationResult operationResult = defaultCommissionAssistantOperationServices.DoDialogClick(zContext, new CommissionAssistantConfig(), commissionAssistantRuntimeState, mat, checkCenterWords: true);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("未知画面", operationResult.Status);
			Assert.False(commissionAssistantRuntimeState.DialogClicked);
			Assert.Null(testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_CheckGameTutorial_UsesTutorialTextArea()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "玩法引导") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.CheckGameTutorial(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("玩法引导", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_CheckGameTutorial_RequiresExactPythonTitle()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "玩法引导中") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.CheckGameTutorial(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("未处于玩法引导", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleKnockKnock_ClosesLatestMessageScreen()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[3]
			{
				new OcrMatchResult(0.99, 10, 10, 100, 20, "knock knock"),
				new OcrMatchResult(0.99, 20, 300, 120, 20, "以上为最新"),
				new OcrMatchResult(0.99, 12, 10, 40, 20, "关闭")
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleKnockKnock(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("按钮-短信-关闭", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1494, 150), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleKnockKnock_DoesNotFuzzilyCloseNearLatestText()
	{
		string text = CreateTempRoot();
		try
		{
			WriteCommissionAssistantStoryScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 10, 10, 100, 20, "knock knock"),
				new OcrMatchResult(0.99, 20, 300, 120, 20, "以上为最X")
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleKnockKnock(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("以上为最X", operationResult.Status);
			Assert.NotEqual(new OneDragon.Core.Abstractions.Geometry.Point(1494, 150), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_CheckFishing_DetectsFishingCommand()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestZzzController testZzzController = new TestZzzController();
			zContext.AttachController(testZzzController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 10, 10, 40, 20, "返回"),
				new OcrMatchResult(0.99, 120, 120, 120, 20, "点击按键抛竿")
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState
			{
				FishingDone = true
			};
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.CheckFishing(zContext, screen, commissionAssistantRuntimeState);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("钓鱼", operationResult.Status);
			Assert.False(commissionAssistantRuntimeState.FishingDone);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(286, 110), testZzzController.LastMouseMovePoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_CheckFishing_ResolvesGameLanguageCastCommand()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.GameTextResolver = (string text2) => (text2 == "点击按键抛竿") ? "Cast" : text2;
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestZzzController testZzzController = new TestZzzController();
			zContext.AttachController(testZzzController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 10, 10, 40, 20, "返回"),
				new OcrMatchResult(0.99, 120, 120, 120, 20, "Cast")
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.CheckFishing(zContext, screen, new CommissionAssistantRuntimeState());
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("钓鱼", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(286, 110), testZzzController.LastMouseMovePoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleFishing_ResolvesGameLanguageCommands()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.GameTextResolver = (string text2) => (text2 == "点击按键抛竿") ? "Cast" : text2;
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestZzzController testZzzController = new TestZzzController();
			zContext.AttachController(testZzzController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 120, 120, 120, 20, "Cast") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleFishing(zContext, screen, new CommissionAssistantRuntimeState());
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("Cast", operationResult.Status);
			Assert.Equal(1, testZzzController.InteractCount);
			Assert.Equal(TimeSpan.FromMilliseconds(200L), testZzzController.LastInteractPressTime);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleFishing_UsesZzzControllerActionsForInteractCommand()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestZzzController testZzzController = new TestZzzController();
			zContext.AttachController(testZzzController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 120, 120, 120, 20, "点击按键抛杆") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantRuntimeState state = new CommissionAssistantRuntimeState();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleFishing(zContext, screen, state);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("点击按键抛竿", operationResult.Status);
			Assert.Equal(1, testZzzController.InteractCount);
			Assert.Equal(TimeSpan.FromMilliseconds(200L), testZzzController.LastInteractPressTime);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleFishing_RepeatedClickPressesSpaceWhenPowerPromptVisible()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using FishingWindowsController fishingWindowsController = new FishingWindowsController();
			zContext.AttachController(fishingWindowsController);
			zContext.OcrService.Matcher = new AreaAwareFakeOcrMatcher(delegate(Mat image)
			{
				int width = image.Width;
				if (1 == 0)
				{
				}
				IReadOnlyList<OcrMatchResult> result = width switch
				{
					1266 => new OcrMatchResult[] { new OcrMatchResult(0.99, 120, 120, 120, 20, "连点") }, 
					134 => new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 20, 20, "左") }, 
					148 => new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "强力左") }, 
					_ => Array.Empty<OcrMatchResult>(), 
				};
				if (1 == 0)
				{
				}
				return result;
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleFishing(zContext, screen, new CommissionAssistantRuntimeState());
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("连点", operationResult.Status);
			Assert.Equal(1, fishingWindowsController.MoveACount);
			Assert.Equal(new (string, TimeSpan?)[] { ("space", TimeSpan.FromMilliseconds(50L)) }, fishingWindowsController.ButtonPresses);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleFishing_HoldPrefersRightWhenBothDirectionsAreVisible()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using FishingWindowsController fishingWindowsController = new FishingWindowsController();
			zContext.AttachController(fishingWindowsController);
			zContext.OcrService.Matcher = new AreaAwareFakeOcrMatcher(delegate(Mat image)
			{
				int width = image.Width;
				if (1 == 0)
				{
				}
				IReadOnlyList<OcrMatchResult> result = width switch
				{
					1266 => new OcrMatchResult[] { new OcrMatchResult(0.99, 120, 120, 120, 20, "长按") }, 
					134 => new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 20, 20, "左") }, 
					168 => new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 20, 20, "右") }, 
					148 => new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "强力左") }, 
					140 => new OcrMatchResult[] { new OcrMatchResult(0.99, 10, 10, 80, 20, "强力右") }, 
					_ => Array.Empty<OcrMatchResult>(), 
				};
				if (1 == 0)
				{
				}
				return result;
			});
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			CommissionAssistantRuntimeState commissionAssistantRuntimeState = new CommissionAssistantRuntimeState();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleFishing(zContext, screen, commissionAssistantRuntimeState);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("长按", operationResult.Status);
			Assert.Equal(1, fishingWindowsController.MoveACount);
			Assert.Equal(1, fishingWindowsController.MoveDCount);
			Assert.Equal("d", commissionAssistantRuntimeState.FishingButtonPressed);
			Assert.Equal(new (string, TimeSpan?)[] { ("space", TimeSpan.FromMilliseconds(50L)) }, fishingWindowsController.ButtonPresses);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleFishing_ReturnsDoneWhenNormalWorldIsDetected()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestZzzController controller = new TestZzzController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices((ZContext _) => new OperationResult(IsSuccess: true, "大世界-普通"));
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleFishing(zContext, screen, new CommissionAssistantRuntimeState());
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("钓鱼结束", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultServices_HandleFishing_ReturnsUnsupportedWithoutZzzControllerActions()
	{
		string text = CreateTempRoot();
		try
		{
			WriteFishingScreenInfo(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ScreenContext.Reload(fromMemory: false, fromSeparatedFiles: true);
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 120, 120, 120, 20, "点击按键抛竿") });
			DefaultCommissionAssistantOperationServices defaultCommissionAssistantOperationServices = new DefaultCommissionAssistantOperationServices();
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			OperationResult operationResult = defaultCommissionAssistantOperationServices.HandleFishing(zContext, screen, new CommissionAssistantRuntimeState());
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("控制器不支持钓鱼按键", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(CommissionAssistantOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[5] { "委托助手", "自动战斗模式", "剧情模式", "未知画面", "钓鱼" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["委托助手"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Equal(5, readOnlyDictionary["剧情模式"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.Equal(50, readOnlyDictionary["钓鱼"].GetCustomAttribute<OperationNodeAttribute>().NodeMaxRetryTimes);
		Assert.False(readOnlyDictionary["未知画面"].GetCustomAttribute<OperationNodeAttribute>().ScreenshotBeforeRound);
		Assert.Contains(readOnlyDictionary["自动战斗模式"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "委托助手" && edge.Status == "自动战斗模式");
		Assert.Contains(readOnlyDictionary["剧情模式"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "委托助手" && edge.Status == "检测剧情模式");
		Assert.Contains(readOnlyDictionary["未知画面"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "剧情模式" && !edge.Success);
		Assert.Contains(readOnlyDictionary["钓鱼"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "剧情模式" && edge.Status == "钓鱼");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteCommissionAssistantScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "commission_assistant.yml"), "screen_id: commission_assistant\nscreen_name: 委托助手\napp_id: commission_assistant\narea_list:\n- area_name: 对话框确认\n  pc_rect:\n  - 1022\n  - 544\n  - 1238\n  - 750\n  text: 确认\n  lcs_percent: 1.0");
	}

	private static void WriteCommissionAssistantDetectionScreenInfo(string rootDirectory)
	{
		WriteCommissionAssistantScreenInfo(rootDirectory);
		string path = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		File.AppendAllText(Path.Combine(path, "commission_assistant.yml"), "\n- area_name: 左上角返回\n  pc_rect:\n  - 82\n  - 13\n  - 150\n  - 90\n  text: 返回\n  lcs_percent: 1.0");
		File.WriteAllText(Path.Combine(path, "battle.yml"), "screen_id: battle\nscreen_name: 战斗画面\narea_list:\n- area_name: 按键-交互\n  pc_rect:\n  - 1403\n  - 916\n  - 1505\n  - 1018\n  text: 交互\n  lcs_percent: 1.0");
		File.WriteAllText(Path.Combine(path, "normal_world_basic.yml"), "screen_id: normal_world_basic\nscreen_name: 大世界-普通\narea_list:\n- area_name: 左上角街区\n  id_mark: true\n  pc_rect:\n  - 234\n  - 26\n  - 394\n  - 78\n  text: 街区\n  lcs_percent: 1.0");
		File.WriteAllText(Path.Combine(path, "normal_world_investigation.yml"), "screen_id: normal_world_investigation\nscreen_name: 大世界-勘域\narea_list:\n- area_name: 勘域标记\n  id_mark: true\n  pc_rect:\n  - 234\n  - 26\n  - 394\n  - 78\n  text: 勘域\n  lcs_percent: 1.0");
	}

	private static void WriteHollowZeroEventScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "hollow_zero_event.yml"), "screen_id: hollow_zero_event\nscreen_name: 零号空洞-事件\narea_list:\n- area_name: 背包\n  pc_rect:\n  - 1448\n  - 980\n  - 1576\n  - 1020\n  text: 背包\n  lcs_percent: 1.0\n- area_name: 通关-完成\n  pc_rect:\n  - 1564\n  - 1004\n  - 1832\n  - 1052\n  text: 完成\n  lcs_percent: 0.5");
	}

	private static void WriteCommissionAssistantStoryScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "commission_assistant.yml"), "screen_id: commission_assistant\nscreen_name: 委托助手\napp_id: commission_assistant\narea_list:\n- area_name: 文本-剧情右上角\n  pc_rect:\n  - 1680\n  - 20\n  - 1840\n  - 165\n- area_name: 右侧选项区域\n  pc_rect:\n  - 1500\n  - 280\n  - 1860\n  - 760\n- area_name: 中间选项区域\n  pc_rect:\n  - 760\n  - 480\n  - 1160\n  - 600\n- area_name: 对话框内容\n  pc_rect:\n  - 580\n  - 790\n  - 1340\n  - 1010\n- area_name: 对话框确认\n  pc_rect:\n  - 1022\n  - 544\n  - 1238\n  - 750\n  text: 确认\n  lcs_percent: 1.0\n- area_name: 按钮-自动\n  pc_rect:\n  - 1686\n  - 186\n  - 1838\n  - 242\n  text: 自动\n  lcs_percent: 0.5\n- area_name: 玩法引导\n  pc_rect:\n  - 342\n  - 213\n  - 565\n  - 285\n- area_name: 标题-短信\n  pc_rect:\n  - 412\n  - 148\n  - 616\n  - 218\n  text: knock knock\n  lcs_percent: 0.5\n- area_name: 区域-短信-文本框\n  pc_rect:\n  - 790\n  - 314\n  - 1474\n  - 920\n- area_name: 按钮-短信-关闭\n  pc_rect:\n  - 1462\n  - 130\n  - 1580\n  - 246\n  text: 关闭\n  lcs_percent: 0.5\n- area_name: 左上角返回\n  pc_rect:\n  - 82\n  - 13\n  - 150\n  - 90\n  text: 返回\n  lcs_percent: 1.0");
	}

	private static void WriteFishingScreenInfo(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "fishing.yml"), "screen_id: fishing\nscreen_name: 钓鱼\napp_id: commission_assistant\narea_list:\n- area_name: 指令文本区域\n  pc_rect:\n  - 286\n  - 110\n  - 1552\n  - 812\n- area_name: 按键-返回\n  pc_rect:\n  - 50\n  - 10\n  - 208\n  - 90\n  text: 返回\n  lcs_percent: 1.0\n- area_name: 按键-时机上鱼\n  pc_rect:\n  - 1516\n  - 798\n  - 1684\n  - 966\n  text: 时机\n  lcs_percent: 0.5\n- area_name: 按键-左\n  pc_rect:\n  - 253\n  - 816\n  - 387\n  - 950\n  text: 左\n  lcs_percent: 1.0\n- area_name: 按键-强力-左\n  pc_rect:\n  - 392\n  - 816\n  - 540\n  - 950\n  text: 强力左\n  lcs_percent: 0.5\n- area_name: 按键-右\n  pc_rect:\n  - 1516\n  - 798\n  - 1684\n  - 966\n  text: 右\n  lcs_percent: 1.0\n- area_name: 按键-强力-右\n  pc_rect:\n  - 1360\n  - 798\n  - 1500\n  - 966\n  text: 强力右\n  lcs_percent: 0.5\n- area_name: 按钮-点击空白处关闭\n  pc_rect:\n  - 824\n  - 966\n  - 1098\n  - 1078\n  text: 点击空白处关闭\n  lcs_percent: 0.5\n- area_name: 标题-挑战结果\n  pc_rect:\n  - 602\n  - 110\n  - 1290\n  - 376\n  text: 挑战结果\n  lcs_percent: 0.5\n- area_name: 按钮-确定\n  pc_rect:\n  - 870\n  - 866\n  - 1090\n  - 930\n  text: 确定\n  lcs_percent: 0.5");
	}
}
