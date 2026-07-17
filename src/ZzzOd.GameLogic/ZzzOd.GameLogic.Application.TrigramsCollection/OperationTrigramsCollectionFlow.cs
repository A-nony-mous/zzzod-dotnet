using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 默认卦象集录 Operation 流程。
/// </summary>
public sealed class OperationTrigramsCollectionFlow : ITrigramsCollectionFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		TrigramsCollectionOperation trigramsCollectionOperation = new TrigramsCollectionOperation(context);
		return trigramsCollectionOperation.ExecuteAsync(cancellationToken);
	}
}
