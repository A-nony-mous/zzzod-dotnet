using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 默认锄大地路线 runner。
/// </summary>
public sealed class OperationWorldPatrolRouteRunner : IWorldPatrolRouteRunner
{
	private readonly object _syncRoot = new object();

	private WorldPatrolRunRoute? _currentOperation;

	/// <inheritdoc />
	public bool IsRunning
	{
		get
		{
			lock (_syncRoot)
			{
				return _currentOperation != null;
			}
		}
	}

	/// <inheritdoc />
	public async Task<OperationResult> RunRouteAsync(ZContext context, WorldPatrolConfig config, WorldPatrolRoute route, bool isRestarted, CancellationToken cancellationToken)
	{
		WorldPatrolRunRoute operation = new WorldPatrolRunRoute(context, route, config, 0, isRestarted);
		lock (_syncRoot)
		{
			_currentOperation = operation;
		}
		try
		{
			return await operation.ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			lock (_syncRoot)
			{
				if (_currentOperation == operation)
				{
					_currentOperation = null;
				}
			}
		}
	}

	/// <inheritdoc />
	public void Pause()
	{
		lock (_syncRoot)
		{
			_currentOperation?.HandlePause();
		}
	}

	/// <inheritdoc />
	public void Resume()
	{
		lock (_syncRoot)
		{
			_currentOperation?.HandleResume();
		}
	}

	/// <inheritdoc />
	public void Stop()
	{
		lock (_syncRoot)
		{
			_currentOperation?.HandleStop();
		}
	}
}
