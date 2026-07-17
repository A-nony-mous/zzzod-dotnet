using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 默认生命热线 Operation 流程。
/// </summary>
public sealed class OperationLifeOnLineAppFlow : ILifeOnLineAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, LifeOnLineConfig config, LifeOnLineRunRecord runRecord, CancellationToken cancellationToken)
	{
		LifeOnLineOperation lifeOnLineOperation = new LifeOnLineOperation(context, config, runRecord);
		return lifeOnLineOperation.ExecuteAsync(cancellationToken);
	}
}
