using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ScratchCard;

/// <summary>
/// 默认刮刮。Operation 流程。
/// </summary>
public sealed class OperationScratchCardAppFlow : IScratchCardAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		ScratchCardOperation scratchCardOperation = new ScratchCardOperation(context);
		return scratchCardOperation.ExecuteAsync(cancellationToken);
	}
}
