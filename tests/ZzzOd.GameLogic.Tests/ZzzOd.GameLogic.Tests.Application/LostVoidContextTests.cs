using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using OneDragon.Core.Input;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operation;
using OneDragon.Core.Operations;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class LostVoidContextTests
{
	private sealed class ScriptedLostVoidLevelExecutor : ILostVoidLevelExecutor
	{
		private readonly Queue<OperationResult> _results;

		public List<string> RegionTypes { get; } = new List<string>();

		public ScriptedLostVoidLevelExecutor(IEnumerable<OperationResult> results)
		{
			_results = new Queue<OperationResult>(results);
		}

		public Task<OperationResult> RunLevelAsync(ZContext context, LostVoidRunRecord runRecord, string regionType, CancellationToken cancellationToken)
		{
			RegionTypes.Add(regionType);
			return Task.FromResult(_results.Dequeue());
		}
	}

	private sealed class ScriptedLostVoidRunLevelRuntime : ILostVoidRunLevelRuntime
	{
		public Queue<LostVoidRunLevelLoadingState> LoadingStates { get; init; } = new Queue<LostVoidRunLevelLoadingState>(new LostVoidRunLevelLoadingState[] { new LostVoidRunLevelLoadingState(InNormalWorld: true) });

		public Queue<LostVoidRunLevelFrame> NonBattleFrames { get; init; } = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[] { new LostVoidRunLevelFrame(InNormalWorld: true) });

		public LostVoidAfterInteractState AfterInteractState { get; init; } = new LostVoidAfterInteractState(InNormalWorld: false);

		public LostVoidBattleState BattleState { get; init; } = new LostVoidBattleState(CurrentFrameInBattle: false);

		public Queue<LostVoidBattleState>? BattleStates { get; init; }

		public OperationResult MoveResult { get; init; } = new OperationResult(IsSuccess: true, "移动完成");

		public OperationResult UpdatePriorityResult { get; init; } = new OperationResult(IsSuccess: true, "需要追加代理人类型优先级");

		public OperationResult AppendPriorityResult { get; init; } = new OperationResult(IsSuccess: true, "非战斗区域");

		public LostVoidTryInteractResult TryInteractResult { get; init; } = LostVoidTryInteractResult.Success("交互成功");

		public LostVoidInteractResult InteractResult { get; init; } = LostVoidInteractResult.Success("进入下层");

		public List<string> MoveTargetTypes { get; } = new List<string>();

		public List<string> RegionTypesForMove { get; } = new List<string>();

		public List<bool> MoveStopWhenInteract { get; } = new List<bool>();

		public List<bool> MoveStopWhenDisappear { get; } = new List<bool>();

		public List<bool> MoveAllowArrivalByInteractButton { get; } = new List<bool>();

		public int StopAutoBattleCount { get; private set; }

		public int StartAutoBattleCount { get; private set; }

		public List<string> BattleLifecycleCalls { get; } = new List<string>();

		public int NonBattleWorldStateCallCount { get; private set; }

		public int NonBattleFrameCallCount { get; private set; }

		public int AppendAgentTypePriorityCallCount { get; private set; }

		public bool CurrentFrameBattleEncounter { get; init; }

		public int CurrentFrameBattleEncounterCheckCount { get; private set; }

		public int PeriodBattleEncounterCheckCount { get; private set; }

		public float? LastPeriodBattleCheckSeconds { get; private set; }

		public int TurnToFindTargetCount { get; private set; }

		public Mat? LastBattleScreen { get; private set; }

		public DateTimeOffset? LastBattleScreenshotTimeUtc { get; private set; }

		public Task<LostVoidRunLevelLoadingState> GetLoadingStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
		{
			return Task.FromResult((LoadingStates.Count > 1) ? LoadingStates.Dequeue() : LoadingStates.Peek());
		}

		public Task<LostVoidRunLevelWorldState> GetNonBattleWorldStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
		{
			NonBattleWorldStateCallCount++;
			LostVoidRunLevelFrame lostVoidRunLevelFrame = ((NonBattleFrames.Count > 0) ? NonBattleFrames.Peek() : new LostVoidRunLevelFrame(InNormalWorld: true));
			return Task.FromResult(new LostVoidRunLevelWorldState(lostVoidRunLevelFrame.InNormalWorld, lostVoidRunLevelFrame.ChallengeConfirmAvailable));
		}

		public Task<LostVoidRunLevelFrame> GetNonBattleFrameAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, IReadOnlyList<string> ignoreList, CancellationToken cancellationToken)
		{
			NonBattleFrameCallCount++;
			return Task.FromResult((NonBattleFrames.Count > 1) ? NonBattleFrames.Dequeue() : NonBattleFrames.Peek());
		}

		public bool CheckBattleEncounterInCurrentFrame(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			CurrentFrameBattleEncounterCheckCount++;
			return CurrentFrameBattleEncounter;
		}

		public bool CheckBattleEncounterInPeriod(LostVoidRunLevel operation, float totalCheckSeconds)
		{
			PeriodBattleEncounterCheckCount++;
			LastPeriodBattleCheckSeconds = totalCheckSeconds;
			return false;
		}

		public (OperationRoundResult? Result, bool Advance) HandleFriendlyTalkInit(LostVoidRunLevel operation, int roomInitedTimes)
		{
			return (null, false);
		}

		public void TurnToFindTarget(LostVoidRunLevel operation)
		{
			TurnToFindTargetCount++;
		}

		public Task<OperationResult> MoveByDetectionAsync(LostVoidRunLevel operation, string regionType, string targetType, bool stopWhenInteract, bool stopWhenDisappear, bool allowArrivalByInteractButton, IReadOnlyList<string> ignoreEntries, CancellationToken cancellationToken)
		{
			BattleLifecycleCalls.Add("StartPathfinding:" + targetType);
			RegionTypesForMove.Add(regionType);
			MoveTargetTypes.Add(targetType);
			MoveStopWhenInteract.Add(stopWhenInteract);
			MoveStopWhenDisappear.Add(stopWhenDisappear);
			MoveAllowArrivalByInteractButton.Add(allowArrivalByInteractButton);
			return Task.FromResult(MoveResult);
		}

		public Task<OperationResult> UpdatePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			return Task.FromResult(UpdatePriorityResult);
		}

		public Task<OperationResult> AppendAgentTypePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			AppendAgentTypePriorityCallCount++;
			return Task.FromResult(AppendPriorityResult);
		}

		public Task<LostVoidTryInteractResult> TryInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, IReadOnlyList<string> interactedTargetKeys, bool interactAttempted, Mat? screen, CancellationToken cancellationToken)
		{
			return Task.FromResult(TryInteractResult);
		}

		public Task<LostVoidInteractResult> HandleInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken)
		{
			return Task.FromResult(InteractResult);
		}

		public Task<LostVoidAfterInteractState> GetAfterInteractStateAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken)
		{
			return Task.FromResult(AfterInteractState);
		}

		public void MoveAfterInteract(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget)
		{
		}

		public void StartAutoBattle(LostVoidRunLevel operation)
		{
			StartAutoBattleCount++;
			BattleLifecycleCalls.Add("StartAutoBattle");
		}

		public void StopAutoBattle(LostVoidRunLevel operation)
		{
			StopAutoBattleCount++;
			BattleLifecycleCalls.Add("StopAutoBattle");
		}

		public Task<LostVoidBattleState> GetBattleStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
		{
			LastBattleScreen = screen;
			LastBattleScreenshotTimeUtc = screenshotTimeUtc;
			if (BattleStates != null && BattleStates.Count > 0)
			{
				return Task.FromResult(BattleStates.Count > 1 ? BattleStates.Dequeue() : BattleStates.Peek());
			}
			return Task.FromResult(BattleState);
		}

		public Task<OperationResult> RestartForRetryAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "准备重试"));
		}

		public Task<OperationResult> PushErrorAsync(LostVoidRunLevel operation, Mat? screen, string? previousNodeName, string? previousStatus, CancellationToken cancellationToken)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, previousStatus));
		}

		public Task<OperationResult> FailExitAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "退出"));
		}
	}

	private enum GraphScenario
	{
		NextLevel,
		Complete,
		BattleFailure,
		NodeTimeoutFailure,
		OperationTimeoutFailure
	}

	private sealed class GraphRecordingLostVoidRunLevelRuntime(GraphScenario scenario) : ILostVoidRunLevelRuntime
	{
		private int _nonBattleFrameIndex;

		private int _handleInteractIndex;

		public List<string> Calls { get; } = new List<string>();

		public Task<LostVoidRunLevelLoadingState> GetLoadingStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
		{
			Calls.Add("等待加载");
			return Task.FromResult(new LostVoidRunLevelLoadingState(InNormalWorld: true));
		}

		public Task<LostVoidRunLevelWorldState> GetNonBattleWorldStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
		{
			Calls.Add("确认大世界");
			if (scenario == GraphScenario.OperationTimeoutFailure)
			{
				SetOperationElapsed(operation, TimeSpan.FromSeconds(600L));
			}
			return Task.FromResult(new LostVoidRunLevelWorldState(InNormalWorld: true));
		}

		public Task<LostVoidRunLevelFrame> GetNonBattleFrameAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, IReadOnlyList<string> ignoreList, CancellationToken cancellationToken)
		{
			Calls.Add("非战斗详细识别");
			_nonBattleFrameIndex++;
			return Task.FromResult(new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, BossBattleStarted: false, BossInteractAvailable: false, new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object((scenario == GraphScenario.NodeTimeoutFailure) ? "0000-感叹号" : "战斗-鸣徽", 100) }, 0.0)));
		}

		public bool CheckBattleEncounterInCurrentFrame(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			return false;
		}

		public bool CheckBattleEncounterInPeriod(LostVoidRunLevel operation, float totalCheckSeconds)
		{
			return false;
		}

		public (OperationRoundResult? Result, bool Advance) HandleFriendlyTalkInit(LostVoidRunLevel operation, int roomInitedTimes)
		{
			return (null, false);
		}

		public void TurnToFindTarget(LostVoidRunLevel operation)
		{
			Calls.Add("转向识别目标");
		}

		public Task<OperationResult> MoveByDetectionAsync(LostVoidRunLevel operation, string regionType, string targetType, bool stopWhenInteract, bool stopWhenDisappear, bool allowArrivalByInteractButton, IReadOnlyList<string> ignoreEntries, CancellationToken cancellationToken)
		{
			Calls.Add($"移动:{targetType}:{stopWhenInteract}:{stopWhenDisappear}");
			if (scenario == GraphScenario.NodeTimeoutFailure)
			{
				return Task.FromResult(new OperationResult(IsSuccess: false, "节点超时"));
			}
			return Task.FromResult(new OperationResult(IsSuccess: true, "到达目标", "战斗-道中危机"));
		}

		public Task<OperationResult> UpdatePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			Calls.Add("更新优先级");
			return Task.FromResult(new OperationResult(IsSuccess: true, "需要追加代理人类型优先级"));
		}

		public Task<OperationResult> AppendAgentTypePriorityAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			Calls.Add("追加代理人类型优先级");
			return Task.FromResult(new OperationResult(IsSuccess: true, "非战斗区域"));
		}

		public Task<LostVoidTryInteractResult> TryInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, IReadOnlyList<string> interactedTargetKeys, bool interactAttempted, Mat? screen, CancellationToken cancellationToken)
		{
			Calls.Add("尝试交互");
			return Task.FromResult(LostVoidTryInteractResult.Success("交互成功"));
		}

		public Task<LostVoidInteractResult> HandleInteractAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken)
		{
			if (scenario == GraphScenario.NextLevel)
			{
				_handleInteractIndex++;
				if (_handleInteractIndex == 1)
				{
					Calls.Add("交互处理:选择");
					return Task.FromResult(new LostVoidInteractResult(LostVoidInteractResultKind.Wait, "选择武备", null, null, null, TimeSpan.Zero));
				}
				Calls.Add("交互处理:选择完成");
				return Task.FromResult(LostVoidInteractResult.Success("进入下层"));
			}
			Calls.Add("交互处理:战后");
			return Task.FromResult(LostVoidInteractResult.Success("迷失之地-挑战结果"));
		}

		public Task<LostVoidAfterInteractState> GetAfterInteractStateAsync(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget, Mat? screen, CancellationToken cancellationToken)
		{
			if (scenario == GraphScenario.NextLevel)
			{
				Calls.Add("交互后处理:下一层");
				return Task.FromResult(new LostVoidAfterInteractState(InNormalWorld: false));
			}
			Calls.Add("交互后处理:挑战结果");
			return Task.FromResult(new LostVoidAfterInteractState(InNormalWorld: false, ChallengeResultConfirmAvailable: false, ChallengeResultFinishAvailable: true));
		}

		public void MoveAfterInteract(LostVoidRunLevel operation, LostVoidInteractTarget? currentTarget)
		{
			Calls.Add("交互后移动");
		}

		public void StartAutoBattle(LostVoidRunLevel operation)
		{
			Calls.Add("准备自动战斗");
		}

		public void StopAutoBattle(LostVoidRunLevel operation)
		{
			Calls.Add("停止自动战斗");
		}

		public Task<LostVoidBattleState> GetBattleStateAsync(LostVoidRunLevel operation, Mat? screen, DateTimeOffset? screenshotTimeUtc, CancellationToken cancellationToken)
		{
			Calls.Add("战斗中");
			return Task.FromResult(new LostVoidBattleState(CurrentFrameInBattle: false, NextRegionHint: false, NoLongerInBattleByDetection: false, InInteractScreen: true, scenario == GraphScenario.BattleFailure));
		}

		public Task<OperationResult> RestartForRetryAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "准备重试"));
		}

		public Task<OperationResult> PushErrorAsync(LostVoidRunLevel operation, Mat? screen, string? previousNodeName, string? previousStatus, CancellationToken cancellationToken)
		{
			Calls.Add("保存错误信息:" + previousStatus);
			return Task.FromResult(new OperationResult(IsSuccess: true, previousStatus));
		}

		public Task<OperationResult> FailExitAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
		{
			Calls.Add("失败退出空洞");
			return Task.FromResult(new OperationResult(IsSuccess: true, "退出"));
		}
	}

	private sealed class TestScreenshotController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);

		public int ClickCount { get; private set; }

		public int ScreenshotStage { get; private set; }

		public bool ClickResult { get; init; } = true;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			ClickCount++;
			return ClickResult;
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

	private sealed class StageOcrMatcher(TestScreenshotController controller, Func<int, IReadOnlyList<string>> stageWords) : IOcrMatcher
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
			return string.Concat(from result in CreateResults()
				select result.Text);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in CreateResults())
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
			return CreateResults();
		}

		private IReadOnlyList<OcrMatchResult> CreateResults()
		{
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => new OcrMatchResult(0.99, 4 + index * 40, 4, 32, 16, word)).ToArray();
		}
	}

	private sealed class SequenceOcrMatcher(IReadOnlyList<IReadOnlyList<OcrMatchResult>> resultSets) : IOcrMatcher
	{
		private readonly Queue<IReadOnlyList<OcrMatchResult>> _resultSets = new Queue<IReadOnlyList<OcrMatchResult>>(resultSets);

		public int OcrCallCount { get; private set; }

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
			OcrCallCount++;
			IReadOnlyList<OcrMatchResult> source = ((_resultSets.Count > 1) ? _resultSets.Dequeue() : _resultSets.Peek());
			return source.Select((OcrMatchResult result) => new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	private sealed class RecordingLogSink : ILogEventSink
	{
		public List<LogEvent> Events { get; } = new List<LogEvent>();

		public void Emit(LogEvent logEvent)
		{
			Events.Add(logEvent);
		}
	}

	private sealed class RecordingButtonController : IButtonController
	{
		public void Tap(string key)
		{
		}

		public void TapCombo(IReadOnlyList<string> keys)
		{
		}

		public void Press(string key, TimeSpan? pressTime = null)
		{
		}

		public void Release(string key)
		{
		}

		public void Reset()
		{
		}
	}

	private sealed class RecordingInputController(IButtonController buttonController) : IInputController
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

	[Fact]
	public void Detector_HelpersReadFrameResultsWithoutModelFiles()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			using LostVoidDetector lostVoidDetector = new LostVoidDetector(context);
			YoloDetectFrameResult frameResult = new YoloDetectFrameResult(new YoloDetectObjectResult[4]
			{
				Object("0000-感叹号", 10),
				Object("0001-距离", 20),
				Object("战斗-鸣徽", 30),
				Object("战斗-鸣徽", 60)
			}, 0.0);
			var (condition, condition2, condition3) = lostVoidDetector.IsFrameWithAll(frameResult);
			Assert.Equal("lost_void_det", "lost_void_det");
			Assert.Equal("https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model", lostVoidDetector.ModelDownloadUrl);
			Assert.True(lostVoidDetector.CoreDetector.Config.RequireLabelsFile);
			Assert.True(condition);
			Assert.True(condition2);
			Assert.True(condition3);
			Assert.True(lostVoidDetector.IsFrameWith(frameResult, "0000-感叹号"));
			Assert.True(lostVoidDetector.IsFrameWith(frameResult, new string[2] { "0001-距离", "战斗-鸣徽" }));
			Assert.Equal(65, lostVoidDetector.GetResultByX(frameResult, "战斗-鸣徽").Center.X);
			Assert.Equal(35, lostVoidDetector.GetResultByX(frameResult, "战斗-鸣徽", byMaxX: false).Center.X);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Detector_MaskBattleAvatarsKeepsCallerImageUnchanged()
	{
		Vec3b original = new(30, 20, 10);
		using Mat source = new(new OpenCvSharp.Size(1920, 1080), MatType.CV_8UC3, new Scalar(original.Item0, original.Item1, original.Item2));
		using Mat masked = LostVoidDetector.MaskBattleAvatars(source);

		Assert.Equal(original, source.At<Vec3b>(40, 104));
		Assert.Equal(original, source.At<Vec3b>(110, 844));
		Assert.Equal(new Vec3b(), masked.At<Vec3b>(40, 104));
		Assert.Equal(new Vec3b(), masked.At<Vec3b>(110, 844));
		Assert.Equal(original, masked.At<Vec3b>(39, 103));
	}

	[Fact]
	public void Detector_ProductionPathsUseMaskedRunEntry()
	{
		string sourceDirectory = Path.Combine(FindBusinessSourceRoot(), "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application.HollowZero.LostVoid");
		string detectorSource = File.ReadAllText(Path.Combine(sourceDirectory, "LostVoidDetector.cs"));
		Assert.Contains("CoreDetector.Run(maskedImage", detectorSource, StringComparison.Ordinal);

		string[] callerFiles = ["ScreenLostVoidRunLevelRuntime.cs", "LostVoidMoveByDetectionOperation.cs"];
		foreach (string callerFile in callerFiles)
		{
			string callerSource = File.ReadAllText(Path.Combine(sourceDirectory, callerFile));
			Assert.DoesNotContain("CoreDetector.Run(", callerSource, StringComparison.Ordinal);
			Assert.Contains(".Detector", callerSource, StringComparison.Ordinal);
			Assert.Contains(".Run(", callerSource, StringComparison.Ordinal);
		}
	}

	[Fact]
	public void Context_LoadsArtifactChallengeAndInvestigationData()
	{
		string text = CreateTempRoot();
		try
		{
			WriteLostVoidData(text);
			WriteChallengeConfig(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.LostVoid.InitBeforeRun("默认-成就模式");
			zContext.TeamConfig.TeamList[3].AutoBattle = "编队战斗";
			Assert.NotNull(zContext.LostVoid.Detector);
			Assert.Equal(3, zContext.LostVoid.AllArtifactList.Count);
			Assert.True(zContext.LostVoid.GearByName.ContainsKey("喷水枪"));
			Assert.True(zContext.LostVoid.CategoryToArtifacts.ContainsKey("通用"));
			Assert.Equal("鸣徽狂热战略", zContext.LostVoid.InvestigationStrategyList[0].StrategyName);
			Assert.Equal(-1, zContext.LostVoid.PredefinedTeamIdx);
			Assert.Equal("自定义战斗", zContext.LostVoid.GetAutoOpName());
			zContext.LostVoid.PredefinedTeamIdx = 3;
			Assert.Equal("编队战斗", zContext.LostVoid.GetAutoOpName());
			Assert.Equal("战斗-鸣徽", zContext.LostVoid.ChallengeConfig.RegionTypePriority[0]);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Context_AppendsCurrentAgentTypesToPriorityAndMissingTypesToAbandonList()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.ANBY.Value));
			zContext.AutoBattleContext.AgentContext.Team.Agents.Add(new AgentInfo(AgentEnum.NICOLE.Value));
			zContext.LostVoid.DynamicPriorityList.Add("击破");
			zContext.LostVoid.AppendAgentTypePriorityFromCurrentTeam();
			Assert.Contains("击破", (IEnumerable<string>)zContext.LostVoid.DynamicPriorityList);
			Assert.Contains("支援", (IEnumerable<string>)zContext.LostVoid.DynamicPriorityList);
			Assert.DoesNotContain("支援", (IEnumerable<string>)zContext.LostVoid.DynamicAbandonList);
			Assert.Contains("强攻", (IEnumerable<string>)zContext.LostVoid.DynamicAbandonList);
			Assert.Contains("防护", (IEnumerable<string>)zContext.LostVoid.DynamicAbandonList);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Context_MatchesArtifactsFromFullAndOcrNames()
	{
		string text = CreateTempRoot();
		try
		{
			WriteLostVoidData(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.LostVoid.LoadArtifactData();
			LostVoidArtifact artifactByFullName = zContext.LostVoid.GetArtifactByFullName("[通用]喷水枪");
			LostVoidArtifact actual = zContext.LostVoid.MatchArtifactByOcrFull("【通用】喷水枪");
			LostVoidArtifact lostVoidArtifact = zContext.LostVoid.MatchArtifactByOcrFull("祝福卡");
			Assert.NotNull(artifactByFullName);
			Assert.Equal("喷水枪", artifactByFullName.Name);
			Assert.Same(artifactByFullName, actual);
			Assert.Equal("卡牌", lostVoidArtifact.Category);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Context_SelectsArtifactsByNewPrimaryPriorityAndDynamicAbandonRules()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "lost_void_challenge");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "优先级.yml"), "artifact_priority_new: true\nartifact_priority:\n  - 优先\nartifact_priority_2:\n  - 次级");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.LostVoid.InitBeforeRun("优先级");
			zContext.LostVoid.DynamicAbandonList.Add("放弃");
			LostVoidArtifactPos lostVoidArtifactPos = ArtifactPos("优先", "优先鸣徽", "A", 100);
			LostVoidArtifactPos lostVoidArtifactPos2 = ArtifactPos("普通", "新鸣徽", "S", 200);
			lostVoidArtifactPos2.IsNew = true;
			LostVoidArtifactPos lostVoidArtifactPos3 = ArtifactPos("优先", "次选鸣徽", "S", 300, 0, 40, 40, isPrimary: false);
			LostVoidArtifactPos lostVoidArtifactPos4 = ArtifactPos("放弃", "放弃鸣徽", "S", 400);
			IReadOnlyList<LostVoidArtifactPos> artifactByPriority = zContext.LostVoid.GetArtifactByPriority(new LostVoidArtifactPos[4] { lostVoidArtifactPos4, lostVoidArtifactPos3, lostVoidArtifactPos, lostVoidArtifactPos2 }, 4, considerPriority1: true, considerPriority2: true, considerNotInPriority: true, null, considerPriorityNew: true);
			Assert.Equal(new LostVoidArtifactPos[4] { lostVoidArtifactPos2, lostVoidArtifactPos, lostVoidArtifactPos4, lostVoidArtifactPos3 }, artifactByPriority);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ChallengeConfig_RuntimePriorityCopiesConfiguredPriorityAndCanReset()
	{
		LostVoidChallengeConfig lostVoidChallengeConfig = new LostVoidChallengeConfig();
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = "击破";
		lostVoidChallengeConfig.ArtifactPriority = list;
		LostVoidChallengeConfig lostVoidChallengeConfig2 = lostVoidChallengeConfig;
		lostVoidChallengeConfig2.ArtifactPriorityInBattle.Add("支援");
		num = 2;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<string> span = CollectionsMarshal.AsSpan(list2);
		span[0] = "击破";
		span[1] = "支援";
		Assert.Equal<List<string>>(list2, lostVoidChallengeConfig2.ArtifactPriorityInBattle);
		lostVoidChallengeConfig2.ClearArtifactPriorityInBattle();
		num = 1;
		List<string> list3 = new List<string>(num);
		CollectionsMarshal.SetCount(list3, num);
		CollectionsMarshal.AsSpan(list3)[0] = "击破";
		Assert.Equal<List<string>>(list3, lostVoidChallengeConfig2.ArtifactPriorityInBattle);
	}

	[Fact]
	public void Context_CheckRegionTypePriorityInputUsesLostVoidRegionTypes()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		(List<string> Items, string ErrorMessage) tuple = zContext.LostVoid.CheckRegionTypePriorityInput(" 战斗-武备 \n未知区域\n邦布商店");
		List<string> item = tuple.Items;
		string item2 = tuple.ErrorMessage;
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "战斗-武备";
		span[1] = "邦布商店";
		Assert.Equal(list, item);
		Assert.Equal("输入非法 未知区域", item2);
	}

	[Fact]
	public void Context_GetEntryByPriorityChoosesRightmostAndHonorsIgnoreList()
	{
		string text = CreateTempRoot();
		try
		{
			WriteChallengeConfig(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.LostVoid.InitBeforeRun("默认-成就模式");
			LostVoidMoveTarget lostVoidMoveTarget = MoveTarget("战斗-鸣徽", 100);
			LostVoidMoveTarget lostVoidMoveTarget2 = MoveTarget("战斗-鸣徽", 220);
			LostVoidMoveTarget lostVoidMoveTarget3 = MoveTarget("战斗-道中危机", 320);
			LostVoidMoveTarget lostVoidMoveTarget4 = MoveTarget("邦布商店", 420);
			LostVoidMoveTarget[] entryList = new LostVoidMoveTarget[4] { lostVoidMoveTarget, lostVoidMoveTarget2, lostVoidMoveTarget3, lostVoidMoveTarget4 };
			Assert.Same(lostVoidMoveTarget2, zContext.LostVoid.GetEntryByPriority(entryList));
			Assert.Same(lostVoidMoveTarget3, zContext.LostVoid.GetEntryByPriority(entryList, new string[] { "战斗-鸣徽" }));
			zContext.LostVoid.HadInteractedOpheliaOnCurrentLevel = true;
			Assert.Same(lostVoidMoveTarget4, zContext.LostVoid.GetEntryByPriority(entryList, new string[] { "战斗-鸣徽" }));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void MoveTargetWrapper_MergesNearbyEntries()
	{
		LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper = WrappedTarget(EntryLabel("战斗-鸣徽"), 100, 0, 20);
		LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper2 = WrappedTarget(EntryLabel("战斗-武备"), 130, 0, 20);
		bool condition = lostVoidMoveTargetWrapper.MergeAnotherTarget(lostVoidMoveTargetWrapper2);
		Assert.True(condition);
		Assert.True(lostVoidMoveTargetWrapper.IsMixed);
		Assert.True(lostVoidMoveTargetWrapper2.IsMixed);
		Assert.Same(lostVoidMoveTargetWrapper, lostVoidMoveTargetWrapper2.MergeParent);
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "战斗-鸣徽";
		span[1] = "战斗-武备";
		Assert.Equal<List<string>>(list, lostVoidMoveTargetWrapper.TargetNames);
		Assert.Equal(100, lostVoidMoveTargetWrapper.EntireRect.X1);
		Assert.Equal(150, lostVoidMoveTargetWrapper.EntireRect.X2);
		Assert.Equal("战斗-鸣徽", lostVoidMoveTargetWrapper.LeftestTargetName);
	}

	[Fact]
	public void MoveByDetectionService_SelectsEntryByPriorityAndOpheliaIgnore()
	{
		string text = CreateTempRoot();
		try
		{
			WriteChallengeConfig(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.LostVoid.InitBeforeRun("默认-成就模式");
			LostVoidMoveByDetectionService instance = LostVoidMoveByDetectionService.Instance;
			LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper = WrappedTarget(EntryLabel("战斗-鸣徽"), 100);
			LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper2 = WrappedTarget(EntryLabel("战斗-道中危机"), 200);
			LostVoidMoveTargetWrapper lostVoidMoveTargetWrapper3 = WrappedTarget(EntryLabel("邦布商店"), 300);
			LostVoidMoveTargetWrapper[] entryList = new LostVoidMoveTargetWrapper[3] { lostVoidMoveTargetWrapper, lostVoidMoveTargetWrapper2, lostVoidMoveTargetWrapper3 };
			Assert.Same(lostVoidMoveTargetWrapper, instance.SelectEntryByPriority(zContext.LostVoid, entryList, Array.Empty<string>()));
			Assert.Same(lostVoidMoveTargetWrapper2, instance.SelectEntryByPriority(zContext.LostVoid, entryList, new string[] { "战斗-鸣徽" }));
			zContext.LostVoid.HadInteractedOpheliaOnCurrentLevel = true;
			Assert.Same(lostVoidMoveTargetWrapper3, instance.SelectEntryByPriority(zContext.LostVoid, entryList, new string[] { "战斗-鸣徽" }));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void MoveByDetectionService_PrefersSameLastTargetBeforePriority()
	{
		string text = CreateTempRoot();
		try
		{
			WriteChallengeConfig(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.LostVoid.InitBeforeRun("默认-成就模式");
			LostVoidMoveByDetectionService instance = LostVoidMoveByDetectionService.Instance;
			LostVoidMoveTargetWrapper lastTarget = WrappedTarget(EntryLabel("邦布商店"), 120);
			YoloDetectFrameResult frameResult = new YoloDetectFrameResult(new YoloDetectObjectResult[2]
			{
				Object(EntryLabel("战斗-鸣徽"), 300),
				Object(EntryLabel("邦布商店"), 130)
			}, 0.0);
			LostVoidMoveTargetWrapper entryTarget = instance.GetEntryTarget(zContext.LostVoid, frameResult, lastTarget);
			Assert.NotNull(entryTarget);
			Assert.Equal("邦布商店", entryTarget.LeftestTargetName);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void MoveByDetectionService_ChoosesDirectionByCurrentRegion()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		LostVoidMoveByDetectionService instance = LostVoidMoveByDetectionService.Instance;
		YoloDetectFrameResult frameResult = new YoloDetectFrameResult(new YoloDetectObjectResult[2]
		{
			Object("0001-距离", 100),
			Object("0001-距离", 300)
		}, 0.0);
		LostVoidMoveTargetWrapper moveTarget = instance.GetMoveTarget(zContext.LostVoid, "入口", "0001-距离", frameResult);
		LostVoidMoveTargetWrapper moveTarget2 = instance.GetMoveTarget(zContext.LostVoid, "战斗-鸣徽", "0001-距离", frameResult);
		Assert.Equal(300, moveTarget.EntireRect.X1);
		Assert.Equal(100, moveTarget2.EntireRect.X1);
	}

	[Fact]
	public void MoveByDetectionService_StopsForInteractionAndCalculatesTurnDistance()
	{
		LostVoidMoveByDetectionService instance = LostVoidMoveByDetectionService.Instance;
		YoloDetectFrameResult frameResult = new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("0001-距离", 100, 0, 120, 120) }, 0.0);
		YoloDetectFrameResult frameResult2 = new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("0000-感叹号", 100, 0, 71, 71) }, 0.0);
		Assert.False(instance.ShouldStopForInteraction(frameResult, stopWhenInteract: true, interactButtonAvailable: false));
		Assert.False(instance.ShouldStopForInteraction(frameResult2, stopWhenInteract: true, interactButtonAvailable: false));
		Assert.True(instance.ShouldStopForInteraction(frameResult2, stopWhenInteract: true, interactButtonAvailable: true));
		Assert.True(instance.ShouldStopForInteraction(new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0), stopWhenInteract: true, interactButtonAvailable: true, allowArrivalByInteractButton: true));
		Assert.Equal(0, instance.CalculateTurnDistance(new OneDragon.Core.Abstractions.Geometry.Point(980, 500), 1920, isMoving: false));
		Assert.Equal(200, instance.CalculateTurnDistance(new OneDragon.Core.Abstractions.Geometry.Point(2200, 500), 1920, isMoving: false));
		Assert.Equal(15, instance.CalculateTurnDistance(new OneDragon.Core.Abstractions.Geometry.Point(1200, 500), 1920, isMoving: true));
		Assert.Equal(-12, instance.CalculateTurnDistance(new OneDragon.Core.Abstractions.Geometry.Point(900, 500), 1920, isMoving: false));
	}

	[Fact]
	public void MoveByDetection_FifthLostTargetEscapesAndStillReturnsNoFound()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			LostVoidMoveByDetectionOperation lostVoidMoveByDetectionOperation = new LostVoidMoveByDetectionOperation(context, "入口", "xxxx-入口");
			int escapeCount = 0;
			for (int i = 0; i < 4; i++)
			{
				OperationRoundResult operationRoundResult = lostVoidMoveByDetectionOperation.HandleLostTargetDuringDetection(5, delegate
				{
					escapeCount++;
				});
				Assert.True(operationRoundResult.IsSuccess);
				Assert.Equal("未识别到目标", operationRoundResult.Status);
			}
			Assert.Equal(0, escapeCount);
			OperationRoundResult operationRoundResult2 = lostVoidMoveByDetectionOperation.HandleLostTargetDuringDetection(5, delegate
			{
				escapeCount++;
			});
			Assert.True(operationRoundResult2.IsSuccess);
			Assert.Equal("未识别到目标", operationRoundResult2.Status);
			Assert.Equal(1, escapeCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void MoveByDetection_TenthLostTargetDuringMoveEscapesAndStillReturnsNoFound()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		LostVoidMoveByDetectionOperation lostVoidMoveByDetectionOperation = new LostVoidMoveByDetectionOperation(context, "入口", "xxxx-入口");
		int escapeCount = 0;
		for (int i = 0; i < 9; i++)
		{
			OperationRoundResult operationRoundResult = lostVoidMoveByDetectionOperation.HandleLostTargetDuringDetection(10, delegate
			{
				escapeCount++;
			});
			Assert.Equal("未识别到目标", operationRoundResult.Status);
		}
		Assert.Equal(0, escapeCount);
		OperationRoundResult operationRoundResult2 = lostVoidMoveByDetectionOperation.HandleLostTargetDuringDetection(10, delegate
		{
			escapeCount++;
		});
		Assert.True(operationRoundResult2.IsSuccess);
		Assert.Equal("未识别到目标", operationRoundResult2.Status);
		Assert.Equal(1, escapeCount);
	}

	[Fact]
	public void MoveByDetection_BriefTargetLossStopsAndWaitsForAnotherFrame()
	{
		string rootDirectory = CreateTempRoot();
		using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
		RecordingButtonController buttonController = new RecordingButtonController();
		context.AttachController(new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(buttonController), null, buttonController, null, null, skipForegroundActivation: true));
		LostVoidMoveByDetectionOperation operation = new LostVoidMoveByDetectionOperation(context, "入口", "xxxx-入口", stopWhenInteract: false)
		{
			DetectFrameOverride = () => new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0)
		};
		using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
		SetOperationScreenshot(operation, screen);

		OperationRoundResult result = InvokeMoveTowards(operation);

		Assert.Equal(OperationRoundResultKind.Wait, result.Kind);
		Assert.Equal("短暂丢失目标", result.Status);
	}

	[Fact]
	public void MoveByDetection_MoveTowardsLogsPythonNodeLabel()
	{
		string text = CreateTempRoot();
		try
		{
			WriteAutoBattleScreenYaml(text);
			RecordingLogSink recordingLogSink = new RecordingLogSink();
			using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(recordingLogSink).CreateLogger();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text), logger);
			zContext.ScreenContext.Reload();
			RecordingButtonController recordingButtonController = new RecordingButtonController();
			zContext.AttachController(new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(recordingButtonController), null, recordingButtonController, null, null, skipForegroundActivation: true));
			LostVoidMoveByDetectionOperation lostVoidMoveByDetectionOperation = new LostVoidMoveByDetectionOperation(zContext, "入口", "xxxx-入口");
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			SetOperationScreenshot(lostVoidMoveByDetectionOperation, screen);
			SetPrivateField(lostVoidMoveByDetectionOperation, "_targetLostAtUtc", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2L));
			OperationRoundResult operationRoundResult = InvokeMoveTowards(lostVoidMoveByDetectionOperation);
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("未识别到目标", operationRoundResult.Status);
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent logEvent) => logEvent.RenderMessage().Contains("寻路节点[移动]", StringComparison.Ordinal)));
			Assert.DoesNotContain((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent logEvent) => logEvent.RenderMessage().Contains("寻路节点[移动前转向]", StringComparison.Ordinal)));
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent logEvent) => logEvent.RenderMessage().Contains("未识别到可追踪目标", StringComparison.Ordinal)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void MoveByDetection_TurnAtFirstLogsDetectionSummaryAndNoTargetReason()
	{
		string text = CreateTempRoot();
		try
		{
			RecordingLogSink recordingLogSink = new RecordingLogSink();
			using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(recordingLogSink).CreateLogger();
			using ZContext context = new ZContext(new OneDragonEnvironment(text), logger);
			double frameTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
			LostVoidMoveByDetectionOperation operation = new LostVoidMoveByDetectionOperation(context, "入口", "xxxx-入口", stopWhenInteract: false)
			{
				IsInNormalWorldOverride = (Mat _) => true,
				DetectFrameOverride = () => new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), frameTime, null, "pathfinding-frame", LostVoidDetector.OverlaySourcePathfinding)
			};
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			SetOperationScreenshot(operation, screen);
			OperationRoundResult operationRoundResult = InvokeTurnAtFirst(operation);
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("未识别到目标", operationRoundResult.Status);
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent logEvent) => logEvent.RenderMessage().Contains("寻路节点[移动前转向]", StringComparison.Ordinal)));
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent logEvent) => logEvent.RenderMessage().Contains("未识别到可追踪目标", StringComparison.Ordinal)));
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text.StartsWith("迷失之地寻路节点[移动前转向]:", StringComparison.Ordinal) && entry.Properties.TryGetValue("FrameId", out LogEventPropertyValue frameId) && frameId.ToString().Contains("pathfinding-frame", StringComparison.Ordinal) && entry.Properties.TryGetValue("OverlaySource", out LogEventPropertyValue source) && source.ToString().Contains(LostVoidDetector.OverlaySourcePathfinding, StringComparison.Ordinal) && entry.Properties.ContainsKey("FrameTimeUtc")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { true, "0000-感叹号", "移动前转向" })]
	[InlineData(new object[] { true, "0001-距离", "移动前转向" })]
	[InlineData(new object[] { false, "0001-距离", "移动" })]
	[InlineData(new object[] { false, "0000-感叹号", "移动" })]
	public void MoveByDetection_LogsHigherPriorityEntryFallback(bool turnAtFirst, string higherPriorityTarget, string expectedNodeName)
	{
		string text = CreateTempRoot();
		try
		{
			RecordingLogSink recordingLogSink = new RecordingLogSink();
			using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(recordingLogSink).CreateLogger();
			using ZContext context = new ZContext(new OneDragonEnvironment(text), logger);
			YoloDetectFrameResult frame = new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object(higherPriorityTarget, 100) }, 0.0);
			LostVoidMoveByDetectionOperation lostVoidMoveByDetectionOperation = new LostVoidMoveByDetectionOperation(context, "入口", "xxxx-入口", stopWhenInteract: false)
			{
				IsInNormalWorldOverride = (Mat _) => true,
				DetectFrameOverride = () => frame
			};
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			SetOperationScreenshot(lostVoidMoveByDetectionOperation, screen);
			if (!turnAtFirst)
			{
				SetPrivateField(lostVoidMoveByDetectionOperation, "_targetLostAtUtc", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2L));
			}
			OperationRoundResult operationRoundResult = (turnAtFirst ? InvokeTurnAtFirst(lostVoidMoveByDetectionOperation) : InvokeMoveTowards(lostVoidMoveByDetectionOperation));
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("需要重新识别", operationRoundResult.Status);
			Assert.Contains((IEnumerable<LogEvent>)recordingLogSink.Events, (Predicate<LogEvent>)((LogEvent logEvent) => logEvent.RenderMessage().Contains("寻路节点[" + expectedNodeName + "]", StringComparison.Ordinal) && logEvent.RenderMessage().Contains("检测到更高优先级目标", StringComparison.Ordinal) && logEvent.RenderMessage().Contains(higherPriorityTarget, StringComparison.Ordinal)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void MoveByDetection_FirstDoorStateGraphFollowsPythonBranches()
	{
		string text = CreateTempRoot();
		try
		{
			WriteAutoBattleScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.ScreenContext.Reload();
			RecordingButtonController recordingButtonController = new RecordingButtonController();
			List<(float X, float Y)> mouseMoves = new List<(float, float)>();
			zContext.AttachController(new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(recordingButtonController), null, recordingButtonController, null, null, skipForegroundActivation: true, delegate(float x, float y)
			{
				mouseMoves.Add((x, y));
			}));
			LostVoidMoveByDetectionOperation lostVoidMoveByDetectionOperation = new LostVoidMoveByDetectionOperation(zContext, "入口", "xxxx-入口", stopWhenInteract: false, stopWhenDisappear: false)
			{
				IsInNormalWorldOverride = (Mat _) => true
			};
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			SetOperationScreenshot(lostVoidMoveByDetectionOperation, screen);
			lostVoidMoveByDetectionOperation.DetectFrameOverride = () => new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("xxxx-入口", 1600) }, 0.0);
			OperationRoundResult operationRoundResult = InvokeTurnAtFirst(lostVoidMoveByDetectionOperation);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult.Kind);
			Assert.Equal("转动朝向目标", operationRoundResult.Status);
			Assert.Contains((IEnumerable<(float, float)>)mouseMoves, (Predicate<(float, float)>)(((float X, float Y) move) => move.X == 5f && move.Y == 0f));
			lostVoidMoveByDetectionOperation.DetectFrameOverride = () => new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("xxxx-入口", 955) }, 0.0);
			OperationRoundResult operationRoundResult2 = InvokeTurnAtFirst(lostVoidMoveByDetectionOperation);
			Assert.True(operationRoundResult2.IsSuccess);
			Assert.Equal("开始移动", operationRoundResult2.Status);
			OperationRoundResult operationRoundResult3 = InvokeMoveTowards(lostVoidMoveByDetectionOperation);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult3.Kind);
			Assert.Equal("移动中", operationRoundResult3.Status);
			lostVoidMoveByDetectionOperation.DetectFrameOverride = () => new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0);
			OperationRoundResult operationRoundResult4 = InvokeMoveTowards(lostVoidMoveByDetectionOperation);
			Assert.Equal(OperationRoundResultKind.Wait, operationRoundResult4.Kind);
			Assert.Equal("短暂丢失目标", operationRoundResult4.Status);
			SetPrivateField(lostVoidMoveByDetectionOperation, "_targetLostAtUtc", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2L));
			lostVoidMoveByDetectionOperation.DetectFrameOverride = () => new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("0001-距离", 200) }, 0.0);
			OperationRoundResult operationRoundResult5 = InvokeMoveTowards(lostVoidMoveByDetectionOperation);
			Assert.True(operationRoundResult5.IsSuccess);
			Assert.Equal("需要重新识别", operationRoundResult5.Status);
			Assert.True(LostVoidMoveByDetectionService.Instance.ShouldStopForInteraction(new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0), stopWhenInteract: true, interactButtonAvailable: true, allowArrivalByInteractButton: true));
			SetPrivateField(lostVoidMoveByDetectionOperation, "_noTargetHandleTimes", 6);
			SetPrivateField(lostVoidMoveByDetectionOperation, "_waitingNoTargetScreenshot", value: true);
			lostVoidMoveByDetectionOperation.DetectFrameOverride = () => new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0);
			OperationRoundResult operationRoundResult6 = InvokeHandleNoTarget(lostVoidMoveByDetectionOperation);
			Assert.True(operationRoundResult6.IsSuccess);
			Assert.Equal("尝试脱困", operationRoundResult6.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void PriorityUpdater_ExtractsAndAppendsDynamicPriorities()
	{
		LostVoidPriorityTextBlock[] blocks = new LostVoidPriorityTextBlock[6]
		{
			new LostVoidPriorityTextBlock("[防护:斗盾]霸气驰援", new OneDragon.Core.Abstractions.Geometry.Rect(100, 10, 220, 30)),
			new LostVoidPriorityTextBlock("[攻击:强攻]无关", new OneDragon.Core.Abstractions.Geometry.Rect(400, 10, 520, 30)),
			new LostVoidPriorityTextBlock("等级1", new OneDragon.Core.Abstractions.Geometry.Rect(120, 50, 200, 70)),
			new LostVoidPriorityTextBlock("【支援：增益】支援鸣徽", new OneDragon.Core.Abstractions.Geometry.Rect(100, 90, 220, 110)),
			new LostVoidPriorityTextBlock("等级2", new OneDragon.Core.Abstractions.Geometry.Rect(120, 130, 200, 150)),
			new LostVoidPriorityTextBlock("等级1", new OneDragon.Core.Abstractions.Geometry.Rect(125, 170, 205, 190))
		};
		using ZContext zContext = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		zContext.LostVoid.DynamicPriorityList.Add("防护");
		IReadOnlyList<string> readOnlyList = LostVoidPriorityUpdater.ExtractDynamicPriorities(blocks);
		LostVoidPriorityUpdater.AppendDynamicPriorities(zContext.LostVoid, readOnlyList);
		Assert.Equal(new string[2] { "防护", "支援" }, readOnlyList);
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "防护";
		span[1] = "支援";
		Assert.Equal<List<string>>(list, zContext.LostVoid.DynamicPriorityList);
		Assert.Equal("异常", LostVoidPriorityUpdater.ExtractPriorityCategoryFromText("【异常：紊乱】测试"));
		Assert.Null(LostVoidPriorityUpdater.ExtractPriorityCategoryFromText("没有分类"));
	}

	[Fact]
	public void InteractService_MatchesTargetTextToEntryNpcAgentAndBoss()
	{
		LostVoidInteractService instance = LostVoidInteractService.Instance;
		LostVoidInteractTarget lostVoidInteractTarget = instance.MatchInteractTarget("<战斗-鸣徽>");
		LostVoidInteractTarget lostVoidInteractTarget2 = instance.MatchInteractTarget("奥菲莉亚");
		LostVoidInteractTarget lostVoidInteractTarget3 = instance.MatchInteractTarget("安比");
		LostVoidInteractTarget lostVoidInteractTarget4 = instance.MatchInteractTarget("终结之役·牲鬼");
		Assert.True(lostVoidInteractTarget.IsEntry);
		Assert.Equal("战斗-鸣徽", lostVoidInteractTarget.Name);
		Assert.True(lostVoidInteractTarget2.IsNpc);
		Assert.Equal("感叹号", lostVoidInteractTarget2.Icon);
		Assert.True(lostVoidInteractTarget3.IsAgent);
		Assert.Equal("安比", lostVoidInteractTarget3.Name);
		Assert.True(lostVoidInteractTarget4.IsEntry);
		Assert.Equal("战斗-终结之役", lostVoidInteractTarget4.Icon);
		Assert.Null(instance.MatchInteractTarget(""));
	}

	[Fact]
	public void InteractService_UsesGameTextResolverBeforePythonDifflibMatch()
	{
		LostVoidInteractService instance = LostVoidInteractService.Instance;
		LostVoidInteractTarget lostVoidInteractTarget = instance.MatchInteractTarget("Ophelia", Resolve);
		LostVoidInteractTarget lostVoidInteractTarget2 = instance.MatchInteractTarget("Combat Resonium", Resolve);
		Assert.True(lostVoidInteractTarget.IsNpc);
		Assert.Equal("奥菲莉亚", lostVoidInteractTarget.Name);
		Assert.True(lostVoidInteractTarget2.IsEntry);
		Assert.Equal("战斗-鸣徽", lostVoidInteractTarget2.Name);
		static string Resolve(string text)
		{
			if (1 == 0)
			{
			}
			string result = ((text == "奥菲莉亚") ? "Ophelia" : ((!(text == "战斗-鸣徽")) ? text : "Combat Resonium"));
			if (1 == 0)
			{
			}
			return result;
		}
	}

	[Fact]
	public void InteractService_ParsesChooseTitleConfirmCountAndLotteryTimes()
	{
		LostVoidInteractService instance = LostVoidInteractService.Instance;
		LostVoidChooseTitleState lostVoidChooseTitleState = instance.ParseChooseTitle(new string[] { "请选择2枚鸣徽" });
		LostVoidChooseTitleState lostVoidChooseTitleState2 = instance.ParseChooseTitle(new string[] { "请选择1个武备" });
		LostVoidChooseTitleState lostVoidChooseTitleState3 = instance.ParseChooseTitle(new string[] { "获得战利品" });
		LostVoidChooseTitleState lostVoidChooseTitleState4 = instance.ParseChooseTitle(Array.Empty<string>(), gearMarkerFound: true);
		Assert.True(lostVoidChooseTitleState.ToChooseArtifact);
		Assert.Equal(2, lostVoidChooseTitleState.ToChooseNum);
		Assert.True(lostVoidChooseTitleState2.ToChooseGear);
		Assert.Equal(1, lostVoidChooseTitleState2.ToChooseNum);
		Assert.True(lostVoidChooseTitleState3.ToChooseArtifact);
		Assert.Equal(0, lostVoidChooseTitleState3.ToChooseNum);
		Assert.True(lostVoidChooseTitleState4.ToChooseGear);
		Assert.Equal(1, lostVoidChooseTitleState4.ToChooseNum);
		Assert.Equal(1, instance.ParseConfirmChosenCount(new string[] { "确定（1/2）" }, 2));
		Assert.Equal(2, instance.ParseConfirmChosenCount(new string[2] { "确定(1/3)", "确定(2/3)" }, 3));
		Assert.Null(instance.ParseConfirmChosenCount(new string[] { "确定" }, 2));
		Assert.True(instance.HasLotteryTimesLeft(new string[] { "剩余次数1" }));
		Assert.False(instance.HasLotteryTimesLeft(new string[] { "剩余次数0" }));
	}

	[Fact]
	public void InteractService_SortsCandidatesAndParsesGearOcrNames()
	{
		LostVoidInteractService instance = LostVoidInteractService.Instance;
		LostVoidArtifactPos lostVoidArtifactPos = ArtifactPos("通用", "副名", "S", 400, 0, 40, 40, isPrimary: false);
		LostVoidArtifactPos lostVoidArtifactPos2 = ArtifactPos("通用", "B级", "B", 300);
		LostVoidArtifactPos lostVoidArtifactPos3 = ArtifactPos("通用", "右侧S", "S", 200);
		LostVoidArtifactPos lostVoidArtifactPos4 = ArtifactPos("通用", "左侧S", "S", 100);
		IReadOnlyList<LostVoidArtifactPos> readOnlyList = instance.SortCandidates(new LostVoidArtifactPos[4] { lostVoidArtifactPos, lostVoidArtifactPos2, lostVoidArtifactPos3, lostVoidArtifactPos4 });
		LostVoidArtifactNameResult lostVoidArtifactNameResult = instance.BuildArtifactFromOcrName("【异常：紊乱】轰鸣引擎");
		LostVoidArtifactNameResult lostVoidArtifactNameResult2 = instance.BuildArtifactFromOcrName("轰鸣引擎");
		IReadOnlyList<string> actual = instance.ExtractNamesFromStitchedOcr(new OcrMatchResult[4]
		{
			new OcrMatchResult(0.9, 50, 10, 20, 10, "[强攻]"),
			new OcrMatchResult(0.9, 80, 10, 20, 10, "鸣徽"),
			new OcrMatchResult(0.9, 40, 55, 20, 10, "[支援]"),
			new OcrMatchResult(0.9, 70, 55, 20, 10, "回响")
		}, 2, 50);
		Assert.Same(lostVoidArtifactPos4, readOnlyList[0]);
		Assert.Same(lostVoidArtifactPos3, readOnlyList[1]);
		Assert.Same(lostVoidArtifactPos2, readOnlyList[2]);
		Assert.Same(lostVoidArtifactPos, readOnlyList[3]);
		Assert.True(lostVoidArtifactNameResult.IsPrimaryName);
		Assert.Equal("异常", lostVoidArtifactNameResult.Artifact.Category);
		Assert.Equal("轰鸣引擎", lostVoidArtifactNameResult.Artifact.Name);
		Assert.Null(lostVoidArtifactNameResult2.Artifact);
		Assert.Equal(new string[2] { "[强攻]鸣徽", "[支援]回响" }, actual);
	}

	[Fact]
	public void ArtifactPos_AttachesStorePriceAndBuyButtonByHorizontalDistance()
	{
		LostVoidArtifactPos lostVoidArtifactPos = ArtifactPos("通用", "喷水枪", "A", 100, 0, 100);
		Assert.True(lostVoidArtifactPos.AddPrice(100, new OneDragon.Core.Abstractions.Geometry.Rect(120, 80, 170, 100)));
		Assert.True(lostVoidArtifactPos.AddBuy(new OneDragon.Core.Abstractions.Geometry.Rect(130, 120, 180, 140)));
		Assert.False(lostVoidArtifactPos.AddPrice(200, new OneDragon.Core.Abstractions.Geometry.Rect(260, 80, 310, 100)));
		Assert.False(lostVoidArtifactPos.AddBuy(new OneDragon.Core.Abstractions.Geometry.Rect(260, 120, 310, 140)));
		Assert.Equal(100, lostVoidArtifactPos.StorePrice);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Rect(130, 120, 180, 140), lostVoidArtifactPos.StoreBuyRect);
	}

	[Fact]
	public void InteractOperations_DeclarePythonFlowNodes()
	{
		Assert.Contains((IEnumerable<string>)NodeNames<LostVoidChooseCommonOperation>(), (Predicate<string>)((string name) => name == "选择"));
		Assert.Contains((IEnumerable<string>)NodeNames<LostVoidChooseGearOperation>(), (Predicate<string>)((string name) => name == "点击携带"));
		Assert.Contains((IEnumerable<string>)NodeNames<LostVoidBangbooStoreOperation>(), (Predicate<string>)((string name) => name == "购买藏品"));
		Assert.Contains((IEnumerable<string>)NodeNames<LostVoidLotteryOperation>(), (Predicate<string>)((string name) => name == "点击后确定"));
		Assert.Contains((IEnumerable<string>)NodeNames<LostVoidRouteChangeOperation>(), (Predicate<string>)((string name) => name == "返回"));
	}

	[Fact]
	public async Task ChooseCommon_ConfirmRefreshesTheFrameBeforeClicking()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidCommonSelectionScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new StageOcrMatcher(controller, delegate(int stage)
			{
				IReadOnlyList<string> result2;
				if (stage != 1)
				{
					IReadOnlyList<string> readOnlyList = new string[] { "确定" };
					result2 = readOnlyList;
				}
				else
				{
					IReadOnlyList<string> readOnlyList = new string[] { "获得战利品" };
					result2 = readOnlyList;
				}
				return result2;
			});
			context.ScreenContext.Reload();
			LostVoidChooseCommonOperation operation = new LostVoidChooseCommonOperation(context);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(3L));
			Assert.True(result.IsSuccess, result.Status);
			Assert.Equal("按钮-确定", result.Status);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(2, controller.ScreenshotStage);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void RunLevel_MapsRegionTypeAndBattleTransition()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			LostVoidRunRecord runRecord = new LostVoidRunRecord(new LostVoidConfig());
			LostVoidRunLevel lostVoidRunLevel = new LostVoidRunLevel(context, runRecord, "入口", new ScriptedLostVoidRunLevelRuntime());
			LostVoidRunLevel lostVoidRunLevel2 = new LostVoidRunLevel(context, runRecord, "战斗-道中危机", new ScriptedLostVoidRunLevelRuntime());
			LostVoidRunLevel lostVoidRunLevel3 = new LostVoidRunLevel(context, runRecord, "战斗-终结之役", new ScriptedLostVoidRunLevelRuntime());
			Assert.Equal("非战斗区域", lostVoidRunLevel.InitForRegionTypeStatus());
			Assert.Equal("战斗区域", lostVoidRunLevel2.InitForRegionTypeStatus());
			Assert.Equal("非战斗区域", lostVoidRunLevel3.InitForRegionTypeStatus());
			Assert.True(lostVoidRunLevel3.BossPreBattle);
			Assert.True(lostVoidRunLevel3.EnterBattle(null, endBossPreBattle: true).IsSuccess);
			Assert.False(lostVoidRunLevel3.BossPreBattle);
			Assert.Equal("战斗区域", lostVoidRunLevel3.InitForRegionTypeStatus());
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { "0000-感叹号", true, false, false })]
	[InlineData(new object[] { "0001-距离", false, true, false })]
	[InlineData(new object[] { "xxxx-入口", true, false, false })]
	public async Task RunLevel_NonBattlePassesPythonMoveStopFlags(string targetType, bool expectedStopWhenInteract, bool expectedStopWhenDisappear, bool expectedAllowArrivalByInteractButton)
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		context.LostVoid.PriorityUpdated = true;
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[] { new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, BossBattleStarted: false, BossInteractAvailable: false, new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object(targetType, 100) }, 0.0)) }),
			MoveResult = new OperationResult(IsSuccess: true, "到达目标", "战斗-道中危机")
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);
		Assert.True((await InvokeNonBattleCheckAsync(operation)).IsSuccess);
		Assert.Equal<List<string>>(new List<string>(1) { targetType }, runtime.MoveTargetTypes);
		Assert.Equal(new List<bool>(1) { expectedStopWhenInteract }, runtime.MoveStopWhenInteract);
		Assert.Equal(new List<bool>(1) { expectedStopWhenDisappear }, runtime.MoveStopWhenDisappear);
		Assert.Equal(new List<bool>(1) { expectedAllowArrivalByInteractButton }, runtime.MoveAllowArrivalByInteractButton);
	}

	[Theory]
	[InlineData(true, true, true, "0000-感叹号")]
	[InlineData(false, true, true, "0001-距离")]
	[InlineData(false, false, true, "xxxx-入口")]
	public async Task RunLevel_NonBattleConsumesTargetsInPythonPriorityOrder(bool withInteract, bool withDistance, bool withEntry, string expectedTarget)
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		context.LostVoid.PriorityUpdated = true;
		List<YoloDetectObjectResult> results = new List<YoloDetectObjectResult>();
		if (withEntry)
		{
			results.Add(Object("xxxx-入口", 300));
		}
		if (withDistance)
		{
			results.Add(Object("0001-距离", 200));
		}
		if (withInteract)
		{
			results.Add(Object("0000-感叹号", 100));
		}
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new[] { new LostVoidRunLevelFrame(InNormalWorld: true, DetectResult: new YoloDetectFrameResult(results, 0.0)) }),
			MoveResult = new OperationResult(IsSuccess: true, "到达目标")
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);

		await InvokeNonBattleCheckAsync(operation);

		Assert.Equal(new[] { expectedTarget }, runtime.MoveTargetTypes);
	}

	[Fact]
	public async Task RunLevel_BossInteractPassesPythonAllowArrivalFlag()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		context.LostVoid.PriorityUpdated = true;
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[] { new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, BossBattleStarted: false, BossInteractAvailable: false, new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("0000-感叹号", 100) }, 0.0)) }),
			MoveResult = new OperationResult(IsSuccess: true, "到达目标")
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "战斗-终结之役", runtime);
		await InvokeNonBattleCheckAsync(operation);
		Assert.Equal(new List<bool>(1) { true }, runtime.MoveAllowArrivalByInteractButton);
	}

	[Fact]
	public void MoveByDetection_EscapeReDetectionKeepsDetectTransition()
	{
		MethodInfo turnAtFirst = typeof(LostVoidMoveByDetectionOperation).GetMethod("TurnAtFirst", BindingFlags.Instance | BindingFlags.NonPublic);
		IReadOnlyList<NodeFromAttribute> edges = turnAtFirst.GetCustomAttributes(typeof(NodeFromAttribute), inherit: false).OfType<NodeFromAttribute>().ToArray();
		Assert.Contains(edges, edge => edge.FromName == "脱困" && edge.Status == LostVoidMoveByDetectionOperation.StatusContinue);

		string sourcePath = Path.Combine(FindBusinessSourceRoot(), "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application.HollowZero.LostVoid", "LostVoidMoveByDetectionOperation.cs");
		string source = File.ReadAllText(sourcePath);
		int escapeStart = source.IndexOf("private OperationRoundResult GetOutOfStuck()", StringComparison.Ordinal);
		int detectRun = source.IndexOf("LostVoid.Detector?.Run", escapeStart, StringComparison.Ordinal);
		int detectFallback = source.IndexOf("return RoundSuccess(\"需要重新识别\")", detectRun, StringComparison.Ordinal);
		Assert.True(escapeStart >= 0 && detectRun > escapeStart && detectFallback > detectRun);
	}

	[Fact]
	public void ChooseCommon_NoDetailFallbackPrecedesGenericCandidateFallback()
	{
		string sourcePath = Path.Combine(FindBusinessSourceRoot(), "ZzzOd.GameLogic", "ZzzOd.GameLogic.Application.HollowZero.LostVoid", "LostVoidChooseCommonOperation.cs");
		string source = File.ReadAllText(sourcePath);
		int chooseArtifact = source.IndexOf("public OperationRoundResult ChooseArtifact()", StringComparison.Ordinal);
		int answerFallback = source.IndexOf("TryFillByAnswerFallback", chooseArtifact, StringComparison.Ordinal);
		int genericFallback = source.IndexOf("TryFillByCanChoose", chooseArtifact, StringComparison.Ordinal);
		Assert.True(chooseArtifact >= 0 && answerFallback > chooseArtifact && genericFallback > answerFallback);
		Assert.Contains("item.Artifact.Category == \"无详情\"", source, StringComparison.Ordinal);
	}

	[Fact]
	public async Task RunLevel_TargetMissingChecksCurrentFrameBattleBeforeTurning()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		context.LostVoid.PriorityUpdated = true;
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			CurrentFrameBattleEncounter = true,
			NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[] { new LostVoidRunLevelFrame(InNormalWorld: true) })
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);

		OperationRoundResult result = await InvokeNonBattleCheckAsync(operation);

		Assert.True(result.IsSuccess);
		Assert.Equal("进入战斗", result.Status);
		Assert.Equal(1, runtime.CurrentFrameBattleEncounterCheckCount);
		Assert.Equal(0, runtime.TurnToFindTargetCount);
		Assert.Equal(0, runtime.PeriodBattleEncounterCheckCount);
	}

	[Fact]
	public async Task RunLevel_TargetMissingKeepsPeriodBattleCheckAfterTurning()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		context.LostVoid.PriorityUpdated = true;
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[] { new LostVoidRunLevelFrame(InNormalWorld: true) })
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);

		OperationRoundResult result = await InvokeNonBattleCheckAsync(operation);

		Assert.Equal(OperationRoundResultKind.Wait, result.Kind);
		Assert.Equal("转动识别目标", result.Status);
		Assert.Equal(1, runtime.CurrentFrameBattleEncounterCheckCount);
		Assert.Equal(1, runtime.TurnToFindTargetCount);
		Assert.Equal(1, runtime.PeriodBattleEncounterCheckCount);
		Assert.Equal(0.5f, runtime.LastPeriodBattleCheckSeconds);
	}

	[Fact]
	public async Task RunLevel_NonBattleUsesCrossNodeOperationTimeoutAfterNormalWorldConfirmation()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[] { new LostVoidRunLevelFrame(InNormalWorld: true) })
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "战斗-终结之役", runtime);
		OperationRoundResult result = await InvokeNonBattleCheckAsync(operation, TimeSpan.FromSeconds(600L));
		Assert.True(result.IsFail);
		Assert.Equal("执行超时", result.Status);
		Assert.Equal(1, runtime.NonBattleWorldStateCallCount);
		Assert.Equal(0, runtime.NonBattleFrameCallCount);
		Assert.Empty(runtime.MoveTargetTypes);
	}

	[Fact]
	public void RunLevel_RecordsChallengeResultAndInteractTargetKey()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			LostVoidConfig config = new LostVoidConfig
			{
				ExtraTask = "刷满周期奖励"
			};
			LostVoidRunRecord lostVoidRunRecord = new LostVoidRunRecord(config);
			LostVoidRunLevel lostVoidRunLevel = new LostVoidRunLevel(context, lostVoidRunRecord, "挚交会谈", new ScriptedLostVoidRunLevelRuntime());
			lostVoidRunLevel.ApplyChallengeResultFinish(rewardEvalFound: false, rewardDnFound: true);
			Assert.True(lostVoidRunRecord.EvalPointComplete);
			Assert.False(lostVoidRunRecord.PeriodRewardComplete);
			lostVoidRunLevel.ApplyChallengeResultFinish(rewardEvalFound: true, rewardDnFound: false);
			Assert.False(lostVoidRunRecord.EvalPointComplete);
			Assert.False(lostVoidRunRecord.PeriodRewardComplete);
			Assert.Equal("感叹号:奥菲莉亚", lostVoidRunLevel.GetInteractTargetKey(new LostVoidInteractTarget("奥菲莉亚", "感叹号", isAgent: false, isNpc: true)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_LoadingChallengeConfirmStopsBeforeTalkInput()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidRuntimeOrderingScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			SequenceOcrMatcher matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[2]
			{
				new OcrMatchResult[] { Ocr("确认") },
				new OcrMatchResult[] { Ocr("代理人") }
			});
			context.OcrService.Matcher = matcher;
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口");
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			LostVoidRunLevelLoadingState state = await ScreenLostVoidRunLevelRuntime.Instance.GetLoadingStateAsync(operation, screen, DateTimeOffset.UtcNow, CancellationToken.None);
			Assert.True(state.ChallengeConfirmAvailable);
			Assert.Null(state.TalkStatus);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(1, matcher.OcrCallCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_NonBattleOutsideWorldStopsAfterChallengeConfirm()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidRuntimeOrderingScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			SequenceOcrMatcher matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[2]
			{
				new OcrMatchResult[] { Ocr("确认") },
				new OcrMatchResult[] { Ocr("战斗开始") }
			});
			context.OcrService.Matcher = matcher;
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口");
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			LostVoidRunLevelWorldState state = await ScreenLostVoidRunLevelRuntime.Instance.GetNonBattleWorldStateAsync(operation, screen, DateTimeOffset.UtcNow, CancellationToken.None);
			Assert.False(state.InNormalWorld);
			Assert.True(state.ChallengeConfirmAvailable);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(1, matcher.OcrCallCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_ChallengeResultStopsBeforeRewardNumbersAreTreatedAsTalk()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidRuntimeOrderingScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			SequenceOcrMatcher matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[2]
			{
				new OcrMatchResult[] { Ocr("挑战结果") },
				new OcrMatchResult[] { Ocr("500"), Ocr("4") }
			});
			context.OcrService.Matcher = matcher;
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "战斗-道中危机");
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);

			LostVoidInteractResult result = await ScreenLostVoidRunLevelRuntime.Instance.HandleInteractAsync(operation, null, screen, CancellationToken.None);

			Assert.Equal(LostVoidInteractResultKind.Success, result.Kind);
			Assert.Equal("迷失之地-挑战结果", result.Status);
			Assert.Equal(1, matcher.OcrCallCount);
			Assert.Equal(0, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_BattleStateDoesNotTreatTitleOnlyAsSelectionScreen()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidTitleOnlyScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[] { new OcrMatchResult[] { Ocr("鸣徽") } });
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈");
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3);
			Cv2.Randu(screen, Scalar.All(0.0), Scalar.All(255.0));
			DateTimeOffset frameTimeUtc = DateTimeOffset.UtcNow;
			operation.EnterBattle(frameTimeUtc - TimeSpan.FromSeconds(2L));
			LostVoidBattleState state = await ScreenLostVoidRunLevelRuntime.Instance.GetBattleStateAsync(operation, screen, frameTimeUtc, CancellationToken.None);
			Assert.False(state.CurrentFrameInBattle);
			Assert.True(state.FinishScreenChecked);
			Assert.False(state.InInteractScreen);
			Assert.False(state.BattleFailed);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_BattleStateChecksFinishScreenOnFirstNonBattleFrame()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidTitleOnlyScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[] { new OcrMatchResult[] { Ocr("鸣徽") } });
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈");
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3);
			Cv2.Randu(screen, Scalar.All(0.0), Scalar.All(255.0));
			DateTimeOffset frameTimeUtc = DateTimeOffset.UtcNow;

			Assert.Null(operation.LastCheckFinishTimeUtc);
			LostVoidBattleState state = await ScreenLostVoidRunLevelRuntime.Instance.GetBattleStateAsync(operation, screen, frameTimeUtc, CancellationToken.None);

			Assert.False(state.CurrentFrameInBattle);
			Assert.True(state.TransitionCheckPerformed);
			Assert.True(state.FinishScreenChecked);
			Assert.False(state.InInteractScreen);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_FinishChallengeResultAccumulatesRewardMarkersAcrossFramesBeforeSavingRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidBattleResultScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[6]
			{
				new OcrMatchResult[] { Ocr("零号业绩") },
				new OcrMatchResult[] { Ocr("丁尼") },
				new OcrMatchResult[] { Ocr("完成") },
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>()
			});
			context.ScreenContext.Reload();
			LostVoidRunRecord runRecord = new LostVoidRunRecord(new LostVoidConfig());
			LostVoidRunLevel operation = new LostVoidRunLevel(context, runRecord, "挚交会谈");
			OperationRoundResult waiting = await InvokeHandleChallengeResultFinishAsync(operation);
			OperationRoundResult result = await InvokeHandleChallengeResultFinishAsync(operation).WaitAsync(TimeSpan.FromSeconds(5L));
			Assert.Equal(OperationRoundResultKind.Wait, waiting.Kind);
			Assert.True(result.IsSuccess);
			Assert.Equal("通关", result.Status);
			Assert.False(runRecord.EvalPointComplete);
			Assert.False(runRecord.PeriodRewardComplete);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_FailedFinishAttemptKeepsRewardMarkersForNextSuccessfulRound()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidBattleResultScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[9]
			{
				new OcrMatchResult[] { Ocr("零号业绩") },
				new OcrMatchResult[] { Ocr("丁尼") },
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>(),
				new OcrMatchResult[] { Ocr("完成") },
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>()
			});
			context.ScreenContext.Reload();
			LostVoidRunRecord runRecord = new LostVoidRunRecord(new LostVoidConfig())
			{
				EvalPointComplete = true,
				PeriodRewardComplete = true
			};
			LostVoidRunLevel operation = new LostVoidRunLevel(context, runRecord, "挚交会谈");
			Assert.False((await InvokeHandleChallengeResultFinishAsync(operation)).IsSuccess);
			Assert.True(runRecord.EvalPointComplete);
			Assert.True(runRecord.PeriodRewardComplete);
			Assert.Equal(OperationRoundResultKind.Wait, (await InvokeHandleChallengeResultFinishAsync(operation)).Kind);
			OperationRoundResult completed = await InvokeHandleChallengeResultFinishAsync(operation);
			Assert.True(completed.IsSuccess);
			Assert.Equal("通关", completed.Status);
			Assert.False(runRecord.EvalPointComplete);
			Assert.False(runRecord.PeriodRewardComplete);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_TryInteractWithoutButtonKeepsOnlyPythonActionWaits()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			RecordingButtonController buttons = new RecordingButtonController();
			context.AttachController(new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(buttons), null, buttons, null, null, skipForegroundActivation: true));
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口");
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			LostVoidTryInteractResult result = await ScreenLostVoidRunLevelRuntime.Instance.TryInteractAsync(operation, null, Array.Empty<string>(), interactAttempted: false, screen, CancellationToken.None);
			Assert.Equal(LostVoidTryInteractKind.Retry, result.Kind);
			Assert.Equal("未发现交互按键", result.Status);
			Assert.Null(result.Delay);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_RestartForRetryAsync_RunsRestartInBattle()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteBattleMenuScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[6]
			{
				Array.Empty<OcrMatchResult>(),
				new OcrMatchResult[] { Ocr("退出战斗") },
				new OcrMatchResult[] { Ocr("重新开始") },
				new OcrMatchResult[] { Ocr("退出战斗确认") },
				new OcrMatchResult[] { Ocr("退出战斗确认") },
				Array.Empty<OcrMatchResult>()
			});
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈");
			ScreenLostVoidRunLevelRuntime runtime = new ScreenLostVoidRunLevelRuntime(TimeSpan.Zero, TimeSpan.Zero);
			OperationResult result = await runtime.RestartForRetryAsync(operation, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3L));
			Assert.True(result.IsSuccess, result.Status);
			Assert.Equal("按钮-退出战斗-确认", result.Status);
			Assert.True(controller.ClickCount >= 2);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_FailExitAsync_RunsExitInBattleUntilChallengeCompleteVisible()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteBattleMenuScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[7]
			{
				Array.Empty<OcrMatchResult>(),
				new OcrMatchResult[] { Ocr("退出战斗") },
				new OcrMatchResult[] { Ocr("退出战斗") },
				new OcrMatchResult[] { Ocr("退出战斗确认") },
				new OcrMatchResult[] { Ocr("退出战斗确认") },
				Array.Empty<OcrMatchResult>(),
				new OcrMatchResult[] { Ocr("完成") }
			});
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈");
			ScreenLostVoidRunLevelRuntime runtime = new ScreenLostVoidRunLevelRuntime(TimeSpan.Zero, TimeSpan.Zero);
			OperationResult result = await runtime.FailExitAsync(operation, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3L));
			Assert.True(result.IsSuccess, result.Status);
			Assert.Equal("按钮-完成", result.Status);
			Assert.True(controller.ClickCount >= 2);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_PushErrorAsync_RecordsPreviousNodeAndStatus()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidNormalWorldTabYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈");
			using Mat nodeScreen = controller.Screenshot().Screen;
			ScreenLostVoidRunLevelRuntime runtime = new ScreenLostVoidRunLevelRuntime(TimeSpan.Zero, TimeSpan.Zero);
			OperationResult result = await runtime.PushErrorAsync(operation, nodeScreen, "非战斗画面识别", "准备最终退出", CancellationToken.None);
			Assert.True(result.IsSuccess);
			Assert.Equal("非战斗画面识别: 准备最终退出", result.Status);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(2, controller.ScreenshotStage);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ScreenRunLevelRuntime_PushErrorAsync_WaitsBeforeAndAfterFailedTabClick()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidNormalWorldTabYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController
			{
				ClickResult = false
			};
			context.AttachController(controller);
			context.ScreenContext.Reload();
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈");
			using Mat nodeScreen = controller.Screenshot().Screen;
			ScreenLostVoidRunLevelRuntime runtime = new ScreenLostVoidRunLevelRuntime(TimeSpan.FromMilliseconds(50L), TimeSpan.FromMilliseconds(50L));
			Stopwatch stopwatch = Stopwatch.StartNew();
			OperationResult result = await runtime.PushErrorAsync(operation, nodeScreen, "非战斗画面识别", "准备最终退出", CancellationToken.None);
			stopwatch.Stop();
			Assert.False(result.IsSuccess);
			Assert.Contains("打开tab页面失败", result.Status);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(1, controller.ScreenshotStage);
			Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(80L), $"TAB 点击前后等待不足: {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_ExecutesEntryPathWithInjectedRuntime()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.LostVoid.PriorityUpdated = true;
			ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
			{
				NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[] { new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, BossBattleStarted: false, BossInteractAvailable: false, new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("战斗-鸣徽", 100) }, 0.0)) }),
				MoveResult = new OperationResult(IsSuccess: true, "移动完成", "战斗-鸣徽"),
				TryInteractResult = LostVoidTryInteractResult.Success("交互成功"),
				InteractResult = LostVoidInteractResult.Success("进入下层"),
				AfterInteractState = new LostVoidAfterInteractState(InNormalWorld: false)
			};
			LostVoidRunRecord runRecord = new LostVoidRunRecord(new LostVoidConfig());
			LostVoidRunLevel operation = new LostVoidRunLevel(context, runRecord, "入口", runtime);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(3L));
			Assert.True(result.IsSuccess);
			Assert.Equal("进入下层", result.Status);
			Assert.Equal("战斗-鸣徽", result.Data);
			Assert.Equal<List<string>>(new List<string>(1) { "xxxx-入口" }, runtime.MoveTargetTypes);
			Assert.Equal<List<string>>(new List<string>(1) { "入口" }, runtime.RegionTypesForMove);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_ExecuteAsync_TraversesCompleteSuccessGraphWithRecordedRuntimeCalls()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			GraphRecordingLostVoidRunLevelRuntime nextLevelRuntime = new GraphRecordingLostVoidRunLevelRuntime(GraphScenario.NextLevel);
			LostVoidRunLevel nextLevel = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", nextLevelRuntime);
			OperationResult nextLevelResult = await nextLevel.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10L));
			Assert.True(nextLevelResult.IsSuccess);
			Assert.Equal("进入下层", nextLevelResult.Status);
			Assert.Equal("战斗-道中危机", nextLevelResult.Data);
			Assert.Equal<List<string>>(new List<string>(12)
			{
				"等待加载", "确认大世界", "非战斗详细识别", "更新优先级", "追加代理人类型优先级", "确认大世界", "非战斗详细识别", "移动:xxxx-入口:True:False", "尝试交互", "交互处理:选择",
				"交互处理:选择完成", "交互后处理:下一层"
			}, nextLevelRuntime.Calls);
			WriteLostVoidBattleResultScreenYaml(rootDirectory);
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[6]
			{
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>(),
				new OcrMatchResult[] { Ocr("完成") },
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>(),
				Array.Empty<OcrMatchResult>()
			});
			context.ScreenContext.Reload();
			GraphRecordingLostVoidRunLevelRuntime completeRuntime = new GraphRecordingLostVoidRunLevelRuntime(GraphScenario.Complete);
			LostVoidRunLevel complete = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "战斗-道中危机", completeRuntime);
			OperationResult completeResult = await complete.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10L));
			Assert.True(completeResult.IsSuccess);
			Assert.Equal("通关", completeResult.Status);
			Assert.Equal("挚交会谈", completeResult.Data);
			Assert.Equal("等待加载", completeRuntime.Calls[0]);
			Assert.Equal("准备自动战斗", completeRuntime.Calls[1]);
			Assert.Equal(3, completeRuntime.Calls.Count((string call) => call == "战斗中"));
			Assert.Contains("停止自动战斗", (IEnumerable<string>)completeRuntime.Calls);
			Assert.Contains("交互处理:战后", (IEnumerable<string>)completeRuntime.Calls);
			Assert.Contains("交互后处理:挑战结果", (IEnumerable<string>)completeRuntime.Calls);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_ExecuteAsync_TraversesBattleFailureExitGraphWithRecordedRuntimeCalls()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidBattleResultScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[4]
			{
				new OcrMatchResult[] { Ocr("撤退") },
				Array.Empty<OcrMatchResult>(),
				new OcrMatchResult[] { Ocr("完成") },
				Array.Empty<OcrMatchResult>()
			});
			context.ScreenContext.Reload();
			GraphRecordingLostVoidRunLevelRuntime runtime = new GraphRecordingLostVoidRunLevelRuntime(GraphScenario.BattleFailure);
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "战斗-道中危机", runtime);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10L));
			Assert.True(result.IsSuccess);
			Assert.Equal("通关", result.Status);
			Assert.Equal("入口", result.Data);
			Assert.Equal("等待加载", runtime.Calls[0]);
			Assert.Equal("准备自动战斗", runtime.Calls[1]);
			Assert.Equal(3, runtime.Calls.Count((string call) => call == "战斗中"));
			Assert.Contains("停止自动战斗", (IEnumerable<string>)runtime.Calls);
			Assert.Equal(2, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_ExecuteAsync_NormalizesMoveNodeTimeoutAndTraversesFinalExitGraph()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidBattleResultScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[2]
			{
				new OcrMatchResult[] { Ocr("完成") },
				Array.Empty<OcrMatchResult>()
			});
			context.ScreenContext.Reload();
			context.LostVoid.PriorityUpdated = true;
			GraphRecordingLostVoidRunLevelRuntime runtime = new GraphRecordingLostVoidRunLevelRuntime(GraphScenario.NodeTimeoutFailure);
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10L));
			Assert.True(result.IsSuccess);
			Assert.Equal("通关", result.Status);
			Assert.Equal("入口", result.Data);
			Assert.Equal<List<string>>(new List<string>(7) { "等待加载", "确认大世界", "非战斗详细识别", "移动:0000-感叹号:True:False", "保存错误信息:执行超时", "停止自动战斗", "失败退出空洞" }, runtime.Calls);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_ExecuteAsync_OperationTimeoutSkipsDetailedDetectionAndTraversesFinalExitGraph()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteLostVoidBattleResultScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new SequenceOcrMatcher(new IReadOnlyList<OcrMatchResult>[2]
			{
				new OcrMatchResult[] { Ocr("完成") },
				Array.Empty<OcrMatchResult>()
			});
			context.ScreenContext.Reload();
			context.LostVoid.PriorityUpdated = true;
			GraphRecordingLostVoidRunLevelRuntime runtime = new GraphRecordingLostVoidRunLevelRuntime(GraphScenario.OperationTimeoutFailure);
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10L));
			Assert.True(result.IsSuccess);
			Assert.Equal("通关", result.Status);
			Assert.Equal("入口", result.Data);
			Assert.Equal<List<string>>(new List<string>(5) { "等待加载", "确认大世界", "保存错误信息:执行超时", "停止自动战斗", "失败退出空洞" }, runtime.Calls);
			Assert.DoesNotContain("非战斗详细识别", (IEnumerable<string>)runtime.Calls);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_AppendsAgentPriorityThenReturnsToNonBattleFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.LostVoid.PriorityUpdated = false;
			ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
			{
				NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new LostVoidRunLevelFrame[2]
				{
					new LostVoidRunLevelFrame(InNormalWorld: true),
					new LostVoidRunLevelFrame(InNormalWorld: true, ChallengeConfirmAvailable: false, BossBattleStarted: false, BossInteractAvailable: false, new YoloDetectFrameResult(new YoloDetectObjectResult[] { Object("战斗-鸣徽", 100) }, 0.0))
				}),
				MoveResult = new OperationResult(IsSuccess: true, "移动完成", "战斗-鸣徽"),
				TryInteractResult = LostVoidTryInteractResult.Success("交互成功"),
				InteractResult = LostVoidInteractResult.Success("进入下层"),
				AfterInteractState = new LostVoidAfterInteractState(InNormalWorld: false)
			};
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(3L));
			Assert.True(result.IsSuccess);
			Assert.Equal("进入下层", result.Status);
			Assert.True(context.LostVoid.PriorityUpdated);
			Assert.Equal(2, runtime.NonBattleFrameCallCount);
			Assert.Equal(1, runtime.AppendAgentTypePriorityCallCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_InBattleRequiresTenYoloExitFramesBeforeStoppingAutoBattle()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
			{
				BattleState = new LostVoidBattleState(CurrentFrameInBattle: true, NextRegionHint: false, NoLongerInBattleByDetection: true)
			};
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);
			for (int i = 0; i < 9; i++)
			{
				Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
				Assert.Equal(0, runtime.StopAutoBattleCount);
			}
			OperationRoundResult completed = await InvokeInBattleAsync(operation);
			Assert.True(completed.IsSuccess);
			Assert.Equal("识别需移动交互", completed.Status);
			Assert.Equal(1, runtime.StopAutoBattleCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_InBattleRequiresThreeInteractScreenFramesBeforeStoppingAutoBattle()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			BattleState = new LostVoidBattleState(CurrentFrameInBattle: false, InInteractScreen: true)
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);

		Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
		Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
		OperationRoundResult completed = await InvokeInBattleAsync(operation);

		Assert.True(completed.IsSuccess);
		Assert.Equal("识别正在交互", completed.Status);
		Assert.Equal(1, runtime.StopAutoBattleCount);
	}

	[Fact]
	public async Task RunLevel_InBattleYoloExitCountResetsOnCleanDetectionFrame()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		LostVoidBattleState yoloExit = new LostVoidBattleState(CurrentFrameInBattle: true, NoLongerInBattleByDetection: true);
		LostVoidBattleState clean = new LostVoidBattleState(CurrentFrameInBattle: true, NoLongerInBattleByDetection: false);
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			BattleStates = new Queue<LostVoidBattleState>(Enumerable.Repeat(yoloExit, 9).Append(clean).Concat(Enumerable.Repeat(yoloExit, 9)))
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);

		for (int i = 0; i < 19; i++)
		{
			Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
		}

		Assert.Equal(0, runtime.StopAutoBattleCount);
	}

	[Fact]
	public async Task RunLevel_NonBattleInteractCountResetsWhenScreenNoLongerMatches()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			BattleStates = new Queue<LostVoidBattleState>(new[]
			{
				new LostVoidBattleState(CurrentFrameInBattle: false, InInteractScreen: true),
				new LostVoidBattleState(CurrentFrameInBattle: false, InInteractScreen: true),
				new LostVoidBattleState(CurrentFrameInBattle: false, InInteractScreen: false),
				new LostVoidBattleState(CurrentFrameInBattle: false, InInteractScreen: true),
				new LostVoidBattleState(CurrentFrameInBattle: false, InInteractScreen: true)
			})
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);

		for (int i = 0; i < 5; i++)
		{
			Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
		}

		Assert.Equal(0, runtime.StopAutoBattleCount);
	}

	[Fact]
	public async Task RunLevel_NextRegionHintStopsAutoBattleImmediately()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			BattleState = new LostVoidBattleState(CurrentFrameInBattle: true, NextRegionHint: true, NoLongerInBattleByDetection: true)
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);

		OperationRoundResult result = await InvokeInBattleAsync(operation);

		Assert.True(result.IsSuccess);
		Assert.Equal("识别需移动交互", result.Status);
		Assert.Equal(1, runtime.StopAutoBattleCount);
	}

	[Fact]
	public async Task RunLevel_InBattleIgnoresInteractScreenSignalUntilBattleFrameEnds()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			BattleState = new LostVoidBattleState(CurrentFrameInBattle: true, NoLongerInBattleByDetection: false, InInteractScreen: true)
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);

		for (int i = 0; i < 3; i++)
		{
			Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
		}

		Assert.Equal(0, runtime.StopAutoBattleCount);
	}

	[Fact]
	public async Task RunLevel_FalseExitSequenceLogsStopPathfindingEnterBattleAndRestartInOrder()
	{
		RecordingLogSink sink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()), logger);
		context.LostVoid.PriorityUpdated = true;
		LostVoidBattleState yoloExit = new LostVoidBattleState(CurrentFrameInBattle: true, NoLongerInBattleByDetection: true);
		ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
		{
			BattleStates = new Queue<LostVoidBattleState>(Enumerable.Repeat(yoloExit, 10)),
			NonBattleFrames = new Queue<LostVoidRunLevelFrame>(new[]
			{
				new LostVoidRunLevelFrame(InNormalWorld: true, DetectResult: new YoloDetectFrameResult(new[] { Object("0000-感叹号", 100) }, 0.0)),
				new LostVoidRunLevelFrame(InNormalWorld: true)
			}),
			MoveResult = new OperationResult(IsSuccess: true, "到达目标"),
			CurrentFrameBattleEncounter = true
		};
		LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "入口", runtime);

		for (int i = 0; i < 9; i++)
		{
			Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
		}
		Assert.Equal("识别需移动交互", (await InvokeInBattleAsync(operation)).Status);
		Assert.Equal("0000-感叹号", (await InvokeNonBattleCheckAsync(operation)).Status);
		Assert.Equal("进入战斗", (await InvokeNonBattleCheckAsync(operation)).Status);
		Assert.True(InvokeInitAutoOp(operation).IsSuccess);

		Assert.Equal(new[] { "StopAutoBattle", "StartPathfinding:0000-感叹号", "StartAutoBattle" }, runtime.BattleLifecycleCalls);
		string[] messages = sink.Events.Select(entry => entry.RenderMessage()).ToArray();
		int stop = Array.FindIndex(messages, message => message.Contains("StopAutoBattleAndMove", StringComparison.Ordinal));
		int pathfinding = Array.FindIndex(messages, message => message.Contains("StartPathfinding", StringComparison.Ordinal));
		int enterBattle = Array.FindIndex(messages, message => message.Contains("EnterBattle", StringComparison.Ordinal));
		int start = Array.FindIndex(messages, enterBattle + 1, message => message.Contains("StartAutoBattle", StringComparison.Ordinal));
		Assert.True(stop >= 0 && pathfinding > stop && enterBattle > pathfinding && start > enterBattle);
		Assert.Contains(sink.Events, entry => entry.MessageTemplate.Text.StartsWith("迷失之地战斗状态转移:", StringComparison.Ordinal)
			&& entry.Properties.TryGetValue("Signal", out LogEventPropertyValue? signal) && signal.ToString().Contains("InBattleYolo", StringComparison.Ordinal)
			&& entry.Properties.TryGetValue("Threshold", out LogEventPropertyValue? threshold) && threshold.ToString() == "10");
	}

	[Fact]
	public async Task RunLevel_InBattleWaitsWhenRuntimeRejectsAnUnusableDetectionFrame()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
			{
				BattleState = new LostVoidBattleState(CurrentFrameInBattle: true, NextRegionHint: true, NoLongerInBattleByDetection: true, InInteractScreen: false, BattleFailed: false, TransitionCheckPerformed: false, DetectorChecked: true)
			};
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);
			Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
			Assert.Equal(0, runtime.StopAutoBattleCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RunLevel_InBattlePassesCurrentNodeFrameAndCaptureTimeToRuntime()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			ScriptedLostVoidRunLevelRuntime runtime = new ScriptedLostVoidRunLevelRuntime
			{
				BattleState = new LostVoidBattleState(CurrentFrameInBattle: true, NextRegionHint: false, NoLongerInBattleByDetection: false, InInteractScreen: false, BattleFailed: false, TransitionCheckPerformed: false)
			};
			LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈", runtime);
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			DateTimeOffset captureTimeUtc = new DateTimeOffset(2026, 7, 16, 14, 30, 0, TimeSpan.Zero);
			SetOperationScreenshot(operation, screen, captureTimeUtc);
			Assert.Equal(OperationRoundResultKind.Wait, (await InvokeInBattleAsync(operation)).Kind);
			Assert.Same(screen, runtime.LastBattleScreen);
			Assert.Equal(captureTimeUtc, runtime.LastBattleScreenshotTimeUtc);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void InBattleProbe_ThrottlesSchedulingAndKeepsSingleFlight()
	{
		List<Action> queued = new List<Action>();
		LostVoidInBattleProbe probe = new LostVoidInBattleProbe(TimeSpan.FromMilliseconds(800L), queued.Add);
		DateTimeOffset start = new DateTimeOffset(2026, 7, 26, 7, 39, 0, TimeSpan.Zero);
		LostVoidInBattleProbeResult Result(DateTimeOffset frameTime) => new LostVoidInBattleProbeResult(frameTime, NoLongerInBattleByDetection: false, NextRegionHint: false, "无", 1.0, DetectorRan: true);

		Assert.True(probe.TrySchedule(start, () => Result(start)));
		// 上一次探测仍在飞行：不重复调度
		Assert.False(probe.TrySchedule(start.AddSeconds(5L), () => Result(start.AddSeconds(5L))));
		queued[0]();
		queued.Clear();
		// 已完成但未到节流间隔：不调度
		Assert.False(probe.TrySchedule(start.AddMilliseconds(799L), () => Result(start.AddMilliseconds(799L))));
		Assert.Empty(queued);
		Assert.True(probe.TrySchedule(start.AddMilliseconds(800L), () => Result(start.AddMilliseconds(800L))));
		// 节流间隔可按区域覆盖（道中危机/终结之役 不受 0.8 秒限制）
		queued[0]();
		Assert.True(probe.TryConsume(out LostVoidInBattleProbeResult _));
		Assert.True(probe.TrySchedule(start.AddMilliseconds(801L), () => Result(start.AddMilliseconds(801L)), TimeSpan.Zero));
	}

	[Fact]
	public void InBattleProbe_ConsumesEachResultOnceAndDropsStaleFrames()
	{
		List<Action> queued = new List<Action>();
		LostVoidInBattleProbe probe = new LostVoidInBattleProbe(TimeSpan.Zero, queued.Add);
		DateTimeOffset start = new DateTimeOffset(2026, 7, 26, 7, 39, 0, TimeSpan.Zero);
		LostVoidInBattleProbeResult Result(DateTimeOffset frameTime, bool noLongerInBattle) => new LostVoidInBattleProbeResult(frameTime, noLongerInBattle, NextRegionHint: false, "无", 1.0, DetectorRan: true);

		Assert.False(probe.TryConsume(out LostVoidInBattleProbeResult _));
		Assert.True(probe.TrySchedule(start, () => Result(start, noLongerInBattle: true)));
		queued[0]();
		queued.Clear();
		Assert.True(probe.TryConsume(out LostVoidInBattleProbeResult first));
		Assert.True(first.NoLongerInBattleByDetection);
		Assert.Equal(start, first.FrameTimeUtc);
		// 同一结果不会被重复消费（脱战计数不会因重复读取加速）
		Assert.False(probe.TryConsume(out LostVoidInBattleProbeResult _));
		// 迟到的旧帧结果被丢弃
		Assert.True(probe.TrySchedule(start.AddSeconds(1L), () => Result(start.AddSeconds(-1L), noLongerInBattle: true)));
		queued[0]();
		queued.Clear();
		Assert.False(probe.TryConsume(out LostVoidInBattleProbeResult _));
		// 探测失败（返回 null）只解除飞行标记，不产生证据
		Assert.True(probe.TrySchedule(start.AddSeconds(2L), () => null));
		queued[0]();
		queued.Clear();
		Assert.False(probe.TryConsume(out LostVoidInBattleProbeResult _));
		// Reset 后旧的已消费帧时间不再抑制新结果
		probe.Reset();
		Assert.True(probe.TrySchedule(start.AddSeconds(-5L), () => Result(start.AddSeconds(-5L), noLongerInBattle: false)));
		queued[0]();
		Assert.True(probe.TryConsume(out LostVoidInBattleProbeResult afterReset));
		Assert.Equal(start.AddSeconds(-5L), afterReset.FrameTimeUtc);
	}

	[Fact]
	public async Task ScreenRuntime_LostVoidYoloPeriodSubmitsIndependentAutoBattleWorkersBeforeDetection()
	{
		RecordingLogSink sink = new RecordingLogSink();
		using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		ZContext context = new ZContext(new OneDragonEnvironment(FindRepoRoot()), logger);
		try
		{
			context.ScreenContext.Reload();
			Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
			try
			{
				PasteBattleTemplate(context, screen, "按键-普通攻击", "btn_normal_attack");
				ManualResetEventSlim detectorEntered = new ManualResetEventSlim();
				try
				{
					ManualResetEventSlim releaseDetector = new ManualResetEventSlim();
					try
					{
						ScreenLostVoidRunLevelRuntime runtime = new ScreenLostVoidRunLevelRuntime(delegate
						{
							detectorEntered.Set();
							Assert.True(context.AutoBattleContext.LastCheckInBattle);
							Assert.Contains((IEnumerable<LogEvent>)sink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text.Contains("自动战斗检测调度", StringComparison.Ordinal) && entry.Properties.TryGetValue("Source", out LogEventPropertyValue value) && value.ToString().Contains("lost_void", StringComparison.Ordinal) && entry.Properties.TryGetValue("SubmittedTaskCount", out LogEventPropertyValue value2) && value2.ToString() != "0"));
							Assert.True(releaseDetector.Wait(TimeSpan.FromSeconds(2L)));
							return new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0d, null, "battle-frame", LostVoidDetector.OverlaySourceBattle);
						});
						LostVoidRunLevel operation = new LostVoidRunLevel(context, new LostVoidRunRecord(new LostVoidConfig()), "挚交会谈");
						DateTimeOffset frameTime = DateTimeOffset.UtcNow;
						Assert.Null(operation.LastDetectTimeUtc);
						// 战斗轮不再阻塞在 YOLO 上：探测仍在跑的同时本轮已经返回
						Stopwatch roundWatch = Stopwatch.StartNew();
						LostVoidBattleState firstRound = await runtime.GetBattleStateAsync(operation, screen, frameTime, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
						roundWatch.Stop();
						Assert.True(detectorEntered.Wait(TimeSpan.FromSeconds(2L)));
						Assert.True(roundWatch.Elapsed < TimeSpan.FromSeconds(1L), $"战斗轮耗时 {roundWatch.ElapsedMilliseconds}ms 不应包含被阻塞的检测");
						Assert.True(firstRound.CurrentFrameInBattle);
						Assert.False(firstRound.DetectorChecked);
						Assert.False(firstRound.TransitionCheckPerformed);
						context.AutoBattleContext.StateRecordService.UpdateState(new StateRecord("自定义-迷失并发", (double)frameTime.ToUnixTimeMilliseconds() / 1000.0));
						Assert.Equal((double)frameTime.ToUnixTimeMilliseconds() / 1000.0, context.AutoBattleContext.StateRecordService.GetStateRecorder("自定义-迷失并发").LastRecordTime);
						releaseDetector.Set();
						// 探测完成后由后续轮次消费其结果
						LostVoidBattleState state = null;
						for (int round = 0; round < 40 && (state == null || !state.DetectorChecked); round++)
						{
							await Task.Delay(TimeSpan.FromMilliseconds(25L));
								state = await runtime.GetBattleStateAsync(operation, screen, frameTime.AddMilliseconds(50 + round * 25), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
						}
						Assert.NotNull(state);
						Assert.True(state.CurrentFrameInBattle);
						Assert.True(state.DetectorChecked);
						Assert.True(state.TransitionCheckPerformed);
						Assert.Contains((IEnumerable<LogEvent>)sink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text.StartsWith("迷失之地战斗检测:", StringComparison.Ordinal) && entry.Properties.ContainsKey("Region") && entry.Properties.ContainsKey("FrameTimeUtc") && entry.Properties.ContainsKey("Detect") && entry.Properties.ContainsKey("ElapsedMilliseconds") && entry.Properties.TryGetValue("FrameId", out LogEventPropertyValue frameId) && frameId.ToString().Contains("battle-frame", StringComparison.Ordinal) && entry.Properties.TryGetValue("OverlaySource", out LogEventPropertyValue source) && source.ToString().Contains(LostVoidDetector.OverlaySourceBattle, StringComparison.Ordinal)));
						Assert.Contains((IEnumerable<LogEvent>)sink.Events, (Predicate<LogEvent>)((LogEvent entry) => entry.MessageTemplate.Text.StartsWith("[.NET诊断] 迷失之地战斗状态:", StringComparison.Ordinal) && entry.Properties.ContainsKey("DetectorChecked") && entry.Properties.ContainsKey("NoLongerInBattleByDetection") && entry.Properties.ContainsKey("DetectorElapsedMilliseconds")));
					}
					finally
					{
						if (releaseDetector != null)
						{
							((IDisposable)releaseDetector).Dispose();
						}
					}
				}
				finally
				{
					if (detectorEntered != null)
					{
						((IDisposable)detectorEntered).Dispose();
					}
				}
			}
			finally
			{
				if (screen != null)
				{
					((IDisposable)screen).Dispose();
				}
			}
		}
		finally
		{
			if (context != null)
			{
				((IDisposable)context).Dispose();
			}
		}
	}

	[Fact]
	public async Task Runner_RepeatsNextLevelUntilCompleteAndResetsOpheliaFlag()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.LostVoid.HadInteractedOpheliaOnCurrentLevel = true;
			ScriptedLostVoidLevelExecutor executor = new ScriptedLostVoidLevelExecutor(new OperationResult[2]
			{
				new OperationResult(IsSuccess: true, "进入下层", "战斗-道中危机"),
				new OperationResult(IsSuccess: true, "通关", "挚交会谈")
			});
			LostVoidRunner runner = new LostVoidRunner(executor);
			OperationResult result = await runner.RunAsync(context, new LostVoidConfig(), new LostVoidRunRecord(new LostVoidConfig()), CancellationToken.None);
			Assert.True(result.IsSuccess);
			Assert.Equal("通关", result.Status);
			Assert.Equal<List<string>>(new List<string>(2) { "入口", "战斗-道中危机" }, executor.RegionTypes);
			Assert.False(context.LostVoid.HadInteractedOpheliaOnCurrentLevel);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static YoloDetectObjectResult Object(string className, int x, int y = 0, int width = 10, int height = 10)
	{
		return new YoloDetectObjectResult(new OpenCvSharp.Rect(x, y, width, height), 0.9, new YoloDetectClass(0, className));
	}

	private static LostVoidMoveTargetWrapper WrappedTarget(string className, int x, int y = 0, int width = 10, int height = 10)
	{
		return new LostVoidMoveTargetWrapper(Object(className, x, y, width, height));
	}

	private static void PasteBattleTemplate(ZContext context, Mat screen, string areaName, string templateId)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("战斗画面", areaName);
		using Mat mat = context.TemplateLoader.GetTemplate("battle", templateId).Raw.Clone();
		using Mat m = new Mat(screen, new OpenCvSharp.Rect(area.Rect.X1, area.Rect.Y1, mat.Width, mat.Height));
		mat.CopyTo(m);
	}

	/// <summary>
	/// 定位资源真源所在的工作区根目录：同时要求资源目录和业务子仓存在，避免锚定到只有同名子目录的上层。
	/// </summary>
	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string fullName = directoryInfo.FullName;
			if (Directory.Exists(Path.Combine(fullName, "assets", "template", "battle"))
				&& Directory.Exists(Path.Combine(fullName, "assets", "game_data", "screen_info"))
				&& Directory.Exists(Path.Combine(fullName, "zzzod-dotnet")))
			{
				return fullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 工作区根目录。");
	}

	/// <summary>
	/// 定位业务子仓的源码根目录。业务源码在 zzzod-dotnet 子仓内，不在工作区根下。
	/// </summary>
	private static string FindBusinessSourceRoot()
	{
		string sourceRoot = Path.Combine(FindRepoRoot(), "zzzod-dotnet", "src");
		if (!Directory.Exists(sourceRoot))
		{
			throw new DirectoryNotFoundException("未找到业务子仓源码目录 " + sourceRoot);
		}
		return sourceRoot;
	}

	private static string EntryLabel(string regionType)
	{
		return "xxxx-" + regionType;
	}

	private static LostVoidMoveTarget MoveTarget(string regionType, int x)
	{
		return new LostVoidMoveTarget(new string[] { regionType }, new OneDragon.Core.Abstractions.Geometry.Rect(x, 0, x + 40, 40));
	}

	private static LostVoidArtifactPos ArtifactPos(string category, string name, string level, int x, int y = 0, int width = 40, int height = 40, bool isPrimary = true)
	{
		return new LostVoidArtifactPos(new LostVoidArtifact
		{
			Category = category,
			Name = name,
			Level = level,
			IsGear = true
		}, new OneDragon.Core.Abstractions.Geometry.Rect(x, y, x + width, y + height), "", isPrimary);
	}

	private static OcrMatchResult Ocr(string text)
	{
		return new OcrMatchResult(0.99, 10, 10, 20, 10, text);
	}

	private static IReadOnlyList<string> NodeNames<T>()
	{
		return (from method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select method.GetCustomAttributes(typeof(OperationNodeAttribute), inherit: false).FirstOrDefault() as OperationNodeAttribute into attribute
			where attribute != null
			select attribute.Name).ToArray();
	}

	private static async Task<OperationRoundResult> InvokeInBattleAsync(LostVoidRunLevel operation)
	{
		MethodInfo method = typeof(LostVoidRunLevel).GetMethod("InBattleAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Task<OperationRoundResult> task = Assert.IsAssignableFrom<Task<OperationRoundResult>>(method.Invoke(operation, null));
		return await task;
	}

	private static OperationRoundResult InvokeInitAutoOp(LostVoidRunLevel operation)
	{
		MethodInfo method = typeof(LostVoidRunLevel).GetMethod("InitAutoOp", BindingFlags.Instance | BindingFlags.NonPublic);
		return Assert.IsType<OperationRoundResult>(method.Invoke(operation, null));
	}

	private static async Task<OperationRoundResult> InvokeNonBattleCheckAsync(LostVoidRunLevel operation, TimeSpan? elapsed = null)
	{
		SetOperationElapsed(operation, elapsed ?? TimeSpan.Zero);
		MethodInfo method = typeof(LostVoidRunLevel).GetMethod("NonBattleCheckAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Task<OperationRoundResult> task = Assert.IsAssignableFrom<Task<OperationRoundResult>>(method.Invoke(operation, null));
		return await task;
	}

	private static void SetOperationElapsed(LostVoidRunLevel operation, TimeSpan elapsed)
	{
		long num = Stopwatch.GetTimestamp() - (long)(elapsed.TotalSeconds * (double)Stopwatch.Frequency);
		typeof(Operation).GetField("_operationStartTimestamp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, num);
	}

	private static async Task<OperationRoundResult> InvokeHandleChallengeResultFinishAsync(LostVoidRunLevel operation)
	{
		SetOperationScreenshot(operation, new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black));
		MethodInfo method = typeof(LostVoidRunLevel).GetMethod("HandleChallengeResultFinishAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Task<OperationRoundResult> task = Assert.IsAssignableFrom<Task<OperationRoundResult>>(method.Invoke(operation, null));
		return await task;
	}

	private static void SetOperationScreenshot(LostVoidMoveByDetectionOperation operation, Mat screen)
	{
		Type typeFromHandle = typeof(ZOperation);
		typeFromHandle.GetField("<LastScreenshot>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, screen);
		typeFromHandle.GetField("<LastScreenshotTimeUtc>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, DateTimeOffset.UtcNow);
	}

	private static void SetOperationScreenshot(LostVoidRunLevel operation, Mat screen, DateTimeOffset? captureTimeUtc = null)
	{
		Type typeFromHandle = typeof(ZOperation);
		typeFromHandle.GetField("<LastScreenshot>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, screen);
		typeFromHandle.GetField("<LastScreenshotTimeUtc>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(operation, captureTimeUtc ?? DateTimeOffset.UtcNow);
	}

	private static OperationRoundResult InvokeMoveTowards(LostVoidMoveByDetectionOperation operation)
	{
		MethodInfo method = typeof(LostVoidMoveByDetectionOperation).GetMethod("MoveTowards", BindingFlags.Instance | BindingFlags.NonPublic);
		return Assert.IsType<OperationRoundResult>(method.Invoke(operation, null));
	}

	private static OperationRoundResult InvokeTurnAtFirst(LostVoidMoveByDetectionOperation operation)
	{
		MethodInfo method = typeof(LostVoidMoveByDetectionOperation).GetMethod("TurnAtFirst", BindingFlags.Instance | BindingFlags.NonPublic);
		return Assert.IsType<OperationRoundResult>(method.Invoke(operation, null));
	}

	private static OperationRoundResult InvokeHandleNoTarget(LostVoidMoveByDetectionOperation operation)
	{
		MethodInfo method = typeof(LostVoidMoveByDetectionOperation).GetMethod("HandleNoTarget", BindingFlags.Instance | BindingFlags.NonPublic);
		return Assert.IsType<OperationRoundResult>(method.Invoke(operation, null));
	}

	private static void SetPrivateField<T>(object target, string fieldName, T value)
	{
		target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
	}

	private static void WriteLostVoidData(string rootDirectory)
	{
		string[] buffer = new string[5];
		buffer[0] = rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "hollow_zero";
		buffer[4] = "lost_void";
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "lost_void_artifact_data.yml"), "- category: 通用\n  name: 喷水枪\n  level: A\n  is_gear: true\n  template_id: water_gun\n- category: 卡牌\n  name: 祝福卡\n  level: S\n  is_gear: false\n- category: 无详情\n  name: 临时说明\n  level: B\n  is_gear: false");
		File.WriteAllText(Path.Combine(text, "lost_void_investigation_strategy.yml"), "- strategy_name: 鸣徽狂热战略");
	}

	private static void WriteChallengeConfig(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "config", "lost_void_challenge");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "默认-成就模式.yml"), "predefined_team_idx: 3\nauto_battle: 自定义战斗\nregion_type_priority:\n  - 战斗-鸣徽\n  - 战斗-道中危机\n  - 邦布商店");
	}

	private static void WriteLostVoidBattleResultScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: lost_void_battle_result\n  screen_name: 迷失之地-挑战结果\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-确定\n      pc_rect: [10, 10, 80, 30]\n      text: 确定\n      lcs_percent: 0.5\n    - area_name: 按钮-完成\n      pc_rect: [10, 30, 80, 50]\n      text: 完成\n      lcs_percent: 0.5\n    - area_name: 奖励-零号业绩\n      pc_rect: [10, 50, 80, 70]\n      text: 零号业绩\n      lcs_percent: 0.5\n    - area_name: 奖励-丁尼\n      pc_rect: [10, 70, 80, 90]\n      text: 丁尼\n      lcs_percent: 0.5\n- screen_id: lost_void_battle_fail\n  screen_name: 迷失之地-战斗失败\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-撤退\n      pc_rect: [10, 10, 80, 30]\n      text: 撤退\n      lcs_percent: 0.5");
	}

	private static void WriteAutoBattleScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 按键-普通攻击\n      pc_rect: [0, 0, 100, 100]\n    - area_name: 距离显示区域\n      pc_rect: [0, 0, 100, 100]");
	}

	private static void WriteLostVoidRuntimeOrderingScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: lost_void_normal_world\n  screen_name: 迷失之地-大世界\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-挑战-确认\n      pc_rect: [10, 10, 80, 30]\n      text: 确认\n      lcs_percent: 0.5\n    - area_name: 区域-对话角色名称\n      pc_rect: [10, 40, 160, 70]\n    - area_name: 区域-文本提示\n      pc_rect: [10, 80, 200, 110]\n- screen_id: lost_void_battle_result\n  screen_name: 迷失之地-挑战结果\n  app_id: lost_void\n  area_list:\n    - area_name: 标题-挑战结果\n      id_mark: true\n      pc_rect: [10, 120, 200, 160]\n      text: 挑战结果\n      lcs_percent: 0.5");
	}

	private static void WriteLostVoidTitleOnlyScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 按键-普通攻击\n      pc_rect: [0, 0, 100, 100]\n    - area_name: 按键-交互\n      pc_rect: [0, 0, 100, 100]\n- screen_id: lost_void_common\n  screen_name: 迷失之地-通用选择\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-确定\n      id_mark: true\n      pc_rect: [0, 0, 100, 40]\n      text: 确定\n    - area_name: 区域-标题\n      pc_rect: [0, 40, 200, 80]\n- screen_id: lost_void_gear\n  screen_name: 迷失之地-武备选择\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-携带\n      id_mark: true\n      pc_rect: [0, 0, 100, 40]\n      text: 携带\n- screen_id: lost_void_result\n  screen_name: 迷失之地-挑战结果\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-完成\n      id_mark: true\n      pc_rect: [0, 0, 100, 40]\n      text: 完成\n- screen_id: lost_void_battle_fail\n  screen_name: 迷失之地-战斗失败\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-撤退\n      id_mark: true\n      pc_rect: [0, 0, 100, 40]\n      text: 撤退");
	}

	private static void WriteLostVoidCommonSelectionScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: lost_void_common\n  screen_name: 迷失之地-通用选择\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-确定\n      pc_rect: [0, 0, 100, 40]\n      text: 确定\n    - area_name: 区域-标题\n      pc_rect: [0, 40, 200, 80]\n    - area_name: 文本-详情\n      pc_rect: [0, 80, 200, 140]");
	}

	private static void WriteBattleMenuScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 菜单\n      pc_rect: [0, 0, 200, 40]\n- screen_id: battle_menu\n  screen_name: 战斗-菜单\n  area_list:\n    - area_name: 按钮-退出战斗\n      pc_rect: [0, 0, 200, 40]\n      text: 退出战斗\n      lcs_percent: 0.8\n    - area_name: 按钮-重新开始\n      pc_rect: [0, 0, 200, 40]\n      text: 重新开始\n      lcs_percent: 0.8\n    - area_name: 按钮-退出战斗-确认\n      pc_rect: [0, 0, 200, 40]\n      text: 退出战斗确认\n      lcs_percent: 0.8\n- screen_id: lost_void_battle_result\n  screen_name: 迷失之地-挑战结果\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-完成\n      pc_rect: [0, 0, 200, 40]\n      text: 完成\n      lcs_percent: 0.8");
	}

	private static void WriteLostVoidNormalWorldTabYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: lost_void_normal_world\n  screen_name: 迷失之地-大世界\n  app_id: lost_void\n  area_list:\n    - area_name: 迷失之地-TAB\n      pc_rect: [10, 10, 80, 40]");
	}

	private static void WriteLostVoidBattleFailScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: lost_void_battle_fail\n  screen_name: 迷失之地-战斗失败\n  app_id: lost_void\n  area_list:\n    - area_name: 按钮-撤退\n      pc_rect: [0, 0, 200, 40]\n      text: 撤退\n      lcs_percent: 0.8");
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
