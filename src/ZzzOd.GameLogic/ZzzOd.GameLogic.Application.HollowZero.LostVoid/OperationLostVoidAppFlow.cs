using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 默认迷失之地流程。
/// </summary>
public sealed class OperationLostVoidAppFlow : ILostVoidAppFlow, ILostVoidAppLifecycle
{
	private readonly ILostVoidRunner _runner;

	/// <summary>
	/// 初始化流程。
	/// </summary>
	public OperationLostVoidAppFlow(ILostVoidRunner? runner = null)
	{
		_runner = runner ?? new LostVoidRunner();
	}

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, LostVoidConfig config, LostVoidRunRecord runRecord, CancellationToken cancellationToken)
	{
		return new LostVoidAppOperation(context, config, runRecord, _runner).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public void Pause(ZContext context)
	{
		if (_runner is ILostVoidRunnerLifecycle lostVoidRunnerLifecycle)
		{
			lostVoidRunnerLifecycle.Pause(context);
		}
	}

	/// <inheritdoc />
	public void Resume(ZContext context)
	{
		if (_runner is ILostVoidRunnerLifecycle lostVoidRunnerLifecycle)
		{
			lostVoidRunnerLifecycle.Resume(context);
		}
	}

	/// <inheritdoc />
	public void Stop(ZContext context)
	{
		if (_runner is ILostVoidRunnerLifecycle lostVoidRunnerLifecycle)
		{
			lostVoidRunnerLifecycle.Stop(context);
		}
		else
		{
			context.AutoBattleContext.StopAutoBattle();
		}
	}
}
