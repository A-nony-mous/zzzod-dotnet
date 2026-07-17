using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations.EnterGame;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地应用。
/// </summary>
public sealed class WorldPatrolApp : ZApplication
{
	private readonly WorldPatrolConfig _config;

	private readonly WorldPatrolRunRecord _runRecord;

	private readonly IWorldPatrolAppFlow? _flow;

	private readonly IWorldPatrolRouteRunner? _routeRunner;

	private readonly Func<TimeSpan, CancellationToken, Task>? _delayAsync;

	private WorldPatrolAppOperation? _currentOperation;

	/// <summary>
	/// 初始化锄大地应用。
	/// </summary>
	public WorldPatrolApp(ZContext context, WorldPatrolConfig? config = null, WorldPatrolRunRecord? runRecord = null, IWorldPatrolAppFlow? flow = null, Func<CancellationToken, Task<OperationResult>>? enterGameAsync = null, IWorldPatrolRouteRunner? routeRunner = null, Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
		: base(context, "world_patrol", runRecord, "锄大地", 1, null, needCheckGameWindow: true, enterGameAsync, (ZContext ctx, CancellationToken cancellationToken) => new OpenAndEnterGame(ctx).ExecuteAsync(cancellationToken))
	{
		_config = config ?? WorldPatrolConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "default");
		_runRecord = runRecord ?? WorldPatrolRunRecord.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.GameAccountConfig.GameRefreshHourOffset);
		_flow = flow;
		_routeRunner = routeRunner;
		_delayAsync = delayAsync;
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		if (_flow != null)
		{
			return await _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		WorldPatrolAppOperation operation = (_currentOperation = new WorldPatrolAppOperation(base.Context, _config, _runRecord, null, _routeRunner, EnterGameAsync, _delayAsync));
		try
		{
			return await operation.ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			_currentOperation = null;
		}
	}

	/// <inheritdoc />
	public override Task OnPauseAsync(CancellationToken cancellationToken)
	{
		_currentOperation?.HandlePause();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override async Task OnResumeAsync(CancellationToken cancellationToken)
	{
		await base.OnResumeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		_currentOperation?.HandleResume();
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		_currentOperation?.HandleStop();
		StopRouteActions(base.Context);
		return Task.CompletedTask;
	}

	private static void StopRouteActions(ZContext context)
	{
		context.AutoBattleContext.StopAutoBattle();
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.StopMovingForward();
		}
	}
}
