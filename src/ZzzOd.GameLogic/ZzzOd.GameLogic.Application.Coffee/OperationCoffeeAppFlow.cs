using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 默认咖啡。Operation 流程。
/// </summary>
public sealed class OperationCoffeeAppFlow : ICoffeeAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CoffeeConfig config, ChargePlanConfig chargePlanConfig, CancellationToken cancellationToken)
	{
		CoffeeOperation coffeeOperation = new CoffeeOperation(context, config, chargePlanConfig);
		return coffeeOperation.ExecuteAsync(cancellationToken);
	}
}
