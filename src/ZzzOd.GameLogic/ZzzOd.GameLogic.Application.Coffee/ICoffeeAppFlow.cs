using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡店应用流程。
/// </summary>
public interface ICoffeeAppFlow
{
	/// <summary>
	/// 运行喝咖啡流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CoffeeConfig config, ChargePlanConfig chargePlanConfig, CancellationToken cancellationToken);
}
