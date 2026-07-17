using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RiduWeekly;

/// <summary>
/// 默认丽都周纪 Operation 流程。
/// </summary>
public sealed class OperationRiduWeeklyAppFlow : IRiduWeeklyAppFlow
{
	private readonly Func<ZContext, CancellationToken, Task<OperationResult>> _executeOperationAsync;

	/// <summary>
	/// 初始化默认流程。
	/// </summary>
	public OperationRiduWeeklyAppFlow(Func<ZContext, CancellationToken, Task<OperationResult>>? executeOperationAsync = null)
	{
		_executeOperationAsync = executeOperationAsync ?? new Func<ZContext, CancellationToken, Task<OperationResult>>(ExecuteDefaultOperationAsync);
	}

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		return _executeOperationAsync(context, cancellationToken);
	}

	private static Task<OperationResult> ExecuteDefaultOperationAsync(ZContext context, CancellationToken cancellationToken)
	{
		RiduWeeklyOperation riduWeeklyOperation = new RiduWeeklyOperation(context);
		return riduWeeklyOperation.ExecuteAsync(cancellationToken);
	}
}
