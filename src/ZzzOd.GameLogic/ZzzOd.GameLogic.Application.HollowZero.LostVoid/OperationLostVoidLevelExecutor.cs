using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class OperationLostVoidLevelExecutor : ILostVoidLevelExecutor, ILostVoidLevelExecutorLifecycle
{
	private readonly ILostVoidRunLevelRuntime? _runtime;

	private LostVoidRunLevel? _currentOperation;

	public OperationLostVoidLevelExecutor(ILostVoidRunLevelRuntime? runtime = null)
	{
		_runtime = runtime;
	}

	public Task<OperationResult> RunLevelAsync(ZContext context, LostVoidRunRecord runRecord, string regionType, CancellationToken cancellationToken)
	{
		return ExecuteAndClearAsync(_currentOperation = new LostVoidRunLevel(context, runRecord, regionType, _runtime), cancellationToken);
	}

	/// <inheritdoc />
	public void Pause(ZContext context)
	{
		if (_currentOperation != null)
		{
			_currentOperation.HandlePause();
		}
		else
		{
			context.AutoBattleContext.StopAutoBattle();
		}
	}

	/// <inheritdoc />
	public void Resume(ZContext context)
	{
		_currentOperation?.HandleResume();
	}

	/// <inheritdoc />
	public void Stop(ZContext context)
	{
		_currentOperation?.HandlePause();
		context.AutoBattleContext.StopAutoBattle();
	}

	private async Task<OperationResult> ExecuteAndClearAsync(LostVoidRunLevel operation, CancellationToken cancellationToken)
	{
		try
		{
			return await operation.ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			if (_currentOperation == operation)
			{
				_currentOperation = null;
			}
		}
	}
}
