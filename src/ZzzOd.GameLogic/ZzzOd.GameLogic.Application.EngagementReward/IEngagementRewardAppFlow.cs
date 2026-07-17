using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EngagementReward;

/// <summary>
/// 活跃度奖励应用流程。
/// </summary>
public interface IEngagementRewardAppFlow
{
	/// <summary>
	/// 运行活跃度奖励领取流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
