using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 默认录像店营。Operation 流程。
/// </summary>
public sealed class OperationRandomPlayAppFlow : IRandomPlayAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, RandomPlayConfig config, RandomPlayRunRecord runRecord, CancellationToken cancellationToken)
	{
		RandomPlayOperation randomPlayOperation = new RandomPlayOperation(context, config, runRecord);
		return randomPlayOperation.ExecuteAsync(cancellationToken);
	}
}
