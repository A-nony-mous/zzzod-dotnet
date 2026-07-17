using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 默认电量计划 Operation 流程。
/// </summary>
public sealed class OperationChargePlanAppFlow : IChargePlanAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, ChargePlanConfig config, ChargePlanRunRecord runRecord, CancellationToken cancellationToken)
	{
		ChargePlanOperation chargePlanOperation = new ChargePlanOperation(context, config, runRecord);
		return chargePlanOperation.ExecuteAsync(cancellationToken);
	}
}
