using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Runtime;
using OneDragon.Core.Inference;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using Serilog;
using ZzzOd.GameLogic.AutoBattle.AtomicOp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public class AutoBattleContext : IRunParticipant
{
	private static readonly string[] DirectMlProvider = new string[1] { GpuExecutor.GetDirectMlProviderName() };

	private static readonly DedicatedTaskScheduler BattleStateCheckScheduler = new DedicatedTaskScheduler("zzz-battle-state", 16);

	private static readonly TaskFactory BattleStateCheckTaskFactory = new TaskFactory(BattleStateCheckScheduler);

	private readonly ZContext _ctx;

	private readonly object _lifecycleLock = new object();

	private RunSession? _registeredRunSession;

	private readonly object _checkChainLock = new object();

	private readonly object _checkQuickLock = new object();

	private readonly object _checkSwitchBackupLock = new object();

	private readonly object _checkEndLock = new object();

	private readonly object _checkDistanceLock = new object();

	private readonly AutoBattleSubmissionGate _agentSubmissionGate = new AutoBattleSubmissionGate();

	private readonly AutoBattleSubmissionGate _targetSubmissionGate = new AutoBattleSubmissionGate();

	private readonly AutoBattleSubmissionGate _quickAssistSubmissionGate = new AutoBattleSubmissionGate();

	private readonly AutoBattleSubmissionGate _switchBackupSubmissionGate = new AutoBattleSubmissionGate();

	private readonly AutoBattleSubmissionGate _distanceSubmissionGate = new AutoBattleSubmissionGate();

	private readonly AutoBattleSubmissionGate _chainSubmissionGate = new AutoBattleSubmissionGate();

	private readonly AutoBattleSubmissionGate _battleEndSubmissionGate = new AutoBattleSubmissionGate();

	private readonly Dictionary<string, AutoBattleOperator> _opCache = new Dictionary<string, AutoBattleOperator>(StringComparer.Ordinal);

	private OneDragon.Core.Screen.ScreenArea? _checkDistanceArea;

	private OneDragon.Core.Screen.ScreenArea? _normalAttackArea;

	private OneDragon.Core.Screen.ScreenArea? _specialAttackArea;

	private OneDragon.Core.Screen.ScreenArea? _ultimateArea;

	private OneDragon.Core.Screen.ScreenArea? _switchArea;

	private OneDragon.Core.Screen.ScreenArea? _switchBackupMarkArea;

	private OneDragon.Core.Screen.ScreenArea? _switchBackupGrayArea;

	private OneDragon.Core.Screen.ScreenArea? _chainLeftArea;

	private OneDragon.Core.Screen.ScreenArea? _chainRightArea;

	private OneDragon.Core.Screen.ScreenArea? _chainBarArea;

	private AutoBattleInterval _checkChainInterval = new AutoBattleInterval(1f, 1f);

	private AutoBattleInterval _checkQuickInterval = new AutoBattleInterval(0.5f, 0.5f);

	private float _checkSwitchBackupInterval = 1f;

	private AutoBattleInterval _checkEndInterval = new AutoBattleInterval(5f, 5f);

	private double _lastCheckChainTime;

	private double _lastCheckQuickTime;

	private double _lastCheckSwitchBackupTime;

	private double _lastCheckEndTime;

	private double _lastCheckDistanceTime;

	private double _lastChainFrontCorrectionTime;

	private long _lastBattleSchedulingDiagnosticAtMilliseconds;

	private long _lastBattleStateDiagnosticAtMilliseconds;

	private int _lastLoggedInBattleState = -1;

	private string? _lastCheckEndResult;

	private long _lastCheckEndResultScreenshotTimeMilliseconds;

	private bool _lastCheckInBattle;

	public ZContext ZContext => _ctx;

	public AutoBattleStateRecordService StateRecordService { get; }

	public AutoBattleAgentContext AgentContext { get; }

	public AutoBattleTargetContext TargetContext { get; }

	public AutoBattleDodgeContext DodgeContext { get; }

	public AutoBattleCustomContext CustomContext { get; }

	public AtomicOpFactory AtomicOpFactory { get; }

	public AutoBattleOperator? AutoOp { get; set; }

	public bool AutoUltimateEnabled { get; set; }

	/// <summary>
	/// 读取 Overlay 状态面板所需的自动战斗真实运行数据。
	/// </summary>
	public AutoBattleOverlayStatusSnapshot GetOverlayStatusSnapshot(DateTimeOffset? now = null)
	{
		AutoBattleOperator? autoOp = AutoOp;
		return AutoBattleOverlayStatusSnapshotFactory.Create(
			autoOp?.GetRuntimeSnapshot().IsRunning ?? false,
			AgentContext.Team.Snapshot(),
			StateRecordService.GetSnapshot(),
			LastCheckDistance,
			now ?? DateTimeOffset.UtcNow);
	}

	public string? LastCheckEndResult
	{
		get
		{
			return Volatile.Read(in _lastCheckEndResult);
		}
		set
		{
			Volatile.Write(ref _lastCheckEndResult, value);
			Interlocked.Exchange(ref _lastCheckEndResultScreenshotTimeMilliseconds, 0L);
		}
	}

	/// <summary>最近一次战斗结束结果所属截图时间。0 表示结果由外部写入，未附带截图时间。</summary>
	public double? LastCheckEndResultScreenshotTime
	{
		get
		{
			long num = Interlocked.Read(in _lastCheckEndResultScreenshotTimeMilliseconds);
			return (num == 0L) ? ((double?)null) : new double?((double)num / 1000.0);
		}
	}

	public bool LastCheckInBattle
	{
		get
		{
			return Volatile.Read(in _lastCheckInBattle);
		}
		private set
		{
			Volatile.Write(ref _lastCheckInBattle, value);
		}
	}

	public float LastCheckDistance => TargetContext.LastCheckDistance;

	public int WithoutDistanceTimes => TargetContext.WithoutDistanceTimes;

	public int WithDistanceTimes => TargetContext.WithDistanceTimes;

	public bool AfterAppShutdownCalled { get; private set; }

	public bool HasZPcController => _ctx.Controller is ZPcController;

	public bool IsRuntimeRunning { get; private set; }

	public string Name => "AutoBattleContext";

	private ZPcController Controller => (_ctx.Controller as ZPcController) ?? throw new InvalidOperationException("AutoBattle atomic operation requires ZPcController.");

	private OneDragon.Core.Screen.ScreenArea CheckDistanceArea => _checkDistanceArea ?? (_checkDistanceArea = RequireScreenArea("距离显示区域"));

	private OneDragon.Core.Screen.ScreenArea NormalAttackArea => _normalAttackArea ?? (_normalAttackArea = RequireScreenArea("按键-普通攻击"));

	private OneDragon.Core.Screen.ScreenArea SpecialAttackArea => _specialAttackArea ?? (_specialAttackArea = RequireScreenArea("按键-特殊攻击"));

	private OneDragon.Core.Screen.ScreenArea UltimateArea => _ultimateArea ?? (_ultimateArea = RequireScreenArea("按键-终结技"));

	private OneDragon.Core.Screen.ScreenArea SwitchArea => _switchArea ?? (_switchArea = RequireScreenArea("按键-切换角色"));

	private OneDragon.Core.Screen.ScreenArea SwitchBackupMarkArea => _switchBackupMarkArea ?? (_switchBackupMarkArea = RequireScreenArea("按键-切换后援标记"));

	private OneDragon.Core.Screen.ScreenArea SwitchBackupGrayArea => _switchBackupGrayArea ?? (_switchBackupGrayArea = RequireScreenArea("按键-切换后援灰度区域"));

	private OneDragon.Core.Screen.ScreenArea ChainLeftArea => _chainLeftArea ?? (_chainLeftArea = RequireScreenArea("连携技-1"));

	private OneDragon.Core.Screen.ScreenArea ChainRightArea => _chainRightArea ?? (_chainRightArea = RequireScreenArea("连携技-2"));

	private OneDragon.Core.Screen.ScreenArea ChainBarArea => _chainBarArea ?? (_chainBarArea = RequireScreenArea("连携条"));

	public AutoBattleContext(ZContext ctx)
	{
		_ctx = ctx;
		StateRecordService = new AutoBattleStateRecordService();
		AgentContext = new AutoBattleAgentContext(ctx);
		TargetContext = new AutoBattleTargetContext(ctx);
		DodgeContext = new AutoBattleDodgeContext(ctx);
		CustomContext = new AutoBattleCustomContext(this);
		AtomicOpFactory = new AtomicOpFactory(this);
	}

	public AutoBattleOperator InitAutoOp(string opName, string subDir = "auto_battle")
	{
		AutoOp = null;
		string key = subDir + "-" + opName;
		bool useMergedFile = _ctx.BattleAssistantConfig.UseMergedFile;
		if (useMergedFile && _opCache.TryGetValue(key, out AutoBattleOperator value))
		{
			AutoOp = value;
		}
		else
		{
			AutoBattleOperator autoBattleOperator = new AutoBattleOperator(this, subDir, opName, useMergedFile);
			var (flag, message) = autoBattleOperator.InitBeforeRunning();
			if (!flag)
			{
				throw new InvalidOperationException(message);
			}
			AutoOp = autoBattleOperator;
			if (useMergedFile)
			{
				_opCache[key] = autoBattleOperator;
			}
		}
		_checkChainInterval = AutoOp.CheckChainInterval;
		_checkQuickInterval = AutoOp.CheckQuickInterval;
		_checkSwitchBackupInterval = 1f;
		_checkEndInterval = AutoOp.CheckEndInterval;
		AgentContext.InitAutoOp(AutoOp);
		DodgeContext.InitAutoOp(AutoOp);
		TargetContext.InitAutoOp(AutoOp);
		TargetContext.ResetDistanceCheckInterval();
		return AutoOp;
	}

	public void StartAutoBattle()
	{
		if (AutoOp != null && TryRegisterCurrentRunSession())
		{
			AutoUltimateEnabled = true;
			InitBattleContext();
			AutoOp.StartRunningAsync();
			StartContextAsync(startOperator: false);
			ClearAllStates();
		}
	}

	public void ResumeAutoBattle()
	{
		if (AutoOp != null && TryRegisterCurrentRunSession())
		{
			AutoOp.StartRunningAsync();
			StartContextAsync(startOperator: false);
			ClearAllStates();
		}
	}

	public void StopAutoBattle()
	{
		AutoOp?.StopRunning();
		StopContext(stopOperator: false);
	}

	public void InitBattleContext()
	{
		AgentContext.InitBattleAgentContext();
		DodgeContext.InitBattleDodgeContext();
		ResetSecondarySubmissionDrops();
		_lastCheckChainTime = 0.0;
		_lastCheckQuickTime = 0.0;
		_lastCheckSwitchBackupTime = 0.0;
		_lastCheckEndTime = 0.0;
		_lastCheckDistanceTime = 0.0;
		_lastChainFrontCorrectionTime = 0.0;
		LastCheckEndResult = null;
		TargetContext.ResetBattleDistance();
	}

	public void InitScreenArea()
	{
		_checkDistanceArea = RequireScreenArea("距离显示区域");
		_normalAttackArea = RequireScreenArea("按键-普通攻击");
		_specialAttackArea = RequireScreenArea("按键-特殊攻击");
		_ultimateArea = RequireScreenArea("按键-终结技");
		_switchArea = RequireScreenArea("按键-切换角色");
		_switchBackupMarkArea = RequireScreenArea("按键-切换后援标记");
		_switchBackupGrayArea = RequireScreenArea("按键-切换后援灰度区域");
		_chainLeftArea = RequireScreenArea("连携技-1");
		_chainRightArea = RequireScreenArea("连携技-2");
		_chainBarArea = RequireScreenArea("连携条");
		AgentContext.InitScreenArea();
	}

	public void StartContextAsync(bool startOperator = true)
	{
		if (!TryRegisterCurrentRunSession())
		{
			return;
		}

		lock (_lifecycleLock)
		{
			if (!IsRuntimeRunning)
			{
				IsRuntimeRunning = true;
				DodgeContext.StartContextAsync();
				if (startOperator)
				{
					AutoOp?.StartRunningAsync();
				}
			}
		}
	}

	public void ResumeContextAsync()
	{
		if (!TryRegisterCurrentRunSession())
		{
			return;
		}

		AutoOp?.StartRunningAsync();
		StartContextAsync(startOperator: false);
		ClearAllStates();
	}

	public void ClearAllStates()
	{
		if (AutoOp == null)
		{
			return;
		}
		foreach (string usageState in AutoOp.UsageStates)
		{
			StateRecordService.GetStateRecorder(usageState)?.ResetToInitial();
		}
	}

	public void StopContext(bool stopOperator = true)
	{
		lock (_lifecycleLock)
		{
			IsRuntimeRunning = false;
		}
		DodgeContext.StopContext();
		if (stopOperator)
		{
			AutoOp?.StopRunning();
		}
		ReleaseKeysIfControllerReady();
	}

	public void RequestEmergencyStop(RunTerminationReason reason)
	{
		StopAutoBattle();
	}

	public Task ReleaseRunResourcesAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	private bool TryRegisterCurrentRunSession()
	{
		RunSession? runSession = _ctx.RunContext.ActiveRunSession;
		if (runSession is null)
		{
			return true;
		}

		lock (_lifecycleLock)
		{
			if (!runSession.IsActive)
			{
				return false;
			}

			if (!ReferenceEquals(_registeredRunSession, runSession))
			{
				_registeredRunSession = runSession;
				runSession.RegisterParticipant(this);
			}

			return runSession.IsActive;
		}
	}

	public void AfterAppShutdown()
	{
		StopAutoBattle();
		AgentContext.AfterAppShutdown();
		DodgeContext.AfterAppShutdown();
		TargetContext.AfterAppShutdown();
		AfterAppShutdownCalled = true;
	}

	public bool CheckBattleState(Mat? screen, DateTimeOffset? screenshotTimeUtc = null, bool checkBattleEndNormalResult = false, bool checkBattleEndHollowResult = false, bool checkBattleEndDefenseResult = false, bool checkDistance = false, bool sync = false, string source = "unknown")
	{
		double screenshotTime = (screenshotTimeUtc.HasValue ? ((double)screenshotTimeUtc.Value.ToUnixTimeMilliseconds() / 1000.0) : Now());
		return CheckBattleState(screen, screenshotTime, checkBattleEndNormalResult, checkBattleEndHollowResult, checkBattleEndDefenseResult, checkDistance, sync, source);
	}

	public bool CheckBattleState(Mat? screen, double screenshotTime, bool checkBattleEndNormalResult = false, bool checkBattleEndHollowResult = false, bool checkBattleEndDefenseResult = false, bool checkDistance = false, bool sync = false, string source = "unknown")
	{
		if (screen == null || screen.Empty())
		{
			Log.Warning("自动战斗截图无效，保持上次战斗状态: InBattle={InBattle}, Source={Source}", LastCheckInBattle, source);
			return LastCheckInBattle;
		}
		long timestamp = Stopwatch.GetTimestamp();
		long timestamp2 = Stopwatch.GetTimestamp();
		bool flag = IsNormalAttackButtonAvailable(screen);
		double totalMilliseconds = Stopwatch.GetElapsedTime(timestamp2).TotalMilliseconds;
		LastCheckInBattle = flag;
		List<Task> list = new List<Task>();
		Mat taskScreen = screen;
		Mat ownedAsyncScreen = null;
		double asyncFrameLeaseElapsedMilliseconds = 0.0;
		long timestamp3 = Stopwatch.GetTimestamp();
		if (flag)
		{
			Task<bool> task = Task.FromResult(result: false);
			if (DodgeContext.TryScheduleDodgeAudioCheck(out var audioRunGeneration))
			{
				long audioQueuedAt = Stopwatch.GetTimestamp();
				task = QueueBattleStateCheck(() => RunDodgeAudioCheck(screenshotTime, audioRunGeneration, Stopwatch.GetElapsedTime(audioQueuedAt).TotalMilliseconds, source));
				list.Add(task);
			}
			if (DodgeContext.TryScheduleDodgeFlashCheck(out var flashRunGeneration))
			{
				long flashQueuedAt = Stopwatch.GetTimestamp();
				Mat flashScreen = GetTaskScreen();
				Task<AutoBattleFlashCheckResult> task2 = (_ctx.ModelConfig.FlashClassifierGpu ? SubmitGpuDetection(() => RunDodgeFlashCheck(flashScreen, screenshotTime, flashRunGeneration, Stopwatch.GetElapsedTime(flashQueuedAt).TotalMilliseconds, source)) : QueueBattleStateCheck(() => RunDodgeFlashCheck(flashScreen, screenshotTime, flashRunGeneration, Stopwatch.GetElapsedTime(flashQueuedAt).TotalMilliseconds, source)));
				list.Add(task2);
				list.Add(ArbitrateDodgeAudioAsync(task2, task, screenshotTime, flashRunGeneration, source));
			}
			TryQueueSecondaryCheck(_agentSubmissionGate, "Agent", source, screenshotTime, GetTaskScreen, delegate(Mat screen2, double queueDelayMilliseconds)
			{
				AgentContext.CheckAgentRelated(screen2, screenshotTime, updateState: true, source, queueDelayMilliseconds);
			}, list);
			TryQueueSecondaryCheck(_targetSubmissionGate, "Target", source, screenshotTime, GetTaskScreen, delegate(Mat screen2, double queueDelayMilliseconds)
			{
				TargetContext.RunAllChecks(screen2, screenshotTime, updateState: true, source, queueDelayMilliseconds);
			}, list);
			TryQueueSecondaryCheck(_quickAssistSubmissionGate, "QuickAssist", source, screenshotTime, GetTaskScreen, delegate(Mat screen2, double queueDelayMilliseconds)
			{
				CheckQuickAssist(screen2, screenshotTime, updateState: true, source, queueDelayMilliseconds);
			}, list);
			TryQueueSecondaryCheck(_switchBackupSubmissionGate, "SwitchBackup", source, screenshotTime, GetTaskScreen, delegate(Mat screen2, double queueDelayMilliseconds)
			{
				CheckSwitchBackup(screen2, screenshotTime, updateState: true, source, queueDelayMilliseconds);
			}, list);
			if (checkDistance)
			{
				TryQueueSecondaryCheck(_distanceSubmissionGate, "Distance", source, screenshotTime, GetTaskScreen, delegate(Mat screen2, double queueDelayMilliseconds)
				{
					CheckBattleDistanceWithLock(screen2, screenshotTime, source, queueDelayMilliseconds);
				}, list, _ctx.ModelConfig.OcrUseGpu);
			}
		}
		else
		{
			TryQueueSecondaryCheck(_chainSubmissionGate, "Chain", source, screenshotTime, GetTaskScreen, delegate(Mat screen2, double queueDelayMilliseconds)
			{
				CheckChainAttack(screen2, screenshotTime, updateState: true, source, queueDelayMilliseconds);
			}, list);
			if (checkBattleEndNormalResult || checkBattleEndHollowResult || checkBattleEndDefenseResult)
			{
				TryQueueSecondaryCheck(_battleEndSubmissionGate, "BattleEnd", source, screenshotTime, GetTaskScreen, delegate(Mat screen2, double queueDelayMilliseconds)
				{
					CheckBattleEnd(screen2, screenshotTime, checkBattleEndNormalResult, checkBattleEndHollowResult, checkBattleEndDefenseResult, source, queueDelayMilliseconds);
				}, list, _ctx.ModelConfig.OcrUseGpu);
			}
		}
		double totalMilliseconds2 = Stopwatch.GetElapsedTime(timestamp3).TotalMilliseconds;
		foreach (Task item in list)
		{
			ObserveDetectionTask(item);
		}
		if (sync && list.Count > 0)
		{
			Task.WhenAll(list).GetAwaiter().GetResult();
		}
		else if (ownedAsyncScreen != null)
		{
			Task.WhenAll(list).ContinueWith(delegate
			{
				ownedAsyncScreen.Dispose();
			}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}
		LogBattleSchedulingDiagnostic(screenshotTime, sync, flag, list.Count, totalMilliseconds, asyncFrameLeaseElapsedMilliseconds, totalMilliseconds2, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds, source);
		LogBattleStateDiagnostic(screenshotTime, source, flag, sync, list.Count);
		return flag;
		Mat GetTaskScreen()
		{
			if (sync)
			{
				return screen;
			}
			if (ownedAsyncScreen == null)
			{
				long timestamp4 = Stopwatch.GetTimestamp();
				ownedAsyncScreen = CreateFrameLease(screen);
				asyncFrameLeaseElapsedMilliseconds = Stopwatch.GetElapsedTime(timestamp4).TotalMilliseconds;
			}
			taskScreen = ownedAsyncScreen;
			return taskScreen;
		}
	}

	internal static Mat CreateFrameLease(Mat screen)
	{
		ArgumentNullException.ThrowIfNull(screen, "screen");
		return new Mat(screen, new Rect(0, 0, screen.Width, screen.Height));
	}

	public bool IsNormalAttackButtonAvailable(Mat screen)
	{
		MatchResultList matchResultList = _ctx.TemplateMatcher.CropAndMatchTemplate(screen, NormalAttackArea.Rect, "battle", "btn_normal_attack", 0.9);
		return matchResultList.Max != null;
	}

	public IReadOnlyList<StateRecord> CheckQuickAssist(Mat screen, double screenshotTime, bool updateState = true, string source = "unknown", double queueDelayMilliseconds = 0.0)
	{
		if (!Monitor.TryEnter(_checkQuickLock))
		{
			return Array.Empty<StateRecord>();
		}
		try
		{
			if (screenshotTime - _lastCheckQuickTime < (double)_checkQuickInterval.NextValue())
			{
				return Array.Empty<StateRecord>();
			}
			_lastCheckQuickTime = screenshotTime;
			using Mat image = CvImageUtils.Crop(screen, SwitchArea.Rect);
			Agent agent = MatchQuickAssistAgentIn(
				image,
				TemplateMatchVisionContext.ForCrop(screen.Width, screen.Height, SwitchArea.X1, SwitchArea.Y1));
			if (agent == null)
			{
				return Array.Empty<StateRecord>();
			}
			List<StateRecord> list = new List<StateRecord>
			{
				new StateRecord("快速支援-" + agent.AgentName, screenshotTime),
				new StateRecord("快速支援-" + agent.AgentType.GetStringValue(), screenshotTime),
				new StateRecord(BattleStateEnum.StatusQuickAssistReady.GetDescription(), screenshotTime)
			};
			if (updateState)
			{
				StateRecordService.BatchUpdateStates(list);
			}
			return list;
		}
		catch (Exception exception)
		{
			ILogger logger = _ctx.Logger;
			double? queueDelayMilliseconds2 = queueDelayMilliseconds;
			AutoBattleDiagnosticLogger.LogFailure(logger, exception, "识别快速支援失败", "QuickAssist", source, screenshotTime, null, queueDelayMilliseconds2);
			return Array.Empty<StateRecord>();
		}
		finally
		{
			Monitor.Exit(_checkQuickLock);
		}
	}

	public IReadOnlyList<StateRecord> CheckSwitchBackup(Mat screen, double screenshotTime, bool updateState = true, string source = "unknown", double queueDelayMilliseconds = 0.0)
	{
		if (!Monitor.TryEnter(_checkSwitchBackupLock))
		{
			return Array.Empty<StateRecord>();
		}
		try
		{
			if (screenshotTime - _lastCheckSwitchBackupTime < (double)_checkSwitchBackupInterval)
			{
				return Array.Empty<StateRecord>();
			}
			_lastCheckSwitchBackupTime = screenshotTime;
			if (!IsSwitchBackupReady(screen))
			{
				return Array.Empty<StateRecord>();
			}
			StateRecord stateRecord = new StateRecord(BattleStateEnum.StatusSwitchBackupReady.GetDescription(), screenshotTime);
			if (updateState)
			{
				StateRecordService.UpdateState(stateRecord);
			}
			return new StateRecord[] { stateRecord };
		}
		catch (Exception exception)
		{
			ILogger logger = _ctx.Logger;
			double? queueDelayMilliseconds2 = queueDelayMilliseconds;
			AutoBattleDiagnosticLogger.LogFailure(logger, exception, "识别切换后援失败", "SwitchBackup", source, screenshotTime, null, queueDelayMilliseconds2);
			return Array.Empty<StateRecord>();
		}
		finally
		{
			Monitor.Exit(_checkSwitchBackupLock);
		}
	}

	public IReadOnlyList<StateRecord> CheckChainAttack(Mat screen, double screenshotTime, bool updateState = true, string source = "unknown", double queueDelayMilliseconds = 0.0)
	{
		if (!Monitor.TryEnter(_checkChainLock))
		{
			return Array.Empty<StateRecord>();
		}
		try
		{
			if (screenshotTime - _lastCheckChainTime < (double)_checkChainInterval.NextValue())
			{
				return Array.Empty<StateRecord>();
			}
			_lastCheckChainTime = screenshotTime;
			return CheckChainAttackCore(screen, screenshotTime, updateState);
		}
		catch (Exception exception)
		{
			ILogger logger = _ctx.Logger;
			double? queueDelayMilliseconds2 = queueDelayMilliseconds;
			AutoBattleDiagnosticLogger.LogFailure(logger, exception, "识别连携技出错", "Chain", source, screenshotTime, null, queueDelayMilliseconds2);
			return Array.Empty<StateRecord>();
		}
		finally
		{
			Monitor.Exit(_checkChainLock);
		}
	}

	public string? CheckBattleEnd(Mat screen, double screenshotTime, bool checkBattleEndNormalResult, bool checkBattleEndHollowResult, bool checkBattleEndDefenseResult, string source = "unknown", double queueDelayMilliseconds = 0.0)
	{
		if (!Monitor.TryEnter(_checkEndLock))
		{
			return null;
		}
		try
		{
			if (screenshotTime - _lastCheckEndTime < (double)_checkEndInterval.NextValue())
			{
				return null;
			}
			_lastCheckEndTime = screenshotTime;
			string lastCheckEndResult = LastCheckEndResult;
			string result = FindBattleEndResult(screen, checkBattleEndNormalResult, checkBattleEndHollowResult, checkBattleEndDefenseResult);
			SetLastCheckEndResult(result, screenshotTime);
			if (LastCheckEndResult != null && !string.Equals(lastCheckEndResult, LastCheckEndResult, StringComparison.Ordinal))
			{
				_ctx.Logger.Information("自动战斗战斗结束识别: Result={Result}, Source={Source}, ScreenshotTime={ScreenshotTime:F3}, Normal={Normal}, Hollow={Hollow}, Defense={Defense}", LastCheckEndResult, source, screenshotTime, checkBattleEndNormalResult, checkBattleEndHollowResult, checkBattleEndDefenseResult);
			}
			return LastCheckEndResult;
		}
		catch (Exception exception)
		{
			ILogger logger = _ctx.Logger;
			double? queueDelayMilliseconds2 = queueDelayMilliseconds;
			AutoBattleDiagnosticLogger.LogFailure(logger, exception, "识别战斗结束失败", "BattleEnd", source, screenshotTime, null, queueDelayMilliseconds2);
			return null;
		}
		finally
		{
			Monitor.Exit(_checkEndLock);
		}
	}

	public float? CheckBattleDistance(Mat screen, float? lastDistance = null)
	{
		IReadOnlyList<OcrMatchResult> ocrResultList = _ctx.OcrService.GetOcrResultList(screen, null, CheckDistanceArea.Rect);
		float? num = null;
		float? num2 = null;
		OcrMatchResult ocrMatchResult = null;
		foreach (OcrMatchResult item in ocrResultList)
		{
			int num3 = item.Text.LastIndexOf('m');
			if (num3 < 0)
			{
				continue;
			}
			string s = Regex.Replace(item.Text.Substring(0, num3), "[^\\d.]+", string.Empty);
			if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				continue;
			}
			num = result;
			if (ocrMatchResult == null)
			{
				ocrMatchResult = item;
				num2 = result;
				continue;
			}
			int num4 = _ctx.ProjectConfig.ScreenStandardWidth / 2;
			if (lastDistance.HasValue ? (Math.Abs(result - lastDistance.Value) < Math.Abs(num2.Value - lastDistance.Value)) : (Math.Abs(item.Center.X - num4) < Math.Abs(ocrMatchResult.Center.X - num4)))
			{
				ocrMatchResult = item;
				num2 = result;
			}
		}
		TargetContext.UpdateBattleDistance(num);
		return num;
	}

	public void RunNamedButtonAction(string actionName, bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		if (!ZzzAtomicButtonActions.IsKnownAction(actionName))
		{
			throw new ArgumentException("非法按键 " + actionName, "actionName");
		}
		if (!Controller.RunNamedAction(actionName, press, pressTime, release))
		{
			throw new ArgumentException("非法按键 " + actionName, "actionName");
		}
		StateRecordService.UpdateState(new StateRecord(BuildButtonStateName(actionName, press, release), Now()));
	}

	public void ExecuteButtonAction(string actionName, bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		switch (actionName)
		{
		case "按键-闪避":
			Dodge(press, pressTime, release);
			break;
		case "按键-切换角色-下一个":
			SwitchNext(press, pressTime, release);
			break;
		case "按键-切换角色-上一个":
			SwitchPrev(press, pressTime, release);
			break;
		case "按键-切换后援":
			SwitchBackup(press, pressTime, release);
			break;
		case "按键-普通攻击":
			NormalAttack(press, pressTime, release);
			break;
		case "按键-特殊攻击":
			SpecialAttack(press, pressTime, release);
			break;
		case "按键-终结技":
			Ultimate(press, pressTime, release);
			break;
		case "按键-连携技-左":
			ChainLeft(press, pressTime, release);
			break;
		case "按键-连携技-右":
			ChainRight(press, pressTime, release);
			break;
		case "按键-连携技-取消":
			ChainCancel(press, pressTime, release);
			break;
		case "按键-移动-前":
			MoveW(press, pressTime, release);
			break;
		case "按键-移动-后":
			MoveS(press, pressTime, release);
			break;
		case "按键-移动-左":
			MoveA(press, pressTime, release);
			break;
		case "按键-移动-右":
			MoveD(press, pressTime, release);
			break;
		case "按键-锁定敌人":
			Lock(press, pressTime, release);
			break;
		default:
			throw new ArgumentException("非法按键 " + actionName, "actionName");
		}
	}

	public void Dodge(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnDodge.GetDescription(), press, pressTime, release);
	}

	public void SwitchNext(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		bool flag = press || !release;
		if (flag && !release)
		{
			ReleaseKeys();
		}
		double updateTime = Now();
		Controller.SwitchNext(press, pressTime, release);
		double triggerTime = Now();
		List<StateRecord> list = new List<StateRecord>
		{
			new StateRecord(BuildButtonStateName(BattleStateEnum.BtnSwitchNext.GetDescription(), press, release), triggerTime)
		};
		if (flag)
		{
			list.AddRange(AgentContext.SwitchNextAgent(updateTime, updateState: false, checkAgent: true));
		}
		StateRecordService.BatchUpdateStates(list);
	}

	public void SwitchPrev(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		bool flag = press || !release;
		if (flag && !release)
		{
			ReleaseKeys();
		}
		double updateTime = Now();
		Controller.SwitchPrev(press, pressTime, release);
		double triggerTime = Now();
		List<StateRecord> list = new List<StateRecord>
		{
			new StateRecord(BuildButtonStateName(BattleStateEnum.BtnSwitchPrev.GetDescription(), press, release), triggerTime)
		};
		if (flag)
		{
			list.AddRange(AgentContext.SwitchPrevAgent(updateTime, updateState: false, checkAgent: true));
		}
		StateRecordService.BatchUpdateStates(list);
	}

	public void SwitchBackup(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnSwitchBackup.GetDescription(), press, pressTime, release);
	}

	public void NormalAttack(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnSwitchNormalAttack.GetDescription(), press, pressTime, release);
	}

	public void SpecialAttack(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnSwitchSpecialAttack.GetDescription(), press, pressTime, release);
	}

	public void Ultimate(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnUltimate.GetDescription(), press, pressTime, release);
	}

	public void ChainLeft(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		bool flag = press || !release;
		double updateTime = Now();
		Controller.ChainLeft(press, pressTime, release);
		double triggerTime = Now();
		List<StateRecord> list = new List<StateRecord>
		{
			new StateRecord(BuildButtonStateName(BattleStateEnum.BtnChainLeft.GetDescription(), press, release), triggerTime)
		};
		if (flag)
		{
			list.AddRange(AgentContext.ChainLeft(updateTime, updateState: false));
		}
		StateRecordService.BatchUpdateStates(list);
	}

	public void ChainRight(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		bool flag = press || !release;
		double updateTime = Now();
		Controller.ChainRight(press, pressTime, release);
		double triggerTime = Now();
		List<StateRecord> list = new List<StateRecord>
		{
			new StateRecord(BuildButtonStateName(BattleStateEnum.BtnChainRight.GetDescription(), press, release), triggerTime)
		};
		if (flag)
		{
			list.AddRange(AgentContext.ChainRight(updateTime, updateState: false));
		}
		StateRecordService.BatchUpdateStates(list);
	}

	public void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnMoveW.GetDescription(), press, pressTime, release);
	}

	public void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnMoveS.GetDescription(), press, pressTime, release);
	}

	public void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnMoveA.GetDescription(), press, pressTime, release);
	}

	public void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnMoveD.GetDescription(), press, pressTime, release);
	}

	public void Lock(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnLock.GetDescription(), press, pressTime, release);
	}

	public void ChainCancel(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		RunNamedButtonAction(BattleStateEnum.BtnChainCancel.GetDescription(), press, pressTime, release);
	}

	public void SetCustomState(IReadOnlyList<string> stateNames, double timeDiff, double timeDiffAdd, int? value, int? valueAdd)
	{
		double num = Now();
		List<StateRecord> list = new List<StateRecord>();
		foreach (string stateName in stateNames)
		{
			list.Add(new StateRecord(stateName, num + timeDiff, value, valueAdd, (timeDiffAdd == 0.0) ? ((double?)null) : new double?(timeDiffAdd)));
		}
		StateRecordService.BatchUpdateStates(list);
	}

	public void ClearCustomState(IReadOnlyList<string> stateNames)
	{
		StateRecordService.BatchUpdateStates(stateNames.Select((string stateName) => new StateRecord(stateName, 0.0, null, null, null, isClear: true)).ToList());
	}

	public void TurnByDistance(float distance)
	{
		Controller.TurnByDistance(distance);
	}

	public void QuickAssist()
	{
		double updateTime = Now();
		(int Position, List<StateRecord> Records) tuple = AgentContext.SwitchQuickAssist(updateTime, updateState: false);
		int item = tuple.Position;
		List<StateRecord> item2 = tuple.Records;
		string text = null;
		switch (item)
		{
		case 2:
			Controller.SwitchNext();
			text = BattleStateEnum.BtnSwitchNext.GetDescription();
			break;
		case 3:
			Controller.SwitchPrev();
			text = BattleStateEnum.BtnSwitchPrev.GetDescription();
			break;
		}
		if (text != null)
		{
			item2.Add(new StateRecord(text, Now()));
			StateRecordService.BatchUpdateStates(item2);
		}
	}

	public void SwitchByName(string agentName)
	{
		double updateTime = Now();
		(int Position, List<StateRecord> Records) tuple = AgentContext.SwitchByAgentName(agentName, updateTime, updateState: false);
		int item = tuple.Position;
		List<StateRecord> item2 = tuple.Records;
		string text = null;
		switch (item)
		{
		case 2:
			Controller.SwitchNext();
			text = BattleStateEnum.BtnSwitchNext.GetDescription();
			break;
		case 3:
			Controller.SwitchPrev();
			text = BattleStateEnum.BtnSwitchPrev.GetDescription();
			break;
		}
		if (text != null)
		{
			item2.Add(new StateRecord(text, Now()));
			StateRecordService.BatchUpdateStates(item2);
		}
	}

	private void ReleaseKeysIfControllerReady()
	{
		if (_ctx.Controller is ZPcController)
		{
			ReleaseKeys();
			SwitchNext(press: false, null, release: true);
			SwitchPrev(press: false, null, release: true);
			SwitchBackup(press: false, null, release: true);
			Lock(press: false, null, release: true);
			Ultimate(press: false, null, release: true);
			ChainCancel(press: false, null, release: true);
			ChainLeft(press: false, null, release: true);
			ChainRight(press: false, null, release: true);
		}
	}

	private void ReleaseKeys()
	{
		RunNamedButtonAction(BattleStateEnum.BtnDodge.GetDescription(), press: false, null, release: true);
		RunNamedButtonAction(BattleStateEnum.BtnSwitchNormalAttack.GetDescription(), press: false, null, release: true);
		RunNamedButtonAction(BattleStateEnum.BtnSwitchSpecialAttack.GetDescription(), press: false, null, release: true);
		RunNamedButtonAction(BattleStateEnum.BtnMoveW.GetDescription(), press: false, null, release: true);
		RunNamedButtonAction(BattleStateEnum.BtnMoveS.GetDescription(), press: false, null, release: true);
		RunNamedButtonAction(BattleStateEnum.BtnMoveA.GetDescription(), press: false, null, release: true);
		RunNamedButtonAction(BattleStateEnum.BtnMoveD.GetDescription(), press: false, null, release: true);
	}

	private static string BuildButtonStateName(string actionName, bool press, bool release)
	{
		if (press)
		{
			return actionName + "-按下";
		}
		if (release)
		{
			return actionName + "-松开";
		}
		return actionName;
	}

	private static double Now()
	{
		return (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
	}

	private IReadOnlyList<StateRecord> CheckChainAttackCore(Mat screen, double screenshotTime, bool updateState)
	{
		OneDragon.Core.Screen.ScreenArea chainLeftArea = ChainLeftArea;
		OneDragon.Core.Screen.ScreenArea chainRightArea = ChainRightArea;
		Mat chain1 = CvImageUtils.Crop(screen, chainLeftArea.Rect);
		try
		{
			Mat chain2 = CvImageUtils.Crop(screen, chainRightArea.Rect);
			try
			{
				TemplateMatchVisionContext leftVisionContext = TemplateMatchVisionContext.ForCrop(screen.Width, screen.Height, chainLeftArea.X1, chainLeftArea.Y1);
				TemplateMatchVisionContext rightVisionContext = TemplateMatchVisionContext.ForCrop(screen.Width, screen.Height, chainRightArea.X1, chainRightArea.Y1);
				Task<Agent> task = QueueBattleStateCheck(() => MatchChainAgentIn(chain1, leftVisionContext));
				Task<Agent> task2 = QueueBattleStateCheck(() => MatchChainAgentIn(chain2, rightVisionContext));
				Mat chainBarScreen = screen.Clone();
				Task task3 = QueueBattleStateCheck(delegate
				{
					using (chainBarScreen)
					{
						CheckChainBar(chainBarScreen, screenshotTime);
					}
				});
				ObserveDetectionTask(task3);
				Agent[] array = new Agent[2]
				{
					GetChainAgentResult(task),
					GetChainAgentResult(task2)
				};
				List<StateRecord> list = new List<StateRecord>();
				HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
				for (int num = 0; num < array.Length; num++)
				{
					Agent agent = array[num];
					if (agent != null)
					{
						list.Add(new StateRecord($"连携技-{num + 1}-{agent.AgentName}", screenshotTime));
						list.Add(new StateRecord($"连携技-{num + 1}-{agent.AgentType.GetStringValue()}", screenshotTime));
						hashSet.Add(agent.AgentName);
					}
				}
				if (list.Count > 0)
				{
					for (int num2 = 0; num2 < array.Length; num2++)
					{
						if (array[num2] == null)
						{
							list.Add(new StateRecord($"连携技-{num2 + 1}-邦布", screenshotTime));
						}
					}
					list.Add(new StateRecord(BattleStateEnum.StatusChainReady.GetDescription(), screenshotTime));
				}
				if (updateState && list.Count > 0)
				{
					StateRecordService.BatchUpdateStates(list);
					CorrectFrontAgentForChainIfNeeded(screenshotTime, hashSet);
				}
				return list;
			}
			finally
			{
				if (chain2 != null)
				{
					((IDisposable)chain2).Dispose();
				}
			}
		}
		finally
		{
			if (chain1 != null)
			{
				((IDisposable)chain1).Dispose();
			}
		}
	}

	private static Agent? GetChainAgentResult(Task<Agent?> task)
	{
		try
		{
			return task.GetAwaiter().GetResult();
		}
		catch (Exception exception)
		{
			Log.Error(exception, "识别连携技角色头像失败");
			return null;
		}
	}

	private void CheckChainBar(Mat screen, double screenshotTime)
	{
		try
		{
			if (IsChainBarReady(screen))
			{
				StateRecordService.UpdateState(new StateRecord(BattleStateEnum.StatusChainReady.GetDescription(), screenshotTime));
			}
		}
		catch (Exception exception)
		{
			Log.Error(exception, "检测连携条轮廓失败");
		}
	}

	private Agent? MatchQuickAssistAgentIn(Mat image, TemplateMatchVisionContext visionContext)
	{
		return MatchAgentByBattleAvatar(image, "avatar_quick_", visionContext);
	}

	private Agent? MatchChainAgentIn(Mat image, TemplateMatchVisionContext visionContext)
	{
		return MatchAgentByBattleAvatar(image, "avatar_chain_", visionContext);
	}

	private Agent? MatchAgentByBattleAvatar(Mat image, string prefix, TemplateMatchVisionContext visionContext)
	{
		foreach (var (agent, text) in AgentContext.GetPossibleAgentList())
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				MatchResultList matchResultList = _ctx.TemplateMatcher.MatchTemplate(
					image,
					"battle",
					prefix + text,
					"raw",
					0.8,
					visionContext: visionContext);
				if (matchResultList.Max != null)
				{
					return agent;
				}
			}
			foreach (string templateId in agent.TemplateIdList)
			{
				if (!string.Equals(templateId, text, StringComparison.Ordinal))
				{
					MatchResultList matchResultList2 = _ctx.TemplateMatcher.MatchTemplate(
						image,
						"battle",
						prefix + templateId,
						"raw",
						0.8,
						visionContext: visionContext);
					if (matchResultList2.Max != null)
					{
						return agent;
					}
				}
			}
		}
		return null;
	}

	private bool IsSwitchBackupReady(Mat screen)
	{
		using Mat part = CvImageUtils.Crop(screen, SwitchBackupMarkArea.Rect);
		using Mat part2 = CvImageUtils.Crop(screen, SwitchBackupGrayArea.Rect);
		return IsSwitchBackupMarkBlack(part) && IsSwitchBackupGrayAreaColorful(part2);
	}

	private static bool IsSwitchBackupMarkBlack(Mat part)
	{
		if (part.Empty())
		{
			return false;
		}
		using Mat mat = new Mat();
		Cv2.CvtColor(part, mat, ColorConversionCodes.RGB2HSV);
		int num2 = mat.Rows * mat.Cols;
		using Mat blackMask = new Mat();
		Cv2.InRange(mat, new Scalar(0.0, 0.0, 0.0), new Scalar(179.0, 10.0, 20.0), blackMask);
		int num = Cv2.CountNonZero(blackMask);
		return num2 > 0 && (double)num / (double)num2 >= 0.9;
	}

	private static bool IsSwitchBackupGrayAreaColorful(Mat part)
	{
		if (part.Empty())
		{
			return false;
		}
		using Mat mat = new Mat();
		Cv2.CvtColor(part, mat, ColorConversionCodes.RGB2HSV);
		int num3 = mat.Rows * mat.Cols;
		using Mat colorfulMask = new Mat();
		using Mat grayishMask = new Mat();
		Cv2.InRange(mat, new Scalar(0.0, 40.0, 90.0), new Scalar(179.0, 255.0, 255.0), colorfulMask);
		Cv2.InRange(mat, new Scalar(0.0, 0.0, 130.0), new Scalar(179.0, 10.0, 170.0), grayishMask);
		int num = Cv2.CountNonZero(colorfulMask);
		int num2 = Cv2.CountNonZero(grayishMask);
		if (num3 == 0 || (double)num / (double)num3 < 0.5)
		{
			return false;
		}
		return (double)num2 / (double)num3 <= 0.2;
	}

	private bool IsChainBarReady(Mat screen)
	{
		using Mat mat = CvImageUtils.Crop(screen, ChainBarArea.Rect);
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.RGB2HSV);
		using Mat mat3 = new Mat();
		Cv2.InRange(mat2, new Scalar(5.0, 173.0, 227.0), new Scalar(25.0, 193.0, 247.0), mat3);
		Cv2.FindContours(mat3, out Point[][] contours, out HierarchyIndex[] _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
		return contours.Any(delegate(Point[] contour)
		{
			double num = Cv2.ArcLength(contour, closed: true);
			return num >= 500.0 && num <= 10000.0;
		});
	}

	private void CorrectFrontAgentForChainIfNeeded(double screenshotTime, HashSet<string> chainAgentNames)
	{
		if (chainAgentNames.Count == 0 || screenshotTime - _lastChainFrontCorrectionTime < 1.0)
		{
			return;
		}
		_lastChainFrontCorrectionTime = screenshotTime;
		string text = AgentEnum.Values.Select((AgentEnum agentEnum) => agentEnum.Value.AgentName).FirstOrDefault(delegate(string name)
		{
			StateRecorder? stateRecorder = StateRecordService.GetStateRecorder("前台-" + name);
			return stateRecorder != null && stateRecorder.LastRecordTime > 0.0;
		});
		if (text == null || !chainAgentNames.Contains(text))
		{
			return;
		}
		foreach (AgentInfo item in AgentContext.Team.Snapshot().Skip(1))
		{
			string text2 = item.Agent?.AgentName;
			if (text2 != null && !chainAgentNames.Contains(text2))
			{
				SwitchByName(text2);
				break;
			}
		}
	}

	private string? FindBattleEndResult(Mat screen, bool checkBattleEndNormalResult, bool checkBattleEndHollowResult, bool checkBattleEndDefenseResult)
	{
		if (checkBattleEndHollowResult)
		{
			if (FindArea(screen, "零号空洞-战斗", "挑战结果"))
			{
				return "零号空洞-挑战结果";
			}
			if (FindArea(screen, "零号空洞-事件", "背包"))
			{
				return "零号空洞-背包";
			}
			if (FindArea(screen, "零号空洞-战斗", "鸣徽-确定"))
			{
				return "鸣徽-确定";
			}
			if (FindArea(screen, "零号空洞-战斗", "结算周期上限-确认"))
			{
				return "零号空洞-结算周期上限";
			}
		}
		if (checkBattleEndDefenseResult)
		{
			if (FindArea(screen, "式舆防卫战", "战斗结束-退出"))
			{
				return "战斗结束-退出";
			}
			if (FindArea(screen, "式舆防卫战", "战斗结束-撤退"))
			{
				return "战斗结束-撤退";
			}
		}
		if (checkBattleEndNormalResult)
		{
			if (FindArea(screen, "战斗画面", "战斗结果-完成"))
			{
				return "普通战斗-完成";
			}
			if (FindArea(screen, "战斗画面", "战斗结果-撤退"))
			{
				return "普通战斗-撤退";
			}
		}
		return null;
	}

	private bool FindArea(Mat screen, string screenName, string areaName)
	{
		return ScreenUtils.FindArea(_ctx, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	private OneDragon.Core.Screen.ScreenArea RequireScreenArea(string areaName)
	{
		return _ctx.ScreenContext.GetArea("战斗画面", areaName) ?? throw new InvalidOperationException("缺少自动战斗识别区域：战斗画面/" + areaName);
	}

	private Task SubmitGpuDetection(Action action)
	{
		return _ctx.GpuExecutor.RunAsync((Func<CancellationToken, Task>)delegate
		{
			action();
			return Task.CompletedTask;
		}, (IEnumerable<string>?)DirectMlProvider, default(CancellationToken));
	}

	private Task<T> SubmitGpuDetection<T>(Func<T> action)
	{
		return _ctx.GpuExecutor.RunAsync((CancellationToken _) => Task.FromResult(action()), DirectMlProvider);
	}

	private AutoBattleFlashCheckResult RunDodgeFlashCheck(Mat screen, double screenshotTime, long runGeneration, double queueDelayMilliseconds, string source)
	{
		try
		{
			return DodgeContext.CheckDodgeFlashVisual(screen, screenshotTime, runGeneration, queueDelayMilliseconds, source);
		}
		finally
		{
			DodgeContext.CompleteDodgeFlashCheck();
		}
	}

	private async Task ArbitrateDodgeAudioAsync(Task<AutoBattleFlashCheckResult> flashTask, Task<bool> audioTask, double screenshotTime, long runGeneration, string source)
	{
		try
		{
			if ((await flashTask.ConfigureAwait(continueOnCapturedContext: false)).ShouldConsumeAudio)
			{
				bool audioDetected = await audioTask.ConfigureAwait(continueOnCapturedContext: false);
				DodgeContext.PublishDodgeAudioResult(audioDetected, screenshotTime, runGeneration);
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			AutoBattleDiagnosticLogger.LogFailure(_ctx.Logger, ex2, "自动战斗闪避结果仲裁失败", "DodgeArbitration", source, screenshotTime, runGeneration);
		}
	}

	private bool RunDodgeAudioCheck(double screenshotTime, long runGeneration, double queueDelayMilliseconds, string source)
	{
		try
		{
			return DodgeContext.CheckDodgeAudio(screenshotTime, runGeneration, queueDelayMilliseconds, source);
		}
		finally
		{
			DodgeContext.CompleteDodgeAudioCheck();
		}
	}

	private static Task QueueBattleStateCheck(Action action)
	{
		return BattleStateCheckTaskFactory.StartNew(action);
	}

	private static Task<T> QueueBattleStateCheck<T>(Func<T> action)
	{
		return BattleStateCheckTaskFactory.StartNew(action);
	}

	private void TryQueueSecondaryCheck(AutoBattleSubmissionGate gate, string detector, string source, double screenshotTime, Func<Mat> getTaskScreen, Action<Mat, double> action, List<Task> tasks, bool useGpu = false)
	{
		if (!gate.TryEnter())
		{
			return;
		}
		try
		{
			Mat taskScreen = getTaskScreen();
			long queuedAt = Stopwatch.GetTimestamp();
			Task item = (useGpu ? SubmitGpuDetection(delegate
			{
				RunSecondaryCheck(gate, delegate(double queueDelayMilliseconds)
				{
					action(taskScreen, queueDelayMilliseconds);
				}, detector, source, screenshotTime, queuedAt);
			}) : QueueBattleStateCheck(delegate
			{
				RunSecondaryCheck(gate, delegate(double queueDelayMilliseconds)
				{
					action(taskScreen, queueDelayMilliseconds);
				}, detector, source, screenshotTime, queuedAt);
			}));
			tasks.Add(item);
		}
		catch (Exception exception)
		{
			gate.Exit();
			AutoBattleDiagnosticLogger.LogFailure(_ctx.Logger, exception, "自动战斗二级检测提交失败", detector, source, screenshotTime);
			throw;
		}
	}

	private void RunSecondaryCheck(AutoBattleSubmissionGate gate, Action<double> action, string detector, string source, double screenshotTime, long queuedAt)
	{
		try
		{
			action(Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds);
		}
		catch (Exception exception)
		{
			ILogger logger = _ctx.Logger;
			double? queueDelayMilliseconds = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds;
			AutoBattleDiagnosticLogger.LogFailure(logger, exception, "自动战斗二级检测失败", detector, source, screenshotTime, null, queueDelayMilliseconds);
			throw;
		}
		finally
		{
			gate.Exit();
		}
	}

	private static void ObserveDetectionTask(Task task)
	{
		task.ContinueWith(delegate(Task completed)
		{
			Log.Error(completed.Exception, "自动战斗检测失败");
		}, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
	}

	/// <summary>
	/// 获取 CaptureAgeMilliseconds 所依据的时间戳来源。
	/// </summary>
	/// <returns>Frame 表示画面成帧时刻，WallClock 表示取图请求发起时刻，Unknown 表示控制器不可用。</returns>
	/// <remarks>
	/// 两种来源口径不同：成帧时刻反映画面产生的时间，请求时刻会把已经产生一段时间的画面记成刚拍到的。
	/// 比较不同来源的记录前必须先对齐口径。
	/// </remarks>
	private string GetCaptureTimeSource()
	{
		if (_ctx.Controller == null)
		{
			return "Unknown";
		}

		return _ctx.Controller.LastCaptureTimeFromFrame ? "Frame" : "WallClock";
	}

	private void LogBattleSchedulingDiagnostic(double screenshotTime, bool sync, bool inBattle, int taskCount, double inBattleProbeElapsedMilliseconds, double asyncFrameLeaseElapsedMilliseconds, double submitElapsedMilliseconds, double totalElapsedMilliseconds, string source)
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long num2 = Interlocked.Read(in _lastBattleSchedulingDiagnosticAtMilliseconds);
		if (num - num2 >= 1000 && Interlocked.CompareExchange(ref _lastBattleSchedulingDiagnosticAtMilliseconds, num, num2) == num2)
		{
			_ctx.Logger.Information("[.NET诊断] 自动战斗检测调度: Source={Source}, Sync={Sync}, InBattle={InBattle}, SubmittedTaskCount={SubmittedTaskCount}, CaptureAgeMilliseconds={CaptureAgeMilliseconds}, CaptureTimeSource={CaptureTimeSource}, BeforeScreenshotElapsedMilliseconds={BeforeScreenshotElapsedMilliseconds:F2}, CaptureElapsedMilliseconds={CaptureElapsedMilliseconds:F2}, UidMaskElapsedMilliseconds={UidMaskElapsedMilliseconds:F2}, InBattleProbeElapsedMilliseconds={InBattleProbeElapsedMilliseconds:F2}, AsyncFrameLeaseElapsedMilliseconds={AsyncFrameLeaseElapsedMilliseconds:F2}, SubmitElapsedMilliseconds={SubmitElapsedMilliseconds:F2}, TotalElapsedMilliseconds={TotalElapsedMilliseconds:F2}, SecondaryDroppedBecauseBusy=Agent={AgentDropped},Target={TargetDropped},QuickAssist={QuickAssistDropped},SwitchBackup={SwitchBackupDropped},Distance={DistanceDropped},Chain={ChainDropped},BattleEnd={BattleEndDropped}", source, sync, inBattle, taskCount, num - (long)(screenshotTime * 1000.0), GetCaptureTimeSource(), _ctx.Controller?.LastBeforeScreenshotElapsedMilliseconds ?? 0.0, _ctx.Controller?.LastCaptureElapsedMilliseconds ?? 0.0, _ctx.Controller?.LastUidMaskElapsedMilliseconds ?? 0.0, inBattleProbeElapsedMilliseconds, asyncFrameLeaseElapsedMilliseconds, submitElapsedMilliseconds, totalElapsedMilliseconds, _agentSubmissionGate.ConsumeDropped(), _targetSubmissionGate.ConsumeDropped(), _quickAssistSubmissionGate.ConsumeDropped(), _switchBackupSubmissionGate.ConsumeDropped(), _distanceSubmissionGate.ConsumeDropped(), _chainSubmissionGate.ConsumeDropped(), _battleEndSubmissionGate.ConsumeDropped());
		}
	}

	private void LogBattleStateDiagnostic(double screenshotTime, string source, bool inBattle, bool sync, int taskCount)
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		int num2 = (inBattle ? 1 : 0);
		int num3 = Interlocked.Exchange(ref _lastLoggedInBattleState, num2);
		long num4 = Interlocked.Read(in _lastBattleStateDiagnosticAtMilliseconds);
		if (num3 != num2 || num - num4 >= 1000)
		{
			Interlocked.Exchange(ref _lastBattleStateDiagnosticAtMilliseconds, num);
			_ctx.Logger.Information("自动战斗战斗状态: Source={Source}, InBattle={InBattle}, PreviousInBattle={PreviousInBattle}, EndResult={EndResult}, EndResultScreenshotTime={EndResultScreenshotTime}, EndResultMatchesCurrentFrame={EndResultMatchesCurrentFrame}, Sync={Sync}, SubmittedTaskCount={SubmittedTaskCount}, CaptureAgeMilliseconds={CaptureAgeMilliseconds}", source, inBattle, (num3 < 0) ? "无" : ((num3 == 1) ? "是" : "否"), LastCheckEndResult ?? "无", LastCheckEndResultScreenshotTime, LastCheckEndResultScreenshotTime.HasValue && Math.Abs(LastCheckEndResultScreenshotTime.Value - screenshotTime) < 0.001, sync, taskCount, num - (long)(screenshotTime * 1000.0));
		}
	}

	private void ResetSecondarySubmissionDrops()
	{
		_agentSubmissionGate.ResetDropped();
		_targetSubmissionGate.ResetDropped();
		_quickAssistSubmissionGate.ResetDropped();
		_switchBackupSubmissionGate.ResetDropped();
		_distanceSubmissionGate.ResetDropped();
		_chainSubmissionGate.ResetDropped();
		_battleEndSubmissionGate.ResetDropped();
	}

	private void SetLastCheckEndResult(string? result, double screenshotTime)
	{
		Volatile.Write(ref _lastCheckEndResult, result);
		Interlocked.Exchange(ref _lastCheckEndResultScreenshotTimeMilliseconds, (long)Math.Round(screenshotTime * 1000.0));
	}

	private void CheckBattleDistanceWithLock(Mat screen, double screenshotTime, string source = "unknown", double queueDelayMilliseconds = 0.0)
	{
		if (!Monitor.TryEnter(_checkDistanceLock))
		{
			return;
		}
		try
		{
			if (!(screenshotTime - _lastCheckDistanceTime < TargetContext.CheckDistanceInterval))
			{
				_lastCheckDistanceTime = screenshotTime;
				CheckBattleDistance(screen);
			}
		}
		catch (Exception exception)
		{
			ILogger logger = _ctx.Logger;
			double? queueDelayMilliseconds2 = queueDelayMilliseconds;
			AutoBattleDiagnosticLogger.LogFailure(logger, exception, "识别距离失败", "Distance", source, screenshotTime, null, queueDelayMilliseconds2);
		}
		finally
		{
			Monitor.Exit(_checkDistanceLock);
		}
	}
}
