using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码应用流程。
/// </summary>
public interface IRedemptionCodeAppFlow
{
	/// <summary>
	/// 运行兑换码输入流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, RedemptionCodeConfig config, RedemptionCodeRunRecord runRecord, CancellationToken cancellationToken);
}
