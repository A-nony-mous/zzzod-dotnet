using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 默认兑换。Operation 流程。
/// </summary>
public sealed class OperationRedemptionCodeAppFlow : IRedemptionCodeAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, RedemptionCodeConfig config, RedemptionCodeRunRecord runRecord, CancellationToken cancellationToken)
	{
		RedemptionCodeOperation redemptionCodeOperation = new RedemptionCodeOperation(context, runRecord);
		return redemptionCodeOperation.ExecuteAsync(cancellationToken);
	}
}
