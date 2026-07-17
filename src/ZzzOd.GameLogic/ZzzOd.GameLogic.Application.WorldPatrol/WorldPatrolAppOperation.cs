using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.EnterGame;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// BaselineParity 等价的锄大地外层状态机。
/// </summary>
public sealed class WorldPatrolAppOperation : ZOperation
{
	private readonly WorldPatrolConfig _config;

	private readonly WorldPatrolRunRecord _runRecord;

	private readonly WorldPatrolService _service;

	private readonly IWorldPatrolRouteRunner _routeRunner;

	private readonly Func<CancellationToken, Task<OperationResult>> _enterGameAsync;

	private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

	private readonly List<WorldPatrolRoute> _routeList = new List<WorldPatrolRoute>();

	private CancellationToken _executionCancellationToken;

	private int _routeIndex;

	/// <summary>当前路线下标。</summary>
	public int RouteIndex => _routeIndex;

	/// <summary>当前加载路线。</summary>
	public IReadOnlyList<WorldPatrolRoute> RouteList => _routeList;

	/// <summary>
	/// 初始化锄大地外层状态机。
	/// </summary>
	public WorldPatrolAppOperation(ZContext context, WorldPatrolConfig config, WorldPatrolRunRecord runRecord, WorldPatrolService? service = null, IWorldPatrolRouteRunner? routeRunner = null, Func<CancellationToken, Task<OperationResult>>? enterGameAsync = null, Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
		: base(context, "锄大地")
	{
		_config = config;
		_runRecord = runRecord;
		_service = service ?? context.WorldPatrolService;
		_routeRunner = routeRunner ?? new OperationWorldPatrolRouteRunner();
		_enterGameAsync = enterGameAsync ?? ((Func<CancellationToken, Task<OperationResult>>)((CancellationToken cancellationToken) => new OpenAndEnterGame(context).ExecuteAsync(cancellationToken)));
		_delayAsync = delayAsync ?? new Func<TimeSpan, CancellationToken, Task>(Task.Delay);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_executionCancellationToken = cancellationToken;
		return Task.CompletedTask;
	}

	/// <summary>
	/// 初始化路线、自动战斗配置和轮次记录。
	/// </summary>
	[OperationNode("初始化", IsStartNode = true)]
	public OperationRoundResult InitializeWorldPatrol()
	{
		base.ZContext.AutoBattleContext.InitAutoOp(_config.AutoBattle);
		_service.LoadData();
		_routeList.Clear();
		_routeList.AddRange(ApplyRouteListFilter(_service.GetWorldPatrolRoutes()));
		_runRecord.TotalRounds = _config.DailyLoopCount;
		_runRecord.SetRoutesPerRound(_routeList.Count);
		_runRecord.CurrentRound = _runRecord.CompletedRounds + 1;
		_runRecord.ResetRoundTiming();
		if (_runRecord.CurrentRound > _runRecord.TotalRounds)
		{
			base.ZContext.Logger.Information("锄大地当日已完成 {CompletedRounds}/{TotalRounds} 轮，无需再跑", _runRecord.CompletedRounds, _runRecord.TotalRounds);
		}
		return RoundSuccess($"加载路线 {_routeList.Count}");
	}

	/// <summary>
	/// 开始前返回大世界。
	/// </summary>
	[NodeFrom("初始化")]
	[OperationNode("开始前返回大世界")]
	public async Task<OperationRoundResult> BackAtFirstAsync()
	{
		return RoundByOperationResult(await new BackToNormalWorld(base.ZContext).ExecuteAsync(_executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 有任务追踪时前往绳网。
	/// </summary>
	[NodeFrom("开始前返回大世界")]
	[OperationNode("前往绳网")]
	public OperationRoundResult GotoInterKnot()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "大世界", "任务追踪");
		OperationRoundResult result;
		if (!operationRoundResult.IsSuccess)
		{
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
			result = RoundByGotoScreen(lastScreenshot, "绳网", null, null, retryDelay);
		}
		else
		{
			result = RoundSuccess("无任务追踪");
		}
		return result;
	}

	/// <summary>
	/// 停止当前任务追踪。
	/// </summary>
	[NodeFrom("前往绳网")]
	[OperationNode("停止追踪")]
	public OperationRoundResult StopTracking()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "绳网", "按钮-停止追踪");
		if (operationRoundResult.IsSuccess)
		{
			return operationRoundResult;
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "绳网", "按钮-追踪");
		return operationRoundResult2.IsSuccess ? RoundSuccess("无需停止追踪") : RoundRetry("未找到追踪按钮", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 停止追踪后返回大世界。
	/// </summary>
	[NodeFrom("停止追踪")]
	[NodeFrom("停止追踪", Success = false)]
	[OperationNode("停止追踪后返回大世界")]
	public async Task<OperationRoundResult> BackAfterStopTrackingAsync()
	{
		return RoundByOperationResult(await new BackToNormalWorld(base.ZContext).ExecuteAsync(_executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 执行当前路线。
	/// </summary>
	[NodeFrom("前往绳网", Status = "无任务追踪")]
	[NodeFrom("停止追踪后返回大世界")]
	[NodeFrom("准备下一轮", Status = "进入下一轮")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("执行路线")]
	public async Task<OperationRoundResult> RunRouteAsync()
	{
		if (_runRecord.CurrentRound > _runRecord.TotalRounds)
		{
			return RoundSuccess("路线已全部完成");
		}
		if (!_runRecord.RoundStartTime.HasValue)
		{
			_runRecord.RoundStartTime = NowSeconds();
			base.ZContext.Logger.Information("开始第 {CurrentRound}/{TotalRounds} 轮锄大地", _runRecord.CurrentRound, _runRecord.TotalRounds);
		}
		if (_routeIndex >= _routeList.Count)
		{
			return RoundSuccess("路线已全部完成");
		}
		WorldPatrolRoute route = _routeList[_routeIndex];
		if (_runRecord.Finished.Contains<string>(route.FullId, StringComparer.Ordinal))
		{
			_routeIndex++;
			base.ZContext.Logger.Information("跳过已完成路线 {RouteId}", route.FullId);
			return RoundWait("跳过已完成路线 " + route.FullId);
		}
		int attemptIndex = 0;
		string status;
		while (true)
		{
			_executionCancellationToken.ThrowIfCancellationRequested();
			bool isRestarted = attemptIndex > 0 && string.Equals(_config.RouteRetryAction, "skip_on_stuck_again", StringComparison.Ordinal);
			OperationResult result = await _routeRunner.RunRouteAsync(base.ZContext, _config, route, isRestarted, _executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (result.IsSuccess)
			{
				_runRecord.AddRecord(route.FullId);
				_routeIndex++;
				base.ZContext.Logger.Information("完成路线 {RouteId}", route.FullId);
				return RoundWait("完成路线 " + route.FullId);
			}
			status = result.Status ?? string.Empty;
			if (string.Equals(status, "疑似界面消失卡死", StringComparison.Ordinal))
			{
				if (string.Equals(_config.UiDisappearAction, "silent_fail", StringComparison.Ordinal))
				{
					base.ZContext.Logger.Warning("第 {CurrentRound}/{TotalRounds} 轮因界面消失静默失败终止任务 路线 {RouteId}", _runRecord.CurrentRound, _runRecord.TotalRounds, route.FullId);
					HandleStop();
					return RoundFail("疑似界面消失卡死 " + route.FullId);
				}
				OperationRoundResult restartResult = await RestartGameForUiDisappearedAsync().ConfigureAwait(continueOnCapturedContext: false);
				if (restartResult.IsFail)
				{
					return restartResult;
				}
				if (!string.Equals(_config.UiDisappearAction, "restart_and_retry", StringComparison.Ordinal) || attemptIndex >= _config.RouteRetryTimes)
				{
					_routeIndex++;
					string skipStatus = (string.Equals(_config.UiDisappearAction, "restart_and_retry", StringComparison.Ordinal) ? ("界面消失重试耗尽，已重开游戏并跳过路线 " + route.FullId) : ("界面消失已重开游戏并跳过路线 " + route.FullId));
					base.ZContext.Logger.Warning("{Status}", skipStatus);
					return RoundWait(skipStatus);
				}
				attemptIndex++;
			}
			else
			{
				if (!IsRestartRouteStatus(status) || attemptIndex >= _config.RouteRetryTimes)
				{
					break;
				}
				attemptIndex++;
			}
		}
		string failureStatus = ((attemptIndex > 0 && IsRestartRouteStatus(status)) ? $"重试 {attemptIndex} 次后仍卡住: {status}" : status);
		_routeIndex++;
		base.ZContext.Logger.Warning("路线失败 {FailureStatus} {RouteId}", failureStatus, route.FullId);
		return RoundWait("路线失败 " + failureStatus + " " + route.FullId);
	}

	/// <summary>
	/// 一轮路线结束后的分支。
	/// </summary>
	[NodeFrom("执行路线", Status = "路线已全部完成")]
	[OperationNode("轮次结束判定")]
	public OperationRoundResult DecideNextRound()
	{
		if (_runRecord.CurrentRound > _runRecord.TotalRounds)
		{
			base.ZContext.Logger.Information("锄大地当日已完成 {CompletedRounds}/{TotalRounds} 轮，无需再跑", _runRecord.CompletedRounds, _runRecord.TotalRounds);
			return RoundSuccess("全部完成");
		}
		_runRecord.IncCompletedRounds();
		if (_runRecord.CurrentRound >= _runRecord.TotalRounds)
		{
			base.ZContext.Logger.Information("锄大地全部循环已完成 共 {TotalRounds} 轮", _runRecord.TotalRounds);
			return RoundSuccess("全部完成");
		}
		double num = NowSeconds() - (_runRecord.RoundStartTime ?? NowSeconds());
		_runRecord.RoundWaitSeconds = Math.Max(0.0, (double)_config.LoopIntervalSeconds - num);
		if (_runRecord.RoundWaitSeconds > 0.0)
		{
			base.ZContext.Logger.Information("第 {CurrentRound}/{TotalRounds} 轮耗时 {RoundDuration:F0}s 最少占用 {LoopIntervalSeconds}s 将等待 {RoundWaitSeconds:F0}s", _runRecord.CurrentRound, _runRecord.TotalRounds, num, _config.LoopIntervalSeconds, _runRecord.RoundWaitSeconds);
		}
		return RoundSuccess("进入轮间等待");
	}

	/// <summary>
	/// 轮间等待前返回录像店。
	/// </summary>
	[NodeFrom("轮次结束判定", Status = "进入轮间等待")]
	[OperationNode("传送回录像店")]
	public async Task<OperationRoundResult> GotoVideoShopAsync()
	{
		return RoundByOperationResult(await new BackToNormalWorld(base.ZContext, ensureNormalWorld: true).ExecuteAsync(_executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 等待下一轮。
	/// </summary>
	[NodeFrom("传送回录像店")]
	[OperationNode("轮间等待")]
	public OperationRoundResult WaitBetweenRounds()
	{
		WorldPatrolRunRecord runRecord = _runRecord;
		double? roundWaitStartTime = runRecord.RoundWaitStartTime;
		double valueOrDefault = roundWaitStartTime.GetValueOrDefault();
		if (!roundWaitStartTime.HasValue)
		{
			valueOrDefault = NowSeconds();
			double? roundWaitStartTime2 = valueOrDefault;
			runRecord.RoundWaitStartTime = roundWaitStartTime2;
		}
		double num = NowSeconds() - _runRecord.RoundWaitStartTime.Value;
		if (num >= _runRecord.RoundWaitSeconds)
		{
			return RoundSuccess("等待完成");
		}
		return RoundWait($"轮间等待中 {num:F0}/{_runRecord.RoundWaitSeconds:F0}s", null, TimeSpan.FromSeconds(Math.Min(1.0, _runRecord.RoundWaitSeconds - num)));
	}

	/// <summary>
	/// 清理本轮记录并进入下一轮。
	/// </summary>
	[NodeFrom("轮间等待", Status = "等待完成")]
	[OperationNode("准备下一轮")]
	public OperationRoundResult PrepareNextRound()
	{
		_runRecord.CurrentRound++;
		_routeIndex = 0;
		_runRecord.ResetFinished();
		_runRecord.ResetRoundTiming();
		return RoundSuccess("进入下一轮");
	}

	/// <summary>
	/// 暂停当前路线动作。
	/// </summary>
	public void HandlePause()
	{
		_routeRunner.Pause();
		if (!_routeRunner.IsRunning)
		{
			StopRouteActions();
		}
	}

	/// <summary>
	/// 恢复当前路线动作。
	/// </summary>
	public void HandleResume()
	{
		_routeRunner.Resume();
	}

	/// <summary>
	/// 停止当前路线动作。
	/// </summary>
	public void HandleStop()
	{
		_routeRunner.Stop();
		StopRouteActions();
	}

	/// <inheritdoc />
	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		HandleStop();
		return base.OnAfterOperationDoneAsync(cancellationToken);
	}

	private IReadOnlyList<WorldPatrolRoute> ApplyRouteListFilter(IReadOnlyList<WorldPatrolRoute> routes)
	{
		if (string.IsNullOrWhiteSpace(_config.RouteList))
		{
			return routes;
		}
		WorldPatrolRouteList worldPatrolRouteList = _service.GetWorldPatrolRouteLists().FirstOrDefault((WorldPatrolRouteList item) => string.Equals(item.Name, _config.RouteList, StringComparison.Ordinal));
		if (worldPatrolRouteList == null)
		{
			return routes;
		}
		HashSet<string> configuredRoutes = worldPatrolRouteList.RouteItems.ToHashSet<string>(StringComparer.Ordinal);
		string listType = worldPatrolRouteList.ListType;
		if (1 == 0)
		{
		}
		IReadOnlyList<WorldPatrolRoute> result = ((listType == "blacklist") ? routes.Where((WorldPatrolRoute route) => !configuredRoutes.Contains(route.FullId)).ToList() : ((!(listType == "whitelist")) ? routes : routes.Where((WorldPatrolRoute route) => configuredRoutes.Contains(route.FullId)).ToList()));
		if (1 == 0)
		{
		}
		return result;
	}

	private async Task<OperationRoundResult> RestartGameForUiDisappearedAsync()
	{
		HandleStop();
		base.ZContext.Controller?.CloseGame();
		await _delayAsync(TimeSpan.FromSeconds(5L), _executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		OperationResult enterResult = await _enterGameAsync(_executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return enterResult.IsSuccess ? RoundSuccess("重开游戏成功") : RoundFail("重开游戏失败 " + enterResult.Status);
	}

	private void StopRouteActions()
	{
		base.ZContext.AutoBattleContext.StopAutoBattle();
		if (base.ZContext.Controller is ZPcController zPcController)
		{
			zPcController.StopMovingForward();
		}
	}

	private static bool IsRestartRouteStatus(string status)
	{
		return status.Contains("重启当前路线", StringComparison.Ordinal);
	}

	private static double NowSeconds()
	{
		return (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
	}
}
