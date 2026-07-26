using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidMoveByDetectionOperation : ZOperation
{
	public const string StatusInBattle = "遭遇战斗";

	public const string StatusArrival = "到达目标";

	public const string StatusNoFound = "未识别到目标";

	public const string StatusContinue = "继续识别目标";

	public const string StatusInteract = "处于交互中";

	public const string StatusNeedDetect = "需要重新识别";

	private readonly string _currentRegion;

	private readonly string _targetType;

	private readonly bool _stopWhenInteract;

	private readonly bool _stopWhenDisappear;

	private readonly IReadOnlyList<string> _ignoreEntryList;

	private readonly bool _allowArrivalByInteractButton;

	private readonly LostVoidMoveByDetectionService _service;

	private LostVoidMoveTargetWrapper? _lastTarget;

	private string? _lastTargetName;

	private readonly List<LostVoidMoveTargetWrapper> _lastVisibleTargets = new List<LostVoidMoveTargetWrapper>();

	private DateTimeOffset? _targetLostAtUtc;

	private DateTimeOffset? _lastAttackButtonCheckUtc;

	private double _sameTargetTimes;

	private int _stuckTimes;

	private int _lostTargetDuringMoveTimes;

	private int _noTargetHandleTimes;

	private int _totalTurnTimes;

	private bool _preferLeftEscape = true;

	private bool _waitingNoTargetScreenshot;

	private bool _noTargetDiagnosticWritten;

	private bool _escapeDiagnosticWritten;

	private int? _lastTargetX;

	private int _lastActualTurnDistance;

	private double _estimatedTurnRatio = 0.2;

	private int _turnCalibrationCount = 1;

	private int _turnSettleFrames;

	internal Func<Mat, bool>? IsInNormalWorldOverride { get; set; }

	internal Func<YoloDetectFrameResult>? DetectFrameOverride { get; set; }

	public LostVoidMoveByDetectionOperation(ZContext context, string currentRegion, string targetType, bool stopWhenInteract = true, bool stopWhenDisappear = true, IReadOnlyList<string>? ignoreEntryList = null, bool allowArrivalByInteractButton = false, LostVoidMoveByDetectionService? service = null)
		: base(context, "迷失之地-识别寻路-" + ((targetType.Length > 5) ? targetType.Substring(5) : targetType), 3, 180.0)
	{
		_currentRegion = currentRegion;
		_targetType = targetType;
		_stopWhenInteract = stopWhenInteract;
		_stopWhenDisappear = stopWhenDisappear;
		_ignoreEntryList = ignoreEntryList ?? Array.Empty<string>();
		_allowArrivalByInteractButton = allowArrivalByInteractButton;
		_service = service ?? LostVoidMoveByDetectionService.Instance;
	}

	[NodeFrom("脱困", Status = "继续识别目标")]
	[NodeFrom("无目标处理", Status = "继续识别目标")]
	[OperationNode("移动前转向", IsStartNode = true, NodeMaxRetryTimes = 20)]
	private OperationRoundResult TurnAtFirst()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图");
		}
		if (!(IsInNormalWorldOverride?.Invoke(base.LastScreenshot) ?? _service.IsInNormalWorld(base.ZContext, base.LastScreenshot)))
		{
			return _service.IsChoosingRewardScreen(base.ZContext, base.LastScreenshot) ? RoundSuccess("处于交互中") : RoundRetry("未在大世界画面");
		}
		YoloDetectFrameResult frameResult = RunDetection(base.LastScreenshot);
		LogDetectionSummary("移动前转向", frameResult);
		if (ShouldStopForInteraction(frameResult))
		{
			StopMovingForward();
			return RoundSuccess("到达目标", _lastTargetName);
		}
		LostVoidMoveTargetWrapper moveTarget = _service.GetMoveTarget(base.ZContext.LostVoid, _currentRegion, _targetType, frameResult, _lastTarget, _ignoreEntryList);
		if (moveTarget == null)
		{
			if (_targetType == "xxxx-入口")
			{
				string text = FirstDetectedClass(frameResult, "0000-感叹号", "0001-距离");
				if (text != null)
				{
					LogHigherPriorityFallback("移动前转向", text);
					return RoundSuccess("需要重新识别");
				}
			}
			LogNoTrackableTarget("移动前转向", frameResult);
			ResetTurnCalibration();
			ResetStuckStatus();
			if (_lastTarget != null)
			{
				OperationRoundResult result = HandleLostTargetDuringDetection(5);
				_lastTarget = null;
				return result;
			}
			_lastTarget = null;
			return RoundSuccess("未识别到目标");
		}
		_noTargetHandleTimes = 0;
		_lastTarget = moveTarget;
		_lastTargetName = moveTarget.LeftestTargetName;
		if (TurnToTarget(moveTarget.EntireRect.Center, isMoving: false))
		{
			return RoundWait("转动朝向目标", null, TimeSpan.FromMilliseconds(500L));
		}
		AutoBattleUtils.SwitchToBestAgentForMoving(base.ZContext);
		return RoundSuccess("开始移动");
	}

	[NodeFrom("移动前转向", Status = "开始移动")]
	[OperationNode("移动")]
	private OperationRoundResult MoveTowards()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图");
		}
		YoloDetectFrameResult frameResult = RunDetection(base.LastScreenshot);
		LogDetectionSummary("移动", frameResult);
		if (ShouldStopForInteraction(frameResult))
		{
			StopMovingForward();
			return RoundSuccess("到达目标", _lastTargetName, TimeSpan.FromMilliseconds(500L));
		}
		LostVoidMoveTargetWrapper moveTarget = _service.GetMoveTarget(base.ZContext.LostVoid, _currentRegion, _targetType, frameResult, _lastTarget, _ignoreEntryList);
		if (moveTarget == null)
		{
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			DateTimeOffset valueOrDefault = _targetLostAtUtc.GetValueOrDefault();
			if (!_targetLostAtUtc.HasValue)
			{
				valueOrDefault = utcNow;
				_targetLostAtUtc = valueOrDefault;
			}
			valueOrDefault = utcNow;
			DateTimeOffset? targetLostAtUtc = _targetLostAtUtc;
			if (valueOrDefault - targetLostAtUtc < TimeSpan.FromSeconds(1L))
			{
				StopMovingForward();
				// 对应 lost_void_move_by_det.py:283 的 wait_round_time=0.1（补足制，非固定延时）。
				return RoundWait("短暂丢失目标", null, null, TimeSpan.FromMilliseconds(100L));
			}
			ResetStuckStatus();
			if (_targetType == "xxxx-入口")
			{
				string text = FirstDetectedClass(frameResult, "0001-距离", "0000-感叹号");
				if (text != null)
				{
					LogHigherPriorityFallback("移动", text);
					return RoundSuccess("需要重新识别");
				}
			}
			LogNoTrackableTarget("移动", frameResult);
			return HandleLostTargetDuringDetection(10);
		}
		_targetLostAtUtc = null;
		_noTargetHandleTimes = 0;
		OperationRoundResult operationRoundResult = CheckStuck(frameResult, moveTarget);
		if (operationRoundResult != null)
		{
			return operationRoundResult;
		}
		_lastTarget = moveTarget;
		_lastTargetName = moveTarget.LeftestTargetName;
		TurnToTarget(moveTarget.EntireRect.Center, isMoving: true);
		base.ZContext.AutoBattleContext.MoveW(press: true);
		// 对应 lost_void_move_by_det.py:322 的 wait_round_time=0.1（补足制，非固定延时）。
		return RoundWait("移动中", null, null, TimeSpan.FromMilliseconds(100L));
	}

	[NodeFrom("移动前转向", Status = "未识别到目标")]
	[NodeFrom("移动", Status = "未识别到目标")]
	[OperationNode("无目标处理")]
	private OperationRoundResult HandleNoTarget()
	{
		StopMovingForward();
		if (!_waitingNoTargetScreenshot)
		{
			_waitingNoTargetScreenshot = true;
			return RoundWait("重新截图", null, TimeSpan.FromMilliseconds(500L));
		}
		_waitingNoTargetScreenshot = false;
		ResetTurnCalibration();
		_noTargetHandleTimes++;
		if (_stopWhenInteract && AutoBattleUtils.CheckBattleEncounterInPeriod(base.ZContext, 1f))
		{
			return RoundSuccess("遭遇战斗");
		}
		if (_stopWhenDisappear)
		{
			return RoundSuccess("到达目标", _lastTargetName);
		}
		YoloDetectFrameResult yoloDetectFrameResult = null;
		if (base.LastScreenshot != null)
		{
			YoloDetectFrameResult yoloDetectFrameResult2 = RunDetection(base.LastScreenshot);
			yoloDetectFrameResult = yoloDetectFrameResult2;
			LogDetectionSummary("移动", yoloDetectFrameResult2);
			if (ShouldStopForInteraction(yoloDetectFrameResult2) && ScreenUtils.FindArea(base.ZContext, base.LastScreenshot, "战斗画面", "按键-交互") == FindAreaResultEnum.True)
			{
				return RoundSuccess("到达目标", _lastTargetName);
			}
		}
		if (_noTargetHandleTimes >= 7)
		{
			WriteTargetedNoTargetEvidence("连续七次无目标，准备脱困", yoloDetectFrameResult ?? new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0));
			_noTargetHandleTimes = 0;
			_stuckTimes++;
			return RoundSuccess("尝试脱困");
		}
		if (_lastTarget != null)
		{
			base.ZContext.AutoBattleContext.MoveW(press: true, TimeSpan.FromMilliseconds(500L), release: true);
			_lastTarget = null;
		}
		if (++_totalTurnTimes >= 100)
		{
			return RoundFail("未识别到目标");
		}
		base.ZContext.AutoBattleContext.TurnByDistance(-200f);
		if (AutoBattleUtils.CheckBattleEncounterInPeriod(base.ZContext, 0.5f))
		{
			return RoundSuccess("遭遇战斗");
		}
		return RoundSuccess("继续识别目标");
	}

	[NodeFrom("移动", Status = "尝试脱困")]
	[NodeFrom("无目标处理", Status = "尝试脱困")]
	[OperationNode("脱困")]
	private OperationRoundResult GetOutOfStuck()
	{
		AutoBattleUtils.SwitchToBestAgentForMoving(base.ZContext);
		if (base.LastScreenshot != null && base.ZContext.AutoBattleContext.IsNormalAttackButtonAvailable(base.LastScreenshot))
		{
			base.ZContext.AutoBattleContext.NormalAttack(press: true, TimeSpan.FromMilliseconds(200L), release: true);
			Thread.Sleep(TimeSpan.FromSeconds(1L));
		}
		int num = (_stuckTimes - 1) % 8;
		TimeSpan value = TimeSpan.FromSeconds(Math.Min((double)num * 0.5, 2.0));
		TimeSpan value2 = TimeSpan.FromSeconds(Math.Min((double)(num + 1) * 0.2, 2.0));
		TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Min((double)num * 0.2, 2.0));
		base.ZContext.AutoBattleContext.MoveS(press: true, value, release: true);
		if (_preferLeftEscape)
		{
			base.ZContext.AutoBattleContext.MoveA(press: true, value2, release: true);
		}
		else
		{
			base.ZContext.AutoBattleContext.MoveD(press: true, value2, release: true);
		}
		_preferLeftEscape = !_preferLeftEscape;
		if (timeSpan > TimeSpan.Zero)
		{
			base.ZContext.AutoBattleContext.MoveW(press: true, timeSpan, release: true);
		}
		Screenshot();
		if (_targetType == "0000-感叹号")
		{
			return RoundSuccess("需要重新识别");
		}
		if (base.LastScreenshot != null)
		{
			YoloDetectFrameResult frameResult = base.ZContext.LostVoid.Detector?.Run(base.LastScreenshot, 0.6f, 0.5f, (double?)base.LastScreenshotTimeUtc?.ToUnixTimeMilliseconds() / 1000.0, _service.BuildLabelList(base.ZContext.LostVoid.Detector, _ignoreEntryList)) ?? new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0);
			WriteTargetedNoTargetEvidence("脱困后重新识别", frameResult, afterEscape: true);
			if (base.ZContext.LostVoid.Detector?.GetResultByX(frameResult, "0001-距离") != null)
			{
				return RoundSuccess("需要重新识别");
			}
		}
		return RoundSuccess("继续识别目标");
	}

	internal OperationRoundResult HandleLostTargetDuringDetection(int escapeInterval, Action? escapeFromStuck = null)
	{
		_lostTargetDuringMoveTimes++;
		if (_lostTargetDuringMoveTimes % escapeInterval == 0)
		{
			_stuckTimes++;
			if (escapeFromStuck == null)
			{
				GetOutOfStuck();
			}
			else
			{
				escapeFromStuck();
			}
		}
		return RoundSuccess("未识别到目标");
	}

	private bool ShouldStopForInteraction(YoloDetectFrameResult frameResult)
	{
		if (!_stopWhenInteract)
		{
			return false;
		}
		bool flag = base.LastScreenshot != null && ScreenUtils.FindArea(base.ZContext, base.LastScreenshot, "战斗画面", "按键-交互") == FindAreaResultEnum.True;
		if (!flag)
		{
			if (base.LastScreenshot != null && !base.ZContext.AutoBattleContext.IsNormalAttackButtonAvailable(base.LastScreenshot))
			{
				DateTimeOffset utcNow = DateTimeOffset.UtcNow;
				DateTimeOffset? lastAttackButtonCheckUtc = _lastAttackButtonCheckUtc;
				if (lastAttackButtonCheckUtc.HasValue)
				{
					DateTimeOffset value = utcNow;
					lastAttackButtonCheckUtc = _lastAttackButtonCheckUtc;
					if (!(value - lastAttackButtonCheckUtc >= TimeSpan.FromSeconds(5L)))
					{
						goto IL_011d;
					}
				}
				_lastAttackButtonCheckUtc = utcNow;
				StopMovingForward();
				Thread.Sleep(TimeSpan.FromMilliseconds(500L));
			}
			goto IL_011d;
		}
		return _service.ShouldStopForInteraction(frameResult, _stopWhenInteract, flag, _allowArrivalByInteractButton);
		IL_011d:
		return false;
	}

	private static string DescribeDetectedClasses(YoloDetectFrameResult frameResult)
	{
		return (frameResult.Results.Count == 0) ? "无" : string.Join(", ", frameResult.Results.Select((YoloDetectObjectResult result) => result.DetectClass.ClassName));
	}

	private YoloDetectFrameResult RunDetection(Mat screen)
	{
		return DetectFrameOverride?.Invoke() ?? base.ZContext.LostVoid.Detector?.Run(screen, 0.6f, 0.5f, (double?)base.LastScreenshotTimeUtc?.ToUnixTimeMilliseconds() / 1000.0, _service.BuildLabelList(base.ZContext.LostVoid.Detector, _ignoreEntryList)) ?? new YoloDetectFrameResult(Array.Empty<YoloDetectObjectResult>(), 0.0);
	}

	private void LogDetectionSummary(string nodeName, YoloDetectFrameResult frameResult)
	{
		base.ZContext.Logger.Information("迷失之地寻路节点[" + nodeName + "]: Target={Target}, Detect={Detect}", _targetType, DescribeDetectedClasses(frameResult));
	}

	private void LogHigherPriorityFallback(string nodeName, string higherPriorityTarget)
	{
		base.ZContext.Logger.Information("迷失之地寻路节点[" + nodeName + "]: Target={Target}, 未检测到入口，但检测到更高优先级目标={HigherPriorityTarget}，返回上层重识别", _targetType, higherPriorityTarget);
	}

	private void LogNoTrackableTarget(string nodeName, YoloDetectFrameResult frameResult)
	{
		base.ZContext.Logger.Information("迷失之地寻路节点[" + nodeName + "]: Target={Target}, 未识别到可追踪目标，Detect={Detect}", _targetType, DescribeDetectedClasses(frameResult));
	}

	private void WriteTargetedNoTargetEvidence(string reason, YoloDetectFrameResult frameResult, bool afterEscape = false)
	{
		if (ActionLevelDebugEvidenceWriter.IsEnabled && base.LastScreenshot != null && !(afterEscape ? _escapeDiagnosticWritten : _noTargetDiagnosticWritten))
		{
			if (afterEscape)
			{
				_escapeDiagnosticWritten = true;
			}
			else
			{
				_noTargetDiagnosticWritten = true;
			}
			string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId("lost_void") + "-lost-void-no-target");
			string beforeScreenshotPath = ActionLevelDebugEvidenceWriter.WriteTargetedScreenshot(fileStem, afterEscape ? "after-escape" : "before-escape", base.LastScreenshot);
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("lost_void"),
				OperationName = "迷失之地",
				NodeName = (afterEscape ? "脱困后重新识别" : "无目标处理"),
				DotNetMethod = "ZzzOd.GameLogic.Application.HollowZero.LostVoid.LostVoidMoveByDetectionOperation",
				BaselineParityRequirement = "lost_void_move_by_det 在无目标时重新截图、转向并在第七次无目标后脱困；实机证据必须保留实际 YOLO 识别结果。",
				BeforeScreenshotPath = beforeScreenshotPath,
				BeforeRecognitionSummary = new
				{
					reason = reason,
					target = _targetType,
					currentRegion = _currentRegion,
					noTargetHandleTimes = _noTargetHandleTimes,
					lostTargetDuringMoveTimes = _lostTargetDuringMoveTimes,
					stuckTimes = _stuckTimes,
					totalTurnTimes = _totalTurnTimes,
					detections = frameResult.Results.Select((YoloDetectObjectResult result) => new
					{
						className = result.DetectClass.ClassName,
						X1 = result.X1,
						Y1 = result.Y1,
						X2 = result.X2,
						Y2 = result.Y2,
						Score = result.Score
					}).ToArray()
				},
				ActionKind = "targeted_failure_capture",
				ActionTarget = _targetType,
				ExpectedNextState = (afterEscape ? "重新检测到入口、距离或更高优先级目标" : "执行 Python 等价脱困后重新识别"),
				TransitionResult = reason,
				FailureReason = reason,
				RetryStoppedBecauseOfSuspectedLoop = false
			});
		}
	}

	private static string? FirstDetectedClass(YoloDetectFrameResult frameResult, params string[] classNames)
	{
		foreach (string className in classNames)
		{
			if (frameResult.Results.Any((YoloDetectObjectResult result) => string.Equals(result.DetectClass.ClassName, className, StringComparison.Ordinal)))
			{
				return className;
			}
		}
		return null;
	}

	private OperationRoundResult? CheckStuck(YoloDetectFrameResult frameResult, LostVoidMoveTargetWrapper target)
	{
		List<LostVoidMoveTargetWrapper> list = frameResult.Results.Select((YoloDetectObjectResult result) => new LostVoidMoveTargetWrapper(result)).ToList();
		if (_lastTarget == null)
		{
			ResetStuckStatus();
			_lastVisibleTargets.Clear();
			_lastVisibleTargets.AddRange(list);
			return null;
		}
		bool flag = AreVisibleTargetsStatic(_lastVisibleTargets, list);
		_sameTargetTimes = (flag ? (_sameTargetTimes + ((list.Count == 1) ? 0.2 : 1.0)) : 0.0);
		_lastVisibleTargets.Clear();
		_lastVisibleTargets.AddRange(list);
		double num = ((list.Count == 1) ? 5.0 : 20.0);
		if (_sameTargetTimes < num)
		{
			return null;
		}
		StopMovingForward();
		_stuckTimes++;
		ResetStuckStatus();
		return (_stuckTimes > 12) ? RoundFail("无法脱困") : RoundSuccess("尝试脱困");
	}

	private bool TurnToTarget(OneDragon.Core.Abstractions.Geometry.Point target, bool isMoving)
	{
		int num = base.ZContext.Controller?.StandardWidth ?? base.ZContext.ProjectConfig.ScreenStandardWidth;
		int num2 = target.X - num / 2;
		if (Math.Abs(num2) <= 50)
		{
			return false;
		}
		int num3 = 5;
		int num4 = (isMoving ? 15 : 200);
		if (!isMoving)
		{
			int? lastTargetX = _lastTargetX;
			if (lastTargetX.HasValue && _lastActualTurnDistance != 0)
			{
				int num5 = _lastTargetX.Value - num / 2;
				int num6 = num5 - num2;
				if (num5 * num2 < 0)
				{
					_estimatedTurnRatio = Math.Max(0.02, _estimatedTurnRatio * 0.5);
					_turnSettleFrames = 1;
				}
				else if (Math.Abs(num6) > 5)
				{
					double num7 = Math.Clamp(Math.Abs((double)_lastActualTurnDistance / (double)num6), 0.02, 1.0);
					_estimatedTurnRatio = ((_turnCalibrationCount == 1) ? num7 : ((_estimatedTurnRatio * (double)(_turnCalibrationCount - 1) + num7) / (double)_turnCalibrationCount));
					_turnCalibrationCount++;
				}
			}
		}
		int num8;
		if (!isMoving && _turnSettleFrames > 0 && Math.Abs(num2) < 120)
		{
			_turnSettleFrames--;
			num8 = 0;
		}
		else if (_turnCalibrationCount == 1)
		{
			num8 = ((num2 > 0) ? 5 : (-5));
			if (!isMoving)
			{
				_turnCalibrationCount++;
			}
		}
		else
		{
			num8 = (int)((double)num2 * _estimatedTurnRatio);
		}
		if (num8 != 0 && Math.Abs(num8) < num3)
		{
			num8 = ((isMoving || Math.Abs(num2) >= 120) ? (Math.Sign(num8) * num3) : 0);
		}
		num8 = Math.Clamp(num8, -num4, num4);
		if (num8 == 0)
		{
			return false;
		}
		if (!isMoving)
		{
			_lastTargetX = target.X;
			_lastActualTurnDistance = num8;
		}
		base.ZContext.AutoBattleContext.TurnByDistance(num8);
		_totalTurnTimes++;
		return true;
	}

	private void ResetTurnCalibration()
	{
		_lastTargetX = null;
		_lastActualTurnDistance = 0;
		_estimatedTurnRatio = 0.2;
		_turnCalibrationCount = 1;
		_turnSettleFrames = 0;
	}

	private void ResetStuckStatus()
	{
		_sameTargetTimes = 0.0;
		_lastVisibleTargets.Clear();
	}

	private static bool AreVisibleTargetsStatic(IReadOnlyList<LostVoidMoveTargetWrapper> previous, IReadOnlyList<LostVoidMoveTargetWrapper> current)
	{
		if (previous.Count == 0 || previous.Count != current.Count)
		{
			return false;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (LostVoidMoveTargetWrapper item in current)
		{
			int num = -1;
			double num2 = double.MaxValue;
			for (int i = 0; i < previous.Count; i++)
			{
				if (!hashSet.Contains(i))
				{
					double num3 = Distance(previous[i].EntireRect.Center, item.EntireRect.Center);
					if (num3 < 10.0 && num3 < num2)
					{
						num = i;
						num2 = num3;
					}
				}
			}
			if (num < 0)
			{
				return false;
			}
			hashSet.Add(num);
		}
		return true;
	}

	private static double Distance(OneDragon.Core.Abstractions.Geometry.Point left, OneDragon.Core.Abstractions.Geometry.Point right)
	{
		int num = left.X - right.X;
		int num2 = left.Y - right.Y;
		return Math.Sqrt(num * num + num2 * num2);
	}

	private void StopMovingForward()
	{
		base.ZContext.AutoBattleContext.MoveW(press: false, null, release: true);
	}
}
