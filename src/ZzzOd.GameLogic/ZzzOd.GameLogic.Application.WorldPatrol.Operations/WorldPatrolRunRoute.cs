using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Utils;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Turning;

namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 执行锄大地单条路线。
/// </summary>
public sealed class WorldPatrolRunRoute : ZOperation
{
	private enum BacktrackStatus
	{
		Started,
		Ongoing,
		Reached,
		Expired,
		Unavailable
	}

	/// <summary>疑似界面消失卡死。</summary>
	public const string StatusUiDisappeared = "疑似界面消失卡死";

	/// <summary>进入战斗。</summary>
	public const string StatusEnterBattle = "进入战斗";

	/// <summary>全部指令已完成。</summary>
	public const string StatusAllOperationsDone = "全部指令已完成";

	/// <summary>坐标计算失败，重启当前路线。</summary>
	public const string StatusRestartRouteNoPos = "坐标计算失败，重启当前路线";

	/// <summary>有坐标但卡住，重启当前路线。</summary>
	public const string StatusRestartRouteStuck = "有坐标但卡住，重启当前路线";

	/// <summary>路线或开始下标有误。</summary>
	public const string StatusRouteOrStartInvalid = "路线或开始下标有误";

	private const int ReachDistance = 10;

	private readonly WorldPatrolConfig _config;

	private readonly WorldPatrolRoute _route;

	private readonly bool _isRestarted;

	private readonly IWorldPatrolRunRouteServices _services;

	private readonly AngleTurnCompensator _turnCompensator;

	private readonly WorldPatrolLargeMap? _currentLargeMap;

	private CancellationToken _executionCancellationToken;

	private int _currentIdx;

	private WorldPatrolPoint _currentPos;

	private WorldPatrolPoint? _routeStartPos;

	private bool _backtrackActive;

	private WorldPatrolPoint? _backtrackTarget;

	private DateTimeOffset? _backtrackDeadline;

	private WorldPatrolPoint? _lastBacktrackTarget;

	private WorldPatrolPoint _stuckPos;

	private DateTimeOffset? _noPosStartTime;

	private DateTimeOffset? _stuckPosStartTime;

	private DateTimeOffset? _lastCheckBattleTime;

	private DateTimeOffset? _uiDisappearStartTime;

	private int _stuckMoveDirection;

	private int _posStuckAttempts;

	private bool _inBattle;

	private double? _lastAngle;

	private double? _lastAngleDiffCommand;

	private bool _routeFailureEvidenceWritten;

	/// <summary>当前指令下标。</summary>
	public int CurrentIdx => _currentIdx;

	/// <summary>当前坐标。</summary>
	public WorldPatrolPoint CurrentPos => _currentPos;

	/// <summary>是否处于战斗中。</summary>
	public bool InBattle => _inBattle;

	private DateTimeOffset CurrentScreenshotTime => base.LastScreenshotTimeUtc ?? _services.Now;

	/// <summary>
	/// 初始化路线执行。
	/// </summary>
	public WorldPatrolRunRoute(ZContext context, WorldPatrolRoute route, WorldPatrolConfig? config = null, int startIdx = 0, bool isRestarted = false, IWorldPatrolRunRouteServices? services = null)
		: base(context, "运行路线")
	{
		_route = route;
		_config = config ?? WorldPatrolConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "default");
		_currentIdx = startIdx;
		_isRestarted = isRestarted;
		_services = services ?? new DefaultWorldPatrolRunRouteServices();
		_turnCompensator = new AngleTurnCompensator(delegate(double angleDiff)
		{
			_services.TurnByAngleDiff(base.ZContext, angleDiff);
		});
		_currentLargeMap = _services.GetRouteLargeMap(base.ZContext, route);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_executionCancellationToken = cancellationToken;
		return Task.CompletedTask;
	}

	/// <summary>
	/// 初始回到大世界。
	/// </summary>
	[OperationNode("初始回到大世界", IsStartNode = true)]
	public async Task<OperationRoundResult> BackAtFirst()
	{
		if (_currentIdx != 0)
		{
			return RoundSuccess("DEBUG");
		}
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext, _executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 传送到路线起点。
	/// </summary>
	[NodeFrom("初始回到大世界")]
	[OperationNode("传送")]
	public async Task<OperationRoundResult> Transport()
	{
		return RoundByOperationResult(await _services.TransportAsync(base.ZContext, _route, _executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 设置起始坐标。
	/// </summary>
	[NodeFrom("初始回到大世界", Status = "DEBUG")]
	[NodeFrom("传送")]
	[OperationNode("设置起始坐标")]
	public OperationRoundResult SetStartIdx()
	{
		WorldPatrolPoint? routePosBeforeOpIdx = _services.GetRoutePosBeforeOpIdx(base.ZContext, _route, _currentIdx);
		if (!routePosBeforeOpIdx.HasValue)
		{
			return RoundFail("路线或开始下标有误");
		}
		_services.StopMovingForward(base.ZContext);
		_services.SwitchToBestAgentForMoving(base.ZContext);
		_currentPos = routePosBeforeOpIdx.Value;
		_routeStartPos = routePosBeforeOpIdx;
		_services.TurnVerticalByDistance(base.ZContext, 300.0);
		return RoundSuccess(null, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 运行当前路线指令。
	/// </summary>
	[NodeFrom("设置起始坐标")]
	[NodeFrom("自动战斗结束")]
	[OperationNode("运行指令")]
	public OperationRoundResult RunOp()
	{
		if (_currentIdx >= _route.OpList.Count)
		{
			return RoundSuccess("全部指令已完成");
		}
		WorldPatrolRouteOperation worldPatrolRouteOperation = _route.OpList[_currentIdx];
		WorldPatrolRouteOperation worldPatrolRouteOperation2 = ((_currentIdx + 1 < _route.OpList.Count) ? _route.OpList[_currentIdx + 1] : null);
		WorldPatrolMiniMapSnapshot worldPatrolMiniMapSnapshot = _services.CutMiniMap(base.ZContext, base.LastScreenshot);
		if (!worldPatrolMiniMapSnapshot.PlayMaskFound)
		{
			return RoundSuccess("进入战斗");
		}
		if (!string.Equals(worldPatrolRouteOperation.OpType, "move", StringComparison.Ordinal))
		{
			return RoundFail("未知指令类型 " + worldPatrolRouteOperation.OpType);
		}
		bool isNextMove = string.Equals(worldPatrolRouteOperation2?.OpType, "move", StringComparison.Ordinal);
		return HandleMove(worldPatrolRouteOperation, worldPatrolMiniMapSnapshot, isNextMove);
	}

	/// <summary>
	/// 初始化自动战斗。
	/// </summary>
	[NodeFrom("运行指令", Status = "进入战斗")]
	[OperationNode("初始化自动战斗")]
	public OperationRoundResult InitAutoBattle()
	{
		_services.StopMovingForward(base.ZContext);
		_services.InitAutoBattle(base.ZContext, _config.AutoBattle);
		_inBattle = true;
		_uiDisappearStartTime = null;
		_services.StartAutoBattle(base.ZContext);
		return RoundSuccess();
	}

	/// <summary>
	/// 自动战斗。
	/// </summary>
	[NodeFrom("初始化自动战斗")]
	[OperationNode("自动战斗", Mute = true)]
	public OperationRoundResult AutoBattle()
	{
		string lastCheckEndResult = base.ZContext.AutoBattleContext.LastCheckEndResult;
		if (lastCheckEndResult != null)
		{
			_services.StopAutoBattle(base.ZContext);
			return RoundSuccess(lastCheckEndResult);
		}
		DateTimeOffset currentScreenshotTime = CurrentScreenshotTime;
		WorldPatrolBattleCheckResult worldPatrolBattleCheckResult = _services.CheckBattleState(base.ZContext, base.LastScreenshot, currentScreenshotTime);
		if (!worldPatrolBattleCheckResult.FrameValid)
		{
			return RoundRetry("未获取截图");
		}
		DateTimeOffset? lastCheckBattleTime = _lastCheckBattleTime;
		if (lastCheckBattleTime.HasValue)
		{
			DateTimeOffset value = currentScreenshotTime;
			lastCheckBattleTime = _lastCheckBattleTime;
			if (value - lastCheckBattleTime <= TimeSpan.FromSeconds(1L))
			{
				return RoundWait(null, null, _services.BattleWaitDelay);
			}
		}
		_lastCheckBattleTime = currentScreenshotTime;
		if (worldPatrolBattleCheckResult.InBattle)
		{
			ResetUiDisappearStuck();
			if (_services.CutMiniMap(base.ZContext, base.LastScreenshot).PlayMaskFound)
			{
				return RoundSuccess("发现地图");
			}
			return RoundWait(null, null, _services.BattleWaitDelay);
		}
		if (_services.HasInteraction(base.ZContext, base.LastScreenshot))
		{
			ResetUiDisappearStuck();
			return RoundSuccess("按键-交互");
		}
		if (_services.CutMiniMap(base.ZContext, base.LastScreenshot).PlayMaskFound)
		{
			ResetUiDisappearStuck();
			return RoundSuccess("发现地图");
		}
		OperationRoundResult operationRoundResult = CheckUiDisappearStuck();
		return operationRoundResult ?? RoundWait(null, null, _services.BattleWaitDelay);
	}

	/// <summary>
	/// 自动战斗结束。
	/// </summary>
	[NodeFrom("自动战斗")]
	[OperationNode("自动战斗结束")]
	public OperationRoundResult AfterAutoBattle()
	{
		_inBattle = false;
		_services.StopAutoBattle(base.ZContext);
		_services.WaitAfterAutoBattleStop(base.ZContext);
		_services.SwitchToBestAgentForMoving(base.ZContext);
		_services.TurnVerticalByDistance(base.ZContext, 300.0);
		_lastAngle = null;
		_lastAngleDiffCommand = null;
		return RoundSuccess();
	}

	/// <summary>
	/// 暂停处理。
	/// </summary>
	public void HandlePause()
	{
		if (_inBattle)
		{
			_services.StopAutoBattle(base.ZContext);
		}
		else
		{
			_services.StopMovingForward(base.ZContext);
		}
	}

	/// <summary>
	/// 恢复处理。
	/// </summary>
	public void HandleResume()
	{
		if (_inBattle)
		{
			_services.StartAutoBattle(base.ZContext);
		}
	}

	/// <inheritdoc />
	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		_services.StopMovingForward(base.ZContext);
		return base.OnAfterOperationDoneAsync(cancellationToken);
	}

	private OperationRoundResult HandleMove(WorldPatrolRouteOperation operation, WorldPatrolMiniMapSnapshot miniMap, bool isNextMove)
	{
		OperationRoundResult operationRoundResult = UpdateCurrentPosition(miniMap);
		if (operationRoundResult != null)
		{
			return operationRoundResult;
		}
		if (operation.Data.Count < 2 || !int.TryParse(operation.Data[0], out var result) || !int.TryParse(operation.Data[1], out var result2))
		{
			return RoundFail("移动指令坐标错误");
		}
		if (_backtrackActive && BacktrackStep(_currentPos, emitLog: true) == BacktrackStatus.Reached)
		{
			return RoundWait("回溯成功，已到达回溯点");
		}
		WorldPatrolPoint worldPatrolPoint;
		if (_backtrackActive)
		{
			WorldPatrolPoint? backtrackTarget = _backtrackTarget;
			if (backtrackTarget.HasValue)
			{
				worldPatrolPoint = _backtrackTarget.Value;
				goto IL_00e8;
			}
		}
		worldPatrolPoint = new WorldPatrolPoint(result, result2);
		goto IL_00e8;
		IL_00e8:
		WorldPatrolPoint worldPatrolPoint2 = worldPatrolPoint;
		TurnAndMove(worldPatrolPoint2, miniMap);
		if (!_backtrackActive && Distance(_currentPos, worldPatrolPoint2) < 10.0)
		{
			_currentIdx++;
			if (isNextMove)
			{
				_services.StopMovingForward(base.ZContext);
				Thread.Sleep(TimeSpan.FromMilliseconds(10L));
				_services.StartMovingForward(base.ZContext);
			}
			_posStuckAttempts = 0;
			return RoundWait($"已到达目标点 {worldPatrolPoint2}");
		}
		// 对应 world_patrol_run_route.py:205-207 的 wait_round_time=0.3：补足制，即"本轮总时长（含截图与识别）不低于 0.3s"，
		// 而不是在识别耗时之外再固定加 300ms。原注释"这个时间设置太小的话，会出现转向之后方向判断不准"针对的正是轮间隔下限。
		return RoundWait($"当前坐标 {_currentPos} 角度 {miniMap.ViewAngle} 目标点 {worldPatrolPoint2}", null, null, TimeSpan.FromMilliseconds(300L));
	}

	private OperationRoundResult? UpdateCurrentPosition(WorldPatrolMiniMapSnapshot miniMap)
	{
		if (_currentLargeMap == null)
		{
			throw new InvalidOperationException("缺少大地图数据，路线配置错误");
		}
		DateTimeOffset currentScreenshotTime = CurrentScreenshotTime;
		DateTimeOffset? noPosStartTime = _noPosStartTime;
		double num = ((!noPosStartTime.HasValue) ? 0.0 : (currentScreenshotTime - _noPosStartTime.Value).TotalSeconds);
		num += 1.0;
		double num2 = num * 50.0;
		int size = miniMap.Size;
		Rect possibleRect = new Rect((int)((double)_currentPos.X - num2 - (double)size), (int)((double)_currentPos.Y - num2 - (double)size), (int)((double)_currentPos.X + num2 + (double)size), (int)((double)_currentPos.Y + num2 + (double)size));
		WorldPatrolPoint? worldPatrolPoint = _services.CalculateCurrentPosition(base.ZContext, _currentLargeMap, miniMap, possibleRect);
		if (worldPatrolPoint.HasValue && !IsNextPositionValid(worldPatrolPoint.Value, num2))
		{
			worldPatrolPoint = null;
		}
		if (!worldPatrolPoint.HasValue)
		{
			DateTimeOffset valueOrDefault = _noPosStartTime.GetValueOrDefault();
			if (!_noPosStartTime.HasValue)
			{
				valueOrDefault = currentScreenshotTime;
				_noPosStartTime = valueOrDefault;
			}
			double totalSeconds = (currentScreenshotTime - _noPosStartTime.Value).TotalSeconds;
			if (totalSeconds > 13.5)
			{
				WriteRouteFailureEvidence("坐标计算失败，重启当前路线", miniMap, totalSeconds);
				return RoundFail("坐标计算失败，重启当前路线");
			}
			if (totalSeconds > 4.5)
			{
				if (_isRestarted)
				{
					return RoundFail("坐标计算失败，重启当前路线");
				}
				DoUnstuckMove("no-pos");
			}
			else if (totalSeconds > 1.5)
			{
				_services.StopMovingForward(base.ZContext);
			}
			_services.TurnVerticalByDistance(base.ZContext, 300.0);
			return RoundWait($"坐标计算失败 持续 {totalSeconds:0.00} 秒");
		}
		_noPosStartTime = null;
		if (ProcessStuckWithPosition(worldPatrolPoint.Value, currentScreenshotTime))
		{
			return RoundFail("有坐标但卡住，重启当前路线");
		}
		_currentPos = worldPatrolPoint.Value;
		return null;
	}

	/// <summary>
	/// 停止路线动作。
	/// </summary>
	public void HandleStop()
	{
		_services.StopAutoBattle(base.ZContext);
		_services.StopMovingForward(base.ZContext);
	}

	private bool IsNextPositionValid(WorldPatrolPoint nextPos, double moveDistance)
	{
		if (Distance(_currentPos, nextPos) > moveDistance)
		{
			base.ZContext.Logger.Information("坐标跳变过大 舍弃 {NextPosition} 距离 {Distance:F1} 允许 {AllowedDistance:F1}", nextPos, Distance(_currentPos, nextPos), moveDistance);
			return false;
		}
		bool flag = IsNextPositionInAngleRange(nextPos);
		if (!flag)
		{
			base.ZContext.Logger.Information("坐标方向偏离 舍弃 {NextPosition}", nextPos);
		}
		return flag;
	}

	private bool IsNextPositionInAngleRange(WorldPatrolPoint nextPos)
	{
		double? lastAngle = _lastAngle;
		if (lastAngle.HasValue)
		{
			lastAngle = _lastAngleDiffCommand;
			if (lastAngle.HasValue)
			{
				if (Distance(_currentPos, nextPos) < 50.0)
				{
					return true;
				}
				double num = CalUtils.CalculateDirectionAngle(ToCorePoint(_currentPos), ToCorePoint(nextPos));
				double num2 = ((!(_lastAngleDiffCommand < 0.0)) ? ((num >= _lastAngle) ? (num - _lastAngle.Value) : (num + 360.0 - _lastAngle.Value)) : ((num <= _lastAngle) ? (num - _lastAngle.Value) : (num - 360.0 - _lastAngle.Value)));
				return (!(_lastAngleDiffCommand < 0.0)) ? (-30.0 <= num2 && num2 <= _lastAngleDiffCommand + 30.0) : (30.0 >= num2 && num2 >= _lastAngleDiffCommand - 30.0);
			}
		}
		return true;
	}

	private bool ProcessStuckWithPosition(WorldPatrolPoint nextPos, DateTimeOffset now)
	{
		if (Distance(nextPos, _stuckPos) >= 10.0)
		{
			_stuckPos = nextPos;
			_stuckPosStartTime = null;
			return false;
		}
		DateTimeOffset valueOrDefault = _stuckPosStartTime.GetValueOrDefault();
		if (!_stuckPosStartTime.HasValue)
		{
			valueOrDefault = now;
			_stuckPosStartTime = valueOrDefault;
		}
		if ((now - _stuckPosStartTime.Value).TotalSeconds <= 2.0)
		{
			return false;
		}
		_services.StopMovingForward(base.ZContext);
		if (_isRestarted)
		{
			base.ZContext.Logger.Error("[with-pos]再次卡住，跳过当前路线");
			WriteRouteFailureEvidence("有坐标但卡住，重启当前路线", null, null);
			return true;
		}
		BacktrackStatus backtrackStatus = BacktrackStep(nextPos, emitLog: true);
		if ((uint)(backtrackStatus - 3) <= 1u)
		{
			DoUnstuckMove("with-pos");
			_posStuckAttempts++;
		}
		_stuckPos = new WorldPatrolPoint(0, 0);
		_stuckPosStartTime = null;
		if (_posStuckAttempts < 6)
		{
			return false;
		}
		_posStuckAttempts = 0;
		base.ZContext.Logger.Information("[with-pos]卡住，重启当前路线");
		WriteRouteFailureEvidence("有坐标但卡住，重启当前路线", null, null);
		return true;
	}

	private void WriteRouteFailureEvidence(string reason, WorldPatrolMiniMapSnapshot? miniMap, double? elapsedSeconds)
	{
		if (!_routeFailureEvidenceWritten && ActionLevelDebugEvidenceWriter.IsEnabled && base.LastScreenshot != null)
		{
			_routeFailureEvidenceWritten = true;
			string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId("world_patrol") + "-world-patrol-route-failure");
			string beforeScreenshotPath = ActionLevelDebugEvidenceWriter.WriteTargetedScreenshot(fileStem, "failure", base.LastScreenshot);
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("world_patrol"),
				OperationName = "锄大地",
				NodeName = "运行路线",
				DotNetMethod = "ZzzOd.GameLogic.Application.WorldPatrol.Operations.WorldPatrolRunRoute",
				BaselineParityRequirement = "world_patrol 在定位失败或卡住重试耗尽时停止当前路线；实机证据保留失败瞬间的完整画面和定位状态。",
				BeforeScreenshotPath = beforeScreenshotPath,
				BeforeRecognitionSummary = new
				{
					reason = reason,
					route = _route.FullId,
					currentOperationIndex = _currentIdx,
					currentPosition = _currentPos,
					restarted = _isRestarted,
					elapsedSeconds = elapsedSeconds,
					miniMap = (((object)miniMap == null) ? null : new { miniMap.PlayMaskFound, miniMap.ViewAngle, miniMap.Size })
				},
				ActionKind = "targeted_failure_capture",
				ActionTarget = _route.FullId,
				ExpectedNextState = "路线重试或按配置结束当前路线",
				TransitionResult = reason,
				FailureReason = reason
			});
		}
	}

	private void TurnAndMove(WorldPatrolPoint targetPos, WorldPatrolMiniMapSnapshot miniMap)
	{
		if (!miniMap.ViewAngle.HasValue)
		{
			_lastAngle = null;
			_lastAngleDiffCommand = null;
			_services.StartMovingForward(base.ZContext);
			return;
		}
		double toAngle = CalUtils.CalculateDirectionAngle(ToCorePoint(_currentPos), ToCorePoint(targetPos));
		double num = CalUtils.AngleDelta(miniMap.ViewAngle.Value, toAngle);
		double? lastAngle = _lastAngle;
		if (lastAngle.HasValue)
		{
			lastAngle = _lastAngleDiffCommand;
			if (lastAngle.HasValue)
			{
				_turnCompensator.Learn(_lastAngle.Value, _lastAngleDiffCommand.Value, miniMap.ViewAngle.Value);
			}
		}
		double value = num * _turnCompensator.Scale;
		if (Math.Abs(value) > 90.0)
		{
			_services.StopMovingForward(base.ZContext);
		}
		double value2 = ((Math.Abs(value) < 2.0) ? 0.0 : _turnCompensator.Turn(num, 45.0));
		_lastAngle = miniMap.ViewAngle;
		_lastAngleDiffCommand = value2;
		_services.StartMovingForward(base.ZContext);
	}

	private BacktrackStatus BacktrackStep(WorldPatrolPoint nextPos, bool emitLog)
	{
		DateTimeOffset currentScreenshotTime = CurrentScreenshotTime;
		if (_backtrackActive)
		{
			WorldPatrolPoint? backtrackTarget = _backtrackTarget;
			if (backtrackTarget.HasValue)
			{
				double num = Distance(nextPos, _backtrackTarget.Value);
				bool flag = num < 10.0;
				DateTimeOffset? backtrackDeadline = _backtrackDeadline;
				bool flag2 = backtrackDeadline.HasValue && currentScreenshotTime >= _backtrackDeadline.Value;
				if (!flag && !flag2)
				{
					DateTimeOffset dateTimeOffset = _backtrackDeadline ?? currentScreenshotTime;
					base.ZContext.Logger.Debug("回溯进行中，当前距离目标 {Distance:F2}，剩余时间 {Seconds:F1}秒", num, (dateTimeOffset - currentScreenshotTime).TotalSeconds);
					return BacktrackStatus.Ongoing;
				}
				if (flag)
				{
					_services.StopMovingForward(base.ZContext);
					base.ZContext.Logger.Information("回溯成功，已到达 {TargetPosition}", _backtrackTarget);
				}
				else
				{
					base.ZContext.Logger.Information("回溯超时");
				}
				_lastBacktrackTarget = _backtrackTarget;
				_backtrackActive = false;
				_backtrackTarget = null;
				_backtrackDeadline = null;
				return flag ? BacktrackStatus.Reached : BacktrackStatus.Expired;
			}
		}
		WorldPatrolPoint? worldPatrolPoint = _services.GetRoutePosBeforeOpIdx(base.ZContext, _route, _currentIdx) ?? _routeStartPos;
		int num2;
		if (worldPatrolPoint.HasValue)
		{
			WorldPatrolPoint? backtrackTarget = _lastBacktrackTarget;
			if (backtrackTarget.HasValue)
			{
				num2 = (worldPatrolPoint.Value.Equals(_lastBacktrackTarget.Value) ? 1 : 0);
				goto IL_009b;
			}
		}
		num2 = 0;
		goto IL_009b;
		IL_009b:
		bool flag3 = (byte)num2 != 0;
		if (!worldPatrolPoint.HasValue || flag3)
		{
			if (flag3)
			{
				base.ZContext.Logger.Information("回溯跳过，回溯点与上次相同");
			}
			return BacktrackStatus.Unavailable;
		}
		base.ZContext.Logger.Information("尝试回溯到上一个目标点 {PreviousPosition}", worldPatrolPoint);
		_backtrackActive = true;
		_backtrackTarget = worldPatrolPoint;
		_backtrackDeadline = currentScreenshotTime + TimeSpan.FromSeconds(15L);
		_services.StartMovingForward(base.ZContext);
		return BacktrackStatus.Started;
	}

	private void DoUnstuckMove(string tag)
	{
		_services.SwitchNextForUnstuck(base.ZContext);
		if (string.Equals(tag, "with-pos", StringComparison.Ordinal))
		{
			base.ZContext.Logger.Information("[{Tag}] 脱困尝试 {Attempt}/6，方向 {Direction}", tag, _posStuckAttempts + 1, _stuckMoveDirection);
		}
		else
		{
			base.ZContext.Logger.Information("[{Tag}] 本次脱困方向 {Direction}", tag, _stuckMoveDirection);
		}
		_services.MoveUnstuck(base.ZContext, _stuckMoveDirection, tag);
		_stuckMoveDirection++;
		if (_stuckMoveDirection > 5)
		{
			_stuckMoveDirection = 0;
		}
	}

	private void ResetUiDisappearStuck()
	{
		_uiDisappearStartTime = null;
	}

	private OperationRoundResult? CheckUiDisappearStuck()
	{
		DateTimeOffset currentScreenshotTime = CurrentScreenshotTime;
		DateTimeOffset valueOrDefault = _uiDisappearStartTime.GetValueOrDefault();
		if (!_uiDisappearStartTime.HasValue)
		{
			valueOrDefault = currentScreenshotTime;
			_uiDisappearStartTime = valueOrDefault;
		}
		double totalSeconds = (currentScreenshotTime - _uiDisappearStartTime.Value).TotalSeconds;
		return (totalSeconds >= (double)_config.UiDisappearSeconds) ? RoundFail("疑似界面消失卡死") : RoundWait($"疑似界面消失 持续 {totalSeconds:0.00} 秒");
	}

	private static double Distance(WorldPatrolPoint first, WorldPatrolPoint second)
	{
		return CalUtils.DistanceBetween(ToCorePoint(first), ToCorePoint(second));
	}

	private static Point ToCorePoint(WorldPatrolPoint point)
	{
		return new Point(point.X, point.Y);
	}
}
