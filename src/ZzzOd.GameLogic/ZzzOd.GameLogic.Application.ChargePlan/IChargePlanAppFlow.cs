using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划应用流程。
/// </summary>
public interface IChargePlanAppFlow
{
	/// <summary>
	/// 运行电量计划。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, ChargePlanConfig config, ChargePlanRunRecord runRecord, CancellationToken cancellationToken);
}
